using Framework;
using GameLogic;
using GameLogic.Input;
using Godot;

public partial class Example : Node3D
{
    [Export] private MissionGraph chain;

    private IInputModule _inputModule;

    public override void _Ready()
    {
        _inputModule = ModuleSystem.GetModule<IInputModule>();
    }

    public override void _Process(double delta)
    {
        if (_inputModule != null && _inputModule.TryHandleJustPressed("combat_up"))
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

        if (Input.IsKeyPressed(Key.S))
        {
            GameAPI.Save();
        }

        if (Input.IsKeyPressed(Key.L))
        {
            GameAPI.Load();
        }
    }
}
