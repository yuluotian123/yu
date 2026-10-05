using System;
using Godot;

public sealed class GraphActionValue
{
    public bool UseBlackboard { get; set; }
    public GraphBlackboardKeyReference Blackboard { get; set; } = new();
    public GraphBlackboardValue Constant { get; set; } = new GraphFloatBlackboardValue();
    public bool TryRead(GraphExecutionContext context, out object value)
    {
        value = null;
        if (UseBlackboard)
        {
            if (context?.Blackboard?.TryGetValue(Blackboard.Key, out GraphBlackboardValue stored) != true) return false;
            value = stored.GetObjectValue();
            return value != null;
        }
        value = Constant?.GetObjectValue();
        return value != null;
    }
    public static GraphActionValue Number(float value) => new() { Constant = new GraphFloatBlackboardValue { Value = value } };
    public static GraphActionValue Text(string value) => new() { Constant = new GraphStringBlackboardValue { Value = value } };
    public static GraphActionValue Key(string key) => new() { UseBlackboard = true, Blackboard = new() { Key = key } };
    public bool TryNumber(GraphExecutionContext context, out float number)
    {
        number = 0;
        if (!TryRead(context, out object value) || value is not (int or float or double)) return false;
        number = Convert.ToSingle(value);
        return float.IsFinite(number);
    }
#if TOOLS
    public Control CreateEditUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        var source = new CheckBox { Text = "Use blackboard", ButtonPressed = UseBlackboard };
        root.AddChild(source);
        var key = Blackboard.CreateEditUI(context, "Key"); root.AddChild(key);
        var literal = new VBoxContainer(); root.AddChild(literal);
        var types = new[] { typeof(GraphBoolBlackboardValue), typeof(GraphIntBlackboardValue), typeof(GraphFloatBlackboardValue),
            typeof(GraphStringBlackboardValue), typeof(GraphVector2BlackboardValue) };
        var option = new OptionButton();
        foreach (var type in types) option.AddItem(((GraphBlackboardValue)Activator.CreateInstance(type)).DisplayName);
        option.Selected = Math.Max(0, Array.IndexOf(types, Constant.GetType())); literal.AddChild(option);
        Control input = Constant.CreateEditUI(context); literal.AddChild(input);
        option.ItemSelected += index =>
        {
            Constant = (GraphBlackboardValue)Activator.CreateInstance(types[(int)index]);
            literal.RemoveChild(input); input.QueueFree();
            input = Constant.CreateEditUI(context); literal.AddChild(input); context?.CurrentGraph?.MarkDirty();
        };
        source.Toggled += enabled => { UseBlackboard = enabled; key.Visible = enabled; literal.Visible = !enabled; context?.CurrentGraph?.MarkDirty(); };
        key.Visible = UseBlackboard; literal.Visible = !UseBlackboard;
        return root;
    }
#endif
}
