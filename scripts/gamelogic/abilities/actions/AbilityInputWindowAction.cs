using Godot;

namespace GameLogic
{
    [GraphCallable("Input Buffer Window", "技能 / 输入", GraphCallableUsage.Timeline,
        TimelineKind = GraphTimelineActionKind.Clip, ChineseName = "输入缓存窗口")]
    public class AbilityInputWindowAction : GraphActionBase
    {
        public string InputName { get; set; } = "primary_attack";
        public override string Description => $"Buffer Input: {InputName}";

        public override void Execute(GraphExecutionContext context)
        {
            var ability = context?.GetUserData<AbilityRuntime>();
            var timeline = context?.GetUserData<FlowTimelineContext>();
            if (ability == null || timeline == null) return;
            if (timeline.Phase == FlowTimelinePhase.Start) ability.OpenInputWindow(InputName);
            else if (timeline.Phase == FlowTimelinePhase.Complete) ability.CloseInputWindow(InputName, true);
            else if (timeline.Phase == FlowTimelinePhase.Cancel) ability.CloseInputWindow(InputName);
        }

#if TOOLS
        public override Control CreateEditUI(GraphEditorContext context) =>
            GraphEditorUi.BuildLineEditRow("Input", InputName, "Input name", value => InputName = value);
#endif
    }
}
