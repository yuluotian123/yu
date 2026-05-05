using Godot;

namespace GameLogic
{
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

    public abstract class HfsmBlackboardConditionBase : HfsmConditionBase
    {
        public GraphBlackboardKeyReference Parameter { get; set; } = new();
        public string ParameterName { get; set; } = string.Empty;

        protected string ParameterKey
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Parameter?.Key))
                    return Parameter.Key;

                return ParameterName;
            }
        }

        protected void SyncLegacyParameterName()
        {
            Parameter ??= new GraphBlackboardKeyReference { Key = ParameterName };
            if (string.IsNullOrWhiteSpace(Parameter.Key) && !string.IsNullOrWhiteSpace(ParameterName))
                Parameter.Key = ParameterName;
        }
    }
}
