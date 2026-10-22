using System;
using System.Linq;
using Godot;
namespace GameLogic.AI;

[Tool, GlobalClass]
public partial class NpcGraphAsset : GraphAsset
{
    public override string GraphType { get; set; } = "NPC";
    // Authoring context comes from the selected host, and is never serialized in the AI graph.
    public bool IncludeDecisionSymbols {get;set;}=true;
    public CharacterProfile CharacterContext {get;set;}
    public override Type ComponentProviderType => typeof(AiAgentComponent3D);
    public override System.Collections.Generic.List<string> GetAllowedNodeTypes() => new();
    [Export] public Godot.Collections.Array<NpcDefinitionResource> Definitions { get; set; } = new();
    public NpcDefinitionResource Resolve(string id) => Definitions.FirstOrDefault(d => d?.DefinitionId == id);
    public override string GetEditorTitle() => "AI 决策";
#if TOOLS
    readonly System.Collections.Generic.HashSet<ulong> _pendingConnectionRefresh=new();
    internal void QueueConnectionRefresh(GraphEdit edit,GraphNode anchor)
    {
        if(edit==null||anchor==null)return;
        ulong canvasId=edit.GetInstanceId();
        edit.SetMeta("npc_graph_asset",GetInstanceId());
        if(!_pendingConnectionRefresh.Add(canvasId))return;
        edit.ClearConnections();
        // Wait until every node has rebuilt its native ports before reconnecting the canvas.
        Callable.From(()=>
        {
            _pendingConnectionRefresh.Remove(canvasId);
            if(!GodotObject.IsInstanceValid(edit)||!GodotObject.IsInstanceValid(anchor)||anchor.GetParent()!=edit
                ||edit.GetMeta("npc_graph_asset").AsUInt64()!=GetInstanceId())return;
            var views=edit.GetChildren().OfType<GraphNode>().ToDictionary(n=>n.Name.ToString());
            edit.ClearConnections();
            foreach(var edge in Connections)
                if(views.TryGetValue(edge.FromNode,out var from)&&views.TryGetValue(edge.ToNode,out var to)
                    &&edge.FromPort>=0&&edge.FromPort<from.GetOutputPortCount()&&edge.ToPort>=0&&edge.ToPort<to.GetInputPortCount())
                    edit.ConnectNode(edge.FromNode,edge.FromPort,edge.ToNode,edge.ToPort);
        }).CallDeferred();
    }
    public Node EditorHostContext {get;private set;}
    public override void BindEditorHost(Node host)
    {
        EditorHostContext=host;
        if(host is GameObject3D actor)CharacterContext=actor.Components.OfType<CharacterDataComponent3D>().FirstOrDefault()?.Profile;
    }
    public override bool HasCustomPropertyEditors(GodotObject target) =>
        target==this||NpcExpressionEditorCatalog.Owns(this, target);
    public override string GetResourceEditorTitle(Resource resource)=>NpcResourcePresentation.Title(this,resource);
    public override string GetResourceEditorDescription(Resource resource)=>NpcResourcePresentation.Description(this,resource);
    public override EditorProperty CreateCustomPropertyEditor(GodotObject target, string property)
    {
        if (!HasCustomPropertyEditors(target)) return null;
        if(target==this&&property==nameof(Definitions))return new NpcDefinitionsProperty(this);
        if(target is NpcActionDefinition&&property==nameof(NpcActionDefinition.Inputs))return new NpcBindingsProperty((Resource)target,property);
        if(target is NpcActionInputDefinition input&&property==nameof(NpcActionInputDefinition.ResourceRequirements))return new NpcInputResourcesProperty(input);
        if(target is NpcActionInputDefinition&&property==nameof(NpcActionInputDefinition.InputType))return new NpcBindingProperty((Resource)target,property);
        if(target is NpcTargetSelectorDefinition&&property==nameof(NpcTargetSelectorDefinition.Adapter))return new NpcBindingProperty((Resource)target,property);
        if(target is NpcTargetSelectorDefinition selector&&property is nameof(NpcTargetSelectorDefinition.Source) or nameof(NpcTargetSelectorDefinition.Selection) or nameof(NpcTargetSelectorDefinition.ParameterKey) or nameof(NpcTargetSelectorDefinition.FixedObjectId) or nameof(NpcTargetSelectorDefinition.Scope) or nameof(NpcTargetSelectorDefinition.Information) or nameof(NpcTargetSelectorDefinition.LocationBinding))return new NpcTargetProperty(this,selector,property);
        if (target is NpcPlanTemplateDefinition plan && property == nameof(NpcPlanTemplateDefinition.ActionIds)) return new NpcPlanActionsProperty(this, plan);
        if (target is NpcEffectDefinition effect && property is nameof(NpcEffectDefinition.Key) or nameof(NpcEffectDefinition.Mode))
            return new NpcEffectProperty(this, effect, property);
        return NpcExpressionEditorCatalog.ExpectedType(this, target, property) is CharacterValueType type
            ? new NpcExpressionProperty(this, target, property, type) : null;
    }
#endif
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if(property["name"].AsString()==nameof(GraphJson))property["usage"]=(long)PropertyUsageFlags.Storage;
    }
    public NpcDefinitionResource Ensure(NpcDefinitionNodeData node)
    {
        var found = Resolve(node.DefinitionId);
        if (found != null) return found;
        var resource = (NpcDefinitionResource)Activator.CreateInstance(node.DefinitionType);
        resource.DisplayName = node.GetDisplayName();
        Definitions.Add(resource); node.DefinitionId = resource.DefinitionId; MarkDirty();
        return resource;
    }
}
