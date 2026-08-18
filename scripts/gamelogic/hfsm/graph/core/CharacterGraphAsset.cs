using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic
{
    [Tool]
    [GlobalClass]
    public partial class CharacterGraphAsset : HfsmGraphAsset
    {
        public const string CharacterGraphTypeName = "CharacterGraph";

        public override string GraphType
        {
            get => CharacterGraphTypeName;
            set { }
        }

        public override List<string> GetAllowedNodeTypes()
        {
            var result = new List<string>();
            result.AddRange(GraphTypeRegistry.GetNodeTypeNamesForGraphType(HfsmGraphAsset.GraphTypeName));
            result.AddRange(GraphTypeRegistry.GetNodeTypeNamesForGraphType(CharacterGraphTypeName));
            return result.Distinct(System.StringComparer.Ordinal).ToList();
        }

        public override string GetEditorTitle() => "Character Graph Editor";
    }
}
