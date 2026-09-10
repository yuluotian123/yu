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
                    typeName == nameof(FlowConditionNodeData) ||
                    typeName == nameof(GraphComponentCallNodeData) ||
                    typeName == nameof(GraphComponentGetNodeData) ||
                    typeName == nameof(GraphComponentSetNodeData))
                .ToList();
            result.AddRange(GraphTypeRegistry.GetNodeTypeNamesForGraphType(CharacterGraphTypeName));
            return result
                .Where(typeName => typeName != nameof(CharacterAddMovementInputNodeData) &&
                    typeName != nameof(CharacterStopMovementInputNodeData) &&
                    typeName != nameof(CharacterJumpInputNodeData))
                .Distinct(System.StringComparer.Ordinal)
                .ToList();
        }

        public bool MigrateMovementNodesToComponents()
    {
        const string movementType = "GameLogic.CharacterMovementComponent2D";
        bool changed = false;
        for (int i = 0; i < Nodes.Count; i++)
        {
            GraphNodeData replacement = Nodes[i] switch
            {
                CharacterAddMovementInputNodeData old => CreateCall(old, movementType, "AddMovementInput"),
                CharacterStopMovementInputNodeData old => CreateCall(old, movementType, "StopMovementInput"),
                CharacterJumpInputNodeData old when old.Command == CharacterJumpCommand.RequestStart =>
                    CreateCall(old, movementType, "RequestJumpStart"),
                CharacterJumpInputNodeData old => CreateSustainCall(old, movementType, old.Command == CharacterJumpCommand.SustainOn),
                _ => null
            };
            if (replacement == null)
                continue;
            Nodes[i] = replacement;
            changed = true;
        }
        if (changed)
            MarkDirty();
        return changed;
    }

    private static GraphComponentCallNodeData CreateCall(GraphNodeData source, string componentType, string actionId)
    {
        return new GraphComponentCallNodeData
        {
            Id = source.Id,
            Position = source.Position,
            ComponentTypeName = componentType,
            MemberId = actionId
        };
    }

    private static GraphComponentCallNodeData CreateSustainCall(CharacterJumpInputNodeData source, string componentType, bool value)
    {
        var node = CreateCall(source, componentType, "SetJumpSustain");
        node.Arguments.Add(new GraphComponentArgument
        {
            Name = "requested",
            Value = new GraphBoolBlackboardValue { Value = value }
        });
        return node;
    }

        public override string GetEditorTitle() => "Character Graph Editor";
        public override GraphConnection CreateConnection() => new CharacterGraphConnection();
    }
}
