using System.Collections.Generic;
using Godot;

/// <summary>
/// 图节点数据基类。
/// </summary>
/// <remarks>
/// <para>
/// 节点数据只负责保存可序列化状态、声明端口、提供运行时回调入口。
/// 节点在编辑器中的控件仍可通过 <see cref="CreateUI"/> 自定义，但节点实例创建、
/// 搜索分类和类型解析由 <see cref="GraphTypeRegistry"/> 与
/// <see cref="GraphNodeDefinition"/> 管理。
/// </para>
/// <para>
/// V2 中节点类型名仍来自 <see cref="NodeType"/>，它会写入 GraphJson。
/// 如果重命名节点类，应该同步更新注册名或在注册中心登记别名。
/// </para>
/// </remarks>
public class GraphNodeData
{
    /// <summary>图内唯一节点 id。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>编辑器画布位置。</summary>
    public Vector2 Position { get; set; } = Vector2.Zero;

    private string _nodeType;

    /// <summary>稳定节点类型名。默认使用 CLR 类型名。</summary>
    public virtual string NodeType
    {
        get => _nodeType ?? GetType().Name;
        set => _nodeType = value;
    }

    public GraphNodeData()
    {
        if (string.IsNullOrEmpty(Id))
            Id = GenerateUniqueId();
    }

    /// <summary>声明节点可用于哪些图类型。All 表示所有图都可用。</summary>
    public virtual List<string> GetGraphTypes() => new() { "All" };

    /// <summary>节点搜索菜单分类。</summary>
    public virtual string GetCategory() => "General";

    /// <summary>额外搜索关键字。</summary>
    public virtual List<string> GetSearchKeywords() => new();

    /// <summary>节点标题。</summary>
    public virtual string GetDisplayName() => NodeType;

    /// <summary>节点主题色。</summary>
    public virtual Color GetNodeColor() => Colors.White;

    /// <summary>输入端口数量。</summary>
    public virtual int GetInputCount() => 0;

    /// <summary>输出端口数量。</summary>
    public virtual int GetOutputCount() => 0;

    /// <summary>输入端口显示名。</summary>
    public virtual string GetInputPortName(int port) => GetInputCount() <= 1 ? "In" : $"In {port}";

    /// <summary>输出端口显示名。</summary>
    public virtual string GetOutputPortName(int port) => GetOutputCount() <= 1 ? "Out" : $"Out {port}";

    /// <summary>输入端口类型。</summary>
    public virtual int GetInputPortType(int port) => 0;

    /// <summary>输出端口类型。</summary>
    public virtual int GetOutputPortType(int port) => 0;

    /// <summary>输入端口颜色。</summary>
    public virtual Color GetInputPortColor(int port) => GetNodeColor();

    /// <summary>输出端口颜色。</summary>
    public virtual Color GetOutputPortColor(int port) => GetNodeColor();

    /// <summary>没有显式入口节点时，是否允许该节点作为图入口。</summary>
    public virtual bool CanBePrime() => true;

    /// <summary>指定输出端口允许的最大连接数。-1 表示不限制。</summary>
    public virtual int GetOutputMaxConnections(int port) => -1;

    /// <summary>指定输入端口允许的最大连接数。默认 1。</summary>
    public virtual int GetInputMaxConnections(int port) => 1;

    /// <summary>
    /// 从旧式 override API 自动生成 V2 节点定义。
    /// </summary>
    public virtual GraphNodeDefinition BuildDefinition()
    {
        var definition = new GraphNodeDefinition
        {
            NodeType = NodeType,
            DisplayName = GetDisplayName(),
            Category = GetCategory(),
            GraphTypes = GetGraphTypes() ?? new List<string> { "All" },
            SearchKeywords = GetSearchKeywords() ?? new List<string>()
        };

        for (int i = 0; i < GetInputCount(); i++)
        {
            definition.InputPorts.Add(new GraphPortDefinition
            {
                Direction = GraphPortDirection.Input,
                Name = GetInputPortName(i),
                PortType = GetInputPortType(i),
                Color = GetInputPortColor(i),
                MaxConnections = GetInputMaxConnections(i)
            });
        }

        for (int i = 0; i < GetOutputCount(); i++)
        {
            definition.OutputPorts.Add(new GraphPortDefinition
            {
                Direction = GraphPortDirection.Output,
                Name = GetOutputPortName(i),
                PortType = GetOutputPortType(i),
                Color = GetOutputPortColor(i),
                MaxConnections = GetOutputMaxConnections(i)
            });
        }

        return definition;
    }

    /// <summary>Builds the compact content shown inside the canvas node.</summary>
    public virtual void CreateNodeUI(GraphEditorContext context)
    {
        var root = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(150f, 0f)
        };

        var typeLabel = new Label
        {
            Text = NodeType,
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipText = true
        };
        typeLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.72f, 0.72f));
        root.AddChild(typeLabel);

        context.GraphNode.AddChild(root);
    }

    /// <summary>Builds the detailed inspector UI shown outside the canvas node.</summary>
    public virtual Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 6);

        root.AddChild(new Label
        {
            Text = GetDisplayName(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        root.AddChild(new HSeparator());
        root.AddChild(CreateInspectorInfoRow("Type", NodeType));
        root.AddChild(CreateInspectorInfoRow("Id", Id));
        return root;
    }

    protected static Control CreateInspectorInfoRow(string label, string value)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(78f, 0f),
            VerticalAlignment = VerticalAlignment.Center
        });

        var valueLabel = new Label
        {
            Text = value ?? string.Empty,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddChild(valueLabel);
        return row;
    }

    /// <summary>创建节点内部编辑 UI。</summary>
    public virtual void CreateUI(GraphEditorContext context)
    {
        var label = new Label { Text = GetDisplayName() + "." + Id };
        context.GraphNode.AddChild(label);
    }

    /// <summary>通用瞬时执行入口。Flow/State 可在自身语义中调用。</summary>
    public virtual void Execute(GraphExecutionContext context) { }

    private static string GenerateUniqueId()
    {
        ulong time = Time.GetTicksUsec();
        uint rand1 = (uint)GD.Randi();
        uint rand2 = (uint)GD.Randi();
        return $"{time:x}_{rand1:x}_{rand2:x}";
    }
}
