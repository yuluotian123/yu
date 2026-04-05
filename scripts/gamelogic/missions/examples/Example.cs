
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

        if (Input.IsKeyPressed(Key.A))
        {
            GameAPI.Broadcast(new GameMessage(GameEventType.A));
        }

        if (Input.IsKeyPressed(Key.B))
        {
            GameAPI.Broadcast(new GameMessage(GameEventType.B));
        }

        if (Input.IsKeyPressed(Key.C))
        {
            GameAPI.Broadcast(new GameMessage(GameEventType.C));
        }

        if (Input.IsKeyPressed(Key.D))
        {
            GameAPI.Broadcast(new GameMessage(GameEventType.D));
        }



    }
}