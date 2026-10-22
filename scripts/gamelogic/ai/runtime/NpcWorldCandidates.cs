using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Goap;
namespace GameLogic.AI;

internal static class NpcWorldCandidates
{
    public static bool Open(AiAgentComponent3D agent,WorldCatalogEntry entry)
    {
        var world=agent.World.Session;if(world.IsDestroyed(entry.Id)||entry.OwnerId.Length>0&&entry.OwnerId!=agent.ActorId)return false;
        if(world.ResolveObject(entry.Id) is {} loaded&&(!loaded.IsActive||loaded.OwnerId.Length>0&&loaded.OwnerId!=agent.ActorId||loaded.Owner.GetComponent<WorldOpeningHoursComponent3D>() is {IsActive:false}||loaded.Owner.GetComponent<WorldInteractionComponent3D>() is {IsActive:false}))return false;
        var hours=entry.Capability<WorldOpeningHoursCapability>();
        return world.ObjectState(entry.Id).Capability<WorldOpeningHoursState>("opening_hours")?.ManualOpen??hours?.Includes(agent.SimulationHours%24)??true;
    }
    public static NpcTargetCandidate Candidate(AiAgentComponent3D agent,string id)
    {
        var world=agent.World.Session;if(!world.Catalog.Objects.TryGetValue(id??"",out var entry))return null;
        var actor=agent.ManagedActor;var route=world.Routes.Find(actor.Level,entry.Level.Id,agent);
        var point=world.ResolvePoint(id);bool local=actor.Level==entry.Level.Id;
        double? distance=local&&entry.Position.HasValue?agent.Owner.GlobalPosition.DistanceTo(point?.UsePosition??entry.Position.Value):null;
        double? hours=EstimateHours(agent,entry,route);
        var operations=entry.Capability<WorldInteractionCapability>()?.Operations;
        bool executable=operations!=null&&(point!=null&&local&&!agent.IsWorldProxy||operations.Count==0||operations.Any(o=>o.SupportsSemantic));
        return new(id,entry.AllTags,local?point?.UsePosition??entry.Position:null,distance,Open(agent,entry),route!=null&&(entry.Position.HasValue||point!=null),agent.PerceivedPoint(id),agent.KnownPoint(id),
            "point",entry.Type.Id,entry.EffectiveCapabilities.Select(c=>c.Key).ToArray(),entry.Capability<WorldInteractionCapability>()?.Operations.Where(o=>!agent.IsWorldProxy||o.SupportsSemantic).Select(o=>o.Id).ToArray(),
            entry.Level.Id,hours,local?(agent.IsWorldProxy?"semantic_local_estimate":"local_estimate"):route==null?"unknown_route":"world_route",executable);
    }
    static double? EstimateHours(AiAgentComponent3D agent,WorldCatalogEntry entry,WorldRoute route)
    {
        if(route==null||!entry.Position.HasValue)return null;
        double meters=0;var position=agent.Owner.GlobalPosition;
        foreach(var leg in route.Legs){meters+=position.DistanceTo(leg.Exit);position=leg.Entry;}
        meters+=position.DistanceTo(entry.Position.Value);
        return route.Hours+meters/Math.Max(.1,agent.ManagedActor.Definition.Speed)/agent.World.Session.Population.SecondsPerHour;
    }
    public static IEnumerable<NpcTargetCandidate> Query(AiAgentComponent3D agent,NpcTargetSelectorSpec selector)
    {
        var ids=NpcWorldTargetQuery.Ids(agent,selector);agent.RecordQuery(selector,ids);
        var world=agent.World.Session;var routes=new Dictionary<string,WorldRoute>(StringComparer.Ordinal);
        double Cost(WorldCatalogEntry entry)
        {
            if(!routes.TryGetValue(entry.Level.Id,out var route)){route=world.Routes.Find(agent.ManagedActor.Level,entry.Level.Id,agent);routes[entry.Level.Id]=route;}
            return EstimateHours(agent,entry,route)??double.PositiveInfinity;
        }
        var shortlist=ids.Select(id=>world.Catalog.Objects.GetValueOrDefault(id)).Where(e=>e!=null&&NpcTargetQuery.MatchesObject(selector,e.Type.Id,e.EffectiveCapabilities.Select(c=>c.Key),e.Capability<WorldInteractionCapability>()?.Operations.Select(o=>o.Id))
            &&selector.AllTags.All(e.AllTags.Contains)&&(selector.AnyTags.Count==0||selector.AnyTags.Any(e.AllTags.Contains))&&!selector.ExcludedTags.Any(e.AllTags.Contains))
            .OrderBy(Cost).ThenBy(e=>e.Id,StringComparer.Ordinal).Take(agent.QueryProbeLimit);
        foreach(var candidate in shortlist.Select(entry=>Candidate(agent,entry.Id)).Where(c=>c!=null&&c.Executable)
            .Where(c=>agent.ActivityPlace.Length==0||world.Catalog.InPlace(world.Catalog.Objects[c.Id].Location?.Id??world.Catalog.Objects[c.Id].Level.Location?.Id??"",agent.ActivityPlace))
            .Where(c=>selector.RequiredOperations==null||selector.RequiredOperations.All(agent.ActivityAllows))
            .OrderBy(c=>c.RankingCost).ThenBy(c=>c.Id,StringComparer.Ordinal).Take(agent.QueryProbeLimit))
        {
            if(!NpcTargetQuery.MatchesObject(selector,candidate.ObjectTypeId,candidate.Capabilities,candidate.Operations))continue;
            if(selector.ActionConstraint?.Graph?.Interaction is {} interaction&&(!agent.ActivityAllows(interaction.Operation)||candidate.Operations?.Contains(interaction.Operation)!=true))continue;
            if(!agent.IsWorldProxy&&candidate.LevelId==agent.ManagedActor.Level&&world.ResolvePoint(candidate.Id) is {} point&&selector.RequireReachable)
            {
                if(!agent.EstimatePath(point,out double distance))continue;
                yield return candidate with{Distance=distance,TravelHours=distance/Math.Max(.1,agent.ManagedActor.Definition.Speed)/world.Population.SecondsPerHour,CostSource="local_navigation"};continue;
            }
            yield return candidate;
        }
    }
    public static IEnumerable<KeyValuePair<string,GoapValue>> Capture(AiAgentComponent3D agent,IReadOnlyCollection<string> ids,bool planning)
    {
        var world=agent.World.Session;
        foreach(string id in ids)
        {
            var candidate=Candidate(agent,id);if(candidate==null)continue;
            var entry=world.Catalog.Objects[id];var state=world.ObjectState(id);string key="point."+id;
            var observation=agent.Character.WorldKnowledge.Observations.GetValueOrDefault(id);bool fresh=observation?.Fresh(agent.SimulationHours)==true;
            bool readable=!planning||agent.CanReadPoint(id)||entry.OwnerId==agent.ActorId;
            GoapValue Number(string member,double? number)=>number.HasValue?agent.Program.Encode(key+"."+member,CharacterValue.Numeric(number.Value)):default;
            yield return new(key+".open",readable?GoapValue.Bool(candidate.Usable):fresh?GoapValue.Bool(observation.Open):default);
            yield return new(key+".stock",Number("stock",readable?state.Capability<WorldStockState>("stock")?.Amount:fresh?observation.Stock:null));
            yield return new(key+".liquid",Number("liquid",readable?state.Capability<WorldLiquidState>("liquid")?.Volume:null));
            var capacity=state.Capability<WorldUsageCapacityState>("usage_capacity");
            yield return new(key+".available",readable&&capacity!=null?GoapValue.Bool(capacity.Available(agent.ActorId,world.RuntimeSeconds)):default);
            bool at=agent.IsWorldProxy?agent.State.Location==id:world.ResolvePoint(id)?.ContainsPosition(agent.Owner.GlobalPosition)==true;
            yield return new(key+".at",GoapValue.Bool(at));
            yield return new(key+".distance",Number("distance",candidate.Distance));yield return new(key+".travel_hours",Number("travel_hours",candidate.TravelHours));
            yield return new(key+".route_known",GoapValue.Bool(candidate.Reachable));yield return new(key+".executable",GoapValue.Bool(candidate.Executable));yield return new(key+".level",GoapValue.Id(entry.Level.Id));
            yield return new(key+".observation_age",Number("observation_age",observation==null?null:Math.Max(0,agent.SimulationHours-observation.Hours)));
            foreach(var operation in entry.Capability<WorldInteractionCapability>()?.Operations??new())
            {string op=NpcProgram.OperationKey(id,operation.Id);yield return new(op+".resource",GoapValue.Id(operation.ConsumedProperty));yield return new(op+".capacity",GoapValue.Bool(operation.RequireCapacity));}
        }
    }
    public static INpcResourceLease Acquire(AiAgentComponent3D agent,string id,string capability)
    {
        if(capability!="capacity"||!agent.World.Session.Catalog.Objects.TryGetValue(id,out var entry)||!Open(agent,entry))return null;
        if(agent.World.Session.ResolveObject(id)?.Owner.GetComponent<WorldUsageCapacityComponent3D>() is {IsActive:false})return null;
        var state=agent.World.Session.ObjectState(id).Capability<WorldUsageCapacityState>("usage_capacity");
        return state?.ReservePersistent(agent.ActorId,agent.World.Session.RuntimeSeconds)==true?new Lease(agent,id,state):null;
    }
    sealed class Lease(AiAgentComponent3D agent,string id,WorldUsageCapacityState state) : INpcResourceLease
    {
        bool released;
        public bool Renew()=>!released&&agent.World.Session.ObjectState(id).Capability<WorldUsageCapacityState>("usage_capacity")==state
            &&Open(agent,agent.World.Session.Catalog.Objects[id])&&state.ReservePersistent(agent.ActorId,agent.World.Session.RuntimeSeconds);
        public void Dispose(){if(released)return;released=true;state.Release(agent.ActorId);}
    }
}
