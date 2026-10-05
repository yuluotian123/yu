#if TOOLS
using Godot;

public static class GraphEditorSignalCleanup
{
    public static void DisconnectSubtree(Node node)
    {
        if (node == null)
            return;

        foreach (Node child in node.GetChildren())
            DisconnectSubtree(child);

        DisconnectSignals(node);
    }

    private static void DisconnectSignals(GodotObject obj)
    {
        if (obj == null)
            return;

        foreach (Godot.Collections.Dictionary signal in obj.GetSignalList())
        {
            if (!signal.ContainsKey("name"))
                continue;

            StringName signalName = signal["name"].AsStringName();
            foreach (Godot.Collections.Dictionary connection in obj.GetSignalConnectionList(signalName))
            {
                if (!connection.ContainsKey("callable"))
                    continue;

                Callable callable = connection["callable"].AsCallable();
                // Native Tree/Container connections maintain internal item and layout state.
                // Only detach the managed delegates created by the plugin.
                if (callable.Delegate == null)
                    continue;
                GodotObject target = callable.Target;
                if (target == null && callable.Delegate == null)
                    continue;
                if (target != null && !GodotObject.IsInstanceValid(target))
                    continue;

                try
                {
                    if (obj.IsConnected(signalName, callable))
                        obj.Disconnect(signalName, callable);
                }
                catch
                {
                    // Editor UI is often rebuilt during tool-script reloads. Best effort cleanup.
                }
            }
        }
    }
}
#endif
