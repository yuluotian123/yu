using System;
using System.Collections.Generic;

/// <summary>Components own domain-specific node and action palettes; the graph editor only hosts them.</summary>
public interface IGraphNodeProvider
{
    IEnumerable<Type> GetGraphNodeTypes(GraphAsset graph, GraphAsset root);
    IEnumerable<Type> GetGraphActionTypes(GraphAsset graph, GraphAsset root);
}

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class GraphProvidedByAttribute : Attribute
{
    public Type ComponentType { get; }
    public GraphProvidedByAttribute(Type componentType) => ComponentType = componentType;
}
