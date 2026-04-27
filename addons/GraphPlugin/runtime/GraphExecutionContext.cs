public sealed class GraphExecutionContext
{
    public GraphExecutionContext(GraphAsset graph, GraphBlackboardRuntime blackboard)
    {
        Graph = graph;
        Blackboard = blackboard;
    }

    public GraphAsset Graph { get; }
    public GraphBlackboardRuntime Blackboard { get; }
}
