using Framework;
using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class PlayerCameraComponent2D : Component2D
    {
        [ExportGroup("Camera")]
        [Export] public NodePath CameraPath { get; set; } = new("Camera2D");
        [Export] public Vector2 BaseOffset { get; set; } = Vector2.Zero;
        [Export(PropertyHint.Range, "0,512,1")]
        public float FollowSmooth { get; set; } = 10f;
        [Export] public bool DisableNativeSmoothing { get; set; } = true;
        [Export] public bool PixelSnap { get; set; } = true;

        [ExportGroup("Look Ahead")]
        [Export(PropertyHint.Range, "0,320,1")]
        public float LookAheadDistance { get; set; } = 96f;
        [Export(PropertyHint.Range, "0,320,1")]
        public float VerticalVelocityLookDistance { get; set; } = 36f;
        [Export(PropertyHint.Range, "1,2400,1")]
        public float VerticalVelocityReference { get; set; } = 760f;

        [ExportGroup("Manual Look")]
        [Export] public bool EnableManualLook { get; set; } = true;
        [Export] public string LookUpAction { get; set; } = "camera_up";
        [Export] public string LookDownAction { get; set; } = "camera_down";
        [Export(PropertyHint.Range, "0,320,1")]
        public float ManualLookDistance { get; set; } = 92f;

        private readonly RandomNumberGenerator _random = new();
        private Camera2D _camera;
        private CharacterMoveComponent2D _move;
        private CharacterBodyMotorComponent2D _motor;
        private IInputModule _input;
        private Vector2 _initialCameraPosition;
        private Vector2 _initialCameraOffset;
        private Vector2 _currentOffset;
        private float _initialCameraRotation;
        private bool _initialPositionSmoothingEnabled;
        private float _shakeTimeRemaining;
        private float _shakeDuration;
        private float _shakeElapsed;
        private Vector2 _shakeAmplitude;
        private float _shakeFrequency;
        private float _shakeDecayPower;
        private float _shakeRotationAmplitude;

        public override int Priority => ComponentPriority.VFX;

        public override void OnInit()
        {
            _camera = Owner?.GetNodeOrNull<Camera2D>(CameraPath);
            _move = Owner?.GetComponent<CharacterMoveComponent2D>();
            _motor = Owner?.GetComponent<CharacterBodyMotorComponent2D>();
            _input = TryGetInputModule();
            _random.Randomize();

            if (_camera == null)
            {
                GD.PushWarning($"[PlayerCamera] Missing Camera2D: {CameraPath}");
                return;
            }

            _initialCameraPosition = _camera.Position;
            _initialCameraOffset = _camera.Offset;
            _initialCameraRotation = _camera.Rotation;
            _initialPositionSmoothingEnabled = _camera.PositionSmoothingEnabled;
            _currentOffset = Vector2.Zero;
            _camera.Enabled = true;
            if (DisableNativeSmoothing)
                _camera.PositionSmoothingEnabled = false;

            _camera.MakeCurrent();
        }

        public override void OnUpdate(double delta)
        {
            if (_camera == null)
                return;

            float dt = Mathf.Max(0f, (float)delta);
            Vector2 targetOffset = BaseOffset + ResolveLookAheadOffset();
            _currentOffset = SmoothDamp(_currentOffset, targetOffset, FollowSmooth, dt);

            Vector2 shakeOffset = UpdateShake(dt, out float shakeRotation);
            Vector2 position = _initialCameraPosition;
            Vector2 offset = _initialCameraOffset + _currentOffset + shakeOffset;

            if (PixelSnap)
            {
                position = SnapToPixel(position);
                offset = SnapToPixel(offset);
            }

            _camera.Position = position;
            _camera.Offset = offset;
            _camera.Rotation = _initialCameraRotation + shakeRotation;

            if (shakeOffset.LengthSquared() > 0.001f || Mathf.Abs(shakeRotation) > 0.0001f)
                _camera.ResetSmoothing();
        }

        public override void OnDestroy()
        {
            if (_camera == null)
                return;

            _camera.Position = _initialCameraPosition;
            _camera.Offset = _initialCameraOffset;
            _camera.Rotation = _initialCameraRotation;
            _camera.PositionSmoothingEnabled = _initialPositionSmoothingEnabled;
        }

        public void Shake(CameraShakeProfile profile)
        {
            if (profile == null)
                return;

            Shake(profile.SafeAmplitude, profile.SafeDuration, profile.Frequency, profile.DecayPower, profile.RotationAmplitudeDegrees);
        }

        public void Shake(float amplitude, float duration)
        {
            Shake(new Vector2(amplitude, amplitude), duration, 38f, 1.4f, 0f);
        }

        public void Shake(Vector2 amplitude, float duration)
        {
            Shake(amplitude, duration, 38f, 1.4f, 0f);
        }

        public void Shake(Vector2 amplitude, float duration, float frequency, float decayPower, float rotationAmplitudeDegrees)
        {
            _shakeDuration = Mathf.Max(0.01f, duration);
            _shakeTimeRemaining = _shakeDuration;
            _shakeElapsed = 0f;
            _shakeAmplitude = new Vector2(Mathf.Max(0f, amplitude.X), Mathf.Max(0f, amplitude.Y));
            _shakeFrequency = Mathf.Max(1f, frequency);
            _shakeDecayPower = Mathf.Max(0f, decayPower);
            _shakeRotationAmplitude = Mathf.DegToRad(Mathf.Max(0f, rotationAmplitudeDegrees));
        }

        private Vector2 ResolveLookAheadOffset()
        {
            return new Vector2(ResolveHorizontalLook(), ResolveVerticalLook());
        }

        private float ResolveHorizontalLook()
        {
            float direction = 0f;

            if (_move?.ApprovedIntent.HasInput == true)
                direction = _move.ApprovedIntent.AxisX;
            else if (_move?.RawIntent.HasInput == true)
                direction = _move.RawIntent.AxisX;
            else if (_move != null && Mathf.Abs(_move.InputX) > 0.01f)
                direction = _move.InputX;
            else if (_move != null)
                direction = _move.Facing;

            if (Mathf.Abs(direction) <= 0.01f)
                return 0f;

            return Mathf.Sign(direction) * LookAheadDistance;
        }

        private float ResolveVerticalLook()
        {
            float manual = ResolveManualLook();
            if (Mathf.Abs(manual) > 0.01f)
                return manual * ManualLookDistance;

            float velocityY = _motor?.Velocity.Y ?? 0f;
            if (Mathf.Abs(velocityY) <= 1f)
                return 0f;

            float normalized = Mathf.Clamp(velocityY / Mathf.Max(1f, VerticalVelocityReference), -1f, 1f);
            return normalized * VerticalVelocityLookDistance;
        }

        private float ResolveManualLook()
        {
            if (!EnableManualLook)
                return 0f;

            _input ??= TryGetInputModule();
            if (_input == null)
                return 0f;

            float up = _input.GetActionStrength(LookUpAction, handlerLayer: "Camera");
            float down = _input.GetActionStrength(LookDownAction, handlerLayer: "Camera");
            return down - up;
        }

        private Vector2 UpdateShake(float delta, out float rotation)
        {
            rotation = 0f;
            if (_shakeTimeRemaining <= 0f)
                return Vector2.Zero;

            _shakeTimeRemaining = Mathf.Max(0f, _shakeTimeRemaining - delta);
            _shakeElapsed += delta;

            float progress = 1f - (_shakeTimeRemaining / Mathf.Max(0.01f, _shakeDuration));
            float decay = Mathf.Pow(1f - Mathf.Clamp(progress, 0f, 1f), _shakeDecayPower);
            float phase = _shakeElapsed * _shakeFrequency;
            float noiseX = Mathf.Sin(phase * 1.17f + _random.RandfRange(-0.15f, 0.15f));
            float noiseY = Mathf.Cos(phase * 1.41f + _random.RandfRange(-0.15f, 0.15f));
            float noiseR = Mathf.Sin(phase * 1.83f);

            rotation = noiseR * _shakeRotationAmplitude * decay;
            return new Vector2(noiseX * _shakeAmplitude.X, noiseY * _shakeAmplitude.Y) * decay;
        }

        private static Vector2 SmoothDamp(Vector2 current, Vector2 target, float smooth, float delta)
        {
            if (smooth <= 0f || delta <= 0f)
                return target;

            float t = 1f - Mathf.Exp(-smooth * delta);
            return current.Lerp(target, t);
        }

        private static Vector2 SnapToPixel(Vector2 value)
        {
            return new Vector2(Mathf.Round(value.X), Mathf.Round(value.Y));
        }

        private static IInputModule TryGetInputModule()
        {
            try
            {
                return ModuleSystem.GetModule<IInputModule>();
            }
            catch
            {
                return null;
            }
        }
    }
}
