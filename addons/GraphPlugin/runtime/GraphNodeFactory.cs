using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class GraphNodeFactory
{
    private static Dictionary<string, List<string>> _nodesByGraphType = new();
    private static Dictionary<string, Type> _nodeTypes = new();
    private static bool _isInitialized = false;

    private static void EnsureInitialized()
    {
        if (!_isInitialized && _nodeTypes.Count == 0)
        {
            AutoRegisterAll();
            _isInitialized = true;
        }
    }

    public static void AutoRegisterAll()
    {
        _nodesByGraphType.Clear();
        _nodeTypes.Clear();

        GD.Print("注册节点");

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(GraphNodeData)));

                foreach (var type in types)
                {
                    try
                    {
                        var instance = (GraphNodeData)Activator.CreateInstance(type);
                        if (string.IsNullOrEmpty(instance.NodeType))
                            continue;

                        _nodeTypes[instance.NodeType] = type;

                        var graphTypes = instance.GetGraphTypes();
                        foreach (var graphType in graphTypes)
                        {
                            if (!_nodesByGraphType.ContainsKey(graphType))
                                _nodesByGraphType[graphType] = new List<string>();
                            _nodesByGraphType[graphType].Add(instance.NodeType);
                        }

                        GD.Print($"注册节点: {instance.NodeType} 用于图类型: {string.Join(", ", graphTypes)}");
                    }
                    catch (Exception ex)
                    {
                        GD.PushWarning($"无法实例化节点类型 {type.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PushWarning($"扫描程序集 {assembly.FullName} 时出错: {ex.Message}");
            }
        }
    }

    public static List<string> GetNodesForGraphType(string graphType)
    {
        EnsureInitialized();

        var listNode = new List<string>();

        if (_nodesByGraphType.TryGetValue(graphType, out var list)) listNode.AddRange(list);
        if (_nodesByGraphType.TryGetValue("All", out var listAll)) listNode.AddRange(listAll);

        return listNode;
    }

    public static GraphNodeData CreateNodeData(string typeName)
    {
        EnsureInitialized();

        GraphNodeData node = null;

        if (_nodeTypes.TryGetValue(typeName, out var type))
            node = (GraphNodeData)Activator.CreateInstance(type);
        else
            node = new GraphNodeData() { NodeType = typeName };

        return node;
    }

    public static GraphNode CreateNodeUI(GraphNodeData data)
    {
        var node = new GraphNode
        {
            Name = data.Id,
            Title = data.GetDisplayName(),
            PositionOffset = data.Position,
            Draggable = true,
            Resizable = true
        };

        var inputCount = data.GetInputCount();
        var outputCount = data.GetOutputCount();
        var maxSlots = Math.Max(inputCount, outputCount);
        var color = data.GetNodeColor();

        for (int i = 0; i < maxSlots; i++)
            node.SetSlot(i, i < inputCount, 0, color, i < outputCount, 0, color);

        data.CreateUI(node);

        return node;
    }

    public static List<string> GetAllRegisteredTypes()
    {
        EnsureInitialized();
        return new List<string>(_nodeTypes.Keys);
    }
}
