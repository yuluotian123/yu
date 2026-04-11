using Framework;
using GameLogic;
using Godot;

[GlobalClass]
public partial class CameraComponent : Component
{
    private Camera3D _camera;
    
    public override int Priority => ComponentPriority.Movement - 10;

    public override void OnInit()
    {

    }

    public override void OnPhysicsUpdate(double delta)
    {
    }

}
