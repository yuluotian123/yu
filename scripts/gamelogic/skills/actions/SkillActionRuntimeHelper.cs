using Godot;

namespace GameLogic
{
    internal static class SkillActionRuntimeHelper
    {
        public static GameObject2D GetGameObject(GraphExecutionContext context)
        {
            return context?.GetUserData<GameObject2D>() ?? context?.GetUserData<HfsmRuntime>()?.GameObject;
        }

        public static T FindFirst<T>(Node root) where T : Node
        {
            if (root == null)
                return null;

            if (root is T typed)
                return typed;

            foreach (Node child in root.GetChildren())
            {
                T result = FindFirst<T>(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
