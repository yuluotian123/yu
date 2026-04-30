using Godot;

/// <summary>
/// Base graph connection data. Serialized by GraphJsonHelper into GraphAsset.ConnectionsJson.
/// </summary>
public class GraphConnection
{
    public string FromNode { get; set; } = "";
    public int FromPort { get; set; } = 0;
    public string ToNode { get; set; } = "";
    public int ToPort { get; set; } = 0;

    public virtual string GetDisplayName() => "Connection";
    public virtual bool IsEditable() => true;

    public virtual bool IsAvailable { get; } = true;

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
