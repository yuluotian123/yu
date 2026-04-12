using Framework;
using GameLogic;
using GameLogic.Input;
using Godot;

//处理camera输入
public partial class InputComponent : Component
{

    public Vector2 CameraMoveAxis { get; private set; } = Vector2.Zero;
    public bool IsSpeedupPressed { get; private set; }
    public bool IsDraggingCamera { get; private set; }
    public Vector2 CameraDragDelta { get; private set; } = Vector2.Zero;
    public bool ZoomInRequested { get; private set; }
    public bool ZoomOutRequested { get; private set; }


    public void OnCameraInputUpdate(double delta)
    {     
        // camera movement uses held-input handling so higher layers can capture it.
        if (!_inputModule.TryHandleVector("camera_left", "camera_right", "camera_up", "camera_down", out var cameraMoveAxis))
            cameraMoveAxis = Vector2.Zero;

        CameraMoveAxis = cameraMoveAxis;
        IsSpeedupPressed = _inputModule.TryHandlePressed("camera_speedup");
        CameraDragDelta = _inputModule.GetMouseDelta();
        IsDraggingCamera = _inputModule.TryHandlePressed("camera_drag");
        ZoomInRequested = _inputModule.TryHandleJustPressed("camera_zoom_in");
        ZoomOutRequested = _inputModule.TryHandleJustPressed("camera_zoom_out");
    }

    public void OnCameraInputDestroy()
    {
        CameraMoveAxis = Vector2.Zero;
        CameraDragDelta = Vector2.Zero;
        IsSpeedupPressed = false;
        IsDraggingCamera = false;
        ZoomInRequested = false;
        ZoomOutRequested = false;
    }
}
