using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic
{
    [Tool]
    [GlobalClass]
    public partial class CharacterGraphAsset : FlowGraphAsset
    {
        public const string CharacterGraphTypeName = "CharacterGraph";

        public override string GraphType
        {
            get => CharacterGraphTypeName;
            set { }
        }

        public override List<string> GetAllowedNodeTypes()
        {
            var result = GraphTypeRegistry
                .GetNodeTypeNamesForGraphType(FlowGraphAsset.GraphTypeName)
                .Where(typeName => typeName == nameof(FlowDelayNodeData) ||
                    typeName == nameof(FlowConditionNodeData))
                .ToList();
            result.AddRange(GraphTypeRegistry.GetNodeTypeNamesForGraphType(CharacterGraphTypeName));
            return result.Distinct(System.StringComparer.Ordinal).ToList();
        }

        public override string GetEditorTitle() => "Character Graph Editor";
        public override GraphConnection CreateConnection() => new CharacterGraphConnection();
    }
}
