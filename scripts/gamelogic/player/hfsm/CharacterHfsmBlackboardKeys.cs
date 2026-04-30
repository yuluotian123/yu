namespace GameLogic
{
    public static class CharacterHfsmBlackboardKeys
    {
        public const string IsOnFloor = "IsOnFloor";
        public const string JumpStartRequested = "JumpStartRequested";
        public const string JumpSustainRequested = "JumpSustainRequested";
        public const string MoveAxisX = "MoveAxisX";
        public const string VelocityY = "VelocityY";
        public const string DashStartRequested = "DashStartRequested";
        public const string DashActive = "DashActive";
        public const string DashFinished = "DashFinished";
        public const string AttackStartRequested = "AttackStartRequested";
        public const string AttackActive = "AttackActive";
        public const string AttackFinished = "AttackFinished";

        public const string DashingTag = "dashing";
        public const string AttackingTag = "attacking";
    }
}
