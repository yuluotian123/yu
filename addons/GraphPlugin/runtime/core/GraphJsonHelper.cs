using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

/// <summary>
/// GraphPlugin 的轻量 JSON 序列化工具。
/// </summary>
/// <remarks>
/// <para>
/// Godot Resource 对纯 C# 多态对象支持有限，因此 GraphPlugin 在每个对象上写入
/// <c>$type</c> 字段保存真实类型。反序列化时会先通过 <see cref="GraphTypeRegistry"/>
/// 解析已注册类型，再回退到 AppDomain 反射查找。
/// </para>
/// <para>
/// 该工具只覆盖图数据需要的常见类型：基础类型、枚举、Vector2、Color、List 和普通对象。
/// 复杂结构应拆成显式类，避免使用 Dictionary 这类当前未支持的容器。
/// </para>
/// </remarks>
public static class GraphJsonHelper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        IncludeFields = false
    };

    private static readonly Dictionary<string, Type> TypeCache = new(StringComparer.Ordinal);

    /// <summary>
    /// 序列化一个对象，并写入多态类型信息。
    /// </summary>
    public static string Serialize(object obj)
    {
        if (obj == null)
            return "null";

        return ObjectToJsonNode(obj).ToJsonString(Options);
    }

    /// <summary>
    /// 序列化列表。列表内每个对象都会保留自己的 <c>$type</c>。
    /// </summary>
    public static string SerializeList<T>(IList<T> list)
    {
        var array = new JsonArray();
        if (list != null)
        {
            foreach (T item in list)
                array.Add(item == null ? null : ValueToJsonNode(item));
        }

        return array.ToJsonString(Options);
    }

    /// <summary>
    /// 反序列化一个对象。
    /// </summary>
    public static T Deserialize<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json) || json == "null")
            return null;

        JsonNode node = JsonNode.Parse(json);
        return JsonNodeToObject(node, typeof(T)) as T;
    }

    /// <summary>
    /// 反序列化对象列表。
    /// </summary>
    public static List<T> DeserializeList<T>(string json) where T : class
    {
        var result = new List<T>();
        if (string.IsNullOrWhiteSpace(json) || json == "null" || json == "[]")
            return result;

        if (JsonNode.Parse(json) is not JsonArray array)
            return result;

        foreach (JsonNode node in array)
        {
            if (node == null)
                continue;

            if (JsonNodeToObject(node, typeof(T)) is T typed)
                result.Add(typed);
        }

        return result;
    }

    private static JsonNode ObjectToJsonNode(object obj)
    {
        Type type = obj.GetType();
        var jsonObject = new JsonObject
        {
            ["$type"] = JsonValue.Create(type.Name)
        };

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            if (property.GetIndexParameters().Length > 0)
                continue;

            if (property.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                continue;

            bool include = property.GetCustomAttribute<JsonIncludeAttribute>() != null ||
                           property.GetMethod?.IsPublic == true;
            if (!include || !property.CanRead)
                continue;

            jsonObject[property.Name] = ValueToJsonNode(property.GetValue(obj));
        }

        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (field.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                continue;

            if (field.GetCustomAttribute<JsonIncludeAttribute>() == null)
                continue;

            jsonObject[field.Name] = ValueToJsonNode(field.GetValue(obj));
        }

        return jsonObject;
    }

    private static JsonNode ValueToJsonNode(object value)
    {
        if (value == null)
            return null;

        Type type = value.GetType();
        if (type == typeof(bool))
            return JsonValue.Create((bool)value);
        if (type == typeof(int))
            return JsonValue.Create((int)value);
        if (type == typeof(float))
            return JsonValue.Create((float)value);
        if (type == typeof(double))
            return JsonValue.Create((double)value);
        if (type == typeof(string))
            return JsonValue.Create((string)value);
        if (type.IsEnum)
            return JsonValue.Create(value.ToString());

        if (type == typeof(Godot.Vector2))
        {
            var vector = (Godot.Vector2)value;
            return new JsonObject { ["x"] = vector.X, ["y"] = vector.Y };
        }

        if (type == typeof(Godot.Color))
        {
            var color = (Godot.Color)value;
            return new JsonObject
            {
                ["r"] = color.R,
                ["g"] = color.G,
                ["b"] = color.B,
                ["a"] = color.A
            };
        }

        if (value is System.Collections.IList list)
        {
            var array = new JsonArray();
            foreach (object item in list)
                array.Add(item == null ? null : ValueToJsonNode(item));
            return array;
        }

        return ObjectToJsonNode(value);
    }

    private static object JsonNodeToObject(JsonNode node, Type targetType)
    {
        if (node == null)
            return null;

        if (node is JsonValue value)
            return ConvertJsonValue(value, targetType);

        if (node is not JsonObject jsonObject)
            return null;

        Type concreteType = ResolveConcreteType(jsonObject, targetType);
        if (concreteType == null || concreteType.IsAbstract || concreteType.IsInterface)
            return null;

        object instance = Activator.CreateInstance(concreteType);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        foreach (PropertyInfo property in concreteType.GetProperties(flags))
        {
            if (property.GetIndexParameters().Length > 0 ||
                property.GetCustomAttribute<JsonIgnoreAttribute>() != null ||
                !property.CanWrite)
            {
                continue;
            }

            bool include = property.GetCustomAttribute<JsonIncludeAttribute>() != null ||
                           property.GetMethod?.IsPublic == true;
            if (!include)
                continue;

            if (!jsonObject.TryGetPropertyValue(property.Name, out JsonNode propertyNode))
                continue;

            object converted = JsonNodeToValue(propertyNode, property.PropertyType);
            if (converted != null || !property.PropertyType.IsValueType)
                property.SetValue(instance, converted);
        }

        foreach (FieldInfo field in concreteType.GetFields(flags))
        {
            if (field.GetCustomAttribute<JsonIgnoreAttribute>() != null ||
                field.GetCustomAttribute<JsonIncludeAttribute>() == null)
            {
                continue;
            }

            if (!jsonObject.TryGetPropertyValue(field.Name, out JsonNode fieldNode))
                continue;

            object converted = JsonNodeToValue(fieldNode, field.FieldType);
            if (converted != null || !field.FieldType.IsValueType)
                field.SetValue(instance, converted);
        }

        return instance;
    }

    private static object JsonNodeToValue(JsonNode node, Type targetType)
    {
        if (node == null)
            return null;

        if (targetType == typeof(bool))
            return node.GetValue<bool>();
        if (targetType == typeof(int))
            return node.GetValue<int>();
        if (targetType == typeof(float))
            return (float)node.GetValue<double>();
        if (targetType == typeof(double))
            return node.GetValue<double>();
        if (targetType == typeof(string))
            return node.GetValue<string>();

        if (targetType.IsEnum)
            return Enum.Parse(targetType, node.GetValue<string>());

        if (targetType == typeof(Godot.Vector2) && node is JsonObject vector)
        {
            return new Godot.Vector2(
                (float)vector["x"].GetValue<double>(),
                (float)vector["y"].GetValue<double>());
        }

        if (targetType == typeof(Godot.Color) && node is JsonObject color)
        {
            return new Godot.Color(
                (float)color["r"].GetValue<double>(),
                (float)color["g"].GetValue<double>(),
                (float)color["b"].GetValue<double>(),
                (float)color["a"].GetValue<double>());
        }

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
        {
            Type elementType = targetType.GetGenericArguments()[0];
            var list = (System.Collections.IList)Activator.CreateInstance(targetType);
            if (node is JsonArray array)
            {
                foreach (JsonNode item in array)
                    list.Add(JsonNodeToValue(item, elementType));
            }

            return list;
        }

        if (node is JsonObject)
            return JsonNodeToObject(node, targetType);

        if (node is JsonValue jsonValue)
            return ConvertJsonValue(jsonValue, targetType);

        return null;
    }

    private static object ConvertJsonValue(JsonValue value, Type targetType)
    {
        try
        {
            if (targetType == typeof(bool))
                return value.GetValue<bool>();
            if (targetType == typeof(int))
                return value.GetValue<int>();
            if (targetType == typeof(float))
                return (float)value.GetValue<double>();
            if (targetType == typeof(double))
                return value.GetValue<double>();
            if (targetType == typeof(string))
                return value.GetValue<string>();
            if (targetType.IsEnum)
                return Enum.Parse(targetType, value.GetValue<string>());
        }
        catch
        {
        }

        return null;
    }

    private static Type ResolveConcreteType(JsonObject jsonObject, Type targetType)
    {
        if (!jsonObject.TryGetPropertyValue("$type", out JsonNode typeNode))
            return targetType;

        string typeName = typeNode?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(typeName))
            return targetType;

        Type found = FindType(typeName);
        return found ?? targetType;
    }

    private static Type FindType(string typeName)
    {
        if (GraphTypeRegistry.TryResolveType(typeName, out Type registeredType))
            return registeredType;

        if (TypeCache.TryGetValue(typeName, out Type cached))
            return cached;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.Name != typeName && type.FullName != typeName)
                        continue;

                    TypeCache[typeName] = type;
                    return type;
                }
            }
            catch (ReflectionTypeLoadException)
            {
            }
        }

        return null;
    }
}
