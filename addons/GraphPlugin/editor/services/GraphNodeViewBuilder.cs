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
        var body = new StyleBoxFlat { BgColor = new Color("22272f"), BorderColor = new Color("101317") };
        body.SetBorderWidthAll(1);
        body.SetCornerRadiusAll(5);
        var selected = (StyleBoxFlat)body.Duplicate();
        selected.BorderColor = new Color("e6b558");
        selected.SetBorderWidthAll(2);
        var title = new StyleBoxFlat { BgColor = data.GetNodeColor().Darkened(0.55f) };
        title.SetCornerRadiusAll(5);
        title.ContentMarginLeft = title.ContentMarginRight = 12;
        title.ContentMarginTop = title.ContentMarginBottom = 7;
        node.AddThemeStyleboxOverride("panel", body);
        node.AddThemeStyleboxOverride("panel_selected", selected);
        node.AddThemeStyleboxOverride("titlebar", title);
        node.AddThemeStyleboxOverride("titlebar_selected", title);
        GraphEditorTranslationService.DisableAutoTranslate(node);

        RefreshNodeUI(data, node, context);
        return node;
    }

    public static void RefreshNodeUI(GraphNodeData data, GraphNode node, GraphEditorContext context)
    {
        foreach (Node child in node.GetChildren())
        {
            GraphEditorSignalCleanup.DisconnectSubtree(child);
            node.RemoveChild(child); child.QueueFree();
        }
        node.ClearAllSlots();
        node.Title = data.GetDisplayName();
        int inputCount = data.GetInputCount();
        int outputCount = data.GetOutputCount();
        int maxSlots = Math.Max(inputCount, outputCount);
        Color color = data.GetNodeColor();

        // State machines use the whole state body as their connection anchor.
        // Transparent slots preserve GraphEdit routing/hit testing and stored topology.
        if (context.CurrentGraph is StateGraphAsset && maxSlots <= 1)
        {
            data.CreateNodeUI(context.WithGraphNode(data, node));
            if (node.GetChildCount() == 0) node.AddChild(new Control { CustomMinimumSize = new Vector2(150, 24) });
            using var transparentImage = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
            var hiddenPort = ImageTexture.CreateFromImage(transparentImage);
            node.SetSlot(0, inputCount > 0, inputCount > 0 ? data.GetInputPortType(0) : 0, color,
                outputCount > 0, outputCount > 0 ? data.GetOutputPortType(0) : 0, color, hiddenPort, hiddenPort);
            GraphEditorTranslationService.DisableAutoTranslateRecursive(node);
            node.CallDeferred("reset_size");
            return;
        }

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
