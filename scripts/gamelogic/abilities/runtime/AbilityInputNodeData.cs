using System.Collections.Generic;
using Godot;

namespace GameLogic
{
    public class AbilityInputNodeData : CharacterInputActionNodeData, IFlowNode
    {
        public string BufferedInput { get; set; } = string.Empty;
        public List<GraphConditionBase> Conditions { get; set; } = new();
        public override List<string> GetGraphTypes() => new() { AbilityFlowGraphAsset.AbilityGraphTypeName };
        public override string GetCategory() => "技能 / 输入";
        public override string GetMenuName() => "技能输入（Ability Input）";
        public override bool CanBePrime() => true;
        public bool IsEligible(GraphExecutionContext context) => GraphConditionEvaluator.IsMet(Conditions, GraphConditionUseMode.And, context);
        public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context) { }
        public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
        public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }
        public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
        {
            completion = NodeCompletion.Next();
            return true;
        }
#if TOOLS
        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddChild(base.CreateInspectorUI(context));
            root.AddChild(GraphEditorUi.BuildLineEditRow("Buffered Input", BufferedInput, "Empty = activation only", value => BufferedInput = value));
            root.AddChild(new ReorderableListControl<GraphConditionBase>(Conditions,
                condition => condition.CreateEditUI(context), condition => condition.Description,
                GraphCallableCatalog.Conditions(GraphCallableUsage.Flow), type => (GraphConditionBase)System.Activator.CreateInstance(type)).Build());
            return root;
        }
#endif
    }
}
