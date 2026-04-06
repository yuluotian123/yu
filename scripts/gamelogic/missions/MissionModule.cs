using Framework;
using GameLogic.Save;
using Godot;

namespace GameLogic.Mission
{
    public class MissionModule : Module, IMissionModule, IProcessModule
    {
        private MissionManager<object> _missionManager;
        private MissionChainManager _missionChainManager;
        private MissionChainSaver _missionChainSaver;

        public override void OnInit()
        {
            _missionManager = new MissionManager<object>();
            _missionChainManager = new MissionChainManager(_missionManager);
            _missionManager.AddComponent(_missionChainManager);

            _missionChainSaver = new MissionChainSaver(_missionManager);
            _missionManager.AddComponent(_missionChainSaver);
            ModuleSystem.GetModule<ISaveModule>().Register(_missionChainSaver);
        }

        public void Process(double elapseSeconds, double realElapseSeconds)
        {
            if (Godot.Input.IsActionJustPressed("combat_up"))
            {
                Debugger.Info("Start Chain");
                StartChain("res://assets/config/graphs/mission_graphs/1.tres");
            }

            if (Godot.Input.IsKeyPressed(Key.A))
            {
                _missionManager.SendMessage(new GameMessage(GameEventType.A));
            }

            if (Godot.Input.IsKeyPressed(Key.B))
            {
                _missionManager.SendMessage(new GameMessage(GameEventType.B));
            }

            if (Godot.Input.IsKeyPressed(Key.C))
            {
                _missionManager.SendMessage(new GameMessage(GameEventType.C));
            }

            if (Godot.Input.IsKeyPressed(Key.D))
            {
                _missionManager.SendMessage(new GameMessage(GameEventType.D));
            }

            if (Godot.Input.IsKeyPressed(Key.S))
            {
                ModuleSystem.GetModule<ISaveModule>().Save();
            }

            if (Godot.Input.IsKeyPressed(Key.L))
            {
                ModuleSystem.GetModule<ISaveModule>().Load();
            }
        }

        public override void Shutdown()
        {
            ModuleSystem.GetModule<ISaveModule>().Unregister(_missionChainSaver);

            _missionManager = null;
            _missionChainManager = null;
            _missionChainSaver = null;
        }

        public void StartChain(string resPath)
        {
            var handle = ModuleSystem.GetModule<IResourceModule>().LoadAsset<MissionGraph>(resPath);
            if (handle.Asset != null)
                _missionChainManager.StartChain(handle.Asset);
            handle.Release();
        }


    }
}