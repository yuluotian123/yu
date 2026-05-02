using System.Collections.Generic;
using Godot;
using Godot.Collections;

public static class GraphRuntimeDebugBridge
{
    public const string CaptureName = "graph_runtime_debug";
    public const string SnapshotMessage = CaptureName + ":snapshots";

    private const ulong MinSendIntervalMsec = 100;
    private static ulong _lastSendMsec;

    public static void NotifyChanged(bool force = false)
    {
        if (!EngineDebugger.IsActive())
            return;

        ulong now = Time.GetTicksMsec();
        if (!force && now - _lastSendMsec < MinSendIntervalMsec)
            return;

        _lastSendMsec = now;
        SendSnapshots();
    }

    private static void SendSnapshots()
    {
        List<GraphRuntimeDebugSnapshot> snapshots = GraphRuntimeDebugRegistry.CreateSnapshots();
        var snapshotArray = new Array();
        for (int i = 0; i < snapshots.Count; i++)
            snapshotArray.Add(GraphRuntimeDebugSerialization.SerializeSnapshot(snapshots[i]));

        var data = new Array { snapshotArray };
        EngineDebugger.SendMessage(SnapshotMessage, data);
    }
}
