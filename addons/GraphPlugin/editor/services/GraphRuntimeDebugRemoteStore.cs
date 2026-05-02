#if TOOLS
using System.Collections.Generic;

public static class GraphRuntimeDebugRemoteStore
{
    private static readonly List<GraphRuntimeDebugSnapshot> Snapshots = new();

    public static string SelectedOwnerPath { get; private set; } = string.Empty;
    public static bool HasReceivedSnapshots { get; private set; }
    public static int SnapshotCount => Snapshots.Count;

    public static void SetSelectedOwnerPath(string ownerPath)
    {
        SelectedOwnerPath = ownerPath ?? string.Empty;
    }

    public static void ReplaceSnapshots(IEnumerable<GraphRuntimeDebugSnapshot> snapshots)
    {
        Snapshots.Clear();
        if (snapshots != null)
            Snapshots.AddRange(snapshots);

        HasReceivedSnapshots = true;
    }

    public static void Clear()
    {
        Snapshots.Clear();
        SelectedOwnerPath = string.Empty;
        HasReceivedSnapshots = false;
    }

    public static List<GraphRuntimeDebugSnapshot> FindSnapshotsForSelectedOwner()
    {
        var result = new List<GraphRuntimeDebugSnapshot>();
        if (string.IsNullOrWhiteSpace(SelectedOwnerPath))
        {
            result.AddRange(Snapshots);
            return result;
        }

        for (int i = 0; i < Snapshots.Count; i++)
        {
            GraphRuntimeDebugSnapshot snapshot = Snapshots[i];
            if (snapshot == null)
                continue;

            if (OwnerPathMatches(SelectedOwnerPath, snapshot.OwnerPath))
                result.Add(snapshot);
        }

        return result;
    }

    public static GraphRuntimeDebugSnapshot ChooseBestSnapshot(
        IReadOnlyList<GraphRuntimeDebugSnapshot> snapshots,
        GraphAsset currentGraph)
    {
        if (snapshots == null || snapshots.Count == 0)
            return null;

        if (currentGraph != null)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                if (GraphRuntimeDebugUtil.FindScopeForGraph(snapshots[i].Scopes, currentGraph) != null)
                    return snapshots[i];
            }
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            if (snapshots[i].IsRunning)
                return snapshots[i];
        }

        return snapshots[0];
    }

    private static bool OwnerPathMatches(string selectedPath, string ownerPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath) || string.IsNullOrWhiteSpace(ownerPath))
            return false;

        if (selectedPath == ownerPath)
            return true;

        return selectedPath.StartsWith(ownerPath + "/", System.StringComparison.Ordinal);
    }
}
#endif
