using Godot;

namespace GameLogic
{
    // Keyed against the sprite's actual frame clock, including variable frame durations.
    [Tool, GlobalClass]
    public partial class WeaponAttackTrail : Resource
    {
        [Export] public StringName Animation { get; set; } = new();
        [Export] public float[] Frames { get; set; } = System.Array.Empty<float>();
        // Native canvas pixels: blade base XY, tip ZW. The hilt is not luminous.
        [Export] public Vector4[] BladePoses { get; set; } = System.Array.Empty<Vector4>();
        [Export(PropertyHint.Range, "0.5,1.5,0.05")] public float FinishStrength { get; set; } = 1f;

        public bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Animation.ToString()) ||
                    Frames.Length < 2 || Frames.Length != BladePoses.Length ||
                    !float.IsFinite(FinishStrength) || FinishStrength <= 0) return false;
                for (int i = 0; i < Frames.Length; i++)
                    if (!float.IsFinite(Frames[i]) || Frames[i] < 0 || !BladePoses[i].IsFinite() ||
                        (i > 0 && Frames[i] <= Frames[i - 1])) return false;
                return true;
            }
        }
    }
}
