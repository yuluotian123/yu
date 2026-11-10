using Godot;

namespace GameLogic
{
    [Tool, GlobalClass]
    public partial class WeaponAnimationProfile : Resource
    {
        private static readonly string[] RequiredAnimations =
            { "run", "attack", "jumpup", "inair", "isfalling", "land", "dash", "hurt", "death" };

        [Export] public SpriteFrames Frames { get; set; }
        [Export] public SpriteFrames AdditionalFrames { get; set; }
        [Export] public SpriteFrames TransitionFrames { get; set; }
        [Export] public SpriteFrames ActionFrames { get; set; }
        [Export] public SpriteFrames AttackFrames { get; set; }
        [Export] public Godot.Collections.Array<WeaponAttackTrail> AttackTrails { get; set; } = new();
        [Export] public Vector2I FrameSize { get; set; } = new(224, 224);
        [Export] public Vector2I FootAnchor { get; set; } = new(112, 192);
        [Export] public float PixelSize { get; set; } = 0.0075f;
        [Export] public float VisualScale { get; set; } = 1.3f;
        [Export] public Godot.Collections.Dictionary<StringName, float> AnimationPixelSizes { get; set; } = new();
        [Export] public Godot.Collections.Dictionary<StringName, Vector2I> AnimationFootAnchors { get; set; } = new();

        public float PixelSizeFor(StringName animation) => AnimationPixelSizes.TryGetValue(animation, out float value) ? value : PixelSize;
        public Vector2I FootAnchorFor(StringName animation) => AnimationFootAnchors.TryGetValue(animation, out Vector2I value) ? value : FootAnchor;

        private SpriteFrames FramesFor(StringName animation) =>
            TransitionFrames?.HasAnimation(animation) == true ? TransitionFrames :
            AttackFrames?.HasAnimation(animation) == true ? AttackFrames :
            ActionFrames?.HasAnimation(animation) == true ? ActionFrames :
            AdditionalFrames?.HasAnimation(animation) == true ? AdditionalFrames : Frames;

        public bool HasAnimation(StringName animation) =>
            FramesFor(animation)?.HasAnimation(animation) == true && FramesFor(animation).GetFrameCount(animation) > 0;

        public bool IsOneShot(StringName animation) => HasAnimation(animation) &&
            FramesFor(animation).GetAnimationLoopMode(animation) == SpriteFrames.LoopMode.None &&
            FramesFor(animation).GetAnimationSpeed(animation) > 0;

        public bool IsLoop(StringName animation) => HasAnimation(animation) &&
            FramesFor(animation).GetAnimationLoopMode(animation) != SpriteFrames.LoopMode.None &&
            FramesFor(animation).GetAnimationSpeed(animation) > 0;

        public Vector2I FrameSizeFor(StringName animation) => HasAnimation(animation)
            ? (Vector2I)FramesFor(animation).GetFrameTexture(animation, 0).GetSize() : FrameSize;

        // Compose per equipment instance; never mutate the shared art resources.
        public SpriteFrames CreatePlaybackFrames()
        {
            if (AdditionalFrames == null && TransitionFrames == null && ActionFrames == null && AttackFrames == null) return Frames;
            var combined = (SpriteFrames)Frames.Duplicate();
            foreach (SpriteFrames source in new[] { AdditionalFrames, ActionFrames, AttackFrames, TransitionFrames })
            {
                if (source == null) continue;
                foreach (StringName animation in source.GetAnimationNames())
                {
                    if (combined.HasAnimation(animation)) combined.RemoveAnimation(animation);
                    combined.AddAnimation(animation);
                    combined.SetAnimationSpeed(animation, source.GetAnimationSpeed(animation));
                    combined.SetAnimationLoopMode(animation, source.GetAnimationLoopMode(animation));
                    for (int i = 0; i < source.GetFrameCount(animation); i++)
                        combined.AddFrame(animation, source.GetFrameTexture(animation, i), source.GetFrameDuration(animation, i));
                }
            }
            return combined;
        }

        public bool IsValid()
        {
            if (Frames == null || !float.IsFinite(PixelSize) || PixelSize <= 0f ||
                !float.IsFinite(VisualScale) || VisualScale <= 0f || FrameSize.X <= 0 || FrameSize.Y <= 0 ||
                FootAnchor.X < 0 || FootAnchor.Y < 0 || FootAnchor.X >= FrameSize.X || FootAnchor.Y >= FrameSize.Y)
                return false;
            foreach (string name in RequiredAnimations)
                if (!HasAnimation(name))
                    return false;
            foreach (var entry in AnimationPixelSizes)
                if (!HasAnimation(entry.Key) || !float.IsFinite(entry.Value) || entry.Value <= 0f)
                    return false;
            foreach (var entry in AnimationFootAnchors)
                if (!HasAnimation(entry.Key) || entry.Value.X < 0 || entry.Value.Y < 0 ||
                    entry.Value.X >= FrameSizeFor(entry.Key).X || entry.Value.Y >= FrameSizeFor(entry.Key).Y)
                    return false;
            return true;
        }
    }
}
