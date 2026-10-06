using System;
using Framework;
using Godot;

namespace GameLogic;

/// <summary>模块只持有当前场景相机的引用，不创建或销毁相机节点。</summary>
public class CameraModule : Module, ICameraModule
{
    private GameObject3D _rig;
    private SceneCameraComponent3D _controller;
    private Camera3D _camera;
    private Node _owner;

    public GameObject3D Rig => GodotObject.IsInstanceValid(_rig) ? _rig : null;
    public Camera3D Camera => GodotObject.IsInstanceValid(_camera) ? _camera : null;
    public Node3D FollowTarget => IsActive ? _controller.FollowTarget : null;
    public bool IsActive => Rig != null && _controller?.HasShot == true;

    public override void OnInit() { }

    public void Activate(Node owner, GameObject3D rig, Node3D target, CameraProfile3D profile = null)
    {
        if (!IsLive(owner) || !IsLive(rig) || !IsLive(target))
            throw new ArgumentException("Camera owner, authored rig and target must be live scene nodes.");
        if (target.GetViewport() != (Engine.GetMainLoop() as SceneTree)?.Root)
            throw new ArgumentException("The global camera follows targets in the main viewport.");
        var controller = rig.GetComponent<SceneCameraComponent3D>();
        if (controller?.SceneCamera == null)
            throw new ArgumentException("The scene camera rig needs a camera EC component and Camera3D child.");
        if (_owner != null) Release(_owner);
        _rig = rig;
        _controller = controller;
        _camera = controller.SceneCamera;
        _owner = owner;
        _owner.TreeExiting += OnOwnerExiting;
        _controller.Activate(target, profile);
    }

    public void Release(Node owner)
    {
        if (!ReferenceEquals(_owner, owner)) return;
        DetachOwner();
        if (Rig != null) _controller.Deactivate();
        _rig = null;
        _camera = null;
        _controller = null;
    }

    public void Follow(Node3D target, bool snap = false)
    {
        if (!IsLive(target) || target.GetViewport() != (Engine.GetMainLoop() as SceneTree)?.Root)
            throw new ArgumentException("Follow target must be a live Node3D in the main viewport.");
        if (IsActive) _controller.Follow(target, snap);
    }

    public void Focus(Vector3 position, bool snap = false) { if (IsActive) _controller.Focus(position, snap); }
    public void SnapToTarget() { if (IsActive) _controller.SnapToTarget(); }
    public void SetViewHeight(float height, bool snap = false) { if (IsActive) _controller.SetViewHeight(height, snap); }
    public void Shake(CameraShakeProfile profile) { if (IsActive) _controller.Shake(profile); }
    public void StopShake() { if (IsActive) _controller.StopShake(); }

    public override void Shutdown()
    {
        if (_owner != null) Release(_owner);
    }

    private void OnOwnerExiting() => Release(_owner);
    private void DetachOwner()
    {
        if (GodotObject.IsInstanceValid(_owner)) _owner.TreeExiting -= OnOwnerExiting;
        _owner = null;
    }

    internal static bool IsLive(Node node) => GodotObject.IsInstanceValid(node) &&
        node.IsInsideTree() && !node.IsQueuedForDeletion();
}
