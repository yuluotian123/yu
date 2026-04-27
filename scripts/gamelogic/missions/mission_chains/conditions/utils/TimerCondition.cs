using Godot;

/// <summary>
/// 计时条件：等待指定秒数后条件满足。
/// 纯 C# 类，序列化由 GraphJsonHelper 负责。
/// </summary>
public class TimerCondition : ConditionBase
{
    public float RequiredSeconds { get; set; } = 5.0f;

    private float _elapsedSeconds = 0f;

    public void Tick(float delta)
    {
        _elapsedSeconds += delta;
    }

    public void Reset()
    {
        _elapsedSeconds = 0f;
    }

    public override bool IsConditionMet => _elapsedSeconds >= RequiredSeconds;

    public override string Description => $"等待 {RequiredSeconds} 秒";

    public override Control CreateEditUI(GraphEditorContext context)
    {
        var hbox = new HBoxContainer();

        var label = new Label { Text = "需要等待（秒）：" };
        hbox.AddChild(label);

        var spin = new SpinBox
        {
            MinValue = 0.1,
            MaxValue = 99999,
            Step = 0.5,
            Value = RequiredSeconds,
            CustomMinimumSize = new Vector2(100, 0)
        };
        spin.ValueChanged += (v) => RequiredSeconds = (float)v;
        hbox.AddChild(spin);

        return hbox;
    }
}
