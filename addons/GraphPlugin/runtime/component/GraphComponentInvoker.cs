using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using GameLogic;

public static class GraphComponentInvoker
{
    public static bool TryRead(GameObject2D owner, string componentTypeName, string memberId, out object value, out string error)
    {
        value = null;
        if (!TryResolveValue(owner, componentTypeName, memberId, out GraphComponentTypeDescriptor type, out GraphComponentValueDescriptor descriptor, out error) ||
            !descriptor.CanRead)
        {
            error ??= $"Component value '{componentTypeName}.{memberId}' is not readable.";
            return false;
        }

        Component2D component = owner.GetComponent(type.ComponentType) as Component2D;
        try
        {
            value = descriptor.Property?.GetValue(component) ?? descriptor.Getter?.Invoke(component, null);
            return true;
        }
        catch (Exception exception)
        {
            error = $"Failed to read '{componentTypeName}.{memberId}': {exception.GetBaseException().Message}";
            return false;
        }
    }

    public static bool TryWrite(GameObject2D owner, string componentTypeName, string memberId, object value, out string error)
    {
        if (!TryResolveValue(owner, componentTypeName, memberId, out GraphComponentTypeDescriptor type, out GraphComponentValueDescriptor descriptor, out error) ||
            !descriptor.CanWrite)
        {
            error ??= $"Component value '{componentTypeName}.{memberId}' is not writable.";
            return false;
        }

        Component2D component = owner.GetComponent(type.ComponentType) as Component2D;
        if (!TryConvert(value, descriptor.ValueType, out object converted))
        {
            error = $"Value for '{componentTypeName}.{memberId}' is not compatible with {descriptor.ValueType.Name}.";
            return false;
        }

        try
        {
            if (descriptor.Property != null)
                descriptor.Property.SetValue(component, converted);
            else
                descriptor.Setter.Invoke(component, new[] { converted });
            return true;
        }
        catch (Exception exception)
        {
            error = $"Failed to write '{componentTypeName}.{memberId}': {exception.GetBaseException().Message}";
            return false;
        }
    }

    public static bool TryInvoke(GameObject2D owner, string componentTypeName, string memberId, object[] arguments, out GraphActionStatus status, out object returnValue, out string error)
    {
        status = GraphActionStatus.Failure;
        returnValue = null;
        error = string.Empty;
        if (!GraphComponentRegistry.TryGet(componentTypeName, out GraphComponentTypeDescriptor type))
        {
            error = $"Unknown component type '{componentTypeName}'.";
            return false;
        }

        GraphComponentActionDescriptor descriptor = type.Actions.FirstOrDefault(action => action.MemberId == memberId);
        Component2D component = owner?.GetComponent(type.ComponentType) as Component2D;
        if (component == null)
        {
            error = $"GameObject does not contain component '{componentTypeName}'.";
            return false;
        }
        if (descriptor == null)
        {
            error = $"Unknown component action '{componentTypeName}.{memberId}'.";
            return false;
        }

        object[] converted = new object[descriptor.Parameters.Count];
        for (int i = 0; i < converted.Length; i++)
        {
            bool supplied = arguments != null && i < arguments.Length;
            object source = supplied ? arguments[i] : descriptor.Parameters[i].DefaultValue;
            if (!supplied && !descriptor.Parameters[i].IsOptional)
            {
                error = $"Missing required argument {i} for '{componentTypeName}.{memberId}'.";
                return false;
            }
            if (!TryConvert(source, descriptor.Parameters[i].ValueType, out converted[i]))
            {
                error = $"Argument {i} of '{componentTypeName}.{memberId}' is not compatible with {descriptor.Parameters[i].ValueType.Name}.";
                return false;
            }
        }

        try
        {
            returnValue = descriptor.Method.Invoke(component, converted);
            status = descriptor.ReturnType == typeof(GraphActionStatus)
                ? (GraphActionStatus)returnValue
                : descriptor.ReturnType == typeof(bool) && returnValue is bool result && !result
                    ? GraphActionStatus.Failure
                    : GraphActionStatus.Success;
            return true;
        }
        catch (Exception exception)
        {
            error = $"Failed to invoke '{componentTypeName}.{memberId}': {exception.GetBaseException().Message}";
            return false;
        }
    }

    private static bool TryResolveValue(GameObject2D owner, string componentTypeName, string memberId, out GraphComponentTypeDescriptor type, out GraphComponentValueDescriptor descriptor, out string error)
    {
        type = null;
        descriptor = null;
        error = string.Empty;
        if (owner == null)
        {
            error = "Graph component invocation has no GameObject owner.";
            return false;
        }
        if (!GraphComponentRegistry.TryGet(componentTypeName, out type))
        {
            error = $"Unknown component type '{componentTypeName}'.";
            return false;
        }
        if (owner.GetComponent(type.ComponentType) == null)
        {
            error = $"GameObject does not contain component '{componentTypeName}'.";
            return false;
        }
        descriptor = type.Values.FirstOrDefault(value => value.MemberId == memberId);
        if (descriptor == null)
        {
            error = $"Unknown component value '{componentTypeName}.{memberId}'.";
            return false;
        }
        return true;
    }

    private static bool TryConvert(object value, Type targetType, out object converted)
    {
        converted = null;
        if (value == null)
        {
            if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
            {
                converted = null;
                return true;
            }
            return false;
        }
        if (targetType.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }
        try
        {
            if (targetType.IsEnum && value is string enumName)
            {
                converted = Enum.Parse(targetType, enumName, true);
                return true;
            }
            if (targetType == typeof(float) && value is double doubleValue)
            {
                converted = (float)doubleValue;
                return true;
            }
            if (targetType == typeof(Godot.NodePath) && value is string nodePath)
            {
                converted = new Godot.NodePath(nodePath);
                return true;
            }
            if (value is IConvertible)
            {
                converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch
        {
        }
        return false;
    }
}
