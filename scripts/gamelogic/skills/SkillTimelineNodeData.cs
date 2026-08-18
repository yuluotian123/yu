using System.Linq;
using System.Collections.Generic;

namespace GameLogic
{
    public class SkillTimelineNodeData : FlowTimelineNodeData
    {
        public override List<string> GetGraphTypes() => new() { SkillFlowGraphAsset.SkillGraphTypeName };
        public override string GetMenuName() => "Skill Timeline";
        public override bool CanBePrime() => true;

        public override void Validate(GraphAsset graph, GraphValidationResult result)
        {
            base.Validate(graph, result);

            if (graph is not SkillFlowGraphAsset skillGraph ||
                skillGraph.Nodes.OfType<FlowEntryNodeData>().Any())
            {
                return;
            }

            List<SkillTimelineNodeData> timelines = skillGraph.Nodes.OfType<SkillTimelineNodeData>().ToList();
            if (timelines.Count != 1 && timelines.FirstOrDefault()?.Id == Id)
                result.AddError("A SkillFlowGraph without Entry requires exactly one Skill Timeline.", Id);
        }
    }
}
