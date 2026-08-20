using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic
{
    [Tool]
    [GlobalClass]
    public partial class AbilityFlowGraphAsset : FlowGraphAsset
    {
        public const string AbilityGraphTypeName = "AbilityFlowGraph";

        public override string GraphType
        {
            get => AbilityGraphTypeName;
            set { }
        }

        public override List<string> GetAllowedNodeTypes()
        {
            var result = new List<string>();
            result.AddRange(GraphTypeRegistry
                .GetNodeTypeNamesForGraphType(FlowGraphAsset.GraphTypeName)
                .Where(typeName => !string.Equals(typeName, nameof(FlowTimelineNodeData), System.StringComparison.Ordinal)));
            result.AddRange(GraphTypeRegistry.GetNodeTypeNamesForGraphType(AbilityGraphTypeName));
            return result.Distinct(System.StringComparer.Ordinal).ToList();
        }

        public override string GetEditorTitle() => "Ability FlowGraph Editor";
    }
}
