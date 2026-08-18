using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic
{
    [Tool]
    [GlobalClass]
    public partial class SkillFlowGraphAsset : FlowGraphAsset
    {
        public const string SkillGraphTypeName = "SkillFlowGraph";

        public override string GraphType
        {
            get => SkillGraphTypeName;
            set { }
        }

        public override List<string> GetAllowedNodeTypes()
        {
            var result = new List<string>();
            result.AddRange(GraphTypeRegistry
                .GetNodeTypeNamesForGraphType(FlowGraphAsset.GraphTypeName)
                .Where(typeName => !string.Equals(typeName, nameof(FlowTimelineNodeData), System.StringComparison.Ordinal)));
            result.AddRange(GraphTypeRegistry.GetNodeTypeNamesForGraphType(SkillGraphTypeName));
            return result.Distinct(System.StringComparer.Ordinal).ToList();
        }

        public override string GetEditorTitle() => "Skill FlowGraph Editor";
    }
}
