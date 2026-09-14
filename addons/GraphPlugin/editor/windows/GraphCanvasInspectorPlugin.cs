#if TOOLS
using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

[Tool]
public partial class GraphCanvasInspectorPlugin : EditorInspectorPlugin
{
    public EditorPlugin Plugin { get; set; }

    public override bool _CanHandle(GodotObject @object)
    {
        return GodotObject.IsInstanceValid(@object) &&
               (@object is GraphAsset || FindGraphProperties(@object).Count > 0);
    }

    public override void _ParseBegin(GodotObject @object)
    {
        if (@object is GraphAsset graph)
            AddGraphButton("Open Graph Editor", graph, @object);

        foreach ((string name, GraphAsset value) in FindGraphProperties(@object))
            AddGraphButton($"Open Graph: {name}", value, @object);
    }

    private void AddGraphButton(string text, GraphAsset graph, GodotObject source)
    {
        var button = new Button { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        button.Pressed += () => (Plugin as GraphPlugin)?.OpenGraphEditor(graph, source);
        AddCustomControl(button);
    }

    public static List<(string name, GraphAsset value)> FindGraphProperties(GodotObject target)
    {
        var result = new List<(string name, GraphAsset value)>();
        if (target == null || !GodotObject.IsInstanceValid(target))
            return result;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        foreach (PropertyInfo property in target.GetType().GetProperties(flags))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0 ||
                !typeof(GraphAsset).IsAssignableFrom(property.PropertyType))
                continue;

            try
            {
                if (property.GetValue(target) is GraphAsset graph)
                    result.Add((property.Name, graph));
            }
            catch (Exception exception)
            {
                GD.PushWarning($"[GraphCanvasInspectorPlugin] Could not read graph property {property.Name}: {exception.Message}");
            }
        }

        return result;
    }
}
#endif
