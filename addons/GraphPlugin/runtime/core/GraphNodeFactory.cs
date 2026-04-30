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

        GD.Print("[GraphNodeFactory] Registering graph nodes");

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

                        GD.Print($"[GraphNodeFactory] Registered node: {instance.NodeType} for graph types: {string.Join(", ", graphTypes)}");
                    }
                    catch (Exception ex)
                    {
                        GD.PushWarning($"[GraphNodeFactory] Can not instantiate node type {type.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[GraphNodeFactory] Error while scanning assembly {assembly.FullName}: {ex.Message}");
            }
        }
    }

    public static List<string> GetNodesForGraphType(string graphType)
    {
        EnsureInitialized();

        var listNode = new List<string>();

        if (_nodesByGraphType.TryGetValue(graphType, out var list))
            listNode.AddRange(list);

        if (_nodesByGraphType.TryGetValue("All", out var listAll))
            listNode.AddRange(listAll);

        return listNode;
    }

    public static GraphNodeData CreateNodeData(string typeName)
    {
        EnsureInitialized();

        GraphNodeData node;

        if (_nodeTypes.TryGetValue(typeName, out var type))
            node = (GraphNodeData)Activator.CreateInstance(type);
        else
            node = new GraphNodeData { NodeType = typeName };

        return node;
    }

    public static GraphNode CreateNodeUI(GraphNodeData data, GraphEditorContext context)
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
        {
            node.AddChild(CreatePortLabelRow(data, i, i < inputCount, i < outputCount));
            node.SetSlot(
                i,
                i < inputCount,
                i < inputCount ? data.GetInputPortType(i) : 0,
                i < inputCount ? data.GetInputPortColor(i) : color,
                i < outputCount,
                i < outputCount ? data.GetOutputPortType(i) : 0,
                i < outputCount ? data.GetOutputPortColor(i) : color);

            if (i < inputCount)
                node.SetSlotMetadataLeft(i, data.GetInputPortName(i));

            if (i < outputCount)
                node.SetSlotMetadataRight(i, data.GetOutputPortName(i));
        }

        data.CreateUI(context.WithGraphNode(data, node));

        return node;
    }

    private static Control CreatePortLabelRow(GraphNodeData data, int port, bool hasInput, bool hasOutput)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(150, 20)
        };

        var inputLabel = new Label
        {
            Text = hasInput ? data.GetInputPortName(port) : string.Empty,
            HorizontalAlignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true
        };
        row.AddChild(inputLabel);

        var outputLabel = new Label
        {
            Text = hasOutput ? data.GetOutputPortName(port) : string.Empty,
            HorizontalAlignment = HorizontalAlignment.Right,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true
        };
        row.AddChild(outputLabel);

        return row;
    }

    public static List<string> GetAllRegisteredTypes()
    {
        EnsureInitialized();
        return new List<string>(_nodeTypes.Keys);
    }
}
