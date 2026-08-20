using Framework;
using Godot;
using System.Collections.Generic;

namespace GameLogic
{
    [GlobalClass]
    public partial class CharacterAnimationComponent2D : Component2D
    {
        public override int Priority => 20;

        [Export] public NodePath SpritePath { get; set; } = new("VisualRoot/AnimatedSprite2D");
        [Export] public HfsmGraphAsset LocomotionGraph { get; set; }

        public string ActiveRequestKey => _activeRequestKey;
        public string ActiveAnimation => _activeAnimation;
        public AnimatedSprite2D Sprite => _sprite;
        public HfsmRuntime LocomotionRuntime { get; private set; }

        private readonly Dictionary<string, AnimationRequest> _animationRequests = new();
        private readonly HashSet<string> _missingAnimationWarnings = new();
        private CharacterMovementComponent2D _movement;
        private AnimatedSprite2D _sprite;
        private ulong _requestSequence;
        private string _activeRequestKey = string.Empty;
        private string _activeAnimation = string.Empty;

        public override void OnInit()
        {
            _movement = Owner?.GetComponent<CharacterMovementComponent2D>();
            _sprite = Owner?.GetNodeOrNull<AnimatedSprite2D>(SpritePath);
            if (_sprite == null)
                Debugger.Warn("[CharacterAnimationComponent2D] Missing AnimatedSprite2D.");

            if (LocomotionGraph != null)
            {
                LocomotionRuntime = new HfsmRuntime(LocomotionGraph);
                LocomotionRuntime.Context.UserData.Add(this);
                LocomotionRuntime.Context.UserData.Add(Owner);
                PublishMovementSnapshot();
                if (!LocomotionRuntime.Start())
                    Debugger.Warn("[CharacterAnimationComponent2D] Failed to start LocomotionGraph.");
            }
        }

        public override void OnPhysicsUpdate(double delta)
        {
            PublishMovementSnapshot();
            LocomotionRuntime?.Update(delta);
            ApplyBestAnimationRequest();
        }

        public override void OnDestroy()
        {
            LocomotionRuntime?.Stop();
            LocomotionRuntime = null;
            _animationRequests.Clear();
            _missingAnimationWarnings.Clear();
            _movement = null;
            _sprite = null;
            _requestSequence = 0;
            _activeRequestKey = string.Empty;
            _activeAnimation = string.Empty;
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
        }

        public void ClearAnimationRequest(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                _animationRequests.Clear();
            else
                _animationRequests.Remove(key.Trim());
        }

        private void PublishMovementSnapshot()
        {
            if (LocomotionRuntime == null || _movement == null)
                return;
            LocomotionRuntime.SetValue(LocomotionBlackboardKeys.MovementMode, _movement.MovementMode.ToString());
            LocomotionRuntime.SetValue(LocomotionBlackboardKeys.MovementIsOnFloor, _movement.IsOnFloor);
            LocomotionRuntime.SetValue(LocomotionBlackboardKeys.MovementMoveAxisX, _movement.MoveInputX);
            LocomotionRuntime.SetValue(LocomotionBlackboardKeys.MovementVelocityY, _movement.Velocity.Y);
        }

        private void ApplyBestAnimationRequest()
        {
            if (_sprite == null || _animationRequests.Count == 0)
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
                if (best == null || request.Priority > best.Priority ||
                    request.Priority == best.Priority && request.Sequence > best.Sequence)
                    best = request;
            }
            if (best != null)
                PlayRequest(best);
        }

        private bool CanPlayAnimation(string animation)
        {
            if (_sprite?.SpriteFrames == null || string.IsNullOrWhiteSpace(animation))
                return false;
            if (_sprite.SpriteFrames.HasAnimation(animation))
                return true;
            if (_missingAnimationWarnings.Add(animation))
                Debugger.Warn($"[CharacterAnimationComponent2D] Missing animation '{animation}'.");
            return false;
        }

        private void PlayRequest(AnimationRequest request)
        {
            var animationName = new StringName(request.Animation);
            bool sameRequest = _activeRequestKey == request.Key;
            bool sameAnimation = _sprite.Animation.ToString() == animationName.ToString();
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

        private static string NormalizeRequestKey(string key, string animation) =>
            !string.IsNullOrWhiteSpace(key) ? key.Trim() :
            string.IsNullOrWhiteSpace(animation) ? "animation" : animation.Trim();

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
