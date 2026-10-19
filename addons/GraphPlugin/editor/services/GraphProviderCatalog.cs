#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

public static class GraphProviderCatalog
{
    public static bool IsProvided(Type type) => type?.GetCustomAttribute<GraphProvidedByAttribute>(true) != null;
    public static string ProviderName(Type type) => type.GetCustomAttribute<GraphProvidedByAttribute>(true)?.ComponentType.Name ?? "Component";
    public static IReadOnlyList<Type> Nodes(GraphEditorContext context) => Collect(context, false);
    public static IReadOnlyList<Type> Actions(GraphEditorContext context) => Collect(context, true);
    static IReadOnlyList<Type> Collect(GraphEditorContext context, bool actions)
    {
        if (context?.CurrentGraph == null) return Array.Empty<Type>();
        var root = context.RootGraph ?? context.CurrentGraph;
        var providers = GraphComponentPanel.GetHostComponents(context.ResolveHost?.Invoke()).OfType<IGraphNodeProvider>().ToList();
        if (context.ResolveSource?.Invoke() is IGraphNodeProvider source && !providers.Contains(source)) providers.Add(source);
        Resource temporary = null;
        if (providers.Count == 0 && root.ComponentProviderType is Type type && typeof(IGraphNodeProvider).IsAssignableFrom(type))
        {
            temporary = Activator.CreateInstance(type) as Resource;
            if (temporary is IGraphNodeProvider provider) providers.Add(provider);
        }
        try
        {
            return providers.SelectMany(p => (actions ? p.GetGraphActionTypes(context.CurrentGraph, root) : p.GetGraphNodeTypes(context.CurrentGraph, root))
                .Where(t => t != null && !t.IsAbstract && t.GetCustomAttribute<GraphProvidedByAttribute>(true)?.ComponentType.IsInstanceOfType(p) == true)).Distinct().ToArray();
        }
        finally { temporary?.Dispose(); }
    }
}
#endif
