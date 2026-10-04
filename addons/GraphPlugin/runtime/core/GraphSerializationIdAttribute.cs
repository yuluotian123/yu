using System;

/// <summary>Optional persistent ID for graph data types that may be renamed.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GraphSerializationIdAttribute : Attribute
{
    public string Id { get; }

    public GraphSerializationIdAttribute(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A graph serialization ID is required.", nameof(id));
        Id = id;
    }
}
