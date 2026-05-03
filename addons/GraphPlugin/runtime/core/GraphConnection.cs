using Godot;

/// <summary>
/// 图连线数据基类。
/// </summary>
/// <remarks>
/// 连线只保存“从哪个节点的哪个输出端口，到哪个节点的哪个输入端口”以及可选业务数据。
/// StateGraph、MissionGraph 可以继承它增加条件、优先级或执行模式。
/// </remarks>
public class GraphConnection
{
    /// <summary>起点节点 id。</summary>
    public string FromNode { get; set; } = string.Empty;

    /// <summary>起点输出端口索引。</summary>
    public int FromPort { get; set; }

    /// <summary>终点节点 id。</summary>
    public string ToNode { get; set; } = string.Empty;

    /// <summary>终点输入端口索引。</summary>
    public int ToPort { get; set; }

    /// <summary>编辑器连线标签文本。</summary>
    public virtual string GetDisplayName() => "Connection";

    /// <summary>是否允许在编辑器中打开连线属性面板。</summary>
    public virtual bool IsEditable() => true;

    /// <summary>运行时是否可用。子类可根据条件返回 false。</summary>
    public virtual bool IsAvailable => true;

    /// <summary>判断端点是否完全相同。</summary>
    public bool Matches(string fromNode, int fromPort, string toNode, int toPort)
    {
        return FromNode == fromNode &&
               FromPort == fromPort &&
               ToNode == toNode &&
               ToPort == toPort;
    }

    /// <summary>创建连线属性编辑 UI。</summary>
    public virtual Control CreateEditUI(GraphEditorContext context)
    {
        var container = new VBoxContainer();
        var info = new Label
        {
            Text = $"From node: {FromNode} (port {FromPort})\nTo node: {ToNode} (port {ToPort})"
        };
        container.AddChild(info);
        return container;
    }

    /// <summary>Builds the detailed inspector UI shown outside the canvas.</summary>
    public virtual Control CreateInspectorUI(GraphEditorContext context)
    {
        return CreateEditUI(context);
    }

    /// <summary>创建显示在连线中点附近的标签。</summary>
    public virtual Label CreateConnectionLabel()
    {
        var label = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = GetDisplayName()
        };
        label.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.8f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        return label;
    }
}
