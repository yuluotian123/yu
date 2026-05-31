using System.Collections.Generic;
using System.Text.Json.Serialization;
using GameLogic;
using Godot;

/// <summary>
/// MissionGraph 中代表一个真实 Mission 的节点。
/// 
/// 节点进入时不会自己完成，而是把 Mission 部署请求交给 MissionGraphRuntime。
/// MissionManager 后续回调任务完成/移除时，runtime 才会把 completion 写回节点。
/// </summary>
public class MissionNode : GraphNodeData, IFlowNode
{
    /// <summary>
    /// 该 Mission 需要满足的需求模板。
    /// </summary>
    [JsonInclude]
    private readonly List<MissionRequireTemplate> _requires =
          new List<MissionRequireTemplate>();

    /// <summary>
    /// 多个需求的组合模式。
    /// </summary>
    [JsonInclude]
    private MissionRequireMode _mode;

    public override List<string> GetGraphTypes()
        => new List<string> { "MissionGraph" };
    public override string GetMenuName() => "Mission";
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;

    /// <summary>
    /// 根据当前节点配置创建 Mission 原型。
    /// GraphName 参数历史上叫 GraphName，新的调用语义实际传入的是 graphPath。
    /// 生成的 MissionId 为 graphPath.nodeId。
    /// </summary>
    public MissionPrototype<object> CreateMissionProto(string GraphName = "")
    {
        var proto = new MissionPrototype<object>(GraphName + "." + Id, _requires.ToArray(), _mode);
        return proto;
    }

    /// <summary>
    /// FlowGraphRuntime 进入节点时调用。
    /// MissionNode 只排队部署请求，等待 Manager drain 后创建真实 Mission。
    /// </summary>
    public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
    {
        if (runtime is MissionGraphRuntime missionRuntime)
        {
            missionRuntime.QueueMission(this);
            return;
        }

        GD.PushWarning($"[MissionNode] MissionNode can only run inside MissionGraphRuntime: {Id}");
    }

    public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }

    /// <summary>
    /// 查询 Mission 是否已经完成。
    /// completion 由 MissionGraphRuntime.OnMissionCompleted 或部署失败路径写入，只消费一次。
    /// </summary>
    public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
    {
        if (runtime is MissionGraphRuntime missionRuntime)
            return missionRuntime.TryConsumeNodeCompletion(Id, out completion);

        completion = NodeCompletion.NoOutput("MissingMissionRuntime");
        return true;
    }

    public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }

    /// <summary>
    /// 编辑器内的需求列表 UI。
    /// </summary>
    public override void CreateNodeUI(GraphEditorContext context)
    {
        var root = BuildNodeSummary();
        context.GraphNode.AddChild(root);
    }

    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(260f, 0f) };
        root.AddThemeConstantOverride("separation", 6);

        var modeOption = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        modeOption.AddItem("All requires", (int)MissionRequireMode.All);
        modeOption.AddItem("Any require", (int)MissionRequireMode.Any);
        modeOption.Selected = (int)_mode;
        modeOption.ItemSelected += index =>
        {
            _mode = (MissionRequireMode)(int)index;
            RefreshNodeSummary(context?.GraphNode);
        };
        root.AddChild(SkillActionEditorHelper.BuildRow("Mode", modeOption));

        root.AddChild(new Label { Text = "Mission Requires" });

        var listControl = new ReorderableListControl<MissionRequireTemplate>(
            items: _requires,
            buildItemUi: require => require.CreateEditUI(context),
            getItemLabel: GetRequireDescription,
            availableTypes: SubTypeCache.GetSubTypes<MissionRequireTemplate>(),
            factory: type => (MissionRequireTemplate)System.Activator.CreateInstance(type)
        );
        listControl.ListChanged += () => RefreshNodeSummary(context?.GraphNode);

        root.AddChild(listControl.Build());
        return root;
    }

    public override void CreateUI(GraphEditorContext context)
    {
        context.GraphNode.AddChild(CreateInspectorUI(context));

    }

    private VBoxContainer BuildNodeSummary()
    {
        var root = new VBoxContainer
        {
            Name = "MissionNodeSummary",
            CustomMinimumSize = new Vector2(180f, 0f)
        };
        root.AddThemeConstantOverride("separation", 3);

        string mode = _mode == MissionRequireMode.Any ? "Any require" : "All requires";
        root.AddChild(new Label
        {
            Text = mode,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        if (_requires == null || _requires.Count == 0)
        {
            AddSummaryLine(root, "No mission requires");
            return root;
        }

        for (int i = 0; i < _requires.Count; i++)
            AddSummaryLine(root, $"{i + 1}. {GetRequireDescription(_requires[i])}");

        return root;
    }

    private static void AddSummaryLine(VBoxContainer root, string text)
    {
        root.AddChild(new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.Off,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = text
        });
    }

    private void RefreshNodeSummary(GraphNode graphNode)
    {
        if (graphNode == null)
            return;

        var oldSummary = graphNode.GetNodeOrNull<VBoxContainer>("MissionNodeSummary");
        if (oldSummary != null)
        {
            graphNode.RemoveChild(oldSummary);
            oldSummary.QueueFree();
        }

        graphNode.AddChild(BuildNodeSummary());
        graphNode.CallDeferred("reset_size");
    }

    private static string GetRequireDescription(MissionRequireTemplate require)
    {
        if (require == null)
            return "null require";

        return string.IsNullOrWhiteSpace(require.Description)
            ? require.GetType().Name
            : require.Description;
    }

}
