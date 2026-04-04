using System.Collections.Generic;
using Godot;

public partial class CalculatorNode : GraphNodeData
{
    public enum Operation { Add, Subtract, Multiply, Divide }

    public Operation CurrentOperation { get; set; } = Operation.Add;
    public float Value { get; set; } = 1.0f;
    public float Value2 { get; set; } = 1.0f;

    public override List<string> GetGraphTypes()
    {
        return new List<string> { "MissionGraph" };
    }

    public override string GetDisplayName() => "计算器";
    public override Color GetNodeColor() => Colors.Cyan;
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;

    public override void CreateUI(GraphNode node)
    {
        var vbox = new VBoxContainer();

        var label = new Label
        {
          Text = Id  
        };
        vbox.AddChild(label);

        var option = new OptionButton();
        option.AddItem("加法", (int)Operation.Add);
        option.AddItem("减法", (int)Operation.Subtract);
        option.AddItem("乘法", (int)Operation.Multiply);
        option.AddItem("除法", (int)Operation.Divide);
        option.Selected = (int)CurrentOperation;
        option.ItemSelected += (idx) => CurrentOperation = (Operation)idx;
        vbox.AddChild(option);

        var spin = new SpinBox
        {
            MinValue = -999999,
            MaxValue = 999999,
            Step = 0.1,
            Value = Value
        };
        spin.ValueChanged += (v) => Value = (float)v;
        vbox.AddChild(spin);

        var spin2 = new SpinBox
        {
            MinValue = -999999,
            MaxValue = 999999,
            Step = 0.1,
            Value = Value2
        };
        spin2.ValueChanged += (v) => Value2 = (float)v;
        vbox.AddChild(spin2);

        node.AddChild(vbox);
    }

    public override void Execute()
    {
        float result = CurrentOperation switch
        {
            Operation.Add => Value + Value2,
            Operation.Subtract => Value - Value2,
            Operation.Multiply => Value * Value2,
            Operation.Divide => Value2 != 0 ? Value / Value2 : 0.0f,
            _ => 0
        };

        GD.Print(result);
    }
}
