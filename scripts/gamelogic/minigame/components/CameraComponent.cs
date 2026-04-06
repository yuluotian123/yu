using Framework;
using GameLogic;
using Godot;

[GlobalClass]
public partial class CameraComponent : Component
{
    [ExportGroup("Follow Settings")]
    [Export] public float FollowSpeed { get; set; } = 5.0f;
    [Export] public Vector3 CameraOffset { get; set; } = new Vector3(0, 1, 3);
    [Export] public bool SmoothFollow { get; set; } = true;

    [ExportGroup("Rotation Settings")]
    [Export] public bool FollowRotation { get; set; } = false;
    [Export] public float RotationSpeed { get; set; } = 3.0f;
    [Export] public bool LookAtTarget { get; set; } = false;
    [Export] public Vector3 LookAtOffset { get; set; } = new Vector3(0, 1, 0);

    [ExportGroup("Deadzone Settings")]
    [Export] public bool UseDeadzone { get; set; } = false;
    [Export] public float DeadzoneRadius { get; set; } = 0.5f;
    [Export] public float DeadzoneSpeed  { get; set; } = 0f;

    [ExportGroup("Boundary Settings")]
    [Export] public bool UseBoundary { get; set; } = false;
    [Export] public Vector3 BoundaryMin { get; set; } = new Vector3(-50, 0, -50);
    [Export] public Vector3 BoundaryMax { get; set; } = new Vector3(50, 20, 50);

    [ExportGroup("Node References")]
    [Export] public NodePath CameraPath { get; set; } = "%PlayerCamera";
    [Export] public NodePath TargetPath { get; set; } = "%Player";

    private Camera3D _camera;
    private Vector3 _lastTargetPosition;
    
    public override int Priority => ComponentPriority.Movement - 10;

    public override void OnInit()
    {
        _camera = Owner.GetNode<Camera3D>(CameraPath);
        if (_camera == null)
        {
            Debugger.Error("CameraComponent requires a Camera3D node named 'PlayeCamera' as a child of the owner.");
            return;
        }

        _camera.Current = true;
        
        var player = Owner.GetNode<CharacterBody3D>(TargetPath);
        if (player != null)
        {
            _lastTargetPosition = CalculateTargetPosition(player);
        }
    }

    public override void OnPhysicsUpdate(double delta)
    {
        var player = Owner.GetNode<CharacterBody3D>(TargetPath);
        if (player == null || _camera == null) return;

        Vector3 targetPosition = CalculateTargetPosition(player);
        
        var speed = FollowSpeed;

        if (UseDeadzone && IsInDeadzone(targetPosition)) speed = DeadzoneSpeed;
    
        if (SmoothFollow)
        {
            _camera.GlobalPosition = _camera.GlobalPosition.Lerp(targetPosition, speed * (float)delta);
        }
        else
        {
            _camera.GlobalPosition = targetPosition;
        }
        
        if (UseBoundary)
            _camera.GlobalPosition = ClampToBoundary(_camera.GlobalPosition);
        
        HandleRotation(player, delta);
        
        _lastTargetPosition = targetPosition;
    }

    private Vector3 CalculateTargetPosition(CharacterBody3D player)
    {
        if (FollowRotation)
        {
            Transform3D playerTransform = player.GlobalTransform;
            return playerTransform.Origin + playerTransform.Basis * CameraOffset;
        }
        return player.GlobalPosition + CameraOffset;
    }

    private bool IsInDeadzone(Vector3 targetPosition)
    {
        float distance = _camera.GlobalPosition.DistanceTo(targetPosition);
        return distance < DeadzoneRadius;
    }

    private Vector3 ClampToBoundary(Vector3 position)
    {
        return new Vector3(
            Mathf.Clamp(position.X, BoundaryMin.X, BoundaryMax.X),
            Mathf.Clamp(position.Y, BoundaryMin.Y, BoundaryMax.Y),
            Mathf.Clamp(position.Z, BoundaryMin.Z, BoundaryMax.Z)
        );
    }

    private void HandleRotation(CharacterBody3D player, double delta)
    {
        if (LookAtTarget)
        {
            Vector3 lookTarget = player.GlobalPosition + LookAtOffset;
            Vector3 direction = (lookTarget - _camera.GlobalPosition).Normalized();
            if (direction.LengthSquared() > 0.001f)
            {
                Transform3D targetTransform = _camera.GlobalTransform.LookingAt(lookTarget, Vector3.Up);
                _camera.GlobalTransform = _camera.GlobalTransform.InterpolateWith(targetTransform, RotationSpeed * (float)delta);
            }
        }
        else if (FollowRotation)
        {
            Quaternion targetRotation = new Quaternion(player.GlobalTransform.Basis);
            Quaternion currentRotation = new Quaternion(_camera.GlobalTransform.Basis);
            Quaternion newRotation = currentRotation.Slerp(targetRotation, RotationSpeed * (float)delta);
            _camera.GlobalTransform = new Transform3D(new Basis(newRotation), _camera.GlobalPosition);
        }
    }
}
