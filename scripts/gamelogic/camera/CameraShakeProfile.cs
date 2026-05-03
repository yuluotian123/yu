using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class CameraShakeProfile : Resource
    {
        [Export] public float Duration { get; set; } = 0.16f;
        [Export] public Vector2 Amplitude { get; set; } = new(8f, 5f);
        [Export] public float Frequency { get; set; } = 38f;
        [Export(PropertyHint.Range, "0,1,0.01")]
        public float RotationAmplitudeDegrees { get; set; } = 0f;
        [Export(PropertyHint.Range, "0,4,0.05")]
        public float DecayPower { get; set; } = 1.4f;

        public float SafeDuration => Mathf.Max(0.01f, Duration);
        public Vector2 SafeAmplitude => new(Mathf.Max(0f, Amplitude.X), Mathf.Max(0f, Amplitude.Y));
    }
}
