using Godot;

namespace GameLogic;

[GlobalClass]
public partial class CameraProfile3D : Resource
{
    [ExportGroup("Follow Framing")]
    /// <summary>跟随焦点相对目标的世界坐标偏移（米）：X 左右、Y 上下、Z 前后。初次激活会从场景镜头偏移中扣除此值以保留构图；调整初始远近或构图请移动 Camera3D。</summary>
    [Export] public Vector3 BaseOffset { get; set; } = new(0f, 0.8f, 0f);
    /// <summary>各世界轴上的跟随死区总宽度（米），两侧各取一半。越大越允许角色在镜头内移动；设为 0 时立即更新该轴的跟随锚点，仍受 FollowSmooth 平滑影响。</summary>
    [Export] public Vector3 DeadZone { get; set; } = new(0.72f, 0.42f, 0.4f);
    /// <summary>仅正交投影生效：把镜头焦点对齐到视口像素网格，减少像素画抖动；透视镜头忽略此项。</summary>
    [Export] public bool PixelSnap { get; set; } = false;

    [ExportGroup("Follow")]
    /// <summary>X/Y/Z 三轴的跟随响应速度（每秒）。数值越大越贴身，越小越有拖尾；0 表示直接到位，无平滑。较小的 Y 值可减轻跳跃时镜头起伏。</summary>
    [Export] public Vector3 FollowSmooth { get; set; } = new(7f, 4.5f, 7f);
    /// <summary>前视偏移的响应速度（每秒），同时影响朝向前视、跳跃前视和手动观察。越大越快，0 表示直接到位。</summary>
    [Export(PropertyHint.Range, "0,30,0.1")] public float LookSmooth { get; set; } = 5f;
    /// <summary>SetViewHeight 调整 Camera3D.Size 时的响应速度（每秒），0 表示直接到位。用于正交/视锥尺寸过渡，不控制透视 FOV 或相机距离。</summary>
    [Export(PropertyHint.Range, "0,30,0.1")] public float ZoomSmooth { get; set; } = 5f;
    /// <summary>相邻更新间目标位移达到此距离（米）时，镜头立即跟上并重置插值，避免长距离追赶。0 表示关闭瞬移检测。</summary>
    [Export(PropertyHint.Range, "0,100,0.1")] public float TeleportDistance { get; set; } = 6.4f;

    [ExportGroup("Look Ahead")]
    /// <summary>沿角色朝向（无移动组件时沿最后移动方向）的前视距离（米）。越大越多展示前方；0 关闭方向前视，停止移动后仍保留朝向。</summary>
    [Export] public float LookAheadDistance { get; set; } = 0.56f;
    /// <summary>跳跃/下落时向上/下前视的最大距离（米），按竖直速度比例计算；0 关闭竖直速度前视。</summary>
    [Export] public float VerticalVelocityLookDistance { get; set; } = 0.12f;
    /// <summary>竖直速度达到此参考值（米/秒）时，前视偏移达到 VerticalVelocityLookDistance 上限；越大越不敏感，计算时最小取 0.01。</summary>
    [Export] public float VerticalVelocityReference { get; set; } = 7.6f;

    [ExportGroup("Manual Look")]
    /// <summary>允许按键抬高/压低焦点。Free3D 移动模式始终跳过手动观察，避免 W/S 与前后移动冲突。</summary>
    [Export] public bool EnableManualLook { get; set; } = true;
    /// <summary>抬高焦点的 InputMap 动作名，仅 EnableManualLook 开启且角色非 Free3D 时使用。</summary>
    [Export] public string LookUpAction { get; set; } = "camera_up";
    /// <summary>压低焦点的 InputMap 动作名，仅 EnableManualLook 开启且角色非 Free3D 时使用。</summary>
    [Export] public string LookDownAction { get; set; } = "camera_down";
    /// <summary>手动观察的最大竖直偏移（米）；有观察输入时替代竖直速度前视，松开后平滑恢复。</summary>
    [Export] public float ManualLookDistance { get; set; } = 0.8f;
}
