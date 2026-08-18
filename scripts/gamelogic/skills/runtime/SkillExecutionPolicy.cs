namespace GameLogic
{
    public readonly struct SkillExecutionPolicy
    {
        public SkillExecutionPolicy(
            int priority,
            bool blocksMovement,
            bool blocksJump,
            bool canInterrupt)
        {
            Priority = priority;
            BlocksMovement = blocksMovement;
            BlocksJump = blocksJump;
            CanInterrupt = canInterrupt;
        }

        public int Priority { get; }
        public bool BlocksMovement { get; }
        public bool BlocksJump { get; }
        public bool CanInterrupt { get; }

        public static SkillExecutionPolicy Default => new(0, false, false, false);
    }
}
