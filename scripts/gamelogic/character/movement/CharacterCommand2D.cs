using Godot;

namespace GameLogic
{
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
