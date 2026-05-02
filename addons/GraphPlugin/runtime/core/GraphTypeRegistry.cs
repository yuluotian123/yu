using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

/// <summary>
/// GraphPlugin V2 的图类型和节点类型注册中心。
/// </summary>
/// <remarks>
/// <para>
/// 注册中心只做三件事：注册类型、查询定义、解析序列化类型。它不直接负责编辑器 UI，
/// 也不承担运行时语义。节点实例创建能力放在 <see cref="GraphNodeDefinition"/> 上，
/// 调用者可以在命令、反序列化或测试中按需创建。
/// </para>
/// <para>
/// 默认会通过反射扫描所有 <see cref="GraphNodeData"/> 子类，适合 Godot C# 项目热加载。
/// 业务层也可以显式调用 <see cref="RegisterNode(Type)"/>，让重要节点更早进入注册表。
/// </para>
/// </remarks>
public static class GraphTypeRegistry
{
    private static readonly Dictionary<string, GraphTypeDefinition> GraphTypes = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, GraphNodeDefinition> NodesByType = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Type> TypesByName = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal);
    private static bool _scanned;

    /// <summary>
    /// 注册图类型。
    /// </summary>
    public static void RegisterGraphType(GraphTypeDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.GraphType))
            return;

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
            definition.DisplayName = definition.GraphType;

        definition.CreateConnection ??= () => new GraphConnection();
        GraphTypes[definition.GraphType] = definition;
    }

    /// <summary>
    /// 注册节点类型。
    /// </summary>
    public static GraphNodeDefinition RegisterNode<TNode>() where TNode : GraphNodeData
    {
        return RegisterNode(typeof(TNode));
    }

    /// <summary>
    /// 注册节点类型。
    /// </summary>
    public static GraphNodeDefinition RegisterNode(Type nodeType)
    {
        if (nodeType == null ||
            !typeof(GraphNodeData).IsAssignableFrom(nodeType) ||
            nodeType.IsAbstract ||
            nodeType.IsInterface)
        {
            return null;
        }

        GraphNodeData template;
        try
        {
            template = Activator.CreateInstance(nodeType) as GraphNodeData;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[GraphTypeRegistry] 无法实例化节点类型 {nodeType.FullName}: {ex.Message}");
            return null;
        }

        if (template == null || string.IsNullOrWhiteSpace(template.NodeType))
            return null;

        GraphNodeDefinition definition = template.BuildDefinition();
        definition.NodeDataType = nodeType;
        definition.Create = () => (GraphNodeData)Activator.CreateInstance(nodeType);

        NodesByType[definition.NodeType] = definition;
        RegisterTypeName(nodeType);

        foreach (string graphType in definition.GraphTypes)
        {
            if (!string.IsNullOrWhiteSpace(graphType) && graphType != "All" && !GraphTypes.ContainsKey(graphType))
            {
                RegisterGraphType(new GraphTypeDefinition
                {
                    GraphType = graphType,
                    DisplayName = graphType
                });
            }
        }

        return definition;
    }

    /// <summary>
    /// 注册旧类型名到新类型名的别名。硬切 V2 后主要用于重命名时的短期工程内迁移。
    /// </summary>
    public static void RegisterAlias(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            return;

        Aliases[oldName] = newName;
    }

    /// <summary>
    /// 扫描当前 AppDomain 中所有节点类型。
    /// </summary>
    public static void AutoRegisterAll()
    {
        if (_scanned)
            return;

        _scanned = true;
        RegisterTypeNamesFromAssemblies();

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(type => type != null).ToArray();
            }
            catch
            {
                continue;
            }

            foreach (Type type in types)
                RegisterNode(type);
        }
    }

    /// <summary>
    /// 返回某个图类型允许创建的节点定义。
    /// </summary>
    public static List<GraphNodeDefinition> GetNodesForGraphType(string graphType)
    {
        EnsureScanned();
        var result = new List<GraphNodeDefinition>();
        foreach (GraphNodeDefinition definition in NodesByType.Values)
        {
            if (definition.GraphTypes.Contains("All") ||
                definition.GraphTypes.Contains(graphType))
            {
                result.Add(definition);
            }
        }

        return result
            .OrderBy(definition => definition.Category)
            .ThenBy(definition => definition.DisplayName)
            .ToList();
    }

    /// <summary>
    /// 返回某个图类型允许创建的节点类型名。
    /// </summary>
    public static List<string> GetNodeTypeNamesForGraphType(string graphType)
    {
        return GetNodesForGraphType(graphType)
            .Select(definition => definition.NodeType)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 查找节点定义。
    /// </summary>
    public static bool TryGetNodeDefinition(string nodeType, out GraphNodeDefinition definition)
    {
        EnsureScanned();
        nodeType = ResolveAlias(nodeType);
        return NodesByType.TryGetValue(nodeType, out definition);
    }

    /// <summary>
    /// 按类型名创建节点数据实例。
    /// </summary>
    public static GraphNodeData CreateNodeData(string nodeType)
    {
        return TryGetNodeDefinition(nodeType, out GraphNodeDefinition definition)
            ? definition.CreateNode()
            : new GraphNodeData { NodeType = nodeType };
    }

    /// <summary>
    /// 返回所有已注册节点类型名。
    /// </summary>
    public static List<string> GetAllRegisteredTypes()
    {
        EnsureScanned();
        return NodesByType.Keys.OrderBy(type => type).ToList();
    }

    /// <summary>
    /// 序列化层使用的 CLR 类型解析入口。
    /// </summary>
    public static bool TryResolveType(string typeName, out Type type)
    {
        EnsureScanned();
        typeName = ResolveAlias(typeName);
        return TypesByName.TryGetValue(typeName, out type);
    }

    /// <summary>
    /// 返回指定图类型的默认连线。
    /// </summary>
    public static GraphConnection CreateConnection(string graphType)
    {
        EnsureScanned();
        return GraphTypes.TryGetValue(graphType, out GraphTypeDefinition definition)
            ? definition.CreateConnection()
            : new GraphConnection();
    }

    private static void EnsureScanned()
    {
        if (!_scanned)
            AutoRegisterAll();
    }

    private static string ResolveAlias(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return typeName;

        return Aliases.TryGetValue(typeName, out string alias) ? alias : typeName;
    }

    private static void RegisterTypeNamesFromAssemblies()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(type => type != null).ToArray();
            }
            catch
            {
                continue;
            }

            foreach (Type type in types)
                RegisterTypeName(type);
        }
    }

    private static void RegisterTypeName(Type type)
    {
        if (type == null)
            return;

        TypesByName[type.Name] = type;
        if (!string.IsNullOrWhiteSpace(type.FullName))
            TypesByName[type.FullName] = type;
    }
}
