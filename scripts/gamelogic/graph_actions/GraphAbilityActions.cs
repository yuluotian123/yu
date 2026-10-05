using Godot;

namespace GameLogic;

[GraphCallable("Use Ability", "技能", GraphCallableUsage.Flow | GraphCallableUsage.BehaviorTree, ChineseName = "释放技能")]
public sealed class UseAbilityAction : GraphSharedAction
{
    public GraphActionComponentReference AbilitySystem { get; set; } = new();
    public GraphActionValue AbilityId { get; set; } = GraphActionValue.Text("attack");
    public bool WaitForCompletion { get; set; } = true;
    public bool CancelOnAbort { get; set; } = true;
    public GraphActionValue Timeout { get; set; } = GraphActionValue.Number(10);
    public override string Description => "Use Ability";
    public override GraphActionTask CreateTask(GraphActionInvocation call)
    {
        AbilitySystemComponent2D system = null;
        AbilityActivationHandle activation = null;
        string id = null;
        double elapsed = 0;
        bool started = false;
        bool valid = Timeout.TryNumber(call.Execution, out float timeout) && timeout > 0;
        void CancelOwned()
        {
            if (CancelOnAbort) activation?.Cancel("Graph action aborted");
        }
        return new GraphDelegateTask(delta =>
        {
            if (!started)
            {
                started = true;
                if (!valid || !AbilityId.TryRead(call.Execution, out object value) || value is not string abilityId
                    || !call.TryGetComponent("Component", AbilitySystem, Description, out system, out _)) return BehaviorTreeStatus.Failure;
                id = abilityId;
                if (system.TryActivateAbility(id, "SharedGraphAction") != AbilityActivationResult.Activated) return BehaviorTreeStatus.Failure;
                activation = new AbilityActivationHandle(system.GetRuntime(id));
                if (!WaitForCompletion) return BehaviorTreeStatus.Success;
            }
            if (activation?.State == AbilityActivationState.Completed) return BehaviorTreeStatus.Success;
            if (activation?.State != AbilityActivationState.Running) return BehaviorTreeStatus.Failure;
            elapsed += delta;
            if (elapsed >= timeout) { CancelOwned(); return BehaviorTreeStatus.Failure; }
            return BehaviorTreeStatus.Running;
        }, CancelOwned);
    }
}

[GraphCallable("Cancel Ability", "技能", GraphCallableUsage.All, ChineseName = "取消技能")]
public sealed class CancelAbilityAction : GraphInstantAction
{
    public GraphActionComponentReference AbilitySystem { get; set; } = new();
    public GraphActionValue AbilityId { get; set; } = GraphActionValue.Text("attack");
    public override string Description => "Cancel Ability";
    protected override bool Run(GraphActionInvocation call) => AbilityId.TryRead(call.Execution, out object value)
        && value is string id && call.TryGetComponent("Component", AbilitySystem, Description, out AbilitySystemComponent2D system, out _)
        && system.CancelAbility(id, "Graph cancel action");
}

[GraphCallable("Can Use Ability", "技能", GraphCallableUsage.Flow | GraphCallableUsage.BehaviorTree, ChineseName = "技能可用")]
public sealed class CanUseAbilityCondition : GraphSharedCondition
{
    public GraphActionComponentReference AbilitySystem { get; set; } = new();
    public GraphActionValue AbilityId { get; set; } = GraphActionValue.Text("attack");
    public override string Description => "Can Use Ability";
    public override bool IsMet(GraphExecutionContext context) => AbilityId.TryRead(context, out object value)
        && value is string id && GraphActionComponentResolver.TryResolve(context, AbilitySystem, Description, out AbilitySystemComponent2D system, out _)
        && system.CanActivateAbility(id) == AbilityActivationResult.Activated;
}
