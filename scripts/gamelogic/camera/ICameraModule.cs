using Godot;

namespace GameLogic;

public interface ICameraModule : ICharacterCameraShake
{
    GameObject3D Rig { get; }
    Camera3D Camera { get; }
    Node3D FollowTarget { get; }
    bool IsActive { get; }
    void Activate(Node owner, GameObject3D rig, Node3D target, CameraProfile3D profile = null);
    void Release(Node owner);
    void Follow(Node3D target, bool snap = false);
    void Focus(Vector3 position, bool snap = false);
    void SnapToTarget();
    void SetViewHeight(float height, bool snap = false);
    void StopShake();
}
