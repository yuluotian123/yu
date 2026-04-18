using Framework;
using GameLogic;
using Godot;

[GlobalClass]
public partial class CameraComponent : Component2D
{
    [Export] public float MoveSpeed { get; set; } = 800f;
    [Export] public float DragSpeed { get; set; } = 1f;
    [Export] public float SpeedupMultiplier { get; set; } = 2f;
    [Export] public float ZoomStep { get; set; } = 0.1f;
    [Export] public float MinZoom { get; set; } = 0.5f;
    [Export] public float MaxZoom { get; set; } = 2.5f;

    private Camera2D _camera;
    private IInputModule _inputModule;
    private bool _missingCameraWarningShown;

    public override int Priority => ComponentPriority.Movement - 10;

    public override void OnInit()
    {
        _inputModule = ModuleSystem.GetModule<IInputModule>();
        var sceneCamera = Owner?.GetParent()?.GetNodeOrNull<Camera2D>("MainCamera");
        _camera = sceneCamera ?? Owner?.GetViewport()?.GetCamera2D();
        _missingCameraWarningShown = false;
    }

    public override void OnUpdate(double delta)
    {
        Vector2 nextPosition = _camera.Position;
        Vector2 keyboardMove = _inputModule.GetVector("camera_left", "camera_right", "camera_up", "camera_down");

        float timeDelta = (float)RootModule.Instance.GameTime.UnscaledDeltaTime;

        if (keyboardMove != Vector2.Zero)
        {
            float moveSpeed = MoveSpeed * timeDelta;
            if (_inputModule.IsPressed("camera_speedup"))
            {
                moveSpeed *= SpeedupMultiplier;
            }

            nextPosition += keyboardMove * moveSpeed;
        }

        bool isPointerBlockedByUI = ViewportInputUtility.IsPointerBlockedByUI(Owner);
        Vector2 cameraDragDelta = _inputModule.GetMouseDelta();
        bool isDraggingCamera = _inputModule.IsPressed("camera_drag",filterConsumed:true);

        if (!isPointerBlockedByUI && isDraggingCamera && cameraDragDelta != Vector2.Zero)
            nextPosition -= cameraDragDelta * DragSpeed * _camera.Zoom;

        _camera.Position = nextPosition;

        bool zoomInRequested = _inputModule.IsJustPressed("camera_zoom_in");
        bool zoomOutRequested = _inputModule.IsJustPressed("camera_zoom_out");
        if (zoomInRequested || zoomOutRequested)
            ApplyZoom(zoomInRequested, zoomOutRequested);
    }

    public override void OnDestroy()
    {
        _camera = null;
        _inputModule = null;
    }

    public void FocusOn(Vector2 worldPosition)
    {
        _camera.GlobalPosition = worldPosition;
    }

    private void ApplyZoom(bool zoomInRequested, bool zoomOutRequested)
    {
        float zoomOffset = 0f;

        if (zoomInRequested)
            zoomOffset -= ZoomStep;

        if (zoomOutRequested)
            zoomOffset += ZoomStep;

        if (Mathf.IsZeroApprox(zoomOffset))
            return;

        float nextZoom = Mathf.Clamp(_camera.Zoom.X + zoomOffset, MinZoom, MaxZoom);
        _camera.Zoom = new Vector2(nextZoom, nextZoom);
    }
}
