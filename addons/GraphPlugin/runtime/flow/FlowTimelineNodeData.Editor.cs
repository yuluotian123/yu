using Godot;

public partial class FlowTimelineNodeData
{
    public override void CreateUI(GraphEditorContext context)
    {
        NormalizeTimelineData();

        var root = new VBoxContainer { CustomMinimumSize = new Vector2(260f, 0f) };

        var durationRow = new HBoxContainer();
        durationRow.AddChild(new Label
        {
            Text = "Duration",
            VerticalAlignment = VerticalAlignment.Center
        });

        var durationSpin = new SpinBox
        {
            MinValue = 0,
            MaxValue = 999999,
            Step = 0.01,
            Value = Duration,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        durationSpin.ValueChanged += value =>
        {
            Duration = (float)value;
            context.CurrentGraph?.MarkDirty();
        };
        durationRow.AddChild(durationSpin);
        root.AddChild(durationRow);

        root.AddChild(new Label
        {
            Text = $"{Tracks.Count} tracks, {Markers.Count} markers",
            ClipText = true
        });

        context.GraphNode.AddChild(root);
    }
}
