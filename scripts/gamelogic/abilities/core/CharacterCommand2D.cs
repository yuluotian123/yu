using Godot;

namespace GameLogic
{
    public readonly struct CharacterActionRequest
    {
        public CharacterActionRequest(string actionId, int priority = 0)
        {
            ActionId = actionId ?? string.Empty;
            Priority = priority;
        }

        public string ActionId { get; }
        public int Priority { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(ActionId);
    }

    /// <summary>Commands are produced by controllers and consumed once by character movement.</summary>
    public readonly struct CharacterCommand2D
    {
        public CharacterCommand2D(
            float moveAxisX,
            bool jumpStartRequested,
            bool jumpSustainRequested)
        {
            MoveAxisX = Mathf.Clamp(moveAxisX, -1f, 1f);
            JumpStartRequested = jumpStartRequested;
            JumpSustainRequested = jumpSustainRequested;
        }

        public float MoveAxisX { get; }
        public bool JumpStartRequested { get; }
        public bool JumpSustainRequested { get; }
        public static CharacterCommand2D None => new(0f, false, false);
    }
}
