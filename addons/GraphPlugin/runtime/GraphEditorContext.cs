using Godot;

public sealed class GraphEditorContext
{
    public GraphAsset CurrentGraph { get; set; }
    public GraphAsset RootGraph { get; set; }
    public GraphEdit GraphEdit { get; set; }
    public GraphNode GraphNode { get; set; }
    public GraphNodeData NodeData { get; set; }
    public GraphConnection Connection { get; set; }
    public GraphBlackboardNode GlobalBlackboard { get; set; }
    public GraphBlackboardEntry BlackboardEntry { get; set; }

    public GraphEditorContext WithGraphNode(GraphNodeData nodeData, GraphNode graphNode)
    {
        return CopyBase(new GraphEditorContext
        {
            NodeData = nodeData,
            GraphNode = graphNode
        });
    }

    public GraphEditorContext WithConnection(GraphConnection connection)
    {
        return CopyBase(new GraphEditorContext
        {
            Connection = connection
        });
    }

    public GraphEditorContext WithBlackboardEntry(GraphBlackboardEntry entry)
    {
        return CopyBase(new GraphEditorContext
        {
            BlackboardEntry = entry
        });
    }

    private GraphEditorContext CopyBase(GraphEditorContext target)
    {
        target.CurrentGraph = CurrentGraph;
        target.RootGraph = RootGraph;
        target.GraphEdit = GraphEdit;
        target.GlobalBlackboard = GlobalBlackboard;
        return target;
    }
}
