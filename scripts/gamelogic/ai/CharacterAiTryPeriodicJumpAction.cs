using Godot;

namespace GameLogic
{
    public class CharacterAiTryPeriodicJumpAction : BehaviorTreeActionBase
    {
        public GraphActionComponentReference Movement { get; set; } = new();
        public override string Description => "Try Periodic Jump";

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddChild(new Label { Text = "Periodic jump behavior" });
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
                    nameof(CharacterAiTryPeriodicJumpAction),
                    out CharacterMovementComponent2D movement,
                    out string componentError))
            {
                GD.PushError(componentError);
                return BehaviorTreeStatus.Failure;
            }

            float dt = (float)delta;
            ai.JumpCooldownTimer -= dt;
            ai.JumpSustainTimer -= dt;

            if (movement.IsOnFloor && ai.JumpCooldownTimer <= 0f)
            {
                ai.JumpSustainTimer = ai.JumpSustainDuration;
                ai.JumpCooldownTimer = ai.JumpInterval;
                ai.RequestFrameJumpStart();
            }

            ai.SetFrameJumpSustain(ai.JumpSustainTimer > 0f);
            return BehaviorTreeStatus.Success;
        }
    }
}
