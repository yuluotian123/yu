#if TOOLS
using Godot;

[Tool]
public partial class GraphCanvasInspectorPlugin : EditorInspectorPlugin
{
    public EditorPlugin Plugin { get; set; }

    public override bool _CanHandle(GodotObject @object)
    {
        return @object is GraphAsset;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        var button = new Button { Text = "打开图编辑器" };
        button.Pressed += () => (Plugin as GraphPlugin)?.OpenGraphEditor(@object as GraphAsset);
        AddCustomControl(button);
    }
}
#endif
