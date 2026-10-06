using Godot;

namespace GameLogic;

[GlobalClass]
public partial class CameraProfile3D : Resource
{
    [ExportGroup("Follow Framing")]
    [Export] public Vector3 BaseOffset { get; set; } = new(0f, 0.8f, 0f);
    [Export] public Vector3 DeadZone { get; set; } = new(0.72f, 0.42f, 0.4f);
    [Export] public bool PixelSnap { get; set; } = false;

    [ExportGroup("Follow")]
    [Export] public Vector3 FollowSmooth { get; set; } = new(7f, 4.5f, 7f);
    [Export(PropertyHint.Range, "0,30,0.1")] public float LookSmooth { get; set; } = 5f;
    [Export(PropertyHint.Range, "0,30,0.1")] public float ZoomSmooth { get; set; } = 5f;
    [Export(PropertyHint.Range, "0,100,0.1")] public float TeleportDistance { get; set; } = 6.4f;

    [ExportGroup("Look Ahead")]
    [Export] public float LookAheadDistance { get; set; } = 0.56f;
    [Export] public float VerticalVelocityLookDistance { get; set; } = 0.12f;
    [Export] public float VerticalVelocityReference { get; set; } = 7.6f;

    [ExportGroup("Manual Look")]
    [Export] public bool EnableManualLook { get; set; } = true;
    [Export] public string LookUpAction { get; set; } = "camera_up";
    [Export] public string LookDownAction { get; set; } = "camera_down";
    [Export] public float ManualLookDistance { get; set; } = 0.8f;
}
