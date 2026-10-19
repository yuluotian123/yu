using System;
using System.Linq;
using Godot;
namespace GameLogic.AI;

public partial class NpcGraphAsset
{
#if TOOLS
    public override void PrepareForEditing()=>MigrateActionBindings();
#endif
    /// <summary>Preserve old authored actions, including their execution graphs, names and connections.</summary>
    public bool MigrateTargetSelectors()
    {
        var root=Nodes.OfType<NpcRootNodeData>().FirstOrDefault();
        if(root==null)return false;
        bool changed=false;
        foreach(var action in Definitions.OfType<NpcActionDefinition>().ToArray())
        {
            if(action.TargetTags.Length==0&&action.TargetIdParameter.Length==0)continue;
            if(action.TargetSelectorId.Length==0&&action.TargetTags.Length>0)
            {
                var selector=new NpcTargetSelectorDefinition{DisplayName=action.DisplayName+" · 目标",AllTags=action.TargetTags.ToArray(),
                    Source=action.TargetIdParameter.Length>0?NpcTargetSource.IdentityParameter:NpcTargetSource.PerceivedPoints,ParameterKey=action.TargetIdParameter};
                var owner=Nodes.OfType<NpcActionNodeData>().FirstOrDefault(n=>n.DefinitionId==action.DefinitionId);
                var node=new NpcTargetSelectorNodeData{Id="targets_"+Guid.NewGuid().ToString("N"),DefinitionId=selector.DefinitionId,
                    Position=owner?.Position+new Vector2(400,0)??new Vector2(1400,Nodes.OfType<NpcTargetSelectorNodeData>().Count()*160)};
                Definitions.Add(selector);Nodes.Add(node);MarkDirty();ConnectNodes(root.Id,2,node.Id,0);
                action.TargetSelectorId=selector.DefinitionId;action.ArrivesAtTarget=true;action.ShareTargetWithinPlan=false;
            }
            action.TargetTags=Array.Empty<string>();action.TargetIdParameter="";changed=true;
        }
        // Resource.Duplicate reads exported GraphJson, not the lazy document cache.
        if(changed){SaveJsonFields();MarkDirty();}
        return changed;
    }
}
