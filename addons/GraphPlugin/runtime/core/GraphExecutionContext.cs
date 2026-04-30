using System.Collections.Generic;

public sealed class GraphExecutionContext
{
    public GraphExecutionContext(GraphAsset graph, GraphBlackboardRuntime blackboard)
    {
        Graph = graph;
        Blackboard = blackboard;
    }

    public GraphAsset Graph { get; }
    public GraphBlackboardRuntime Blackboard { get; }
    public List<object> UserData { get; } = new();

    public T GetUserData<T>() where T : class
    {
        for (int i = 0; i < UserData.Count; i++)
        {
            if (UserData[i] is T typed)
                return typed;
        }

        return null;
    }

    public IReadOnlyList<T> GetUserDataAll<T>() where T : class
    {
        var result = new List<T>();
        for (int i = 0; i < UserData.Count; i++)
        {
            if (UserData[i] is T typed)
                result.Add(typed);
        }

        return result;
    }
}
