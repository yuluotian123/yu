namespace GameLogic
{
    /// <summary>Runtime dependencies exposed to a skill without coupling every skill action to HFSM.</summary>
    public sealed class SkillExecutionContext
    {
        public HfsmRuntime Hfsm { get; init; }
        public HfsmComponent2D HfsmComponent => Hfsm?.Owner;
        public GameObject2D GameObject => Hfsm?.GameObject;
        public SkillManagerComponent2D Manager { get; init; }
    }
}
