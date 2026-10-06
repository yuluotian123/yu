using System;
using Godot;

namespace GameLogic;

public static class GraphActionTarget
{
    public static void SetMoveAxis(GraphExecutionContext context, CharacterMovementComponent3D movement, float axis, int priority)
    {
        if (!GodotObject.IsInstanceValid(movement)) return;
        movement.StopMovementInput(priority);
        if (axis != 0) movement.AddMovementInput(axis, priority);
    }
    // A target is a world-space Vector3 or a scene node path stored in the blackboard.
    public static bool TryPosition(GraphActionValue target, GraphExecutionContext context, out Vector3 position)
    {
        position = default;
        if (!target.TryRead(context, out object value)) return false;
        if (value is Vector3 point) { position = point; return point.IsFinite(); }
        var host = context.GameObject;
        if (value is not string path || string.IsNullOrWhiteSpace(path)
            || !GodotObject.IsInstanceValid(host) || !host.IsInsideTree()) return false;
        var node = host.GetNodeOrNull<Node3D>(new NodePath(path));
        if (!GodotObject.IsInstanceValid(node) || node.IsQueuedForDeletion()) return false;
        position = node.GlobalPosition;
        return position.IsFinite();
    }
}

[GraphCallable("Find Nearest Target", "目标查询", GraphCallableUsage.All, ChineseName = "寻找最近目标")]
public sealed class FindNearestTargetAction : GraphInstantAction
{
    public string Group { get; set; } = "enemies";
    public GraphActionValue Radius { get; set; } = GraphActionValue.Number(6);
    public GraphBlackboardKeyReference ResultKey { get; set; } = new() { Key = "Target" };
    public override string Description => "Find Nearest Target";
    protected override bool Run(GraphActionInvocation call)
    {
        var context = call.Execution;
        var host = context.GameObject;
        if (!GodotObject.IsInstanceValid(host) || !host.IsInsideTree() || string.IsNullOrWhiteSpace(Group)
            || !Radius.TryNumber(context, out float radius) || radius < 0) return false;
        Node3D nearest = null;
        float distance = radius * radius;
        foreach (Node node in host.GetTree().GetNodesInGroup(Group))
        {
            if (node is not Node3D candidate || candidate == host || candidate.IsQueuedForDeletion()) continue;
            float next = host.GlobalPosition.DistanceSquaredTo(candidate.GlobalPosition);
            if (next > distance) continue;
            nearest = candidate; distance = next;
        }
        bool written = context.Blackboard.SetValue(ResultKey.Key, nearest?.GetPath().ToString() ?? "");
        return written && nearest != null;
    }
}

[GraphCallable("Target Valid", "目标查询", GraphCallableUsage.Flow | GraphCallableUsage.BehaviorTree, ChineseName = "目标有效")]
public sealed class TargetValidCondition : GraphSharedCondition
{
    public GraphActionValue Target { get; set; } = GraphActionValue.Key("Target");
    public override string Description => "Target Valid";
    public override bool IsMet(GraphExecutionContext context) => GraphActionTarget.TryPosition(Target, context, out _);
}

[GraphCallable("Target Within Distance", "目标查询", GraphCallableUsage.Flow | GraphCallableUsage.BehaviorTree, ChineseName = "目标距离判断")]
public sealed class TargetDistanceCondition : GraphSharedCondition
{
    public GraphActionValue Target { get; set; } = GraphActionValue.Key("Target");
    public GraphActionValue Distance { get; set; } = GraphActionValue.Number(1);
    public bool HorizontalOnly { get; set; }
    public override string Description => "Target Within Distance";
    public override bool IsMet(GraphExecutionContext context)
    {
        var host = context.GameObject;
        if (!GodotObject.IsInstanceValid(host) || !host.IsInsideTree()
            || !GraphActionTarget.TryPosition(Target, context, out Vector3 position)
            || !Distance.TryNumber(context, out float distance) || distance < 0) return false;
        return HorizontalOnly ? Mathf.Abs(host.GlobalPosition.X - position.X) <= distance
            : host.GlobalPosition.DistanceTo(position) <= distance;
    }
}

