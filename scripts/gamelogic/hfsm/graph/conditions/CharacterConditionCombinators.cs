using Godot;

namespace GameLogic
{
    /// <summary>Negates one existing graph condition so character transitions can express Not without changing the graph runtime.</summary>
    public sealed class CharacterNotCondition : HfsmConditionBase
    {
        public HfsmConditionBase Condition { get; set; }

        public override string Description => Condition == null
            ? "Not (missing condition)"
            : $"Not ({Condition.Description})";

        public override bool IsMet(HfsmRuntime runtime)
        {
            return Condition != null && !Condition.IsMet(runtime);
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            return new Label { Text = Description };
        }
    }
}
