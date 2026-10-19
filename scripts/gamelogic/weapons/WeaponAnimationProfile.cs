using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class WeaponAnimationProfile : Resource
    {
        private static readonly string[] RequiredAnimations =
            { "idle", "run", "attack", "jumpup", "inair", "isfalling", "land", "dash", "hurt", "death" };

        [Export] public SpriteFrames Frames { get; set; }
        [Export] public Vector2I FrameSize { get; set; } = new(224, 224);
        [Export] public Vector2I FootAnchor { get; set; } = new(112, 192);
        [Export] public float PixelSize { get; set; } = 0.0075f;
        [Export] public float VisualScale { get; set; } = 1.3f;
        [Export] public Godot.Collections.Dictionary<StringName, float> AnimationPixelSizes { get; set; } = new();
        [Export] public Godot.Collections.Dictionary<StringName, Vector2I> AnimationFootAnchors { get; set; } = new();

        public float PixelSizeFor(StringName animation) => AnimationPixelSizes.TryGetValue(animation, out float value) ? value : PixelSize;
        public Vector2I FootAnchorFor(StringName animation) => AnimationFootAnchors.TryGetValue(animation, out Vector2I value) ? value : FootAnchor;

        public bool IsValid()
        {
            if (Frames == null || !float.IsFinite(PixelSize) || PixelSize <= 0f ||
                !float.IsFinite(VisualScale) || VisualScale <= 0f || FrameSize.X <= 0 || FrameSize.Y <= 0 ||
                FootAnchor.X < 0 || FootAnchor.Y < 0 || FootAnchor.X >= FrameSize.X || FootAnchor.Y >= FrameSize.Y)
                return false;
            foreach (string name in RequiredAnimations)
                if (!Frames.HasAnimation(name) || Frames.GetFrameCount(name) == 0)
                    return false;
            foreach (var entry in AnimationPixelSizes)
                if (!Frames.HasAnimation(entry.Key) || !float.IsFinite(entry.Value) || entry.Value <= 0f)
                    return false;
            foreach (var entry in AnimationFootAnchors)
                if (!Frames.HasAnimation(entry.Key) || entry.Value.X < 0 || entry.Value.Y < 0 ||
                    entry.Value.X >= FrameSize.X || entry.Value.Y >= FrameSize.Y)
                    return false;
            return true;
        }
    }
}
