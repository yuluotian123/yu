using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 图验证问题严重级别。
/// </summary>
public enum GraphValidationSeverity
{
    /// <summary>警告，不一定阻止保存或运行。</summary>
    Warning,

    /// <summary>错误，应阻止保存或运行。</summary>
    Error
}

/// <summary>
/// 单条图验证问题。
/// </summary>
public sealed class GraphValidationIssue
{
    /// <summary>严重级别。</summary>
    public GraphValidationSeverity Severity { get; set; }

    /// <summary>中文问题描述。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>关联节点 id，可为空。</summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>关联连线，可为空。</summary>
    public GraphConnection Connection { get; set; }
}

/// <summary>
/// 图验证结果。
/// </summary>
public sealed class GraphValidationResult
{
    /// <summary>所有问题。</summary>
    public List<GraphValidationIssue> Issues { get; } = new();

    /// <summary>错误列表。</summary>
    public IReadOnlyList<GraphValidationIssue> Errors => Issues
        .Where(issue => issue.Severity == GraphValidationSeverity.Error)
        .ToList();

    /// <summary>警告列表。</summary>
    public IReadOnlyList<GraphValidationIssue> Warnings => Issues
        .Where(issue => issue.Severity == GraphValidationSeverity.Warning)
        .ToList();

    /// <summary>是否没有错误。</summary>
    public bool IsValid => !Issues.Any(issue => issue.Severity == GraphValidationSeverity.Error);

    /// <summary>加入错误。</summary>
    public void AddError(string message, string nodeId = "", GraphConnection connection = null)
    {
        Issues.Add(new GraphValidationIssue
        {
            Severity = GraphValidationSeverity.Error,
            Message = message,
            NodeId = nodeId ?? string.Empty,
            Connection = connection
        });
    }

    /// <summary>加入警告。</summary>
    public void AddWarning(string message, string nodeId = "", GraphConnection connection = null)
    {
        Issues.Add(new GraphValidationIssue
        {
            Severity = GraphValidationSeverity.Warning,
            Message = message,
            NodeId = nodeId ?? string.Empty,
            Connection = connection
        });
    }

    /// <summary>生成适合弹窗显示的多行文本。</summary>
    public string ToDisplayText()
    {
        if (Issues.Count == 0)
            return "图验证通过。";

        return string.Join("\n", Issues.Select(issue =>
            $"{(issue.Severity == GraphValidationSeverity.Error ? "错误" : "警告")}: {issue.Message}"));
    }
}

/// <summary>
/// 图结构验证服务。
/// </summary>
public static class GraphValidationService
{
    /// <summary>
    /// 验证图资源的结构完整性。
    /// </summary>
    public static GraphValidationResult Validate(GraphAsset graph)
    {
        var result = new GraphValidationResult();
        if (graph == null)
        {
            result.AddError("图资源为空。");
            return result;
        }

        ValidateNodes(graph, result);
        ValidateConnections(graph, result);
        ValidateBlackboard(graph, result);
        return result;
    }

    private static void ValidateNodes(GraphAsset graph, GraphValidationResult result)
    {
        var ids = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            GraphNodeData node = graph.Nodes[i];
            if (node == null)
            {
                result.AddError($"节点列表第 {i} 项为空。");
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.Id))
            {
                result.AddError($"节点 {node.GetDisplayName()} 的 Id 为空。");
                continue;
            }

            if (!ids.Add(node.Id))
                result.AddError($"节点 Id 重复：{node.Id}。", node.Id);

