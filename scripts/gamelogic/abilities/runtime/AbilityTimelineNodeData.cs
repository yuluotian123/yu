using System.Collections.Generic;
using System.Linq;

namespace GameLogic
{
    public class AbilityTimelineNodeData : FlowTimelineNodeData
    {
        public override List<string> GetGraphTypes() => new() { AbilityFlowGraphAsset.AbilityGraphTypeName };
        public override string GetMenuName() => "Ability Timeline";
        public override bool CanBePrime() => true;

        public override void Validate(GraphAsset graph, GraphValidationResult result)
        {
            base.Validate(graph, result);
            if (graph is not AbilityFlowGraphAsset abilityGraph ||
                abilityGraph.Nodes.OfType<FlowEntryNodeData>().Any())
                return;

            List<AbilityTimelineNodeData> timelines = abilityGraph.Nodes.OfType<AbilityTimelineNodeData>().ToList();
            if (timelines.Count != 1 && timelines.FirstOrDefault()?.Id == Id)
                result.AddError("An AbilityFlowGraph without Entry requires exactly one Ability Timeline.", Id);
        }
    }
}
