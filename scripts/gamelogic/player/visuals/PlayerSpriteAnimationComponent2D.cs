using Framework;
using Godot;
using System.Collections.Generic;

namespace GameLogic
{
    [GlobalClass]
    public partial class PlayerSpriteAnimationComponent2D : Component2D
    {
        public override int Priority => ComponentPriority.VFX;

        [Export] public NodePath SpritePath { get; set; } = new("VisualRoot/AnimatedSprite2D");
        [Export] public string IdleAnimation { get; set; } = "idle";
        [Export] public string RunAnimation { get; set; } = "run";
        [Export] public string JumpUpAnimation { get; set; } = "jumpup";
        [Export] public string InAirAnimation { get; set; } = "inair";
        [Export] public string FallingAnimation { get; set; } = "isfalling";
        [Export] public string LandAnimation { get; set; } = "land";
        [Export] public bool UseAutomaticLocomotion { get; set; }
        [Export] public int AutomaticLocomotionPriority { get; set; } = -100;
        [Export] public float MinRunSpeed { get; set; } = 5f;
        [Export] public float LandingPoseTime { get; set; } = 0.1f;
        [Export] public float RiseVelocity { get; set; } = -180f;
        [Export] public float ApexVelocity { get; set; } = 120f;

        private const string LocomotionRequestKey = "locomotion";

        private AnimatedSprite2D _sprite;
        private CharacterMoveComponent2D _move;
        private CharacterBodyMotorComponent2D _motor;
        private readonly Dictionary<string, AnimationRequest> _animationRequests = new();
        private readonly HashSet<string> _missingAnimationWarnings = new();
        private bool _wasAirborne;
        private float _landingTimer;
        private ulong _requestSequence;
        private string _activeRequestKey = string.Empty;
        private string _activeAnimation = string.Empty;

        public override void OnInit()
        {
            _activeRequestKey = string.Empty;
            _activeAnimation = string.Empty;
            _wasAirborne = false;
            _landingTimer = 0f;
            _missingAnimationWarnings.Clear();

            _sprite = Owner.GetNodeOrNull<AnimatedSprite2D>(SpritePath);
            _move = Owner.GetComponent<CharacterMoveComponent2D>();
            _motor = Owner.GetComponent<CharacterBodyMotorComponent2D>();

            if (_sprite == null)
            {
                Debugger.Warn("[PlayerSpriteAnimationComponent2D] Missing AnimatedSprite2D.");
                return;
            }

            RequestAnimation(LocomotionRequestKey, IdleAnimation, AutomaticLocomotionPriority);
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
            _move = null;
            _motor = null;
        }

        public override void OnPhysicsUpdate(double delta)
        {
            if (_sprite == null)
                return;

            if (!UseAutomaticLocomotion)
                return;

            float dt = (float)delta;
            bool airborne = IsAirborne();
            UpdateAirborneTimers(airborne, dt);

            RequestAnimation(LocomotionRequestKey, ResolveLocomotionAnimation(), AutomaticLocomotionPriority);
        }

        private void UpdateAirborneTimers(bool airborne, float delta)
        {
            if (airborne)
                _landingTimer = 0f;
            else
            {
                if (_wasAirborne)
                    _landingTimer = LandingPoseTime;
                else
                    _landingTimer = Mathf.Max(0f, _landingTimer - delta);
            }

            _wasAirborne = airborne;
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

        private string ResolveLocomotionAnimation()
        {
            if (IsAirborne())
                return ResolveAirborneAnimation();

            if (_landingTimer > 0f)
                return LandAnimation;

            return IsRunning() ? RunAnimation : IdleAnimation;
        }

        private string ResolveAirborneAnimation()
        {
            if (_motor == null)
                return InAirAnimation;

            float velocityY = _motor.Velocity.Y;
            if (velocityY < RiseVelocity)
                return JumpUpAnimation;

            if (velocityY <= ApexVelocity)
                return InAirAnimation;

            return FallingAnimation;
        }

        private bool IsAirborne()
        {
            return _motor != null && !_motor.IsOnFloor;
        }

        private bool IsRunning()
        {
            if (_move != null && Mathf.Abs(_move.InputX) > 0.01f)
                return true;

            return _motor != null && Mathf.Abs(_motor.Velocity.X) > MinRunSpeed;
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
                Debugger.Warn($"[PlayerSpriteAnimationComponent2D] Missing animation '{animation}'.");

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
