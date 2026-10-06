using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameLogic;

public static class GraphComponentRegistry
{
    private static readonly Dictionary<string, GraphComponentTypeDescriptor> ByName = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, GraphComponentTypeDescriptor> ByScriptPath = new(StringComparer.Ordinal);
    private static bool _scanned;

    public static void EnsureScanned()
    {
        if (_scanned)
            return;

        _scanned = true;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types.Where(type => type != null).ToArray();
            }
            catch
            {
                continue;
            }

            foreach (Type type in types)
            {
                if (type == null || type.IsAbstract || !typeof(IComponent).IsAssignableFrom(type))
                    continue;
                Register(type);
            }
        }
    }

    public static GraphComponentTypeDescriptor Register(Type componentType)
    {
        if (componentType == null || componentType.IsAbstract || !typeof(IComponent).IsAssignableFrom(componentType))
            return null;

        string typeName = componentType.FullName ?? componentType.Name;
        if (ByName.TryGetValue(typeName, out GraphComponentTypeDescriptor existing))
            return existing;

        var descriptor = new GraphComponentTypeDescriptor
        {
            ComponentType = componentType,
            TypeName = typeName,
            DisplayName = (componentType.Name.EndsWith("Component2D", StringComparison.Ordinal) || componentType.Name.EndsWith("Component3D", StringComparison.Ordinal))
                ? componentType.Name[..^"Component3D".Length]
                : componentType.Name
        };
        ByName[typeName] = descriptor;
        ByName[componentType.Name] = descriptor;
        foreach (Godot.ScriptPathAttribute attribute in componentType.GetCustomAttributes<Godot.ScriptPathAttribute>(false))
        {
            if (!string.IsNullOrWhiteSpace(attribute.Path))
                ByScriptPath[attribute.Path] = descriptor;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        foreach (PropertyInfo property in componentType.GetProperties(flags))
        {
            GraphValueAttribute attribute = property.GetCustomAttribute<GraphValueAttribute>();
            if (attribute == null || !IsSupportedType(property.PropertyType))
                continue;

            descriptor.Values.Add(new GraphComponentValueDescriptor
            {
                ComponentTypeName = typeName,
                MemberId = attribute.Id,
                DisplayName = string.IsNullOrWhiteSpace(attribute.DisplayName) ? property.Name : attribute.DisplayName,
                Description = attribute.Description,
                ValueType = property.PropertyType,
                Access = attribute.Access,
                Property = property
            });
        }

        foreach (MethodInfo method in componentType.GetMethods(flags))
        {
            GraphValueAttribute valueAttribute = method.GetCustomAttribute<GraphValueAttribute>();
            if (valueAttribute != null)
            {
                BuildMethodValueDescriptor(descriptor, method, valueAttribute);
                continue;
            }

            GraphActionAttribute actionAttribute = method.GetCustomAttribute<GraphActionAttribute>();
            if (actionAttribute == null)
                continue;

            var action = new GraphComponentActionDescriptor
            {
                ComponentTypeName = typeName,
                MemberId = actionAttribute.Id,
                DisplayName = string.IsNullOrWhiteSpace(actionAttribute.DisplayName) ? method.Name : actionAttribute.DisplayName,
                Description = actionAttribute.Description,
                Method = method,
                ReturnType = method.ReturnType,
                HasSupportedReturnType = method.ReturnType == typeof(void) || method.ReturnType == typeof(bool) || method.ReturnType == typeof(GraphActionStatus),
                UseInputEventValue = actionAttribute.UseInputEventValue
            };
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                action.Parameters.Add(new GraphComponentParameterDescriptor
                {
                    Name = parameter.Name ?? $"arg{parameter.Position}",
                    ValueType = parameter.ParameterType,
                    IsSupported = IsSupportedType(parameter.ParameterType),
                    IsOptional = parameter.IsOptional,
                    DefaultValue = parameter.DefaultValue
                });
            }

            if (action.Parameters.All(parameter => parameter.IsSupported) && action.HasSupportedReturnType)
                descriptor.Actions.Add(action);
        }

        return descriptor;
    }

    public static bool TryGet(Type componentType, out GraphComponentTypeDescriptor descriptor)
    {
        EnsureScanned();
        descriptor = null;
        if (componentType == null)
            return false;
        return ByName.TryGetValue(componentType.FullName ?? componentType.Name, out descriptor) ||
               ByName.TryGetValue(componentType.Name, out descriptor);
    }

    public static bool TryGet(string typeName, out GraphComponentTypeDescriptor descriptor)
    {
        EnsureScanned();
        return ByName.TryGetValue(typeName ?? string.Empty, out descriptor);
    }

    public static bool TryGetByScriptPath(string scriptPath, out GraphComponentTypeDescriptor descriptor)
    {
        EnsureScanned();
        return ByScriptPath.TryGetValue(scriptPath ?? string.Empty, out descriptor);
    }

    public static IReadOnlyList<GraphComponentTypeDescriptor> GetAll()
    {
        EnsureScanned();
        return ByName.Values.Distinct().OrderBy(value => value.DisplayName).ToList();
    }

    public static bool IsSupportedType(Type type)
    {
        if (type == null)
            return false;
        return type == typeof(bool) || type == typeof(int) || type == typeof(float) ||
               type == typeof(double) || type == typeof(string) || type == typeof(Godot.Vector2) ||
               type == typeof(Godot.Vector3) || type == typeof(Godot.Color) || type == typeof(Godot.NodePath) ||
               type.IsEnum || typeof(Godot.Resource).IsAssignableFrom(type);
    }

    private static void BuildMethodValueDescriptor(GraphComponentTypeDescriptor type, MethodInfo method, GraphValueAttribute attribute)
    {
        ParameterInfo[] parameters = method.GetParameters();
        if (method.ReturnType != typeof(void) && parameters.Length == 0 && IsSupportedType(method.ReturnType))
        {
            type.Values.Add(new GraphComponentValueDescriptor
            {
                ComponentTypeName = type.TypeName,
                MemberId = attribute.Id,
                DisplayName = string.IsNullOrWhiteSpace(attribute.DisplayName) ? method.Name : attribute.DisplayName,
                Description = attribute.Description,
                ValueType = method.ReturnType,
                Access = attribute.Access,
                Getter = method
            });
        }
        else if (method.ReturnType == typeof(void) && parameters.Length == 1 && IsSupportedType(parameters[0].ParameterType))
        {
            type.Values.Add(new GraphComponentValueDescriptor
            {
                ComponentTypeName = type.TypeName,
                MemberId = attribute.Id,
                DisplayName = string.IsNullOrWhiteSpace(attribute.DisplayName) ? method.Name : attribute.DisplayName,
                Description = attribute.Description,
                ValueType = parameters[0].ParameterType,
                Access = attribute.Access,
                Setter = method
            });
        }
    }
}
