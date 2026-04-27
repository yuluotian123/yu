using Godot;

/// <summary>
/// 图连接数据，纯 C# 类。
/// 序列化由 GraphJsonHelper 负责（存储在 GraphAsset.ConnectionsJson 中）。
/// </summary>
public class GraphConnection
{
    public string FromNode { get; set; } = "";
    public int FromPort { get; set; } = 0;
    public string ToNode { get; set; } = "";
    public int ToPort { get; set; } = 0;

    public virtual string GetDisplayName() => " 连接";
    public virtual bool IsEditable() => true;

    public virtual bool IsAvailable{get;} = true;

    public virtual Control CreateEditUI(GraphEditorContext context)
    {
        var container = new VBoxContainer();
        var info = new Label
        {
            Text = $"从节点: {FromNode} (端口 {FromPort})\n到节点: {ToNode} (端口 {ToPort})"
        };
        container.AddChild(info);
        return container;
    }

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