            if (!GraphTypeRegistry.TryGetNodeDefinition(node.NodeType, out _))
                result.AddError($"未知节点类型：{node.NodeType}。", node.Id);
        }
    }

    private static void ValidateConnections(GraphAsset graph, GraphValidationResult result)
    {
        var seenConnections = new HashSet<string>(System.StringComparer.Ordinal);
        var outputCounts = new Dictionary<string, int>(System.StringComparer.Ordinal);
        var inputCounts = new Dictionary<string, int>(System.StringComparer.Ordinal);
        Type requiredConnectionType = graph.CreateConnection()?.GetType() ?? typeof(GraphConnection);

        for (int i = 0; i < graph.Connections.Count; i++)
        {
            GraphConnection connection = graph.Connections[i];
            if (connection == null)
            {
                result.AddError($"连线列表第 {i} 项为空。");
                continue;
            }

            if (!requiredConnectionType.IsAssignableFrom(connection.GetType()))
            {
                result.AddError(
                    $"连线类型不匹配：图类型 {graph.GraphType} 需要 {requiredConnectionType.Name}，当前是 {connection.GetType().Name}。",
                    connection.FromNode,
                    connection);
            }

            string connectionKey = $"{connection.FromNode}:{connection.FromPort}->{connection.ToNode}:{connection.ToPort}";
            if (!seenConnections.Add(connectionKey))
                result.AddError($"重复连线：{connectionKey}。", connection.FromNode, connection);

            CountPort(outputCounts, $"{connection.FromNode}:{connection.FromPort}");
            CountPort(inputCounts, $"{connection.ToNode}:{connection.ToPort}");

            GraphNodeData from = graph.FindNodeById(connection.FromNode);
            GraphNodeData to = graph.FindNodeById(connection.ToNode);

            if (from == null)
                result.AddError($"连线起点节点不存在：{connection.FromNode}。", connection.FromNode, connection);
            else if (connection.FromPort < 0 || connection.FromPort >= from.GetOutputCount())
                result.AddError($"连线起点端口越界：{connection.FromNode}:{connection.FromPort}。", connection.FromNode, connection);

            if (to == null)
                result.AddError($"连线终点节点不存在：{connection.ToNode}。", connection.ToNode, connection);
            else if (connection.ToPort < 0 || connection.ToPort >= to.GetInputCount())
                result.AddError($"连线终点端口越界：{connection.ToNode}:{connection.ToPort}。", connection.ToNode, connection);

            if (from != null &&
                to != null &&
                connection.FromPort >= 0 &&
                connection.FromPort < from.GetOutputCount() &&
                connection.ToPort >= 0 &&
                connection.ToPort < to.GetInputCount() &&
                from.GetOutputPortType(connection.FromPort) != to.GetInputPortType(connection.ToPort))
            {
                result.AddError(
                    $"连线端口类型不兼容：{connection.FromNode}:{connection.FromPort} -> {connection.ToNode}:{connection.ToPort}。",
                    connection.FromNode,
                    connection);
            }
        }

        ValidatePortConnectionLimits(graph, outputCounts, inputCounts, result);
    }

    private static void CountPort(Dictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out int count);
        counts[key] = count + 1;
    }

    private static void ValidatePortConnectionLimits(
        GraphAsset graph,
        Dictionary<string, int> outputCounts,
        Dictionary<string, int> inputCounts,
        GraphValidationResult result)
    {
        foreach (GraphNodeData node in graph.Nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id))
                continue;

            for (int port = 0; port < node.GetOutputCount(); port++)
            {
                int maxConnections = node.GetOutputMaxConnections(port);
                if (maxConnections < 0)
                    continue;

                outputCounts.TryGetValue($"{node.Id}:{port}", out int count);
                if (count > maxConnections)
                    result.AddError($"节点 {node.Id} 的输出端口 {port} 连接数超过上限 {maxConnections}。", node.Id);
            }

            for (int port = 0; port < node.GetInputCount(); port++)
            {
                int maxConnections = node.GetInputMaxConnections(port);
                if (maxConnections < 0)
                    continue;

                inputCounts.TryGetValue($"{node.Id}:{port}", out int count);
                if (count > maxConnections)
                    result.AddError($"节点 {node.Id} 的输入端口 {port} 连接数超过上限 {maxConnections}。", node.Id);
            }
        }
    }

    private static void ValidateBlackboard(GraphAsset graph, GraphValidationResult result)
    {
        if (!GraphBlackboardValidator.TryValidate(graph.BlackboardEntries, out string error))
            result.AddError(error);
    }
}
