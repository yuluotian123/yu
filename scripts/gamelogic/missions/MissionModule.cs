using Framework;

namespace GameLogic
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
            return;
        
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
