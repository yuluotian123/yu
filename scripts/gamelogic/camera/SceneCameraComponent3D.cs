using Framework;
using Godot;

namespace GameLogic;

/// <summary>场景 CameraRig 上的镜头行为；保留场景中的镜头构图和投影。</summary>
[GlobalClass]
public partial class SceneCameraComponent3D : Component3D, ICharacterCameraShake
{
    public override int Priority => ComponentPriority.VFX;
    /// <summary>相对于 CameraRig 的场景 Camera3D 路径。镜头的位置、旋转、投影和 FOV 直接在该节点编辑。</summary>
    [Export] public NodePath CameraPath { get; set; } = new("Camera3D");
    /// <summary>相对于 CameraRig 的初始跟随目标路径，通常为 ../Player；运行时自动等待目标初始化后绑定。</summary>
    [Export] public NodePath TargetPath { get; set; } = new("../Player");
    /// <summary>相对于 CameraRig 的编辑器构图参照点路径，通常为 ../CameraFramingTarget。参照点应位于场景中角色的初始位置，使读档和重新激活后保持原构图；它不会传送角色。留空则按激活时目标位置计算偏移。</summary>
    [Export] public NodePath FramingOriginPath { get; set; } = new("");
    /// <summary>镜头跟随配置资源：控制死区、响应速度和前视。Spacelevel 使用 hd2d_free_3d.tres；镜头距离、俯角和透视 FOV 在 Camera3D 节点调整。</summary>
    [Export] public CameraProfile3D Profile { get; set; }

    public bool HasShot { get; private set; }
    public Node3D FollowTarget => CameraModule.IsLive(_target) ? _target : null;
    public Vector3 FocusPosition => _focus;
    public Camera3D SceneCamera => CameraModule.IsLive(_camera) ? _camera : null;
    private Camera3D _camera;
    private CameraProfile3D _profile;
    private Node3D _target;
    private CharacterMovementComponent3D _movement;
    private IInputModule _input;
    private Vector3 _anchor;
    private Vector3 _focus;
    private Vector3 _previousPosition;
    private Vector3 _look;
    private Vector3 _direction;
    private float _viewHeight;
    private float _desiredViewHeight;
    private CameraShakeProfile _shake;
    private float _shakeElapsed;
    private Vector3 _authoredOffset;
    private Basis _authoredBasis;
    private ICameraModule _module;
    private bool _boundOnce;
    private Transform3D _sceneCameraTransform;

    public override void OnInit()
    {
        _camera = Owner.GetNode<Camera3D>(CameraPath);
        _sceneCameraTransform = _camera.GlobalTransform;
        // 相机在渲染帧更新，只插值目标，避免再次对镜头做物理插值。
        _camera.PhysicsInterpolationMode = Node.PhysicsInterpolationModeEnum.Off;
        Owner.SetMeta("scene_camera", this);
        if (HasShot)
        {
            Present(Vector2.Zero, 0f);
            _camera.MakeCurrent();
        }
    }

    internal void Activate(Node3D target, CameraProfile3D profile)
    {
        _module = ModuleSystem.GetModule<ICameraModule>();
        _boundOnce = true;
        _profile = profile ?? new CameraProfile3D();
        _input = _profile.EnableManualLook ? ModuleSystem.GetModule<IInputModule>() : null;
        _viewHeight = _desiredViewHeight = Mathf.Clamp(_camera.Size, 0.1f, 100f);
        // A scene marker defines the authored framing independently of a saved target position.
        // Never recalculate that offset from the already-following camera on reactivation.
        var origin = FramingOriginPath.IsEmpty ? null : Owner.GetNodeOrNull<Node3D>(FramingOriginPath);
        Transform3D frame = origin != null ? _sceneCameraTransform : _camera.GlobalTransform;
        _authoredOffset = frame.Origin - (origin?.GlobalPosition ?? target.GlobalPosition) - _profile.BaseOffset;
        _authoredBasis = frame.Basis.Orthonormalized();
        _shake = null;
        HasShot = true;
        Follow(target, true);
        if (CameraModule.IsLive(_camera)) _camera.MakeCurrent();
    }

