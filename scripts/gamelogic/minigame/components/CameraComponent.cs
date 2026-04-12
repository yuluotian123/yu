using Framework;
using GameLogic;
using Godot;

[GlobalClass]
public partial class CameraComponent : Component
{
    [Export] public float MoveSpeed { get; set; } = 800f;
    [Export] public float DragSpeed { get; set; } = 1f;
    [Export] public float SpeedupMultiplier { get; set; } = 2f;
    [Export] public float ZoomStep { get; set; } = 0.1f;
    [Export] public float MinZoom { get; set; } = 0.5f;
    [Export] public float MaxZoom { get; set; } = 2.5f;

    private Camera2D _camera;
    private InputComponent _inputComponent;
    private bool _missingCameraWarningShown;
    private bool _missingInputWarningShown;

    public override int Priority => ComponentPriority.Movement - 10;

    public override void OnInit()
    {
        _inputComponent = Owner?.GetComponent<InputComponent>();
        _camera = ResolveCamera();
        _missingCameraWarningShown = false;
        _missingInputWarningShown = false;
    }

    public override void OnUpdate(double delta)
    {
        if (!TryResolveDependencies())
            return;

        Vector2 nextPosition = _camera.Position;
        Vector2 keyboardMove = _inputComponent.CameraMoveAxis;
        var timedelta = (float)RootModule.Instance.GameTime.UnscaledDeltaTime;

        if (keyboardMove != Vector2.Zero)
        {
            float moveSpeed = MoveSpeed * timedelta;
            if (_inputComponent.IsSpeedupPressed)
                moveSpeed *= SpeedupMultiplier;

            nextPosition += keyboardMove * moveSpeed;
        }

        if (_inputComponent.IsDraggingCamera && _inputComponent.CameraDragDelta != Vector2.Zero)
        {
            nextPosition -= _inputComponent.CameraDragDelta * DragSpeed * _camera.Zoom;
        }

        _camera.Position = nextPosition;

        if (_inputComponent.ZoomInRequested || _inputComponent.ZoomOutRequested)
        {
            ApplyZoom();
        }
    }

    public override void OnDestroy()
    {
        _camera = null;
        _inputComponent = null;
    }

    private bool TryResolveDependencies()
    {
        if (_inputComponent == null)
        {
            _inputComponent = Owner?.GetComponent<InputComponent>();
            if (_inputComponent == null)
            {
                if (!_missingInputWarningShown)
                {
                    Debugger.Warn("[CameraComponent] InputComponent not found on owner.");
                    _missingInputWarningShown = true;
                }

                return false;
            }
        }

        if (_camera == null || !IsInstanceValid(_camera))
        {
            _camera = ResolveCamera();
            if (_camera == null)
            {
                if (!_missingCameraWarningShown)
                {
                    Debugger.Warn("[CameraComponent] Camera2D not found for camera control.");
                    _missingCameraWarningShown = true;
                }

                return false;
            }
        }

        return true;
    }

    private Camera2D ResolveCamera()
    {
        Camera2D sceneCamera = Owner?.GetParent()?.GetNodeOrNull<Camera2D>("MainCamera");
        return sceneCamera ?? Owner?.GetViewport()?.GetCamera2D();
    }

    private void ApplyZoom()
    {
        float zoomOffset = 0f;

        if (_inputComponent.ZoomInRequested)
            zoomOffset -= ZoomStep;

        if (_inputComponent.ZoomOutRequested)
            zoomOffset += ZoomStep;

        if (Mathf.IsZeroApprox(zoomOffset))
            return;

        float nextZoom = Mathf.Clamp(_camera.Zoom.X + zoomOffset, MinZoom, MaxZoom);
        _camera.Zoom = new Vector2(nextZoom, nextZoom);
    }
}
