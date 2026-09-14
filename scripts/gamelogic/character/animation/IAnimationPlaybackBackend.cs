using Godot;

namespace GameLogic
{
    /// <summary>
    /// Playback abstraction used by the 2D animation instance. This keeps the
    /// animation graph independent from a concrete visual node.
    /// </summary>
    public interface IAnimationPlaybackBackend
    {
        bool IsValid { get; }
        string CurrentAnimation { get; }
        bool IsPlaying { get; }
        bool HasAnimation(string animation);
        void Play(string animation, float speed, bool fromEnd, bool restartIfPlaying);
        void Stop();
    }

    public sealed class AnimatedSprite2DPlaybackBackend : IAnimationPlaybackBackend
    {
        private readonly AnimatedSprite2D _sprite;

        public AnimatedSprite2DPlaybackBackend(AnimatedSprite2D sprite)
        {
            _sprite = sprite;
        }

        public bool IsValid => _sprite != null && GodotObject.IsInstanceValid(_sprite);
        public string CurrentAnimation => IsValid ? _sprite.Animation.ToString() : string.Empty;
        public bool IsPlaying => IsValid && _sprite.IsPlaying();

        public bool HasAnimation(string animation)
        {
            return IsValid && _sprite.SpriteFrames != null &&
                   !string.IsNullOrWhiteSpace(animation) &&
                   _sprite.SpriteFrames.HasAnimation(animation);
        }

        public void Play(string animation, float speed, bool fromEnd, bool restartIfPlaying)
        {
            if (!HasAnimation(animation))
                return;

            StringName animationName = new(animation);
            bool sameAnimation = CurrentAnimation == animation;
            if (sameAnimation && !restartIfPlaying)
            {
                _sprite.SpeedScale = speed;
                if (!_sprite.IsPlaying())
                    _sprite.Play(animationName, speed, fromEnd);
                return;
            }

            _sprite.Play(animationName, speed, fromEnd);
        }

        public void Stop()
        {
            if (IsValid)
                _sprite.Stop();
        }
    }
}
