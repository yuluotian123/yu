using Framework;
using Godot;

namespace GameLogic;

/// <summary>白模场景的总览、平面与原角色步行预览，行为保持在 EC 宿主中。</summary>
[Tool, GlobalClass]
public partial class BlockoutPreviewComponent3D : Component3D
{
    public override int Priority => ComponentPriority.Input;
    [Export] public NodePath PlayerPath { get; set; } = new("Player");
    [Export] public NodePath WalkRigPath { get; set; } = new("WalkRig");
    [Export] public NodePath OverviewCameraPath { get; set; } = new("OverviewCamera");
    [Export] public NodePath PlanCameraPath { get; set; } = new("PlanCamera");
    [Export] public NodePath AnnotationsPath { get; set; } = new("Village/Annotations");
    [Export] public NodePath ModeLabelPath { get; set; } = new("HUD/ModeLabel");
    [Export] public NodePath SpawnPath { get; set; } = new("Spawn");

    public string CurrentMode { get; private set; } = "Overview";
    private const string ControlLock = "blockout_preview";
    private static readonly Key[] Keys = { Key.F1, Key.F2, Key.F3, Key.L, Key.R };
    private GameObject3D _player;
    private PlayerCharacterInputComponent3D _playerInput;
    private CharacterMovementComponent3D _movement;
    private ICameraModule _cameraModule;
    private Camera3D _overviewCamera, _planCamera, _walkCamera;
    private Node3D _annotations, _spawn;
    private Label _modeLabel;
    private bool _originalGameplayBlocked;
    private int _previousKeys;

    public override void OnInit()
    {
        _player = Owner.GetNode<GameObject3D>(PlayerPath);
        _playerInput = _player.GetComponent<PlayerCharacterInputComponent3D>();
        _movement = _player.GetComponent<CharacterMovementComponent3D>();
        _originalGameplayBlocked = _playerInput?.GameplayBlocked ?? false;
        _overviewCamera = Owner.GetNode<Camera3D>(OverviewCameraPath);
        _planCamera = Owner.GetNode<Camera3D>(PlanCameraPath);
        _annotations = Owner.GetNode<Node3D>(AnnotationsPath);
        _spawn = Owner.GetNode<Node3D>(SpawnPath);
        _modeLabel = Owner.GetNode<Label>(ModeLabelPath);

        var walkRig = Owner.GetNode<GameObject3D>(WalkRigPath);
        var controller = walkRig.GetComponent<SceneCameraComponent3D>();
        _walkCamera = controller.SceneCamera;
        _cameraModule = ModuleSystem.GetModule<ICameraModule>();
        // 先完成跟随绑定，避免 WalkRig 首帧自动激活后抢走总览镜头。
        _cameraModule.Activate(Owner, walkRig, _player, controller.Profile);
        Owner.SetMeta("blockout_preview", this);
        ResetPlayer();
        ShowOverview();
        _previousKeys = ReadKeys();
    }

    public override void OnUpdate(double delta)
    {
        int keys = ReadKeys();
        int pressed = keys & ~_previousKeys;
        _previousKeys = keys;
        if ((pressed & 1) != 0) ShowOverview();
        else if ((pressed & 2) != 0) ShowPlan();
        else if ((pressed & 4) != 0) ShowWalk();
        if ((pressed & 8) != 0) ToggleAnnotations();
        if ((pressed & 16) != 0) ResetPlayer();
    }

    public void ShowOverview() => SetMode("Overview", _overviewCamera, false);
    public void ShowPlan() => SetMode("Plan", _planCamera, false);
    public void ShowWalk()
    {
        _cameraModule?.SnapToTarget();
        SetMode("Walk", _walkCamera, true);
    }

    public void ToggleAnnotations()
    {
        if (!GodotObject.IsInstanceValid(_annotations)) return;
        _annotations.Visible = !_annotations.Visible;
        UpdateLabel();
    }

    public void ResetPlayer()
    {
        if (!GodotObject.IsInstanceValid(_player) || !GodotObject.IsInstanceValid(_spawn)) return;
        _player.GlobalPosition = _spawn.GlobalPosition;
        if (GodotObject.IsInstanceValid(_movement))
        {
            _movement.Velocity = Vector3.Zero;
            _movement.SubmitCommand(CharacterCommand3D.None, int.MaxValue);
            _movement.SyncBodyToOwner();
        }
        _player.ResetPhysicsInterpolation();
        _cameraModule?.SnapToTarget();
    }

    private void SetMode(string mode, Camera3D camera, bool walking)
    {
        if (!GodotObject.IsInstanceValid(camera)) return;
        CurrentMode = mode;
        if (GodotObject.IsInstanceValid(_playerInput))
            _playerInput.GameplayBlocked = !walking || _originalGameplayBlocked;
        if (GodotObject.IsInstanceValid(_movement))
        {
            if (walking) _movement.ClearControlLock(ControlLock);
            else
            {
                _movement.SetControlLock(ControlLock, true, true, 1000);
                _movement.Velocity = Vector3.Zero;
                _movement.SubmitCommand(CharacterCommand3D.None, int.MaxValue);
            }
        }
        _player.Visible = true;
        _annotations.Visible = !walking;
        camera.MakeCurrent();
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        string name = CurrentMode == "Walk" ? "步行尺度" : CurrentMode == "Plan" ? "村落平面" : "村落总览";
        _modeLabel.Text = $"{name}  /  标注{(_annotations.Visible ? "已显示" : "已隐藏")}";
    }

    private static int ReadKeys()
    {
        int pressed = 0;
        for (int i = 0; i < Keys.Length; i++)
            if (Input.IsPhysicalKeyPressed(Keys[i])) pressed |= 1 << i;
        return pressed;
    }

    public override void OnDestroy()
    {
        _cameraModule?.Release(Owner);
        if (GodotObject.IsInstanceValid(_playerInput)) _playerInput.GameplayBlocked = _originalGameplayBlocked;
        if (GodotObject.IsInstanceValid(_movement)) _movement.ClearControlLock(ControlLock);
        if (GodotObject.IsInstanceValid(Owner)) Owner.RemoveMeta("blockout_preview");
        _player = null;
        _playerInput = null;
        _movement = null;
        _cameraModule = null;
        _overviewCamera = _planCamera = _walkCamera = null;
        _annotations = _spawn = null;
        _modeLabel = null;
        _previousKeys = 0;
    }
}
