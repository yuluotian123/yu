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
        //camera movement
        CameraMoveAxis = _inputModule.GetVector("camera_left", "camera_right", "camera_up", "camera_down");
        IsSpeedupPressed = _inputModule.IsPressed("camera_speedup");
        CameraDragDelta = _inputModule.GetMouseDelta();
        IsDraggingCamera = _inputModule.IsPressed("camera_drag");
        ZoomInRequested = _inputModule.IsJustPressed("camera_zoom_in");
        ZoomOutRequested = _inputModule.IsJustPressed("camera_zoom_out");
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
