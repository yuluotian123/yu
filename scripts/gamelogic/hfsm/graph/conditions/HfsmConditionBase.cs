using Godot;

namespace GameLogic
{
    public enum HfsmConditionUseMode
    {
        And,
        Or
    }

    public enum HfsmFloatComparison
    {
        Less,
        LessOrEqual,
        Equal,
        GreaterOrEqual,
        Greater
    }

    public abstract class HfsmConditionBase : StateConditionBase
    {
        public abstract bool IsMet(HfsmRuntime runtime);

        public override bool IsMet(StateGraphRuntime runtime)
        {
            return IsMet(runtime as HfsmRuntime ?? runtime?.Context?.GetUserData<HfsmRuntime>());
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            return new Label { Text = Description };
        }
    }
}
