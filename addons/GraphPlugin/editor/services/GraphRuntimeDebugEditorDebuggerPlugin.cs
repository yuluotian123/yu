#if TOOLS
using System.Collections.Generic;
using Godot;
using Godot.Collections;

public partial class GraphRuntimeDebugEditorDebuggerPlugin : EditorDebuggerPlugin
{
    public override bool _HasCapture(string capture)
    {
        return capture == GraphRuntimeDebugBridge.CaptureName;
    }

    public override bool _Capture(string message, Array data, int sessionId)
    {
        if (message != GraphRuntimeDebugBridge.SnapshotMessage)
            return false;

        var snapshots = new List<GraphRuntimeDebugSnapshot>();
        if (data != null && data.Count > 0)
        {
            Array snapshotArray = data[0].AsGodotArray();
            for (int i = 0; i < snapshotArray.Count; i++)
            {
                Dictionary dict = snapshotArray[i].AsGodotDictionary();
                if (dict == null || dict.Count == 0)
                    continue;

                GraphRuntimeDebugSnapshot snapshot = GraphRuntimeDebugSerialization.DeserializeSnapshot(dict);
                if (snapshot != null)
                    snapshots.Add(snapshot);
            }
        }

        GraphRuntimeDebugRemoteStore.ReplaceSnapshots(snapshots);
        return true;
    }
}
#endif
