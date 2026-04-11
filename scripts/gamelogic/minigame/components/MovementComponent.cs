using Framework;
using GameLogic;
using Godot;

[GlobalClass]
public partial class MovementComponent : Component
{
    public override int Priority => ComponentPriority.Movement;

    public override void OnInit()
    {

    }

    public override void OnPhysicsUpdate(double delta)
    {
       
    }
}