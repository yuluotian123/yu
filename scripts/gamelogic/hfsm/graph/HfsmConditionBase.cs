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

    public abstract class HfsmConditionBase
    {
        public virtual string Description => GetType().Name;

        public abstract bool IsMet(HfsmRuntime runtime);

        public virtual Control CreateEditUI(GraphEditorContext context)
        {
            return new Label { Text = Description };
        }
    }
}
