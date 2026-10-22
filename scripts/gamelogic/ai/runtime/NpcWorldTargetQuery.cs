using System;
using System.Collections.Generic;
using System.Linq;
using Framework;
namespace GameLogic.AI;

/// <summary>Resolve identity and knowledge before touching loaded objects or navigation.</summary>
public static class NpcWorldTargetQuery
{
    public static IReadOnlyList<string> Ids(AiAgentComponent3D agent,NpcTargetSelectorSpec selector)
    {
        var world=(WorldModule)ModuleSystem.GetModule<IWorldModule>();var knowledge=agent.Character.WorldKnowledge;
        IEnumerable<string> ids=selector.Source switch
        {
            NpcTargetSource.FixedObject=>new[]{selector.FixedObjectId},
            NpcTargetSource.IdentityParameter=>new[]{agent.Program.Parameter(selector.ParameterKey).Symbol??""},
            NpcTargetSource.CharacterBinding=>new[]{knowledge.Objects.GetValueOrDefault(selector.ParameterKey,"")},
            NpcTargetSource.CurrentLocation=>new[]{agent.State.Location},
            NpcTargetSource.KnownPoints=>knowledge.KnownObjects,
            NpcTargetSource.PerceivedPoints=>agent.PerceivedPoints,
            NpcTargetSource.WorldPoints=>agent.World.Points.Keys,
            NpcTargetSource.WorldDirectory=>Directory(world,knowledge,selector,agent.Owner),
            _=>Array.Empty<string>()
        };
        return ids.Where(id=>!string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
    }
    public static IReadOnlyList<string> Directory(WorldModule world,WorldKnowledge knowledge,NpcTargetSelectorSpec selector,Godot.Node actor)
    {
        if(world.Catalog==null)return Array.Empty<string>();
        string level=selector.Scope==NpcWorldScope.CurrentLevel?world.LevelFor(actor)?.Id??"":"";
        string place=selector.Scope switch
        {
            NpcWorldScope.CurrentPlace=>world.ResolveObject((actor as GameObject3D)?.GetComponent<CharacterDataComponent3D>()?.State.Location)?.EffectiveLocation?.Id??world.LevelFor(actor)?.Location?.Id??"",
            NpcWorldScope.FixedPlace=>selector.LocationId,
            NpcWorldScope.CharacterPlace=>knowledge.Locations.GetValueOrDefault(selector.LocationBinding,""),
            _=>""
        };
        if(selector.Scope==NpcWorldScope.CurrentLevel&&level.Length==0||selector.Scope is NpcWorldScope.CurrentPlace or NpcWorldScope.FixedPlace or NpcWorldScope.CharacterPlace&&place.Length==0)return Array.Empty<string>();
        IEnumerable<WorldCatalogEntry> source=selector.Information==NpcWorldInformation.KnownObjects
            ?knowledge.KnownObjects.Select(id=>world.Catalog.Objects.GetValueOrDefault(id)).Where(record=>record!=null&&world.Catalog.Matches(record,level,place,selector.AllTags,selector.Adapter,selector.ObjectTypeId,selector.RequiredCapabilities,selector.RequiredOperations))
            :world.Catalog.Query(level,place,selector.AllTags,selector.Adapter,selector.ObjectTypeId,selector.RequiredCapabilities,selector.RequiredOperations);
        return source.Where(record=>
            selector.Information==NpcWorldInformation.Authoritative||knowledge.KnownObjects.Contains(record.Id)
            ||selector.Information==NpcWorldInformation.PublicServices&&world.Catalog.Locations.TryGetValue(record.Location?.Id??record.Level.Location?.Id??"",out var location)
                &&knowledge.KnownLocations.Contains(location.Id)&&location.PublicServices.Intersect(record.AllTags,StringComparer.Ordinal).Any())
            .Where(record=>!world.IsDestroyed(record.Id)).Select(record=>record.Id).ToArray();
    }
}
