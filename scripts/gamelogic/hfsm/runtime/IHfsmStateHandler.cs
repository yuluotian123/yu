namespace GameLogic
{
    public interface IHfsmStateHandler
    {
        void OnHfsmStateEnter(HfsmRuntime runtime, IHfsmStateNodeData state);
        void OnHfsmStateUpdate(HfsmRuntime runtime, IHfsmStateNodeData state, double delta);
        void OnHfsmStateExit(HfsmRuntime runtime, IHfsmStateNodeData state);
    }
}
