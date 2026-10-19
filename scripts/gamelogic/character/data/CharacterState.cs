using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace GameLogic;

public sealed class CharacterNeedsModel
{
    public Dictionary<string,double> Values { get; } = new(StringComparer.Ordinal);
    readonly IReadOnlyList<CharacterNeedSpec> _specs;
    public CharacterNeedsModel(IReadOnlyList<CharacterNeedSpec> specs) { _specs=specs;foreach(var n in specs){Values[n.Id]=n.Initial;}Refresh(); }
    public void Tick(double hours,Func<string,CharacterValue> facts,Func<string,CharacterValue> parameters)
    {
        var changes=_specs.Select(n=>(n,scale:n.Multiplier?.Evaluate(facts,parameters)??CharacterValue.Numeric(1))).ToArray();
        foreach(var (n,scale) in changes)if(scale.Type==CharacterValueType.Number)Values[n.Id]=Math.Clamp(Values[n.Id]+hours*n.Rate*scale.Number,n.Min,n.Max);
        Refresh();
    }
    public void Refresh(){foreach(var n in _specs)Values[n.Id]=Math.Clamp(Values[n.Id],n.Min,n.Max);}

}
public sealed class CharacterRelationsModel
{
    public Dictionary<string,Dictionary<string,double>> Contacts { get; } = new(StringComparer.Ordinal);
    readonly CharacterProfileSpec _program;
    public CharacterRelationsModel(CharacterProfileSpec program)=>_program=program;
    public bool Adjust(string contact,string dimension,double amount)
    {
        if(!_program.RelationDefaults.ContainsKey(dimension)||!double.IsFinite(amount)||string.IsNullOrWhiteSpace(contact))return false;
        if(!Contacts.TryGetValue(contact,out var values))
        {if(Contacts.Count>=_program.MaximumContacts)return false;Contacts[contact]=values=_program.RelationDefaults.ToDictionary(x=>x.Key,x=>x.Value.Number,StringComparer.Ordinal);}
        var definition=_program.FeatureDefinitions[dimension];values[dimension]=Math.Clamp(values[dimension]+amount,definition.Minimum,definition.Maximum);return true;
    }
}
public sealed class CharacterAffectModel
{
    public Dictionary<string,double> Values { get; }
    readonly CharacterProfileSpec _program;
    public CharacterAffectModel(CharacterProfileSpec p){_program=p;Values=p.Affect.ToDictionary(x=>x.Key,x=>x.Value.Number,StringComparer.Ordinal);}
    public bool Adjust(string dimension,double amount)
    {
        if(!double.IsFinite(amount)||!Values.TryGetValue(dimension,out double previous)||!double.IsFinite(previous+amount))return false;
        var definition=_program.FeatureDefinitions[dimension];Values[dimension]=Math.Clamp(previous+amount,definition.Minimum,definition.Maximum);return true;
    }
    public void Tick(double hours){foreach(var key in Values.Keys.ToArray()){double target=_program.Affect[key].Number;Values[key]=Move(Values[key],target,hours*_program.AffectRecovery[key]);}}
    static double Move(double a,double b,double step)=>a<b?Math.Min(b,a+step):Math.Max(b,a-step);
}
/// <summary>Module-owned state can be used without an AI component or a scene host.</summary>
public sealed class CharacterState
{
    public CharacterProfileSpec Profile { get; }
    public CharacterNeedsModel Needs { get; }
    public CharacterRelationsModel Relations { get; }
    public CharacterAffectModel Affect { get; }
    public Dictionary<string,CharacterValue> PrivateFacts { get; }
    public string Location { get; set; } = "";
    public double LastHours { get; private set; } = double.NaN;
    public long Revision { get; private set; }
    public void UpdateLocation(string location)
    {
        if(Location==location)return;
        Location=location;Revision++;
    }
    public CharacterState(CharacterProfileSpec program)
    {
        Profile=program;Needs=new(program.Needs);Relations=new(program);Affect=new(program);
        PrivateFacts=program.Facts.Values.Where(f=>f.Key.StartsWith("private.",StringComparison.Ordinal)).ToDictionary(f=>f.Key,f=>f.Initial,StringComparer.Ordinal);
    }
    public void Advance(double hours,Func<string,CharacterValue> facts=null,Func<string,CharacterValue> parameters=null)
    {
        double elapsed=double.IsFinite(LastHours)?Math.Max(0,hours-LastHours):0;LastHours=hours;
        Needs.Tick(elapsed,facts??Read,parameters??Profile.Parameter);Affect.Tick(elapsed);
    }
    public CharacterValue Read(string key)
    {
        if(PrivateFacts.TryGetValue(key,out var value))return value;
        foreach(var n in Profile.Needs)
        {
            string prefix="needs."+n.Id+".";
            if(key==prefix+"value")return CharacterValue.Numeric(Needs.Values[n.Id]);
        }
        if(key.StartsWith("personality."))return Profile.Personality.GetValueOrDefault(key[12..]);
        if(key.StartsWith("preference."))return Profile.Preferences.GetValueOrDefault(key[11..]);
        if(key.StartsWith("trait."))return CharacterValue.Bool(Profile.Traits.Contains(key[6..]));
        if(key.StartsWith("affect."))return Affect.Values.TryGetValue(key[7..],out var a)?CharacterValue.Numeric(a):default;
        if(key.StartsWith("relations.")&&key.EndsWith(".max")){string id=key[10..^4];return CharacterValue.Numeric(Relations.Contacts.Values.Select(c=>c.GetValueOrDefault(id)).DefaultIfEmpty(Profile.RelationDefaults.GetValueOrDefault(id).Number).Max());}
        return key=="self.location"?CharacterValue.Text(Location):default;
    }
    public void Apply(IEnumerable<KeyValuePair<string,CharacterValue>> changes)
    {
        var values=changes.ToArray();
        foreach(var item in values)
        {
            CharacterProfileSpec.Require(Profile.Facts.TryGetValue(item.Key,out var spec)&&spec.Writable&&item.Value.Type==spec.Type&&(item.Value.Type!=CharacterValueType.Number||double.IsFinite(item.Value.Number)&&item.Value.Number>=spec.Minimum-1e-8&&item.Value.Number<=spec.Maximum+1e-8),"角色数据写入无效："+item.Key);
        }
        foreach(var item in values)
            if(item.Key.StartsWith("needs."))Needs.Values[item.Key[6..^6]]=item.Value.Number;
            else PrivateFacts[item.Key]=item.Value;
        Needs.Refresh();Revision++;
    }
    public JsonObject Capture()=>new(){["last_hours"]=double.IsFinite(LastHours)?LastHours:0,["location"]=Location,
        ["needs"]=JsonSerializer.SerializeToNode(Needs.Values),["private"]=JsonSerializer.SerializeToNode(PrivateFacts),
        ["relations"]=JsonSerializer.SerializeToNode(Relations.Contacts),["affect"]=JsonSerializer.SerializeToNode(Affect.Values)};
    public void Restore(JsonObject data)
    {
        if(data==null)return;
        double hours=data["last_hours"]?.GetValue<double>()??0;CharacterProfileSpec.Require(double.IsFinite(hours)&&hours>=0,"角色存档时间无效。");
        // Validate the complete state before touching the live modules.
        var needs=data["needs"]?.Deserialize<Dictionary<string,double>>()??new();var priv=data["private"]?.Deserialize<Dictionary<string,CharacterValue>>()??new();
        foreach(var n in Profile.Needs)if(needs.TryGetValue(n.Id,out double v))CharacterProfileSpec.Require(double.IsFinite(v)&&v>=n.Min&&v<=n.Max,"需求存档范围无效。");
        foreach(var f in priv){if(!Profile.Facts.TryGetValue(f.Key,out var spec))continue;CharacterProfileSpec.Require(f.Value.Type==CharacterValueType.Unknown||f.Value.Type==spec.Type&&(f.Value.Type!=CharacterValueType.Number||double.IsFinite(f.Value.Number)&&f.Value.Number>=spec.Minimum&&f.Value.Number<=spec.Maximum),"私有事实存档无效。");}
        var contacts=data["relations"]?.Deserialize<Dictionary<string,Dictionary<string,double>>>()??new();
        var affect=data["affect"]?.Deserialize<Dictionary<string,double>>()??new();
        string location=data["location"]?.GetValue<string>()??"";
        bool InRange(string id,double value)=>double.IsFinite(value)&&Profile.FeatureDefinitions.TryGetValue(id,out var d)&&value>=d.Minimum&&value<=d.Maximum;
        CharacterProfileSpec.Require(contacts.Count<=Profile.MaximumContacts&&contacts.All(c=>!string.IsNullOrWhiteSpace(c.Key)&&c.Value!=null&&c.Value.All(x=>Profile.RelationDefaults.ContainsKey(x.Key)&&InRange(x.Key,x.Value)))&&affect.All(x=>Profile.Affect.ContainsKey(x.Key)&&InRange(x.Key,x.Value)),"社会模块存档无效。");
        foreach(var n in Profile.Needs)if(needs.ContainsKey(n.Id))Needs.Values[n.Id]=needs[n.Id];
        foreach(var key in PrivateFacts.Keys.ToArray())if(priv.ContainsKey(key))PrivateFacts[key]=priv[key];
        Relations.Contacts.Clear();foreach(var e in contacts){var restored=Profile.RelationDefaults.ToDictionary(x=>x.Key,x=>x.Value.Number,StringComparer.Ordinal);foreach(var value in e.Value)restored[value.Key]=value.Value;Relations.Contacts[e.Key]=restored;}
        foreach(var e in affect)Affect.Values[e.Key]=e.Value;
        Location=location;LastHours=hours;Needs.Refresh();Revision++;
    }
}
