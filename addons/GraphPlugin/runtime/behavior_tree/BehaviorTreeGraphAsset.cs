using System.Collections.Generic;
using System.Linq;
using Godot;

[Tool]
[GlobalClass]
public partial class BehaviorTreeGraphAsset : GraphAsset
{
    public const string GraphTypeName = "BehaviorTree";

    public override string GraphType
    {
        get => GraphTypeName;
        set { }
    }

    public BehaviorRootNodeData RootNode => Nodes.OfType<BehaviorRootNodeData>().FirstOrDefault();

    public override List<string> GetAllowedNodeTypes()
    {
        return GraphTypeRegistry
            .GetNodeTypeNamesForGraphType(GraphTypeName)
            .Where(nodeType => GraphTypeRegistry.CreateNodeData(nodeType) is BehaviorTreeNodeData)
            .ToList();
    }

    public override GraphConnection CreateConnection() => new BehaviorTreeConnection();
    public override string GetEditorTitle() => "BehaviorTree Editor";

    public List<BehaviorTreeChildLink> GetChildLinks(string nodeId)
    {
        return GetOutgoingConnections(nodeId)
            .OfType<BehaviorTreeConnection>()
            .Select(connection =>
            {
                GraphNodeData target = FindNodeById(connection.ToNode);
                return target is BehaviorTreeNodeData node
                    ? new BehaviorTreeChildLink(node, connection)
                    : null;
            })
            .Where(link => link != null)
            .OrderBy(link => link.Connection.Order)
            .ThenBy(link => link.Node.Position.Y)
            .ThenBy(link => link.Node.Position.X)
            .ThenBy(link => link.Node.Id)
            .ToList();
    }

    public List<BehaviorTreeNodeData> GetChildren(string nodeId)
    {
        return GetChildLinks(nodeId)
            .Select(link => link.Node)
            .ToList();
    }
}

public sealed class BehaviorTreeChildLink
{
    public BehaviorTreeChildLink(BehaviorTreeNodeData node, BehaviorTreeConnection connection)
    {
        Node = node;
        Connection = connection;
    }

    public BehaviorTreeNodeData Node { get; }
    public BehaviorTreeConnection Connection { get; }
}
