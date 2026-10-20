#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace GameLogic.AI;

public partial class NpcGraphAsset
{
    public override Godot.Collections.Array<Resource> CopyNodeResources(IReadOnlyList<GraphNodeData> nodes)
    {
        var resources = new Godot.Collections.Array<Resource>();
        foreach (string id in nodes.OfType<NpcDefinitionNodeData>().Select(n => n.DefinitionId).Distinct())
        {
            if (Resolve(id) is not { } definition) continue;
            // Graph nodes live in a lazy document; include execution edits not yet written to GraphJson.
            if (definition is NpcActionDefinition action) action.ExecutionGraph?.SaveJsonFields();
            resources.Add(definition.DuplicateDeep(Resource.DeepDuplicateMode.All));
        }
        return resources;
    }

    public override Godot.Collections.Array<Resource> PreparePasteResources(IReadOnlyList<GraphNodeData> nodes,
        Godot.Collections.Array<Resource> copiedResources)
    {
        var resources = new Godot.Collections.Array<Resource>();
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in copiedResources.OfType<NpcDefinitionResource>())
        {
            var copy = (NpcDefinitionResource)source.DuplicateDeep(Resource.DeepDuplicateMode.All);
            copy.DefinitionId = Guid.NewGuid().ToString("N");
            ids.Add(source.DefinitionId, copy.DefinitionId);
            resources.Add(copy);
        }
        foreach (var node in nodes.OfType<NpcDefinitionNodeData>())
            if (ids.TryGetValue(node.DefinitionId, out string id)) node.DefinitionId = id;
        // A copied plan uses the copied actions; references outside the selection stay intact.
        foreach (var plan in resources.OfType<NpcPlanTemplateDefinition>())
            plan.ActionIds = plan.ActionIds.Select(id => ids.GetValueOrDefault(id, id)).ToArray();
        return resources;
    }

    public override void AddNodeResources(Godot.Collections.Array<Resource> resources)
    {
        foreach (var definition in resources.OfType<NpcDefinitionResource>())
            if (!Definitions.Contains(definition)) Definitions.Add(definition);
        MarkDirty();
    }

    public override void RemoveNodeResources(Godot.Collections.Array<Resource> resources)
    {
        foreach (var definition in resources.OfType<NpcDefinitionResource>()) Definitions.Remove(definition);
        MarkDirty();
    }
}
#endif
