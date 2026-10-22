using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Goap;
using Godot;
namespace GameLogic.AI;

public sealed record NpcObjectProperty(string Name,string Label,CharacterValueType Type,bool Writable=false,double Minimum=-1000000,double Maximum=1000000);
public interface INpcResourceLease : IDisposable { bool Renew(); }

/// <summary>Object-specific facts, validation, transactions and leases stay outside the action model.</summary>
public interface INpcBindingAdapter
{
    string Id { get; }
    string Label { get; }
    IReadOnlyList<NpcObjectProperty> Properties { get; }
    IReadOnlyList<string> Capabilities { get; }
    IEnumerable<KeyValuePair<string,GoapValue>> Capture(AiAgentComponent3D agent,IReadOnlyCollection<string> ids,bool planning);
    IEnumerable<NpcTargetCandidate> Candidates(AiAgentComponent3D agent,NpcTargetSelectorSpec selector);
    bool Matches(AiAgentComponent3D agent,CharacterValue value,NpcTargetSelectorSpec selector);
    GoapWorldState Project(NpcProgram program,GoapWorldState state,string prefix,CharacterValue value,double? distance,bool physical);
    string WriteKey(string member,CharacterValue value);
    bool Validate(AiAgentComponent3D agent,CharacterValue value);
    bool CanClaim(NpcProgram program,GoapWorldState state,string prefix,string capability);
    INpcResourceLease Acquire(AiAgentComponent3D agent,CharacterValue value,string capability);
}

public static class NpcBindingAdapters
{
    static readonly Dictionary<string,INpcBindingAdapter> _adapters=new(StringComparer.Ordinal);
    static NpcBindingAdapters()=>Register(new NpcPointBindingAdapter());
    public static IEnumerable<INpcBindingAdapter> All=>_adapters.Values;
    public static void Register(INpcBindingAdapter adapter)
    {
        if(string.IsNullOrWhiteSpace(adapter?.Id)||!_adapters.TryAdd(adapter.Id,adapter))throw new ArgumentException("重复或无效的 NPC 对象适配器。");
    }
    public static INpcBindingAdapter Find(string id)=>_adapters.GetValueOrDefault(id??"");
}

