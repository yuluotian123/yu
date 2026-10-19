using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Goap;
using Godot;
namespace GameLogic.AI;

public sealed record NpcObjectProperty(string Name,string Label,NpcValueType Type,bool Writable=false,double Minimum=-1000000,double Maximum=1000000);
public interface INpcResourceLease : IDisposable { bool Renew(); }

/// <summary>Object-specific facts, validation, transactions and leases stay outside the action model.</summary>
public interface INpcBindingAdapter
{
    string Id { get; }
    string Label { get; }
    IReadOnlyList<NpcObjectProperty> Properties { get; }
    IReadOnlyList<string> Capabilities { get; }
    IEnumerable<KeyValuePair<string,GoapValue>> Capture(AiAgentComponent3D agent);
    IEnumerable<NpcTargetCandidate> Candidates(AiAgentComponent3D agent);
    bool Matches(AiAgentComponent3D agent,NpcValue value,NpcTargetSelectorSpec selector);
    GoapWorldState Project(NpcProgram program,GoapWorldState state,string prefix,NpcValue value,double? distance,bool physical);
    string WriteKey(string member,NpcValue value);
    bool Validate(AiAgentComponent3D agent,NpcValue value);
    bool CanClaim(NpcProgram program,GoapWorldState state,string prefix,string capability);
    INpcResourceLease Acquire(AiAgentComponent3D agent,NpcValue value,string capability);
    void Commit(AiAgentComponent3D agent,NpcValue value,string member,NpcValue result);
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
        new NpcObjectProperty("id","对象",NpcValueType.Symbol),new("open","可用",NpcValueType.Boolean),
        new("stock","库存",NpcValueType.Number,true,0),new("distance","距离",NpcValueType.Number),new("at","自身已在范围内",NpcValueType.Boolean),new("available","有空余容量",NpcValueType.Boolean)});
    public IReadOnlyList<string> Capabilities {get;}=Array.AsReadOnly(new[]{"capacity"});
    public GoapWorldState Project(NpcProgram program,GoapWorldState state,string prefix,NpcValue value,double? distance,bool physical)
    {
        string id=value.Symbol;
        double d=distance??(!physical&&program.Read(state,"self.location").Symbol==id?0:program.Read(state,"point."+id+".distance").Number);
        return state.With(GoapEffect.Set(prefix+".id",GoapValue.Id(id)),GoapEffect.Set(prefix+".open",state["point."+id+".open"]),
            GoapEffect.Set(prefix+".stock",state["point."+id+".stock"]),GoapEffect.Set(prefix+".available",state["point."+id+".available"]),GoapEffect.Set(prefix+".distance",program.Encode(prefix+".distance",NpcValue.Numeric(d))),
            GoapEffect.Set(prefix+".at",physical?state["point."+id+".at"]:GoapValue.Bool(program.Read(state,"self.location").Symbol==id)));
    }
    public string WriteKey(string member,NpcValue value)=>member=="stock"?"point."+value.Symbol+".stock":null;
    static AiInteractionPointComponent3D Point(AiAgentComponent3D agent,NpcValue value)=>agent.World?.Points.GetValueOrDefault(value.Symbol);
    public bool Validate(AiAgentComponent3D agent,NpcValue value)=>Point(agent,value) is {} p&&p.IsActive&&GodotObject.IsInstanceValid(p.Owner);
    public bool CanClaim(NpcProgram program,GoapWorldState state,string prefix,string capability)=>capability=="capacity"&&program.Read(state,prefix+".available").IsTrue;
    public IEnumerable<KeyValuePair<string,GoapValue>> Capture(AiAgentComponent3D agent)
    {
        foreach(var p in agent.World.Points.Values)
        {
            string key="point."+p.PointId;
            yield return new(key+".open",GoapValue.Bool(p.CanUse(agent.ActorId)));
            yield return new(key+".available",GoapValue.Bool(p.CanReserve(agent.ActorId)));
            yield return new(key+".at",GoapValue.Bool(p.ContainsPosition(agent.Owner.GlobalPosition)));
            yield return new(key+".stock",agent.Program.Encode(key+".stock",NpcValue.Numeric(p.Stock)));
            yield return new(key+".distance",agent.Program.Encode(key+".distance",NpcValue.Numeric(agent.Owner.GlobalPosition.DistanceTo(p.UsePosition))));
        }
    }
    public IEnumerable<NpcTargetCandidate> Candidates(AiAgentComponent3D agent)
    {
        foreach(var p in agent.World.Points.Values.Where(p=>p.IsActive))
        {
            double distance=agent.Owner.GlobalPosition.DistanceTo(p.UsePosition);
            bool reachable=agent.Navigation!=null&&agent.Navigation.TryEstimateBetween(agent.Owner.GlobalPosition,p.UsePosition,out distance);
            if(!reachable)distance=agent.Owner.GlobalPosition.DistanceTo(p.UsePosition);
            yield return new(p.PointId,Array.AsReadOnly(p.Tags.ToArray()),p.UsePosition,distance,p.CanUse(agent.ActorId),reachable,agent.PerceivedPoint(p.PointId),agent.Program.KnownPoints.Contains(p.PointId),Id);
        }
    }
    public bool Matches(AiAgentComponent3D agent,NpcValue value,NpcTargetSelectorSpec selector)
    {
        if(Point(agent,value) is not {} p)return false;
        var candidate=new NpcTargetCandidate(p.PointId,p.Tags.ToArray(),p.UsePosition,agent.Owner.GlobalPosition.DistanceTo(p.UsePosition),p.CanUse(agent.ActorId),true,agent.PerceivedPoint(p.PointId),agent.Program.KnownPoints.Contains(p.PointId));
        return NpcTargetQuery.Matches(agent.Program,selector,agent.Snapshot(),candidate);
    }
    public INpcResourceLease Acquire(AiAgentComponent3D agent,NpcValue value,string capability)=>capability=="capacity"&&Point(agent,value) is {} point&&point.TryReserve(agent.ActorId)?new Lease(agent,point):null;
    sealed class Lease(AiAgentComponent3D agent,AiInteractionPointComponent3D point) : INpcResourceLease
    {
        bool released;
        public bool Renew()=>!released&&agent.World?.Points.GetValueOrDefault(point.PointId)==point&&point.IsActive&&point.TryReserve(agent.ActorId);
        public void Dispose(){if(released)return;released=true;if(GodotObject.IsInstanceValid(point))point.Release(agent.ActorId);}
    }
    public void Commit(AiAgentComponent3D agent,NpcValue value,string member,NpcValue result)
    {
        if(member=="stock"&&Point(agent,value) is {} p){p.Stock=result.Number;p.MarkChanged();}
    }
}
