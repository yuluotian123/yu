namespace GameLogic
{
    public class CharacterAiTryPeriodicJumpAction : BehaviorTreeActionBase
    {
        public override string Description => "Try Periodic Jump";

        public override BehaviorTreeStatus Tick(
            BehaviorTreeRuntime runtime,
            GraphExecutionContext context,
            double delta)
        {
            SimpleAICharacterControllerComponent2D ai = CharacterBehaviorTreeContext.GetAi(context);
            if (ai == null)
                return BehaviorTreeStatus.Failure;

            float dt = (float)delta;
            ai.JumpCooldownTimer -= dt;
            ai.JumpSustainTimer -= dt;

            if (ai.Movement != null && ai.Movement.IsOnFloor && ai.JumpCooldownTimer <= 0f)
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