    internal void Follow(Node3D target, bool snap)
    {
        _target = target;
        // 有 Movement 时使用其最终物理速度；普通 Node3D 仍能仅靠坐标跟随。
        _movement = (target as GameObject3D)?.GetComponent<CharacterMovementComponent3D>();
        _anchor = _previousPosition = target.GlobalPosition;
        // 提前初始化 Godot 的插值采样，首个跟随渲染帧即可使用。
        if (target.IsPhysicsInterpolatedAndEnabled()) target.GetGlobalTransformInterpolated();
        _look = _direction = Vector3.Zero;
        if (snap) SnapToTarget();
    }

    internal void Focus(Vector3 position, bool snap)
    {
        _target = null;
        _movement = null;
        _anchor = position;
        _look = Vector3.Zero;
        if (snap) SnapToTarget();
    }

    internal void SnapToTarget()
    {
        if (FollowTarget != null)
        {
            _anchor = _previousPosition = _target.GlobalPosition;
            _focus = _anchor + _profile.BaseOffset;
            _look = _direction = Vector3.Zero;
        }
        else if (_target != null)
        {
            _target = null;
            _movement = null;
            _anchor = _focus;
        }
        else _focus = _anchor;
        Present(Vector2.Zero, 0f);
    }

    internal void SetViewHeight(float height, bool snap)
    {
        _desiredViewHeight = Mathf.Clamp(height, 0.1f, 100f);
        if (!snap) return;
        _viewHeight = _desiredViewHeight;
        Present(Vector2.Zero, 0f);
    }

    public override void OnUpdate(double delta)
    {
        // CameraRig 往往排在 Player 前面；等目标完成 EC 初始化再绑定。
        if (!_boundOnce)
        {
            var target = Owner.GetNodeOrNull<Node3D>(TargetPath);
            if (target == null || !target.IsNodeReady()) return;
            ModuleSystem.GetModule<ICameraModule>().Activate(Owner, Owner, target, Profile);
        }
        if (!HasShot || _profile == null || delta <= 0d) return;
        float dt = (float)delta;
        Vector3 desired;
        if (FollowTarget != null)
        {
            Vector3 physicsPosition = _target.GlobalPosition;
            Vector3 displacement = physicsPosition - _previousPosition;
            bool teleported = _profile.TeleportDistance > 0f && displacement.Length() >= _profile.TeleportDistance;
            if (teleported)
            {
                _target.ResetPhysicsInterpolation();
                SnapToTarget();
            }
            // 角色实际渲染的位置与物理 tick 的终点不同，相机必须跟随前者。
            Vector3 position = !teleported && _target.IsPhysicsInterpolatedAndEnabled()
                ? _target.GetGlobalTransformInterpolated().Origin : physicsPosition;
            Vector3 velocity = teleported ? Vector3.Zero : _movement?.Velocity ?? displacement / dt;
            _previousPosition = physicsPosition;
            Vector3 planar = new(velocity.X, 0f, velocity.Z);
            if (planar.LengthSquared() > 0.0001f) _direction = planar.Normalized();
            Vector3 half = _profile.DeadZone.Max(Vector3.Zero) * 0.5f;
            _anchor = _anchor.Clamp(position - half, position + half);
            Vector3 look = (_movement?.FacingDirection ?? _direction) * _profile.LookAheadDistance;
            look.Y = Mathf.Clamp(velocity.Y / Mathf.Max(0.01f, _profile.VerticalVelocityReference), -1f, 1f)
                * _profile.VerticalVelocityLookDistance;
            if (_profile.EnableManualLook && _input != null && _movement?.MovementSpace != CharacterMovementSpace.Free3D)
            {
                float manual = _input.GetActionStrength(_profile.LookUpAction, handlerLayer: "Camera") -
                    _input.GetActionStrength(_profile.LookDownAction, handlerLayer: "Camera");
                if (Mathf.Abs(manual) > 0.01f) look.Y = manual * _profile.ManualLookDistance;
            }
            _look = _look.Lerp(look, Weight(_profile.LookSmooth, dt));
            desired = _anchor + _profile.BaseOffset + _look;
        }
        else
        {
            if (_target != null)
            {
                _target = null;
                _movement = null;
                _anchor = _focus;
            }
            desired = _anchor;
        }
        _focus = new Vector3(
            Mathf.Lerp(_focus.X, desired.X, Weight(_profile.FollowSmooth.X, dt)),
            Mathf.Lerp(_focus.Y, desired.Y, Weight(_profile.FollowSmooth.Y, dt)),
            Mathf.Lerp(_focus.Z, desired.Z, Weight(_profile.FollowSmooth.Z, dt)));
        _viewHeight = Mathf.Lerp(_viewHeight, _desiredViewHeight, Weight(_profile.ZoomSmooth, dt));
        Vector2 shake = UpdateShake(dt, out float roll);
        Present(shake, roll);
    }

