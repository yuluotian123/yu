using Godot;

namespace GameLogic
{
    [GraphCallable("Consume Buffered Input", "技能 / 输入", GraphCallableUsage.Flow, ChineseName = "消耗缓存输入")]
    public class AbilityConsumeInputCondition : GraphConditionBase
    {
        public string InputName { get; set; } = "primary_attack";
        public override string Description => $"Consume Input: {InputName}";
        public override bool IsMet(GraphExecutionContext context) =>
            context?.GetUserData<AbilityRuntime>()?.ConsumeBufferedInput(InputName) == true;

#if TOOLS
        public override Control CreateEditUI(GraphEditorContext context) =>
            GraphEditorUi.BuildLineEditRow("Input", InputName, "Input name", value => InputName = value);
#endif
    }
}
