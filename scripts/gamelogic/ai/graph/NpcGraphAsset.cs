using System;
using System.Linq;
using Godot;
namespace GameLogic.AI;

[Tool, GlobalClass]
public partial class NpcGraphAsset : GraphAsset
{
    public override string GraphType { get; set; } = "NPC";
    public override Type ComponentProviderType => typeof(AiAgentComponent3D);
    public override System.Collections.Generic.List<string> GetAllowedNodeTypes() => new();
    [Export] public Godot.Collections.Array<NpcDefinitionResource> Definitions { get; set; } = new();
    public NpcDefinitionResource Resolve(string id) => Definitions.FirstOrDefault(d => d?.DefinitionId == id);
    public override string GetEditorTitle() => "NPC 人物与行为";
#if TOOLS
    public override bool HasCustomPropertyEditors(GodotObject target) =>
        target==this||NpcExpressionEditorCatalog.Owns(this, target);
    public override string GetResourceEditorTitle(Resource resource)=>NpcResourcePresentation.Title(this,resource);
    public override string GetResourceEditorDescription(Resource resource)=>NpcResourcePresentation.Description(this,resource);
    public override EditorProperty CreateCustomPropertyEditor(GodotObject target, string property)
    {
        if (!HasCustomPropertyEditors(target)) return null;
        if(target==this&&property==nameof(Definitions))return new NpcDefinitionsProperty(this);
        if(target is NpcActionDefinition&&property==nameof(NpcActionDefinition.Inputs)||target is NpcPlanTemplateDefinition&&property==nameof(NpcPlanTemplateDefinition.Variables))return new NpcBindingsProperty((Resource)target,property);
        if(target is NpcActionInputDefinition input&&property==nameof(NpcActionInputDefinition.ResourceRequirements))return new NpcInputResourcesProperty(input);
        if(target is NpcBindingDefinition&&property is nameof(NpcBindingDefinition.Source) or nameof(NpcBindingDefinition.ValueType) or nameof(NpcBindingDefinition.Adapter) or nameof(NpcBindingDefinition.SelectorId) or nameof(NpcBindingDefinition.Variable))return new NpcBindingProperty(this,(Resource)target,property);
        if(target is NpcTargetSelectorDefinition&&property==nameof(NpcTargetSelectorDefinition.Adapter))return new NpcBindingProperty(this,(Resource)target,property);
        if(target is NpcTargetSelectorDefinition selector&&property is nameof(NpcTargetSelectorDefinition.Source) or nameof(NpcTargetSelectorDefinition.Selection) or nameof(NpcTargetSelectorDefinition.ParameterKey))return new NpcTargetProperty(this,selector,property);
        if (target is NpcNeedsDefinition needs && property == nameof(NpcNeedsDefinition.Needs)) return new NpcNeedsProperty(needs);
        if (target is NpcPlanTemplateDefinition plan && property == nameof(NpcPlanTemplateDefinition.ActionIds)) return new NpcPlanActionsProperty(this, plan);
        if (target is NpcEffectDefinition effect && property is nameof(NpcEffectDefinition.Key) or nameof(NpcEffectDefinition.Mode))
            return new NpcEffectProperty(this, effect, property);
        return NpcExpressionEditorCatalog.ExpectedType(this, target, property) is NpcValueType type
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
        if (resource is NpcNeedsDefinition needs)
            needs.Needs = needs.MergePreset(GD.Load<NpcNeedsDefinition>(NpcNeedsDefinition.CommonPresetPath));
        resource.DisplayName = node.GetDisplayName();
        Definitions.Add(resource); node.DefinitionId = resource.DefinitionId; MarkDirty();
        return resource;
    }
}