public sealed class NpcPointBindingAdapter : INpcBindingAdapter
{
    public string Id=>"point";
    public string Label=>"场景交互点";
    public IReadOnlyList<NpcObjectProperty> Properties {get;}=Array.AsReadOnly(new[]{
        new NpcObjectProperty("id","对象",CharacterValueType.Symbol),new("open","可用",CharacterValueType.Boolean),
        new("stock","库存",CharacterValueType.Number,true,0),new("distance","距离",CharacterValueType.Number),new("at","自身已在范围内",CharacterValueType.Boolean),new("available","有空余容量",CharacterValueType.Boolean)});
    public IReadOnlyList<string> Capabilities {get;}=Array.AsReadOnly(new[]{"capacity"});
    public GoapWorldState Project(NpcProgram program,GoapWorldState state,string prefix,CharacterValue value,double? distance,bool physical)
    {
        string id=value.Symbol;
        double d=distance??(!physical&&program.Read(state,"self.location").Symbol==id?0:program.Read(state,"point."+id+".distance").Number);
        return state.With(GoapEffect.Set(prefix+".id",GoapValue.Id(id)),GoapEffect.Set(prefix+".open",state["point."+id+".open"]),
            GoapEffect.Set(prefix+".stock",state["point."+id+".stock"]),GoapEffect.Set(prefix+".available",state["point."+id+".available"]),GoapEffect.Set(prefix+".distance",program.Encode(prefix+".distance",CharacterValue.Numeric(d))),
            GoapEffect.Set(prefix+".at",physical?state["point."+id+".at"]:GoapValue.Bool(program.Read(state,"self.location").Symbol==id)));
    }
    public string WriteKey(string member,CharacterValue value)=>member=="stock"?"point."+value.Symbol+".stock":null;
    static WorldInteractionComponent3D Point(AiAgentComponent3D agent,CharacterValue value)=>agent.World?.Points.GetValueOrDefault(value.Symbol);
    static bool MatchesObject(WorldInteractionComponent3D point,NpcTargetSelectorSpec selector)=>NpcTargetQuery.MatchesObject(selector,
        point.Object.ObjectType.Id,point.Object.Definitions.Select(c=>c.Key),point.Operations?.Select(o=>o.Id));
    static NpcTargetCandidate Candidate(AiAgentComponent3D agent,WorldInteractionComponent3D point,double distance,bool reachable)=>new(
        point.PointId,Array.AsReadOnly(point.Tags.ToArray()),point.UsePosition,distance,point.Object.CanUse(agent.ActorId),reachable,
        agent.PerceivedPoint(point.PointId),agent.KnownPoint(point.PointId),"point",point.Object.ObjectType.Id,
        Array.AsReadOnly(point.Object.Definitions.Select(c=>c.Key).ToArray()),Array.AsReadOnly(point.Operations?.Select(o=>o.Id).ToArray()??Array.Empty<string>()));
    public bool Validate(AiAgentComponent3D agent,CharacterValue value)=>Point(agent,value) is {} p&&p.IsActive&&!agent.World.Session.IsDestroyed(p.PointId)&&GodotObject.IsInstanceValid(p.Owner);
    public bool CanClaim(NpcProgram program,GoapWorldState state,string prefix,string capability)=>capability=="capacity"&&program.Read(state,prefix+".available").IsTrue;
    public IEnumerable<KeyValuePair<string,GoapValue>> Capture(AiAgentComponent3D agent,IReadOnlyCollection<string> ids,bool planning)
    {
        foreach(var id in ids)
        {
            if(!agent.World.Points.TryGetValue(id,out var p))continue;
            string key="point."+p.PointId;
            bool readable=!planning||agent.CanReadPoint(id)||p.Object.OwnerId==agent.ActorId;
            var stock=p.Owner.GetComponent<WorldStockComponent3D>();var capacity=p.Owner.GetComponent<WorldUsageCapacityComponent3D>();
            yield return new(key+".open",readable?GoapValue.Bool(p.Object.CanUse(agent.ActorId)):default);
            yield return new(key+".available",readable&&capacity!=null?GoapValue.Bool(capacity.Available(agent.ActorId)):default);
            yield return new(key+".at",GoapValue.Bool(p.ContainsPosition(agent.Owner.GlobalPosition)));
            yield return new(key+".stock",readable&&stock is {IsActive:true}?agent.Program.Encode(key+".stock",CharacterValue.Numeric(stock.Amount)):default);
            yield return new(key+".distance",agent.Program.Encode(key+".distance",CharacterValue.Numeric(agent.Owner.GlobalPosition.DistanceTo(p.UsePosition))));
        }
    }
    public IEnumerable<NpcTargetCandidate> Candidates(AiAgentComponent3D agent,NpcTargetSelectorSpec selector)
    {
        var ids=NpcWorldTargetQuery.Ids(agent,selector);
        agent.RecordQuery(selector,ids);
        int wanted=selector.Selection==NpcTargetSelection.Candidates?Math.Min(agent.Program.Policy.Targets,selector.Limit>0?selector.Limit:agent.Program.Policy.Targets):1;
        var points=ids.Select(id=>agent.World.Points.GetValueOrDefault(id)).Where(p=>p is {IsActive:true}&&!agent.World.Session.IsDestroyed(p.PointId)
            &&MatchesObject(p,selector)
            &&selector.AllTags.All(p.Tags.Contains)&&(selector.AnyTags.Count==0||selector.AnyTags.Any(p.Tags.Contains))&&!selector.ExcludedTags.Any(p.Tags.Contains))
            .OrderBy(p=>agent.Owner.GlobalPosition.DistanceSquaredTo(p.UsePosition)).ThenBy(p=>p.PointId,StringComparer.Ordinal).Take(agent.QueryProbeLimit).ToArray();
        int found=0;
        foreach(var p in points)
        {
            double distance=agent.Owner.GlobalPosition.DistanceTo(p.UsePosition);
            if(selector.MaximumDistance>0&&distance>selector.MaximumDistance||selector.RequireUsable&&!p.Object.CanUse(agent.ActorId))continue;
            bool reachable=!selector.RequireReachable||agent.EstimatePath(p,out distance);
            if(selector.RequireReachable&&!reachable)continue;
            if(selector.Filter!=null)
            {
                var state=agent.Program.TargetContext(agent.Snapshot(new[]{p.PointId},true),p.PointId,distance,Id);
                if(!selector.Filter.Evaluate(k=>agent.Program.Read(state,k),agent.Program.Parameter).IsTrue)continue;
            }
            yield return Candidate(agent,p,distance,reachable);
            found++;
            if(selector.Selection is not (NpcTargetSelection.HighestScore or NpcTargetSelection.LowestScore)&&found>=wanted)yield break;
        }
    }
    public bool Matches(AiAgentComponent3D agent,CharacterValue value,NpcTargetSelectorSpec selector)
    {
        if(Point(agent,value) is not {} p)return false;
        var candidate=Candidate(agent,p,agent.Owner.GlobalPosition.DistanceTo(p.UsePosition),true);
        return NpcTargetQuery.Matches(agent.Program,selector,agent.Snapshot(),candidate);
    }
    public INpcResourceLease Acquire(AiAgentComponent3D agent,CharacterValue value,string capability)=>capability=="capacity"&&Point(agent,value)?.Owner.GetComponent<WorldUsageCapacityComponent3D>() is {} capacity&&capacity.Reserve(agent.ActorId)?new Lease(agent,capacity):null;
    sealed class Lease(AiAgentComponent3D agent,WorldUsageCapacityComponent3D capacity) : INpcResourceLease
    {
        bool released;
        public bool Renew()=>!released&&capacity.Object!=null&&agent.World?.Session.ResolveObject(capacity.Object.ObjectId)==capacity.Object&&capacity.IsActive&&capacity.Reserve(agent.ActorId);
        public void Dispose(){if(released)return;released=true;if(GodotObject.IsInstanceValid(capacity))capacity.Release(agent.ActorId);}
    }
}
