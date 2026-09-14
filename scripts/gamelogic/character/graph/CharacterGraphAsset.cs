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

        public CharacterGraphAsset()
        {
            ResourceLocalToScene = true;
        }

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
                if (replacement != null)
                {
                    Nodes[i] = replacement;
                    changed = true;
                    continue;
                }

                if (Nodes[i] is GraphComponentCallNodeData componentCall && NeedsArgumentInitialization(componentCall))
                {
                    componentCall.InitializeArguments();
                    changed = true;
                }

                if (Nodes[i] is GraphComponentNodeData componentNode &&
                    !string.IsNullOrWhiteSpace(componentNode.ComponentTypeName) &&
                    componentNode.Component?.IsAssigned != true)
                {
                    componentNode.Component ??= new GraphActionComponentReference();
                    componentNode.Component.ComponentTypeName = componentNode.ComponentTypeName;
                    componentNode.Component.ComponentSlot = 0;
                    changed = true;
                }
            }
            if (changed)
                MarkDirty();
            return changed;
        }

        private static GraphComponentCallNodeData CreateCall(GraphNodeData source, string componentType, string actionId)
        {
            var node = new GraphComponentCallNodeData
            {
                Id = source.Id,
                Position = source.Position,
                ComponentTypeName = componentType,
                Component = new GraphActionComponentReference
                {
                    ComponentTypeName = componentType,
                    ComponentSlot = 0
                },
                MemberId = actionId
            };
            node.InitializeArguments();
            return node;
        }

        private static bool NeedsArgumentInitialization(GraphComponentCallNodeData node)
        {
            if (node == null || !GraphComponentRegistry.TryGet(node.ComponentTypeName, out GraphComponentTypeDescriptor descriptor))
                return false;
            GraphComponentActionDescriptor action = descriptor.Actions.FirstOrDefault(value => value.MemberId == node.MemberId);
            if (action == null || node.Arguments == null || node.Arguments.Count != action.Parameters.Count)
                return action != null;
            for (int i = 0; i < action.Parameters.Count; i++)
            {
                GraphComponentArgument argument = node.Arguments[i];
                if (argument == null || argument.Value == null || argument.Name != action.Parameters[i].Name)
                    return true;
            }
            return false;
        }

        private static GraphComponentCallNodeData CreateSustainCall(CharacterJumpInputNodeData source, string componentType, bool value)
        {
            var node = CreateCall(source, componentType, "SetJumpSustain");
            node.InitializeArguments();
            if (node.Arguments.Count == 0)
                node.Arguments.Add(new GraphComponentArgument { Name = "requested" });
            node.Arguments[0].Name = "requested";
            node.Arguments[0].Value = new GraphBoolBlackboardValue { Value = value };
            return node;
        }

        public override string GetEditorTitle() => "Character Graph Editor";
        public override GraphConnection CreateConnection() => new CharacterGraphConnection();
    }
}
