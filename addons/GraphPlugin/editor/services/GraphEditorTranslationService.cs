#if TOOLS
using Godot;

public static class GraphEditorTranslationService
{
    private const int DefaultColumn = 0;

    public static T DisableAutoTranslate<T>(T node) where T : Node
    {
        if (node == null)
            return null;

        node.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;

        if (node is Control control)
            control.TooltipAutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;

        return node;
    }

    public static void DisableAutoTranslateRecursive(Node node)
    {
        if (node == null)
            return;

        DisableAutoTranslate(node);
        foreach (Node child in node.GetChildren())
            DisableAutoTranslateRecursive(child);
    }

    public static TreeItem DisableAutoTranslate(TreeItem item, int column = DefaultColumn)
    {
        if (item == null)
            return null;

        item.SetAutoTranslateMode(column, Node.AutoTranslateModeEnum.Disabled);
        return item;
    }
}
#endif
