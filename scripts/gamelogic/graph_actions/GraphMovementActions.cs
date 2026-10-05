using Godot;

namespace GameLogic;

public abstract class GraphMovementAction : GraphSharedAction
{
    public GraphActionComponentReference Movement { get; set; } = new();
    public int Priority { get; set; } = ComponentPriority.AI;
    protected bool Resolve(GraphActionInvocation call, out CharacterMovementComponent2D movement) =>
        call.TryGetComponent("Component", Movement, Description, out movement, out _);
    protected void Move(CharacterMovementComponent2D movement, float axis)
    {
        if (!GodotObject.IsInstanceValid(movement)) return;
        movement.StopMovementInput(Priority);
        if (axis != 0) movement.AddMovementInput(axis, Priority);
    }
}

[GraphCallable("Set Facing", "移动", ChineseName = "设置朝向")]
public sealed class SetFacingAction : GraphInstantAction
{
    public GraphActionComponentReference Movement { get; set; } = new();
    public GraphActionValue Direction { get; set; } = GraphActionValue.Number(1);
    public override string Description => "Set Facing";
    protected override bool Run(GraphActionInvocation call)
    {
        if (!call.TryGetComponent("Component", Movement, Description, out CharacterMovementComponent2D movement, out _)
            || !Direction.TryNumber(call.Execution, out float direction) || direction == 0) return false;
        movement.RestoreFacing(direction < 0 ? -1 : 1);
        return true;
    }
}

[GraphCallable("Move In Direction", "移动", GraphCallableUsage.Flow | GraphCallableUsage.BehaviorTree, ChineseName = "定向移动")]
public sealed class MoveInDirectionAction : GraphMovementAction
{
    public GraphActionValue Direction { get; set; } = GraphActionValue.Number(1);
    // Zero runs until the graph cancels this action. Direction is evaluated every tick.
    public GraphActionValue Duration { get; set; } = GraphActionValue.Number(0);
    public override string Description => "Move In Direction";
    public override GraphActionTask CreateTask(GraphActionInvocation call)
    {
        bool valid = Resolve(call, out var movement);
        valid &= Duration.TryNumber(call.Execution, out float duration) && duration >= 0;
        double elapsed = 0;
        return new GraphDelegateTask(delta =>
        {
            if (!valid || !GodotObject.IsInstanceValid(movement) || !Direction.TryNumber(call.Execution, out float axis))
            { Move(movement, 0); return BehaviorTreeStatus.Failure; }
            elapsed += delta;
            if (duration > 0 && elapsed >= duration) { Move(movement, 0); return BehaviorTreeStatus.Success; }
            Move(movement, Mathf.Clamp(axis, -1, 1));
            return BehaviorTreeStatus.Running;
        }, () => Move(movement, 0));
    }
}

[GraphCallable("Patrol", "移动", GraphCallableUsage.Flow | GraphCallableUsage.BehaviorTree, ChineseName = "往返巡逻", Keywords = "AI turn edge 转向 边缘")]
public sealed class PatrolAction : GraphMovementAction
{
    public GraphActionValue Distance { get; set; } = GraphActionValue.Number(120);
    public GraphActionValue StartDirection { get; set; } = GraphActionValue.Number(1);
    public GraphActionValue TurnPause { get; set; } = GraphActionValue.Number(0.12f);
    public GraphActionValue ReverseAtEdges { get; set; } = new() { Constant = new GraphBoolBlackboardValue { Value = true } };
    public GraphActionValue LookAhead { get; set; } = GraphActionValue.Number(18);
    public override string Description => "Patrol";
    public override GraphActionTask CreateTask(GraphActionInvocation call)
    {
        bool valid = Resolve(call, out var movement);
        var host = call.Execution.GameObject;
        valid &= GodotObject.IsInstanceValid(host) && host.IsInsideTree();
        valid &= Distance.TryNumber(call.Execution, out float distance) && distance > 0;
        valid &= StartDirection.TryNumber(call.Execution, out float initial) && initial != 0;
        valid &= TurnPause.TryNumber(call.Execution, out float pause) && pause >= 0;
        valid &= LookAhead.TryNumber(call.Execution, out float lookAhead) && lookAhead >= 0;
        valid &= ReverseAtEdges.TryRead(call.Execution, out object edgeValue) && edgeValue is bool;
        bool reverseAtEdges = edgeValue is true;
        float origin = valid ? host.GlobalPosition.X : 0;
        int direction = initial < 0 ? -1 : 1;
        double remaining = 0;
        return new GraphDelegateTask(delta =>
        {
            if (!valid || !GodotObject.IsInstanceValid(movement) || !GodotObject.IsInstanceValid(host) || !host.IsInsideTree())
            { Move(movement, 0); return BehaviorTreeStatus.Failure; }
            if (remaining > 0)
            { remaining -= delta; Move(movement, 0); return BehaviorTreeStatus.Running; }
            float offset = host.GlobalPosition.X - origin;
            bool atBound = direction > 0 ? offset >= distance : offset <= -distance;
            bool atEdge = reverseAtEdges && movement.IsOnFloor && !movement.HasGroundAhead(direction, lookAhead);
            if (atBound || atEdge)
            {
                direction = -direction;
                remaining = pause;
                movement.RestoreFacing(direction);
                Move(movement, 0);
            }
            else Move(movement, direction);
            return BehaviorTreeStatus.Running;
        }, () => Move(movement, 0));
    }
}

