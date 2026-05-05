using Framework;
using Godot;
using System.Collections.Generic;

namespace GameLogic
{
    [GlobalClass]
    public partial class SpriteAnimationComponent2D : Component2D
    {
        public override int Priority => ComponentPriority.VFX;

        [Export] public NodePath SpritePath { get; set; } = new("VisualRoot/AnimatedSprite2D");

        public string ActiveRequestKey => _activeRequestKey;
        public string ActiveAnimation => _activeAnimation;
        public AnimatedSprite2D Sprite => _sprite;

        private AnimatedSprite2D _sprite;
        private readonly Dictionary<string, AnimationRequest> _animationRequests = new();
        private readonly HashSet<string> _missingAnimationWarnings = new();
        private ulong _requestSequence;
        private string _activeRequestKey = string.Empty;
        private string _activeAnimation = string.Empty;

        public override void OnInit()
        {
            _activeRequestKey = string.Empty;
            _activeAnimation = string.Empty;
            _missingAnimationWarnings.Clear();

            _sprite = Owner.GetNodeOrNull<AnimatedSprite2D>(SpritePath);
            if (_sprite == null)
            {
                Debugger.Warn("[SpriteAnimationComponent2D] Missing AnimatedSprite2D.");
                return;
            }

            ApplyBestAnimationRequest();
        }

        public override void OnDestroy()
        {
            _animationRequests.Clear();
            _missingAnimationWarnings.Clear();
            _requestSequence = 0;
            _activeRequestKey = string.Empty;
            _activeAnimation = string.Empty;
            _sprite = null;
        }

        public void RequestAnimation(
            string key,
            string animation,
            int priority,
            float speed = 1f,
            bool fromEnd = false,
            bool restartIfPlaying = true)
        {
            if (string.IsNullOrWhiteSpace(animation))
                return;

            key = NormalizeRequestKey(key, animation);
            _animationRequests[key] = new AnimationRequest
            {
                Key = key,
                Animation = animation,
                Priority = priority,
                Speed = speed,
                FromEnd = fromEnd,
                RestartIfPlaying = restartIfPlaying,
                Sequence = ++_requestSequence
            };

            ApplyBestAnimationRequest();
        }

        public void ClearAnimationRequest(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                _animationRequests.Clear();
            }
            else
            {
                _animationRequests.Remove(key.Trim());
            }

            ApplyBestAnimationRequest();
        }

        private void ApplyBestAnimationRequest()
        {
            if (_sprite == null)
                return;

            if (_animationRequests.Count == 0)
            {
                _activeRequestKey = string.Empty;
                _activeAnimation = string.Empty;
                return;
            }

            AnimationRequest best = null;
            foreach (AnimationRequest request in _animationRequests.Values)
            {
                if (!CanPlayAnimation(request.Animation))
                    continue;

                if (best == null ||
                    request.Priority > best.Priority ||
                    request.Priority == best.Priority && request.Sequence > best.Sequence)
                {
                    best = request;
                }
            }

            if (best == null)
                return;

            PlayRequest(best);
        }

        private bool CanPlayAnimation(string animation)
        {
            if (_sprite?.SpriteFrames == null || string.IsNullOrWhiteSpace(animation))
                return false;

            if (_sprite.SpriteFrames.HasAnimation(animation))
                return true;

            if (_missingAnimationWarnings.Add(animation))
                Debugger.Warn($"[SpriteAnimationComponent2D] Missing animation '{animation}'.");

            return false;
        }

        private void PlayRequest(AnimationRequest request)
        {
            var animationName = new StringName(request.Animation);
            bool sameRequest = string.Equals(_activeRequestKey, request.Key, System.StringComparison.Ordinal);
            bool sameAnimation = IsSameAnimation(_sprite.Animation, animationName);

            if (sameRequest && sameAnimation)
            {
                _sprite.SpeedScale = request.Speed;
                if (!_sprite.IsPlaying() && _sprite.SpriteFrames.GetAnimationLoop(animationName))
                    _sprite.Play(animationName, request.Speed, request.FromEnd);
                return;
            }

            if (sameAnimation && !request.RestartIfPlaying)
            {
                _activeRequestKey = request.Key;
                _activeAnimation = request.Animation;
                _sprite.SpeedScale = request.Speed;
                return;
            }

            _activeRequestKey = request.Key;
            _activeAnimation = request.Animation;
            _sprite.Play(animationName, request.Speed, request.FromEnd);
        }

        private static string NormalizeRequestKey(string key, string animation)
        {
            if (!string.IsNullOrWhiteSpace(key))
                return key.Trim();

            return string.IsNullOrWhiteSpace(animation)
                ? "animation"
                : animation.Trim();
        }

        private static bool IsSameAnimation(StringName current, StringName target)
        {
            return string.Equals(current.ToString(), target.ToString(), System.StringComparison.Ordinal);
        }

        private sealed class AnimationRequest
        {
            public string Key;
            public string Animation;
            public int Priority;
            public float Speed;
            public bool FromEnd;
            public bool RestartIfPlaying;
            public ulong Sequence;
        }
    }
}
