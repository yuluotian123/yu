using System.Collections.Generic;
using Godot;

/// <summary>
/// Flow 连接的推进时机。
/// </summary>
public enum FlowConnectionMode
{
    /// <summary>
    /// 源节点完成时推进。对应传统流程图的“下一步”。
    /// </summary>
    Sequence,

    /// <summary>
    /// 源节点进入时推进。用于和源节点并行启动另一条流程。
    /// </summary>
    Parallel
}

/// <summary>
/// FlowGraph 默认连接。
/// 
/// 除了普通 GraphConnection 的端口信息，还包含推进时机和条件列表。
/// 业务图如果没有特殊连接类型，可以直接复用 FlowConnection。
/// </summary>
public class FlowConnection : GraphConnection
{
    /// <summary>
    /// 连接在源节点进入时还是完成时推进。
    /// </summary>
    public FlowConnectionMode Mode { get; set; } = FlowConnectionMode.Sequence;

    /// <summary>
    /// 多个条件的组合方式。
    /// </summary>
    public GraphConditionUseMode UseMode { get; set; } = GraphConditionUseMode.And;

    /// <summary>
    /// 连接条件。列表为空时视为总是可通过。
    /// </summary>
    public List<GraphConditionBase> Conditions { get; set; } = new();

    public bool IsSequence => Mode == FlowConnectionMode.Sequence;
    public bool IsParallel => Mode == FlowConnectionMode.Parallel;

    /// <summary>
    /// 判断当前执行上下文是否允许穿越该连接。
    /// </summary>
    public bool CanTraverse(GraphExecutionContext context)
    {
        if (Conditions == null || Conditions.Count == 0)
            return true;

        if (UseMode == GraphConditionUseMode.Or)
        {
            for (int i = 0; i < Conditions.Count; i++)
            {
                if (Conditions[i]?.IsMet(context) == true)
                    return true;
            }

            return false;
        }

        for (int i = 0; i < Conditions.Count; i++)
        {
            if (Conditions[i]?.IsMet(context) != true)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 连接标签中展示推进时机和条件数量。
    /// </summary>
    public override string GetDisplayName()
    {
        string mode = Mode == FlowConnectionMode.Parallel ? "Parallel" : "Sequence";
        if (Conditions == null || Conditions.Count == 0)
            return mode;

        string use = UseMode == GraphConditionUseMode.Or ? "Any" : "All";
        return $"{mode} ({use}: {Conditions.Count})";
    }

    /// <summary>
    /// 编辑器画布上的连接标签。
    /// </summary>
    public override Label CreateConnectionLabel()
    {
        var label = base.CreateConnectionLabel();
        label.Text = GetDisplayName();
        return label;
    }

#if TOOLS
    /// <summary>
    /// 编辑器属性 UI：推进时机、条件组合方式、条件列表。
    /// </summary>
    public override Control CreateEditUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };

        var modeRow = new HBoxContainer();
        modeRow.AddChild(new Label { Text = "推进时机：" });

        var modeOption = new OptionButton();
        modeOption.AddItem("完成后 (Sequence)", (int)FlowConnectionMode.Sequence);
        modeOption.AddItem("进入时 (Parallel)", (int)FlowConnectionMode.Parallel);
        modeOption.Selected = (int)Mode;
        modeOption.ItemSelected += index => Mode = (FlowConnectionMode)(int)index;
        modeRow.AddChild(modeOption);
        root.AddChild(modeRow);

        var useRow = new HBoxContainer();
        useRow.AddChild(new Label { Text = "条件组合：" });

        var useOption = new OptionButton();
        useOption.AddItem("全部满足 (And)", (int)GraphConditionUseMode.And);
        useOption.AddItem("任意满足 (Or)", (int)GraphConditionUseMode.Or);
        useOption.Selected = (int)UseMode;
        useOption.ItemSelected += index => UseMode = (GraphConditionUseMode)(int)index;
        useRow.AddChild(useOption);
        root.AddChild(useRow);

        root.AddChild(new HSeparator());
        root.AddChild(new Label { Text = "条件列表：" });

        var listControl = new ReorderableListControl<GraphConditionBase>(
            items: Conditions,
            buildItemUi: condition => condition.CreateEditUI(context),
            getItemLabel: condition => condition.Description,
            availableTypes: SubTypeCache.GetSubTypes<GraphConditionBase>(),
            factory: type => (GraphConditionBase)System.Activator.CreateInstance(type)
        );

        root.AddChild(listControl.Build());
        return root;
    }
#endif
}
