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
            result.AddRange(GraphNodeFactory.GetNodesForGraphType(FlowGraphAsset.GraphTypeName));
            result.AddRange(GraphNodeFactory.GetNodesForGraphType(SkillGraphTypeName));
            return result.Distinct(System.StringComparer.Ordinal).ToList();
        }

        public override string GetEditorTitle() => "Skill FlowGraph Editor";
    }
}
