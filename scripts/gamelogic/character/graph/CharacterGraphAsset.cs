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

        public override List<string> GetAllowedNodeTypes() =>
            GraphTypeRegistry.GetNodeTypeNamesForGraphType(CharacterGraphTypeName);

        public void InitializeComponentArguments()
        {
            foreach (var call in Nodes.OfType<GraphComponentCallNodeData>())
                call.InitializeArguments();
        }

        public override string GetEditorTitle() => "Character Graph Editor";
        public override GraphConnection CreateConnection() => new CharacterGraphConnection();
    }
}
