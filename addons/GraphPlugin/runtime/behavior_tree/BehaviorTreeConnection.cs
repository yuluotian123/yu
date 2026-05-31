using Godot;

public class BehaviorTreeConnection : GraphConnection
{
    public int Order { get; set; }
    public float Weight { get; set; } = 1f;

    public override string GetDisplayName()
    {
        string label = $"#{Order}";
        if (!Mathf.IsEqualApprox(Weight, 1f))
            label += $" w:{Weight:0.##}";

        return label;
    }

    public override Label CreateConnectionLabel()
    {
        var label = base.CreateConnectionLabel();
        label.Text = GetDisplayName();
        return label;
    }

#if TOOLS
    public override Control CreateEditUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(260f, 0f) };
        root.AddThemeConstantOverride("separation", 6);

        var orderRow = new HBoxContainer();
        orderRow.AddChild(new Label
        {
            Text = "Order",
            CustomMinimumSize = new Vector2(72f, 0f),
            VerticalAlignment = VerticalAlignment.Center
        });
        var orderSpin = new SpinBox
        {
            MinValue = -999,
            MaxValue = 999,
            Step = 1,
            Value = Order,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        orderSpin.ValueChanged += value => Order = (int)value;
        orderRow.AddChild(orderSpin);
        root.AddChild(orderRow);

        var weightRow = new HBoxContainer();
        weightRow.AddChild(new Label
        {
            Text = "Weight",
            CustomMinimumSize = new Vector2(72f, 0f),
            VerticalAlignment = VerticalAlignment.Center
        });
        var weightSpin = new SpinBox
        {
            MinValue = 0.001,
            MaxValue = 999999,
            Step = 0.1,
            Value = Weight,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        weightSpin.ValueChanged += value => Weight = (float)value;
        weightRow.AddChild(weightSpin);
        root.AddChild(weightRow);

        return root;
    }
#endif
}
