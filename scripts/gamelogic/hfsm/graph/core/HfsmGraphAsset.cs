using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic
{
    [Tool]
    [GlobalClass]
    public partial class HfsmGraphAsset : StateGraphAsset
    {
        public new const string GraphTypeName = "HfsmGraph";

        public override string GraphType
        {
            get => GraphTypeName;
            set { }
        }

        public override GraphConnection CreateConnection() => new HfsmTransitionConnection();
        public override string GetEditorTitle() => "HFSM Editor";

        protected override bool IsAllowedStateGraphNode(GraphNodeData node)
        {
            return node is IHfsmStateNodeData || node is IHfsmPseudoNodeData;
        }
    }
}