[GraphCallable("Jump", "移动 / 跳跃", GraphCallableUsage.Flow | GraphCallableUsage.BehaviorTree, ChineseName = "跳跃")]
public sealed class JumpAction : GraphMovementAction
{
    public GraphActionValue HoldDuration { get; set; } = GraphActionValue.Number(0.12f);
    public bool RequireGround { get; set; } = true;
    public override string Description => "Jump";
    public override GraphActionTask CreateTask(GraphActionInvocation call)
    {
        bool valid = Resolve(call, out var movement);
        valid &= HoldDuration.TryNumber(call.Execution, out float hold) && hold >= 0;
        bool started = false;
        double elapsed = 0;
        return new GraphDelegateTask(delta =>
        {
            if (!valid || !GodotObject.IsInstanceValid(movement)) return BehaviorTreeStatus.Failure;
            if (!started)
            {
                if (RequireGround && !movement.IsOnFloor) return BehaviorTreeStatus.Failure;
                started = true;
                movement.RequestJumpStart(Priority);
                movement.SetJumpSustain(hold > 0, Priority);
                // Preserve the start request until the movement component consumes this frame.
                return hold == 0 ? BehaviorTreeStatus.Success : BehaviorTreeStatus.Running;
            }
            elapsed += delta;
            movement.SetJumpSustain(elapsed < hold, Priority);
            return elapsed >= hold ? BehaviorTreeStatus.Success : BehaviorTreeStatus.Running;
        }, () => { if (started && GodotObject.IsInstanceValid(movement)) movement.ClearJumpInput(Priority); });
    }
}

[GraphCallable("Periodic Jump", "移动 / 跳跃", GraphCallableUsage.Flow | GraphCallableUsage.BehaviorTree, ChineseName = "周期跳跃", Keywords = "AI interval 定时")]
public sealed class PeriodicJumpAction : GraphMovementAction
{
    public GraphActionValue Interval { get; set; } = GraphActionValue.Number(1.8f);
    public GraphActionValue HoldDuration { get; set; } = GraphActionValue.Number(0.12f);
    public override string Description => "Periodic Jump";
    public override GraphActionTask CreateTask(GraphActionInvocation call)
    {
        bool valid = Resolve(call, out var movement);
        valid &= Interval.TryNumber(call.Execution, out float interval) && interval > 0;
        valid &= HoldDuration.TryNumber(call.Execution, out float hold) && hold >= 0;
        double cooldown = interval, remaining = 0;
        bool ownsJump = false;
        void Stop() { if (ownsJump && GodotObject.IsInstanceValid(movement)) movement.ClearJumpInput(Priority); ownsJump = false; }
        return new GraphDelegateTask(delta =>
        {
            if (!valid || !GodotObject.IsInstanceValid(movement)) { Stop(); return BehaviorTreeStatus.Failure; }
            cooldown -= delta;
            remaining = System.Math.Max(0, remaining - delta);
            if (cooldown <= 0 && movement.IsOnFloor)
            {
                movement.RequestJumpStart(Priority);
                ownsJump = true; remaining = hold; cooldown = interval;
            }
            if (ownsJump) movement.SetJumpSustain(remaining > 0, Priority);
            return BehaviorTreeStatus.Running;
        }, Stop);
    }
}

[GraphCallable("Is On Floor", "移动 / 检测", GraphCallableUsage.Flow | GraphCallableUsage.BehaviorTree, ChineseName = "处于地面")]
public sealed class IsOnFloorCondition : GraphSharedCondition
{
    public GraphActionComponentReference Movement { get; set; } = new();
    public override string Description => "Is On Floor";
    public override bool IsMet(GraphExecutionContext context) =>
        new GraphActionInvocation(context).TryGetComponent("Component", Movement, Description, out CharacterMovementComponent2D movement, out _) && movement.IsOnFloor;
}

[GraphCallable("Ground Ahead", "移动 / 检测", GraphCallableUsage.Flow | GraphCallableUsage.BehaviorTree, ChineseName = "前方有地面")]
public sealed class GroundAheadCondition : GraphSharedCondition
{
    public GraphActionComponentReference Movement { get; set; } = new();
    public GraphActionValue Direction { get; set; } = GraphActionValue.Number(1);
    public GraphActionValue LookAhead { get; set; } = GraphActionValue.Number(18);
    public override string Description => "Ground Ahead";
    public override bool IsMet(GraphExecutionContext context) =>
        new GraphActionInvocation(context).TryGetComponent("Component", Movement, Description, out CharacterMovementComponent2D movement, out _)
        && Direction.TryNumber(context, out float direction) && direction != 0
        && LookAhead.TryNumber(context, out float distance) && distance >= 0 && movement.HasGroundAhead(direction, distance);
}
