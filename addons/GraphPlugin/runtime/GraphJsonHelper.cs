using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

/// <summary>
/// GraphPlugin JSON 序列化辅助类。
/// 支持多态：序列化时写入 "$type" 字段（类名），反序列化时据此还原具体子类实例。
/// </summary>
public static class GraphJsonHelper
{
    private static readonly JsonSerializerOptions _opts = new JsonSerializerOptions
    {
        WriteIndented = false,
        IncludeFields = false,
    };

    // ── 序列化 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 将对象序列化为 JSON 字符串，自动写入 "$type" 字段（具体类名）。
    /// </summary>
    public static string Serialize(object obj)
    {
        if (obj == null) return "null";

        var node = ObjectToJsonNode(obj);
        return node.ToJsonString(_opts);
    }

    /// <summary>
    /// 将列表序列化为 JSON 字符串，每个元素都写入 "$type"。
    /// </summary>
    public static string SerializeList<T>(IList<T> list)
    {
        var array = new JsonArray();
        foreach (var item in list)
            array.Add(item == null ? null : ObjectToJsonNode(item));
        return array.ToJsonString(_opts);
    }

    // ── 反序列化 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 从 JSON 字符串还原对象（根据 "$type" 创建具体子类实例）。
    /// </summary>
    public static T Deserialize<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json) || json == "null") return null;
        var node = JsonNode.Parse(json);
        return (T)JsonNodeToObject(node, typeof(T));
    }

    /// <summary>
    /// 从 JSON 字符串还原列表（每个元素根据 "$type" 还原为具体子类）。
    /// </summary>
    public static List<T> DeserializeList<T>(string json) where T : class
    {
        var result = new List<T>();
        if (string.IsNullOrWhiteSpace(json) || json == "null" || json == "[]")
            return result;

        var array = JsonNode.Parse(json)?.AsArray();
        if (array == null) return result;

        foreach (var node in array)
        {
            if (node == null) continue;
            var obj = JsonNodeToObject(node, typeof(T));
            if (obj is T typed)
                result.Add(typed);
        }
        return result;
    }

    // ── 内部实现 ──────────────────────────────────────────────────────────────

    private static JsonNode ObjectToJsonNode(object obj)
    {
        var type = obj.GetType();
        var jObj = new JsonObject();

        // 写入具体类型名
        jObj["$type"] = JsonValue.Create(type.Name);

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // 处理属性
        foreach (var prop in type.GetProperties(flags))
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;

            var include = prop.GetCustomAttribute<JsonIncludeAttribute>();
            bool shouldSerialize = include != null || (prop.GetMethod?.IsPublic == true && prop.CanRead);
            
            if (shouldSerialize && prop.CanRead)
            {
                var value = prop.GetValue(obj);
                jObj[prop.Name] = ValueToJsonNode(value);
            }
        }

        // 处理字段
        foreach (var field in type.GetFields(flags))
        {
            if (field.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
            if (field.GetCustomAttribute<JsonIncludeAttribute>() != null)
            {
                var value = field.GetValue(obj);
                jObj[field.Name] = ValueToJsonNode(value);
            }
        }

        return jObj;
    }

    private static JsonNode ValueToJsonNode(object value)
    {
        if (value == null) return null;

        var type = value.GetType();

        // 基本类型
        if (type == typeof(bool))   return JsonValue.Create((bool)value);
        if (type == typeof(int))    return JsonValue.Create((int)value);
        if (type == typeof(float))  return JsonValue.Create((float)value);
        if (type == typeof(double)) return JsonValue.Create((double)value);
        if (type == typeof(string)) return JsonValue.Create((string)value);
        if (type.IsEnum)            return JsonValue.Create(value.ToString());

        // Godot.Vector2
        if (type == typeof(Godot.Vector2))
        {
            var v = (Godot.Vector2)value;
            return new JsonObject { ["x"] = v.X, ["y"] = v.Y };
        }

        // Godot.Color
        if (type == typeof(Godot.Color))
        {
            var c = (Godot.Color)value;
            return new JsonObject { ["r"] = c.R, ["g"] = c.G, ["b"] = c.B, ["a"] = c.A };
        }

        // 列表 / 数组
        if (value is System.Collections.IList list)
        {
            var arr = new JsonArray();
            foreach (var item in list)
                arr.Add(item == null ? null : ValueToJsonNode(item));
            return arr;
        }

        // 嵌套对象（递归，带 $type）
        return ObjectToJsonNode(value);
    }

    private static object JsonNodeToObject(JsonNode node, Type targetType)
    {
        if (node == null) return null;

        // 如果是 JsonObject，尝试根据 "$type" 确定具体类型
        if (node is JsonObject jObj)
        {
            Type concreteType = targetType;

            if (jObj.TryGetPropertyValue("$type", out var typeNode))
            {
                var typeName = typeNode?.GetValue<string>();
                if (!string.IsNullOrEmpty(typeName))
                {
                    var found = FindType(typeName);
                    if (found != null) concreteType = found;
                }
            }

            // 不能实例化抽象/接口类型
            if (concreteType.IsAbstract || concreteType.IsInterface)
                return null;

            var instance = Activator.CreateInstance(concreteType);

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            // 处理属性
            foreach (var prop in concreteType.GetProperties(flags))
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                if (prop.Name == "$type") continue;
                if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;

                var include = prop.GetCustomAttribute<JsonIncludeAttribute>();
                bool shouldDeserialize = include != null || (prop.GetMethod?.IsPublic == true && prop.CanWrite);

                if (shouldDeserialize && prop.CanWrite && jObj.TryGetPropertyValue(prop.Name, out var propNode))
                {
                    var converted = JsonNodeToValue(propNode, prop.PropertyType);
                    if (converted != null)
                        prop.SetValue(instance, converted);
                }
            }

            // 处理字段
            foreach (var field in concreteType.GetFields(flags))
            {
                if (field.Name == "$type") continue;
                if (field.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;

                if (field.GetCustomAttribute<JsonIncludeAttribute>() != null && jObj.TryGetPropertyValue(field.Name, out var fieldNode))
                {
                    var converted = JsonNodeToValue(fieldNode, field.FieldType);
                    if (converted != null)
                        field.SetValue(instance, converted);
                }
            }

            return instance;
        }

        // 基本值节点
        if (node is JsonValue jVal)
            return ConvertJsonValue(jVal, targetType);

        return null;
    }

    private static object JsonNodeToValue(JsonNode node, Type targetType)
    {
        if (node == null) return null;

        if (targetType == typeof(bool))   return node.GetValue<bool>();
        if (targetType == typeof(int))    return node.GetValue<int>();
        if (targetType == typeof(float))  return (float)node.GetValue<double>();
        if (targetType == typeof(double)) return node.GetValue<double>();
        if (targetType == typeof(string)) return node.GetValue<string>();

        if (targetType.IsEnum)
        {
            var s = node.GetValue<string>();
            return Enum.Parse(targetType, s);
        }

        if (targetType == typeof(Godot.Vector2) && node is JsonObject v2)
            return new Godot.Vector2((float)v2["x"].GetValue<double>(), (float)v2["y"].GetValue<double>());

        if (targetType == typeof(Godot.Color) && node is JsonObject col)
            return new Godot.Color(
                (float)col["r"].GetValue<double>(),
                (float)col["g"].GetValue<double>(),
                (float)col["b"].GetValue<double>(),
                (float)col["a"].GetValue<double>());

        // 泛型列表 List<T>
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elemType = targetType.GetGenericArguments()[0];
            var listInstance = (System.Collections.IList)Activator.CreateInstance(targetType);
            if (node is JsonArray arr)
            {
                foreach (var item in arr)
                    listInstance.Add(JsonNodeToObject(item, elemType));
            }
            return listInstance;
        }

        // 嵌套对象
        if (node is JsonObject)
            return JsonNodeToObject(node, targetType);

        return null;
    }

    private static object ConvertJsonValue(JsonValue jVal, Type targetType)
    {
        try
        {
            if (targetType == typeof(bool))   return jVal.GetValue<bool>();
            if (targetType == typeof(int))    return jVal.GetValue<int>();
            if (targetType == typeof(float))  return (float)jVal.GetValue<double>();
            if (targetType == typeof(double)) return jVal.GetValue<double>();
            if (targetType == typeof(string)) return jVal.GetValue<string>();
            if (targetType.IsEnum)            return Enum.Parse(targetType, jVal.GetValue<string>());
        }
        catch { }
        return null;
    }

    // ── 类型查找缓存 ──────────────────────────────────────────────────────────

    private static readonly Dictionary<string, Type> _typeCache = new();

    private static Type FindType(string typeName)
    {
        if (_typeCache.TryGetValue(typeName, out var cached))
            return cached;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name == typeName)
                    {
                        _typeCache[typeName] = type;
                        return type;
                    }
                }
            }
            catch (ReflectionTypeLoadException) { }
        }

        return null;
    }
}
