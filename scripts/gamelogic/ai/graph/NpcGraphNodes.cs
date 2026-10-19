using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Godot;
namespace GameLogic.AI;

[GraphProvidedBy(typeof(AiAgentComponent3D))]
public class NpcRootNodeData : GraphNodeData
{
    public override List<string> GetGraphTypes() => new() { "NPC" };
    public override string GetDisplayName() => "AI · 决策";
    public override string GetDisplayName(GraphAsset graph)
    {
        if(graph is NpcGraphAsset {CharacterContext.Identity.DisplayName.Length:>0} npc)return "AI · "+npc.CharacterContext.Identity.DisplayName;
        return GetDisplayName();
    }
    public override string GetCategory() => "NPC";
    public override int GetOutputCount() => 3;
    public override string GetOutputPortName(int port) => port switch {0=>"模块",1=>"目标",_=>"目标选择器"};
    public override Color GetNodeColor() => new(.3f,.7f,.65f);
    public override GodotObject GetInspectorObject(GraphAsset graph) => graph;
    public override void CreateNodeUI(GraphEditorContext context) => context.GraphNode.AddChild(new Label { Text="决策规则、目标与计划",CustomMinimumSize=new(190,28) });
    public override void Validate(GraphAsset graph, GraphValidationResult result)
    {
        try { NpcProgram.Compile((NpcGraphAsset)graph); }
        catch (Exception error) { result.AddError(error.Message,Id); }
    }
}
[GraphProvidedBy(typeof(AiAgentComponent3D))]
public abstract class NpcDefinitionNodeData : GraphNodeData
{
    public string DefinitionId { get; set; } = "";
    public override string GetDisplayName(GraphAsset graph) => (graph as NpcGraphAsset)?.Resolve(DefinitionId) is { DisplayName.Length: > 0 } definition ? definition.DisplayName : GetDisplayName();
    [JsonIgnore] public abstract Type DefinitionType { get; }
    public override List<string> GetGraphTypes() => new() { "NPC" };
    public override bool CanBePrime() => false;
    public override string GetCategory() => "NPC";
    public override int GetInputCount() => 1;
    public override int GetInputMaxConnections(int port) => -1;
    public override GodotObject GetInspectorObject(GraphAsset graph) => ((NpcGraphAsset)graph).Ensure(this);
    public override void CreateNodeUI(GraphEditorContext context)
    {
        var definition = ((NpcGraphAsset)context.CurrentGraph).Ensure(this);
        var box = new VBoxContainer { CustomMinimumSize = new(180,0) };
        box.AddChild(new Label { Text = definition.Category });
        var graph = (NpcGraphAsset)context.CurrentGraph;
        if (this is not (NpcGoalNodeData or NpcActionNodeData or NpcTemplateNodeData or NpcTargetSelectorNodeData))
        {
            var choices = graph.Definitions.Where(d=>d!=null&&DefinitionType.IsInstanceOfType(d)).ToArray();
            var binding = new OptionButton();
            for(int i=0;i<choices.Length;i++) { binding.AddItem(string.IsNullOrWhiteSpace(choices[i].DisplayName)?GetDisplayName():choices[i].DisplayName); if(choices[i].DefinitionId==DefinitionId)binding.Select(i); }
            binding.ItemSelected += index => { DefinitionId=choices[(int)index].DefinitionId;graph.MarkDirty();context.GraphNode.Title=GetDisplayName(graph);context.InspectNode?.Invoke(context.GraphNode); };
            box.AddChild(binding);
        }
        if (definition is NpcActionDefinition action)
        {
            var edit = new Button { Text = "进入执行图" };
            edit.Pressed += () =>
            {
                action.ExecutionGraph ??= new FlowGraphAsset();
                context.OpenSubGraph?.Invoke(action.ExecutionGraph);
            };
            box.AddChild(edit);
        }
        context.GraphNode.AddChild(box);
    }
}
public class NpcScheduleNodeData : NpcDefinitionNodeData { public override Type DefinitionType => typeof(NpcScheduleDefinition); public override string GetDisplayName()=>"日程"; }
public class NpcPerceptionNodeData : NpcDefinitionNodeData { public override Type DefinitionType => typeof(NpcPerceptionDefinition); public override string GetDisplayName()=>"感知"; }
public class NpcMemoryNodeData : NpcDefinitionNodeData { public override Type DefinitionType => typeof(NpcMemoryDefinition); public override string GetDisplayName()=>"记忆"; }
public class NpcPolicyNodeData : NpcDefinitionNodeData { public override Type DefinitionType => typeof(NpcPolicyDefinition); public override string GetDisplayName()=>"决策预算"; }
public class NpcGoalNodeData : NpcDefinitionNodeData
{
    public override Type DefinitionType=>typeof(NpcGoalDefinition);
    public override string GetDisplayName()=>"目标";
    public override int GetOutputCount()=>2;
    public override string GetOutputPortName(int port)=>port==0?"候选行动":"计划";
}
public class NpcActionNodeData : NpcDefinitionNodeData { public override Type DefinitionType=>typeof(NpcActionDefinition); public override string GetDisplayName()=>"行动"; }
public class NpcTemplateNodeData : NpcDefinitionNodeData { public override Type DefinitionType=>typeof(NpcPlanTemplateDefinition); public override string GetDisplayName()=>"计划"; }
public class NpcTargetSelectorNodeData : NpcDefinitionNodeData { public override Type DefinitionType=>typeof(NpcTargetSelectorDefinition); public override string GetDisplayName()=>"目标选择器"; }
