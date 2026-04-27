using System.Collections.Generic;
using Godot;

/// <summary>
/// 带条件的图连接，纯 C# 类。
/// 序列化由 GraphJsonHelper 负责。
/// </summary>
public class ConnectionWithConditon : GraphConnection
{
    public bool HasCondition { get; set; } = false;
    public ConditionExecuteMode ExecuteMode { get; set; } = ConditionExecuteMode.Sequence;
    public ConditionUseMode UseMode { get; set; } = ConditionUseMode.And;

    /// <summary>条件列表，纯 C# List，序列化时由 GraphJsonHelper 多态处理</summary>
    public List<ConditionBase> Conditions { get; set; } = new();

    // ── 运行时属性 ────────────────────────────────────────────────────────────

    public override bool IsAvailable
    {
        get
        {
            if (!HasCondition || Conditions.Count == 0) return true;

            switch (UseMode)
            {
                case ConditionUseMode.And:
                    foreach (var condition in Conditions)
                        if (!condition.IsConditionMet)
                            return false;
                    return true;

                case ConditionUseMode.Or:
                    foreach (var condition in Conditions)
                        if (condition.IsConditionMet)
                            return true;
                    return false;
            }

            return true;
        }
    }

    public bool IsSequence => ExecuteMode == ConditionExecuteMode.Sequence;
    public bool IsParallel => ExecuteMode == ConditionExecuteMode.Parallel;

    // ── CreateConnectionLabel ─────────────────────────────────────────────────

    public override Label CreateConnectionLabel()
    {
        string outstring = "";

        switch (ExecuteMode)
        {
            case ConditionExecuteMode.Parallel:
                outstring += "并行\n";
                break;
            case ConditionExecuteMode.Sequence:
                outstring += "序列\n";
                break;
        }

        if (Conditions != null && Conditions.Count > 0)
        {
            switch (UseMode)
            {
                case ConditionUseMode.And:
                    outstring += "同时达成以下条件：\n";
                    break;
                case ConditionUseMode.Or:
                    outstring += "达成以下任意条件：\n";
                    break;
            }

            foreach (var condition in Conditions)
                outstring += $"<if {condition.Description}\n";
        }

        var label = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = outstring
        };
        label.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.8f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        return label;
    }

    // ── CreateEditUi（仅编辑器）──────────────────────────────────────────────
#if TOOLS
    public override Control CreateEditUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.CustomMinimumSize = new Vector2(380, 0);

        // ── 1. 执行模式 & 使用模式 选择器 ────────────────────────────────────
        var modeRow = new HBoxContainer();
        root.AddChild(modeRow);

        modeRow.AddChild(new Label { Text = "执行模式：" });

        var execOption = new OptionButton();
        execOption.AddItem("序列", (int)ConditionExecuteMode.Sequence);
        execOption.AddItem("并行", (int)ConditionExecuteMode.Parallel);
        execOption.Selected = (int)ExecuteMode;
        execOption.ItemSelected += (idx) => ExecuteMode = (ConditionExecuteMode)(int)idx;
        modeRow.AddChild(execOption);

        modeRow.AddChild(new Label { Text = "   条件组合：" });

        var useOption = new OptionButton();
        useOption.AddItem("全部满足 (And)", (int)ConditionUseMode.And);
        useOption.AddItem("任意满足 (Or)", (int)ConditionUseMode.Or);
        useOption.Selected = (int)UseMode;
        useOption.ItemSelected += (idx) => UseMode = (ConditionUseMode)(int)idx;
        modeRow.AddChild(useOption);

        root.AddChild(new HSeparator());
        root.AddChild(new Label { Text = "条件列表：" });

        // ── 2. 可排序条件列表
        //   - 增：底部下拉选择子类型 → 点「+ 添加」
        //   - 删：每行 ✕ 按钮
        //   - 排序：每行 ↑ / ↓ 按钮
        var listControl = new ReorderableListControl<ConditionBase>(
            items: Conditions,
            buildItemUi: condition => condition.CreateEditUI(context),
            getItemLabel: condition => condition.GetType().Name,
            availableTypes: SubTypeCache.GetSubTypes<ConditionBase>(),
            factory: type => (ConditionBase)System.Activator.CreateInstance(type)
        );

        root.AddChild(listControl.Build());

        return root;
    }
#endif
}
