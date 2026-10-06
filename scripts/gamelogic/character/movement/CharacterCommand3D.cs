using Godot;

namespace GameLogic
{
    /// <summary>Commands are produced by controllers and consumed once by character movement.</summary>
    public readonly struct CharacterCommand3D
    {
        public CharacterCommand3D(
            float moveAxisX,
            bool jumpStartRequested,
            bool jumpSustainRequested, float moveAxisZ = 0f)
        {
            var axes = new Vector2(Mathf.Clamp(moveAxisX, -1f, 1f), Mathf.Clamp(moveAxisZ, -1f, 1f));
            MoveAxisX = axes.X;
            MoveAxisZ = axes.Y;
            JumpStartRequested = jumpStartRequested;
            JumpSustainRequested = jumpSustainRequested;
        }

        public float MoveAxisX { get; }
        public float MoveAxisZ { get; }
        public bool JumpStartRequested { get; }
        public bool JumpSustainRequested { get; }
        public static CharacterCommand3D None => new(0f, false, false);
    }
}
