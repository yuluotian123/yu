using System;
using System.Collections.Generic;
using System.Reflection;

public sealed class GraphComponentParameterDescriptor
{
    public string Name { get; set; } = string.Empty;
    public Type ValueType { get; set; }
    public bool IsSupported { get; set; }
    public bool IsOptional { get; set; }
    public object DefaultValue { get; set; }
}

public sealed class GraphComponentValueDescriptor
{
    public string ComponentTypeName { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Type ValueType { get; set; }
    public GraphValueAccess Access { get; set; }
    public PropertyInfo Property { get; set; }
    public MethodInfo Getter { get; set; }
    public MethodInfo Setter { get; set; }

    public bool CanRead => Access != GraphValueAccess.WriteOnly && (Property?.CanRead == true || Getter != null);
    public bool CanWrite => Access != GraphValueAccess.ReadOnly && (Property?.CanWrite == true || Setter != null);
}

public sealed class GraphComponentActionDescriptor
{
    public string ComponentTypeName { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MethodInfo Method { get; set; }
    public Type ReturnType { get; set; }
    public bool HasSupportedReturnType { get; set; }
    public bool UseInputEventValue { get; set; }
    public List<GraphComponentParameterDescriptor> Parameters { get; } = new();
}

public sealed class GraphComponentTypeDescriptor
{
    public Type ComponentType { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<GraphComponentValueDescriptor> Values { get; } = new();
    public List<GraphComponentActionDescriptor> Actions { get; } = new();
}
