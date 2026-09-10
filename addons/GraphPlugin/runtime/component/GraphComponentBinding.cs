using GameLogic;

public enum GraphComponentBindingDirection
{
    ComponentToBlackboard,
    BlackboardToComponent,
    TwoWay
}

public sealed class GraphComponentBinding
{
    public string ComponentTypeName { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public GraphComponentBindingDirection Direction { get; set; } = GraphComponentBindingDirection.ComponentToBlackboard;

    public bool CanRead => Direction == GraphComponentBindingDirection.ComponentToBlackboard || Direction == GraphComponentBindingDirection.TwoWay;
    public bool CanWrite => Direction == GraphComponentBindingDirection.BlackboardToComponent || Direction == GraphComponentBindingDirection.TwoWay;
}

public static class GraphComponentBindingRuntime
{
    public static bool ApplyDefaultBindings(GraphAsset graph)
    {
        if (graph == null || !string.Equals(graph.GraphType, HfsmGraphAsset.GraphTypeName, System.StringComparison.Ordinal))
            return false;

        const string movementType = "GameLogic.CharacterMovementComponent2D";
        var defaults = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            ["Character.Movement.Mode"] = "MovementModeName",
            ["Character.Movement.IsOnFloor"] = "IsOnFloor",
            ["Character.Movement.MoveAxisX"] = "MoveInputX",
            ["Character.Movement.VelocityY"] = "VelocityY"
        };
        bool changed = false;
        foreach (GraphBlackboardEntry entry in graph.BlackboardEntries)
        {
            if (entry == null || entry.Binding != null || !defaults.TryGetValue(entry.Key, out string memberId))
                continue;
            entry.Binding = new GraphComponentBinding
            {
                ComponentTypeName = movementType,
                MemberId = memberId,
                Direction = GraphComponentBindingDirection.ComponentToBlackboard
            };
            changed = true;
        }
        if (changed)
            graph.MarkDirty();
        return changed;
    }

    public static bool SyncFromComponents(GraphExecutionContext context) => Sync(context, true);
    public static bool SyncToComponents(GraphExecutionContext context) => Sync(context, false);

    private static bool Sync(GraphExecutionContext context, bool toBlackboard)
    {
        if (context?.Graph?.BlackboardEntries == null)
            return true;

        ApplyDefaultBindings(context.Graph);

        bool success = true;
        GameObject2D owner = context.GetUserData<GameObject2D>();
        foreach (GraphBlackboardEntry entry in context.Graph.BlackboardEntries)
        {
            GraphComponentBinding binding = entry?.Binding;
            if (binding == null || (toBlackboard ? !binding.CanRead : !binding.CanWrite))
                continue;

            bool stepSuccess;
            string error;
            if (toBlackboard)
            {
                stepSuccess = GraphComponentInvoker.TryRead(owner, binding.ComponentTypeName, binding.MemberId, out object value, out error);
                if (stepSuccess)
                    stepSuccess = context.Blackboard.SetValue(entry.Key, value);
            }
            else
            {
                if (!context.Blackboard.TryGetValue(entry.Key, out object value))
                {
                    stepSuccess = false;
                    error = $"Blackboard key '{entry.Key}' is missing from the runtime scope.";
                }
                else
                {
                    stepSuccess = GraphComponentInvoker.TryWrite(owner, binding.ComponentTypeName, binding.MemberId,
                        value, out error);
                }
            }

            if (stepSuccess)
                continue;

            success = false;
            Godot.GD.PushError($"[GraphComponentBinding] {error}");
        }

        return success;
    }
}
