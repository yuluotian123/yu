#if TOOLS
using System;
using Godot;

/// <summary>
/// 负责把节点数据转换成 Godot GraphNode 视图。
/// </summary>
/// <remarks>
/// 旧版把这件事和类型注册混在一起。V2 把视图构建限制在 editor 层，
/// 运行时 registry 不再依赖 GraphNode。
/// </remarks>
public static class GraphNodeViewBuilder
{
    /// <summary>创建节点视图。</summary>
    public static GraphNode CreateNodeUI(GraphNodeData data, GraphEditorContext context)
    {
        var node = new GraphNode
        {
            Name = data.Id,
            Title = data.GetDisplayName(),
            PositionOffset = data.Position,
            Draggable = true,
            Resizable = true
        };
        GraphEditorTranslationService.DisableAutoTranslate(node);

        int inputCount = data.GetInputCount();
        int outputCount = data.GetOutputCount();
        int maxSlots = Math.Max(inputCount, outputCount);
        Color color = data.GetNodeColor();

        for (int i = 0; i < maxSlots; i++)
        {
            node.AddChild(CreatePortLabelRow(data, i, i < inputCount, i < outputCount));
            node.SetSlot(
                i,
                i < inputCount,
                i < inputCount ? data.GetInputPortType(i) : 0,
                i < inputCount ? data.GetInputPortColor(i) : color,
                i < outputCount,
                i < outputCount ? data.GetOutputPortType(i) : 0,
                i < outputCount ? data.GetOutputPortColor(i) : color);

            if (i < inputCount)
                node.SetSlotMetadataLeft(i, data.GetInputPortName(i));

            if (i < outputCount)
                node.SetSlotMetadataRight(i, data.GetOutputPortName(i));
        }

        data.CreateNodeUI(context.WithGraphNode(data, node));
        GraphEditorTranslationService.DisableAutoTranslateRecursive(node);
        node.CallDeferred("reset_size");
        return node;
    }

    private static Control CreatePortLabelRow(GraphNodeData data, int port, bool hasInput, bool hasOutput)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(150, 20)
        };

        var inputLabel = new Label
        {
            Text = hasInput ? data.GetInputPortName(port) : string.Empty,
            HorizontalAlignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true
        };
        row.AddChild(inputLabel);

        var outputLabel = new Label
        {
            Text = hasOutput ? data.GetOutputPortName(port) : string.Empty,
            HorizontalAlignment = HorizontalAlignment.Right,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true
        };
        row.AddChild(outputLabel);

        return row;
    }
}
#endif
