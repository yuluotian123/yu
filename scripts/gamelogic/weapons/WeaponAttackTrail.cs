using Godot;

namespace GameLogic
{
    // Keyed against the sprite's actual frame clock, including variable frame durations.
    [Tool, GlobalClass]
    public partial class WeaponAttackTrail : Resource
    {
        [Export] public string AbilityId { get; set; } = string.Empty;
        [Export] public StringName Animation { get; set; } = new();
        [Export] public float[] Frames { get; set; } = System.Array.Empty<float>();
        // Native canvas pixels: blade base XY, tip ZW. The hilt is not luminous.
        [Export] public Vector4[] BladePoses { get; set; } = System.Array.Empty<Vector4>();

        public bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AbilityId) || string.IsNullOrWhiteSpace(Animation.ToString()) ||
                    Frames.Length < 2 || Frames.Length != BladePoses.Length) return false;
                for (int i = 0; i < Frames.Length; i++)
                    if (!float.IsFinite(Frames[i]) || Frames[i] < 0 || !BladePoses[i].IsFinite() ||
                        (i > 0 && Frames[i] <= Frames[i - 1])) return false;
                return true;
            }
        }
    }
}
