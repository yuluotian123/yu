using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.AI;
using Godot;

namespace GameLogic;

[GraphProvidedBy(typeof(AiAgentComponent3D))]
public sealed class NpcDialogueNodeData : SubGraphNodeData
{
    public const int PortType = 201;
    public string EntryId { get; set; } = "talk";
    public string Label { get; set; } = "交谈";
    public override List<string> GetGraphTypes() => new() { "NPC" };
    public override string GetCategory() => "NPC";
    public override string GetDisplayName() => "对话 · " + Label;
    public override string GetMenuName() => "对话入口";
    public override bool CanBePrime() => false;
    public override int GetInputPortType(int port) => PortType;
    public override int GetOutputCount() => 0;
    public override Type GetSubGraphType() => typeof(DialogueGraphAsset);
    public override GraphAsset CreateSubGraphAsset() => new DialogueGraphAsset();
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var box = new VBoxContainer();
        box.AddChild(new Label { Text = "将 NPC 根节点的「对话」端口连到这里。" });
        box.AddChild(new Label { Text = "入口标识（同一 NPC 内唯一）" });
        var id = new LineEdit { Text = EntryId };
        id.TextChanged += value => { EntryId = value; context.CurrentGraph.MarkDirty(); };
        box.AddChild(id);
        box.AddChild(new Label { Text = "交互名称" });
        var label = new LineEdit { Text = Label };
        label.TextChanged += value => { Label = value; context.CurrentGraph.MarkDirty(); };
        box.AddChild(label);
        return box;
    }
    public override void Validate(GraphAsset graph, GraphValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(EntryId)) result.AddError("对话入口标识不能为空。", Id);
        if (GetSubGraph() is not DialogueGraphAsset child) result.AddError("对话入口需要绑定 DialogueGraphAsset。", Id);
        else
        {
            var validation = GraphValidationService.Validate(child);
            DialogueSession.ValidateStructure(child, validation);
            if (!validation.IsValid) result.AddError("对话子图无效：" + validation.ToDisplayText(), Id);
        }
    }
    public static IEnumerable<NpcDialogueNodeData> Entries(NpcGraphAsset graph)
    {
        if (graph == null) return Enumerable.Empty<NpcDialogueNodeData>();
        var root = graph.Nodes.OfType<NpcRootNodeData>().FirstOrDefault();
        return root == null ? Enumerable.Empty<NpcDialogueNodeData>() : graph.Connections
            .Where(e => e.IsAvailable && e.FromNode == root.Id && e.FromPort == 3 && e.ToPort == 0)
            .Select(e => graph.FindNodeById(e.ToNode)).OfType<NpcDialogueNodeData>();
    }
}
