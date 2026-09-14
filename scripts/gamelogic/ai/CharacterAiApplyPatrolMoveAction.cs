using Godot;

namespace GameLogic
{
    public class CharacterAiApplyPatrolMoveAction : BehaviorTreeActionBase
    {
        public GraphActionComponentReference Movement { get; set; } = new();
        public override string Description => "Apply Patrol Move";

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddChild(new Label { Text = "Patrol movement behavior" });
            root.AddChild(Movement.CreateEditUI("Movement Component", context, () => { }));
            return root;
        }

        public override BehaviorTreeStatus Tick(
            BehaviorTreeRuntime runtime,
            GraphExecutionContext context,
            double delta)
        {
            SimpleAICharacterControllerComponent2D ai = CharacterBehaviorTreeContext.GetAi(context);
            if (ai == null)
                return BehaviorTreeStatus.Failure;

            if (!GraphActionComponentResolver.TryResolve(
                    context,
                    Movement,
                    typeof(CharacterMovementComponent2D),
                    nameof(CharacterAiApplyPatrolMoveAction),
                    out _,
                    out string componentError))
            {
                GD.PushError(componentError);
                return BehaviorTreeStatus.Failure;
            }

            if (ai.TurnPauseTimer > 0f)
            {
                ai.TurnPauseTimer -= (float)delta;
                ai.SetFrameMoveAxis(0f);
                return BehaviorTreeStatus.Success;
            }

            ai.SetFrameMoveAxis(ai.Direction);
            return BehaviorTreeStatus.Success;
        }
    }
}
