
using Framework;
using Godot;

public partial class Example : Node3D
{
    [Export] private MissionGraph chain;

    public override void _Process(double delta)
    {
       
        if (Input.IsActionJustPressed("combat_up"))
        {
            Debugger.Info("Start Chain");
            GameAPI.MissionChainManager.StartChain(chain);
        }



    }
}