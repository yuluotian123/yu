#if TOOLS
using Godot;

internal static class BehaviorTreeEditorUi
{
    public static Control BuildEnumRow<TEnum>(
        string label,
        TEnum selected,
        System.Action<TEnum> onChanged)
        where TEnum : struct, System.Enum
    {
        TEnum[] values = System.Enum.GetValues<TEnum>();
        var option = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        int selectedIndex = 0;
        for (int i = 0; i < values.Length; i++)
        {
            option.AddItem(values[i].ToString(), i);
            if (values[i].Equals(selected))
                selectedIndex = i;
        }

        option.Selected = selectedIndex;
        option.ItemSelected += index =>
        {
            if (index >= 0 && index < values.Length)
                onChanged(values[index]);
        };

        return BuildRow(label, option);
    }

    public static Control BuildSpinRow(
        string label,
        double value,
        double min,
        double max,
        double step,
        System.Action<double> onChanged)
    {
        var spin = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        spin.ValueChanged += changed => onChanged(changed);
        return BuildRow(label, spin);
    }

    private static Control BuildRow(string label, Control input)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(100, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        row.AddChild(input);
        return row;
    }
}
#endif
