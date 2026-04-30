namespace GameLogic
{
    public static class HfsmRuntimeExtensions
    {
        public static GameObject2D GetGameObject(this HfsmRuntime runtime)
        {
            return runtime?.Context?.GetUserData<GameObject2D>() ?? runtime?.GameObject;
        }

        public static T GetContextComponent<T>(this HfsmRuntime runtime) where T : Component2D
        {
            return runtime.GetGameObject()?.GetComponent<T>() ?? runtime?.GetComponent<T>();
        }
    }
}
