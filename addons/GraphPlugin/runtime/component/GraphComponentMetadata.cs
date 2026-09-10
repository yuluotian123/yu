using System;

public enum GraphValueAccess
{
    ReadOnly,
    WriteOnly,
    ReadWrite
}

public enum GraphActionStatus
{
    Success,
    Failure,
    Running
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class GraphValueAttribute : Attribute
{
    public GraphValueAttribute(string id)
    {
        Id = id ?? string.Empty;
    }

    public string Id { get; }
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public GraphValueAccess Access { get; set; } = GraphValueAccess.ReadOnly;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class GraphActionAttribute : Attribute
{
    public GraphActionAttribute(string id)
    {
        Id = id ?? string.Empty;
    }

    public string Id { get; }
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool UseInputEventValue { get; set; }
}
