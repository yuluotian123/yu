using Godot;

namespace GameLogic
{
    /// <summary>Negates an HFSM condition without changing the graph runtime.</summary>
    [GraphCallable("Not", "状态条件 / Hfsm", GraphCallableUsage.Hfsm, ChineseName = "条件取反")]
    public sealed class HfsmNotCondition : HfsmConditionBase
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