[GraphCallable("Face Target", "移动", GraphCallableUsage.All, ChineseName = "面向目标")]
public sealed class FaceTargetAction : GraphInstantAction
{
    public GraphActionComponentReference Movement { get; set; } = new();
    public GraphActionValue Target { get; set; } = GraphActionValue.Key("Target");
    public override string Description => "Face Target";
    protected override bool Run(GraphActionInvocation call)
    {
        if (!call.TryGetComponent("Component", Movement, Description, out CharacterMovementComponent3D movement, out _)
            || !GraphActionTarget.TryPosition(Target, call.Execution, out Vector3 position)
            || !GodotObject.IsInstanceValid(call.Execution.GameObject)) return false;
        float offset = position.X - call.Execution.GameObject.GlobalPosition.X;
        if (!Mathf.IsZeroApprox(offset)) movement.RestoreFacing(offset < 0 ? -1 : 1);
        if (movement.MovementSpace == CharacterMovementSpace.Free3D)
            movement.RestoreFacingDirection(position - call.Execution.GameObject.GlobalPosition);
        return true;
    }
}

[GraphCallable("Stop Movement", "移动", GraphCallableUsage.All, ChineseName = "停止移动")]
public sealed class StopMovementAction : GraphInstantAction
{
    public GraphActionComponentReference Movement { get; set; } = new();
    public int Priority { get; set; } = ComponentPriority.AI;
    public override string Description => "Stop Movement";
    protected override bool Run(GraphActionInvocation call)
    {
        if (!call.TryGetComponent("Component", Movement, Description, out CharacterMovementComponent3D movement, out _)) return false;
        GraphActionTarget.SetMoveAxis(call.Execution, movement, 0, Priority);
        return true;
    }
}

[GraphCallable("Move To Target", "移动", GraphCallableUsage.Flow | GraphCallableUsage.BehaviorTree, ChineseName = "接近目标")]
public sealed class MoveToTargetAction : GraphSharedAction
{
    public GraphActionComponentReference Movement { get; set; } = new();
    public GraphActionValue Target { get; set; } = GraphActionValue.Key("Target");
    public GraphActionValue ArrivalDistance { get; set; } = GraphActionValue.Number(0.24f);
    public GraphActionValue Timeout { get; set; } = GraphActionValue.Number(5);
    public int Priority { get; set; } = ComponentPriority.AI;
    public override string Description => "Move To Target";
    public override GraphActionTask CreateTask(GraphActionInvocation call)
    {
        CharacterMovementComponent3D movement = null;
        bool valid = call.TryGetComponent("Component", Movement, Description, out movement, out _);
        valid &= ArrivalDistance.TryNumber(call.Execution, out float arrival) && arrival >= 0;
        valid &= Timeout.TryNumber(call.Execution, out float timeout) && timeout > 0;
        double elapsed = 0;
        void Stop() => GraphActionTarget.SetMoveAxis(call.Execution, movement, 0, Priority);
        return new GraphDelegateTask(delta =>
        {
            elapsed += delta;
            var host = call.Execution.GameObject;
            if (!valid || !GodotObject.IsInstanceValid(movement) || !GodotObject.IsInstanceValid(host)
                || !host.IsInsideTree() || !GraphActionTarget.TryPosition(Target, call.Execution, out Vector3 target))
            { Stop(); return BehaviorTreeStatus.Failure; }
            Vector3 offset = target - host.GlobalPosition;
            offset.Y = 0f;
            if (movement.MovementSpace == CharacterMovementSpace.SideView) offset.Z = 0f;
            if (offset.Length() <= arrival) { Stop(); return BehaviorTreeStatus.Success; }
            if (elapsed >= timeout) { Stop(); return BehaviorTreeStatus.Failure; }
            Vector3 direction = offset.Normalized();
            movement.SubmitCommand(new CharacterCommand3D(direction.X, false, false, direction.Z), Priority);
            return BehaviorTreeStatus.Running;
        }, Stop);
    }
}
