using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameLogic.Goap;
namespace GameLogic.AI;

public sealed class NpcNeedsModel
{
    public Dictionary<string,double> Values { get; } = new(StringComparer.Ordinal);
    public Dictionary<string,bool> Urgent { get; } = new(StringComparer.Ordinal);
    readonly IReadOnlyList<NpcNeedSpec> _specs;
    public NpcNeedsModel(IReadOnlyList<NpcNeedSpec> specs) { _specs=specs;foreach(var n in specs){Values[n.Id]=n.Initial;Urgent[n.Id]=false;}Refresh(); }
    public void Tick(double hours,Func<string,NpcValue> facts,Func<string,NpcValue> parameters)
    {
        var changes=_specs.Select(n=>(n,scale:n.Multiplier?.Evaluate(facts,parameters)??NpcValue.Numeric(1))).ToArray();
        foreach(var (n,scale) in changes)if(scale.Type==NpcValueType.Number)Values[n.Id]=Math.Clamp(Values[n.Id]+hours*n.Rate*scale.Number,n.Min,n.Max);
        Refresh();
    }
    public void Refresh(){foreach(var n in _specs){double v=Values[n.Id];Urgent[n.Id]=n.Higher?(Urgent[n.Id]?v>n.Exit:v>=n.Enter):(Urgent[n.Id]?v<n.Exit:v<=n.Enter);}}
}
public sealed class NpcRelationsModel
{
    public Dictionary<string,Dictionary<string,double>> Contacts { get; } = new(StringComparer.Ordinal);
    readonly NpcProgram _program;
    public NpcRelationsModel(NpcProgram program)=>_program=program;
    public bool Adjust(string contact,string dimension,double amount)
    {
        if(!_program.RelationDefaults.ContainsKey(dimension)||!double.IsFinite(amount)||string.IsNullOrWhiteSpace(contact))return false;
        if(!Contacts.TryGetValue(contact,out var values))
        {if(Contacts.Count>=_program.MaximumContacts)return false;Contacts[contact]=values=_program.RelationDefaults.ToDictionary(x=>x.Key,x=>x.Value.Number,StringComparer.Ordinal);}
        values[dimension]=Math.Clamp(values[dimension]+amount,_program.RelationMin,_program.RelationMax);return true;
    }
}
public sealed record NpcMemoryEntry(string Category,string Subject,double Hours,string Detail);
public sealed class NpcMemoryModel
{
    public List<NpcMemoryEntry> Entries { get; } = new();
    readonly NpcProgram _program;
    public NpcMemoryModel(NpcProgram program)=>_program=program;
    public bool Remember(NpcMemoryEntry entry)
    {if(!_program.MemoryCategories.Contains(entry.Category))return false;Entries.Add(entry);while(Entries.Count>_program.MemoryCapacity)Entries.RemoveAt(0);return true;}
    public void Expire(double hours)=>Entries.RemoveAll(e=>hours-e.Hours>_program.MemoryLifetime);
}
public sealed class NpcAffectModel
{
    public Dictionary<string,double> Values { get; }
    readonly NpcProgram _program;
    public NpcAffectModel(NpcProgram p){_program=p;Values=p.Affect.ToDictionary(x=>x.Key,x=>x.Value.Number,StringComparer.Ordinal);}
    public void Tick(double hours){foreach(var key in Values.Keys.ToArray()){double target=_program.Affect[key].Number;Values[key]=Move(Values[key],target,hours*_program.AffectRecovery);}}
    static double Move(double a,double b,double step)=>a<b?Math.Min(b,a+step):Math.Max(b,a-step);
}
/// <summary>Module-owned state can be used without an AI component or a scene host.</summary>
public sealed class NpcCharacterState
{
    public NpcProgram Program { get; }
    public NpcNeedsModel Needs { get; }
    public NpcRelationsModel Relations { get; }
    public NpcMemoryModel Memory { get; }
    public NpcAffectModel Affect { get; }
    public Dictionary<string,NpcValue> PrivateFacts { get; }
    public string Location { get; set; } = "";
    public double LastHours { get; private set; } = double.NaN;
    public long Revision { get; private set; }
    public void UpdateLocation(string location)
    {
        if(Location==location)return;
        Location=location;Revision++;
    }
    public NpcCharacterState(NpcProgram program)
    {
        Program=program;Needs=new(program.Needs);Relations=new(program);Memory=new(program);Affect=new(program);
        PrivateFacts=program.Facts.Values.Where(f=>f.Key.StartsWith("private.",StringComparison.Ordinal)).ToDictionary(f=>f.Key,f=>f.Initial,StringComparer.Ordinal);
    }
    public void Advance(double hours,Func<string,NpcValue> facts=null,Func<string,NpcValue> parameters=null)
    {
        double elapsed=double.IsFinite(LastHours)?Math.Max(0,hours-LastHours):0;LastHours=hours;
        Needs.Tick(elapsed,facts??Read,parameters??Program.Parameter);Affect.Tick(elapsed);Memory.Expire(hours);
    }
    public NpcValue Read(string key)
    {
        if(PrivateFacts.TryGetValue(key,out var value))return value;
        foreach(var n in Program.Needs)
        {
            string prefix="needs."+n.Id+".";
            if(key==prefix+"value")return NpcValue.Numeric(Needs.Values[n.Id]);
            if(key==prefix+"urgent")return NpcValue.Bool(Needs.Urgent[n.Id]);
            if(key==prefix+"urgency")return NpcValue.Numeric(Math.Pow(Math.Clamp(n.Higher?(Needs.Values[n.Id]-n.Min)/(n.Max-n.Min):(n.Max-Needs.Values[n.Id])/(n.Max-n.Min),0,1),n.Exponent));
        }
        if(key.StartsWith("personality."))return Program.Personality.GetValueOrDefault(key[12..]);
        if(key.StartsWith("preference."))return Program.Preferences.GetValueOrDefault(key[11..]);
        if(key.StartsWith("trait."))return NpcValue.Bool(Program.Traits.Contains(key[6..]));
        if(key.StartsWith("affect."))return Affect.Values.TryGetValue(key[7..],out var a)?NpcValue.Numeric(a):default;
        if(key.StartsWith("memory.")&&key.EndsWith(".count"))return NpcValue.Numeric(Memory.Entries.Count(e=>e.Category==key[7..^6]));
        if(key.StartsWith("relations.")&&key.EndsWith(".max")){string id=key[10..^4];return NpcValue.Numeric(Relations.Contacts.Values.Select(c=>c.GetValueOrDefault(id)).DefaultIfEmpty(Program.RelationDefaults.GetValueOrDefault(id).Number).Max());}
        if(key.StartsWith("schedule."))
        {
            string tag=key[9..];double hour=double.IsFinite(LastHours)?LastHours%24:12;
            var active=Program.Windows.Where(w=>w.Start==w.End||(w.Start<w.End?hour>=w.Start&&hour<w.End:hour>=w.Start||hour<w.End)).ToArray();
            int priority=active.Select(w=>w.Priority).DefaultIfEmpty(int.MinValue).Max();
            return NpcValue.Bool(active.Any(w=>w.Priority==priority&&w.Tags.Contains(tag)));
        }
        return key=="self.location"?NpcValue.Text(Location):default;
    }
    public void Apply(GoapWorldState result,IEnumerable<string> changed)
    {
        foreach(var spec in changed.Distinct().Where(Program.Facts.ContainsKey).Select(k=>Program.Facts[k]))
        {
            var value=Program.Read(result,spec.Key);
            if(spec.Key.StartsWith("needs."))Needs.Values[spec.Key[6..^6]]=value.Number;
            else PrivateFacts[spec.Key]=value;
        }
        Needs.Refresh();Revision++;
    }
    public JsonObject Capture()=>new(){["last_hours"]=double.IsFinite(LastHours)?LastHours:0,["location"]=Location,
        ["needs"]=JsonSerializer.SerializeToNode(Needs.Values),["urgent"]=JsonSerializer.SerializeToNode(Needs.Urgent),["private"]=JsonSerializer.SerializeToNode(PrivateFacts),
        ["relations"]=JsonSerializer.SerializeToNode(Relations.Contacts),["memory"]=JsonSerializer.SerializeToNode(Memory.Entries),["affect"]=JsonSerializer.SerializeToNode(Affect.Values)};
    public void Restore(JsonObject data)
    {
        if(data==null)return;
        double hours=data["last_hours"]?.GetValue<double>()??0;NpcProgram.Require(double.IsFinite(hours)&&hours>=0,"NPC 存档时间无效。");
        // Validate the complete state before touching the live modules.
        var needs=data["needs"]?.Deserialize<Dictionary<string,double>>()??new();var priv=data["private"]?.Deserialize<Dictionary<string,NpcValue>>()??new();
        foreach(var n in Program.Needs)if(needs.TryGetValue(n.Id,out double v))NpcProgram.Require(double.IsFinite(v)&&v>=n.Min&&v<=n.Max,"需求存档范围无效。");
        foreach(var f in priv){if(!Program.Facts.TryGetValue(f.Key,out var spec))continue;NpcProgram.Require(f.Value.Type==NpcValueType.Unknown||f.Value.Type==spec.Type&&(f.Value.Type!=NpcValueType.Number||double.IsFinite(f.Value.Number)&&f.Value.Number>=spec.Minimum&&f.Value.Number<=spec.Maximum),"私有事实存档无效。");}
        var contacts=data["relations"]?.Deserialize<Dictionary<string,Dictionary<string,double>>>()??new();
        var memories=data["memory"]?.Deserialize<List<NpcMemoryEntry>>()??new();var affect=data["affect"]?.Deserialize<Dictionary<string,double>>()??new();
        var urgent=data["urgent"]?.Deserialize<Dictionary<string,bool>>()??new();string location=data["location"]?.GetValue<string>()??"";
        NpcProgram.Require(contacts.Count<=Program.MaximumContacts&&contacts.Values.All(c=>c!=null&&c.Count==Program.RelationDefaults.Count&&c.All(x=>Program.RelationDefaults.ContainsKey(x.Key)&&double.IsFinite(x.Value)&&x.Value>=Program.RelationMin&&x.Value<=Program.RelationMax))&&memories.Count<=Program.MemoryCapacity&&memories.All(m=>m!=null&&Program.MemoryCategories.Contains(m.Category)&&double.IsFinite(m.Hours))&&affect.All(x=>Program.Affect.ContainsKey(x.Key)&&double.IsFinite(x.Value)),"社会模块存档无效。");
        foreach(var n in Program.Needs)if(needs.ContainsKey(n.Id))Needs.Values[n.Id]=needs[n.Id];
        foreach(var n in Program.Needs)Needs.Urgent[n.Id]=urgent.GetValueOrDefault(n.Id);
        foreach(var key in PrivateFacts.Keys.ToArray())if(priv.ContainsKey(key))PrivateFacts[key]=priv[key];
        Relations.Contacts.Clear();foreach(var e in contacts)Relations.Contacts[e.Key]=e.Value;
        Memory.Entries.Clear();Memory.Entries.AddRange(memories);foreach(var e in affect)Affect.Values[e.Key]=e.Value;
        Location=location;LastHours=hours;Needs.Refresh();Revision++;
    }
}
