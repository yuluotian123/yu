#if TOOLS
using Godot;

public static class GraphEditorSignalCleanup
{
    public static void PrepareForRemoval(Node node)
    {
        if (!GodotObject.IsInstanceValid(node))
            return;
        MuteSubtree(node);
        // Queue while the node still belongs to its actual SceneTree. Once detached,
        // QueueFree falls back to the singleton tree (which can differ in editor tools).
        node.QueueFree();
    }

    private static void MuteSubtree(Node node)
    {
        // Godot releases un-restorable delegates before OnBeforeSerialize. Even
        // disconnecting them hashes the dead GC handle. Silence retiring controls;
        // their native destructor drops outgoing connections without hashing them.
        node.SetBlockSignals(true);
        foreach (Node child in node.GetChildren())
            MuteSubtree(child);
    }
}
#endif
