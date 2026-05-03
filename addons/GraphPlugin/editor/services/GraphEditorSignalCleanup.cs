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
                if (callable.Equals(default(Callable)))
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