    private void Present(Vector2 shake, float roll)
    {
        if (!CameraModule.IsLive(_camera) || _profile == null) return;
        Vector3 up = _authoredBasis.Y;
        Vector3 right = _authoredBasis.X;
        Vector3 center = _focus + right * shake.X + up * shake.Y;
        if (_profile.PixelSnap && _camera.Projection == Camera3D.ProjectionType.Orthogonal)
        {
            float pixel = _viewHeight / Mathf.Max(1f, _camera.GetViewport().GetVisibleRect().Size.Y);
            float screenY = center.Dot(up);
            float screenX = center.Dot(right);
            center += right * (Mathf.Round(screenX / pixel) * pixel - screenX)
                + up * (Mathf.Round(screenY / pixel) * pixel - screenY);
        }
        _camera.Size = _viewHeight;
        _camera.GlobalPosition = center + _authoredOffset;
        _camera.GlobalBasis = _authoredBasis;
        _camera.RotateObjectLocal(Vector3.Back, roll);
    }

    public void Shake(CameraShakeProfile profile) { _shake = profile; _shakeElapsed = 0f; }
    internal void StopShake() { _shake = null; Present(Vector2.Zero, 0f); }

    private Vector2 UpdateShake(float dt, out float roll)
    {
        roll = 0f;
        if (_shake == null) return Vector2.Zero;
        _shakeElapsed += dt;
        if (_shakeElapsed >= _shake.SafeDuration)
        {
            _shake = null;
            return Vector2.Zero;
        }
        float decay = Mathf.Pow(1f - _shakeElapsed / _shake.SafeDuration, Mathf.Max(0f, _shake.DecayPower));
        float phase = _shakeElapsed * Mathf.Max(1f, _shake.Frequency);
        roll = Mathf.Sin(phase * 1.83f) * Mathf.DegToRad(_shake.RotationAmplitudeDegrees) * decay;
        return new Vector2(Mathf.Sin(phase * 1.17f) * _shake.SafeAmplitude.X,
            Mathf.Cos(phase * 1.41f) * _shake.SafeAmplitude.Y) * decay;
    }

    internal void Deactivate()
    {
        StopShake();
        HasShot = false;
        _target = null;
        _movement = null;
        _input = null;
        if (CameraModule.IsLive(_camera)) _camera.ClearCurrent(false);
    }

    public override void OnDestroy()
    {
        _module?.Release(Owner);
        Deactivate();
        if (GodotObject.IsInstanceValid(Owner)) Owner.RemoveMeta("scene_camera");
        _camera = null;
        _profile = null;
        _module = null;
        _boundOnce = false;
    }
    private static float Weight(float speed, float dt) => speed <= 0f ? 1f : 1f - Mathf.Exp(-speed * dt);
}
