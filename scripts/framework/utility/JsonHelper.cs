using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Godot;

namespace Framework
{
    /// <summary>
    /// Shared JSON helper with support for polymorphic objects, lists and Godot resources.
    /// </summary>
    public static class JsonHelper
    {
        private static readonly JsonSerializerOptions _opts = new JsonSerializerOptions
        {
            WriteIndented = false,
            IncludeFields = false,
        };

        private static readonly Dictionary<string, Type> _typeCache = new();

        public static string Serialize(object obj)
        {
            if (obj == null)
                return "null";

            return ValueToJsonNode(obj)?.ToJsonString(_opts) ?? "null";
        }

        public static string SerializeList<T>(IList<T> list)
        {
            if (list == null)
                return "null";

            var array = new JsonArray();
            foreach (var item in list)
                array.Add(item == null ? null : ValueToJsonNode(item));

            return array.ToJsonString(_opts);
        }

        public static T Deserialize<T>(string json)
        {
            var value = Deserialize(json, typeof(T));
            return value == null ? default : (T)value;
        }

        public static List<T> DeserializeList<T>(string json)
        {
            var result = new List<T>();
            if (string.IsNullOrWhiteSpace(json) || json == "null" || json == "[]")
                return result;

            var array = JsonNode.Parse(json)?.AsArray();
            if (array == null)
                return result;

            foreach (var node in array)
            {
                var value = JsonNodeToValue(node, typeof(T));
                if (value is T typed)
                    result.Add(typed);
                else if (value == null && CanAssignNull(typeof(T)))
                    result.Add(default);
            }

            return result;
        }

        public static object Deserialize(string json, Type targetType)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return null;

            var node = JsonNode.Parse(json);
            return JsonNodeToValue(node, targetType);
        }

        private static JsonNode ObjectToJsonNode(object obj)
        {
            var type = obj.GetType();
            var jsonObject = new JsonObject
            {
                ["$type"] = JsonValue.Create(type.Name),
            };

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (var prop in type.GetProperties(flags))
            {
                if (prop.GetIndexParameters().Length > 0)
                    continue;
                if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                    continue;

                var include = prop.GetCustomAttribute<JsonIncludeAttribute>();
                bool shouldSerialize = include != null || (prop.GetMethod?.IsPublic == true && prop.CanRead);

                if (!shouldSerialize || !prop.CanRead)
                    continue;

                jsonObject[prop.Name] = ValueToJsonNode(prop.GetValue(obj));
            }

            foreach (var field in type.GetFields(flags))
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

            var type = value.GetType();

            if (type == typeof(bool)) return JsonValue.Create((bool)value);
            if (type == typeof(int)) return JsonValue.Create((int)value);
            if (type == typeof(long)) return JsonValue.Create((long)value);
            if (type == typeof(float)) return JsonValue.Create((float)value);
            if (type == typeof(double)) return JsonValue.Create((double)value);
            if (type == typeof(string)) return JsonValue.Create((string)value);
            if (type == typeof(Vector2)) return Vector2ToJsonNode((Vector2)value);
            if (type == typeof(Vector3)) return Vector3ToJsonNode((Vector3)value);
            if (type.IsEnum) return JsonValue.Create(value.ToString());

            if (value is Resource resource)
            {
                var path = resource.ResourcePath;
                if (!string.IsNullOrEmpty(path))
                    return new JsonObject { ["$res"] = path };

                return null;
            }

            if (value is Dictionary<string, string> stringDictionary)
            {
                var obj = new JsonObject();
                foreach (var kvp in stringDictionary)
                    obj[kvp.Key] = JsonValue.Create(kvp.Value);
                return obj;
            }

            if (value is System.Collections.IList list)
            {
                var array = new JsonArray();
                foreach (var item in list)
                    array.Add(item == null ? null : ValueToJsonNode(item));
                return array;
            }

            return ObjectToJsonNode(value);
        }

        private static Resource LoadResource(string path)
        {
            var resModule = ModuleSystem.GetModule<IResourceModule>();
            if (resModule != null)
            {
                var handle = resModule.LoadAsset<Resource>(path);
                var asset = handle.Asset;
                handle.Release();
                return asset;
            }

            return ResourceLoader.Load(path);
        }

        private static object JsonNodeToObject(JsonNode node, Type targetType)
        {
            if (node == null)
                return null;

            if (targetType == typeof(Vector2) || targetType == typeof(Vector3))
                return JsonNodeToValue(node, targetType);

            if (node is JsonArray)
            {
                bool canReadArray = targetType.IsArray
                    || (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>));
                return canReadArray ? JsonNodeToValue(node, targetType) : null;
            }

            if (node is JsonValue jsonValue)
                return ConvertJsonValue(jsonValue, targetType);

            if (node is not JsonObject jsonObject)
                return null;

            if (jsonObject.ContainsKey("$res") && typeof(Resource).IsAssignableFrom(targetType))
            {
                var resPath = jsonObject["$res"]?.GetValue<string>();
                return string.IsNullOrEmpty(resPath) ? null : LoadResource(resPath);
            }

            if (targetType == typeof(Dictionary<string, string>))
            {
                var dict = new Dictionary<string, string>();
                foreach (var kvp in jsonObject)
                {
                    if (kvp.Key == "$type")
                        continue;

                    dict[kvp.Key] = kvp.Value?.GetValue<string>() ?? string.Empty;
                }

                return dict;
            }

            var concreteType = ResolveConcreteType(targetType, jsonObject);
            if (concreteType == null || concreteType.IsAbstract || concreteType.IsInterface)
                return null;

            var instance = Activator.CreateInstance(concreteType);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (var prop in concreteType.GetProperties(flags))
            {
                if (prop.GetIndexParameters().Length > 0)
                    continue;
                if (prop.Name == "$type")
                    continue;
                if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                    continue;

                var include = prop.GetCustomAttribute<JsonIncludeAttribute>();
                bool shouldDeserialize = include != null || (prop.SetMethod?.IsPublic == true && prop.CanWrite);

                if (!shouldDeserialize || !prop.CanWrite)
                    continue;
                if (!jsonObject.TryGetPropertyValue(prop.Name, out var propNode))
                    continue;

                var converted = JsonNodeToValue(propNode, prop.PropertyType);
                if (converted != null || CanAssignNull(prop.PropertyType))
                    prop.SetValue(instance, converted);
            }

            foreach (var field in concreteType.GetFields(flags))
            {
                if (field.Name == "$type")
                    continue;
                if (field.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                    continue;
                if (field.GetCustomAttribute<JsonIncludeAttribute>() == null)
                    continue;
                if (!jsonObject.TryGetPropertyValue(field.Name, out var fieldNode))
                    continue;

                var converted = JsonNodeToValue(fieldNode, field.FieldType);
                if (converted != null || CanAssignNull(field.FieldType))
                    field.SetValue(instance, converted);
            }

            return instance;
        }

        private static object JsonNodeToValue(JsonNode node, Type targetType)
        {
            if (node == null)
                return null;

            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
                return JsonNodeToValue(node, nullableType);

            if (targetType == typeof(bool)) return node.GetValue<bool>();
            if (targetType == typeof(int)) return node.GetValue<int>();
            if (targetType == typeof(long)) return node.GetValue<long>();
            if (targetType == typeof(float)) return (float)node.GetValue<double>();
            if (targetType == typeof(double)) return node.GetValue<double>();
            if (targetType == typeof(string)) return node.GetValue<string>();
            if (targetType == typeof(Vector2)) return JsonNodeToVector2(node);
            if (targetType == typeof(Vector3)) return JsonNodeToVector3(node);

            if (targetType.IsEnum)
                return Enum.Parse(targetType, node.GetValue<string>());

            if (targetType.IsArray && node is JsonArray jsonArray)
                return JsonArrayToArray(jsonArray, targetType.GetElementType());

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
                return JsonArrayToList(node as JsonArray, targetType);

            if (targetType == typeof(Dictionary<string, string>) && node is JsonObject dictObject)
            {
                var dict = new Dictionary<string, string>();
                foreach (var kvp in dictObject)
                    dict[kvp.Key] = kvp.Value?.GetValue<string>() ?? string.Empty;
                return dict;
            }

            if (node is JsonObject || node is JsonValue)
                return JsonNodeToObject(node, targetType);

            return null;
        }

        private static object ConvertJsonValue(JsonValue value, Type targetType)
        {
            try
            {
                var nullableType = Nullable.GetUnderlyingType(targetType);
                if (nullableType != null)
                    return ConvertJsonValue(value, nullableType);

                if (targetType == typeof(bool)) return value.GetValue<bool>();
                if (targetType == typeof(int)) return value.GetValue<int>();
                if (targetType == typeof(long)) return value.GetValue<long>();
                if (targetType == typeof(float)) return (float)value.GetValue<double>();
                if (targetType == typeof(double)) return value.GetValue<double>();
                if (targetType == typeof(string)) return value.GetValue<string>();
                if (targetType.IsEnum) return Enum.Parse(targetType, value.GetValue<string>());
            }
            catch
            {
            }

            return null;
        }

        private static System.Collections.IList JsonArrayToList(JsonArray array, Type listType)
        {
            if (array == null)
                return (System.Collections.IList)Activator.CreateInstance(listType);

            var elementType = listType.GetGenericArguments()[0];
            var list = (System.Collections.IList)Activator.CreateInstance(listType);

            foreach (var item in array)
            {
                var converted = JsonNodeToValue(item, elementType);
                if (converted != null || CanAssignNull(elementType))
                    list.Add(converted);
            }

            return list;
        }

        private static Array JsonArrayToArray(JsonArray array, Type elementType)
        {
            if (array == null)
                return Array.CreateInstance(elementType, 0);

            var result = Array.CreateInstance(elementType, array.Count);
            for (int i = 0; i < array.Count; i++)
            {
                var converted = JsonNodeToValue(array[i], elementType);
                result.SetValue(converted ?? GetDefaultValue(elementType), i);
            }

            return result;
        }

        private static Type ResolveConcreteType(Type targetType, JsonObject jsonObject)
        {
            if (!jsonObject.TryGetPropertyValue("$type", out var typeNode))
                return targetType;

            var typeName = typeNode?.GetValue<string>();
            if (string.IsNullOrEmpty(typeName))
                return targetType;

            return FindType(typeName) ?? targetType;
        }

        private static JsonObject Vector2ToJsonNode(Vector2 value)
        {
            return new JsonObject
            {
                ["$type"] = nameof(Vector2),
                ["X"] = JsonValue.Create(value.X),
                ["Y"] = JsonValue.Create(value.Y),
            };
        }

        private static JsonObject Vector3ToJsonNode(Vector3 value)
        {
            return new JsonObject
            {
                ["$type"] = nameof(Vector3),
                ["X"] = JsonValue.Create(value.X),
                ["Y"] = JsonValue.Create(value.Y),
                ["Z"] = JsonValue.Create(value.Z),
            };
        }

        private static Vector2 JsonNodeToVector2(JsonNode node)
        {
            if (node is not JsonObject jsonObject)
                return Vector2.Zero;

            return new Vector2(
                ReadSingle(jsonObject, "X", "x"),
                ReadSingle(jsonObject, "Y", "y"));
        }

        private static Vector3 JsonNodeToVector3(JsonNode node)
        {
            if (node is not JsonObject jsonObject)
                return Vector3.Zero;

            return new Vector3(
                ReadSingle(jsonObject, "X", "x"),
                ReadSingle(jsonObject, "Y", "y"),
                ReadSingle(jsonObject, "Z", "z"));
        }

        private static float ReadSingle(JsonObject jsonObject, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (!jsonObject.TryGetPropertyValue(propertyName, out var valueNode) || valueNode == null)
                    continue;

                if (valueNode is not JsonValue jsonValue)
                    continue;

                try
                {
                    return (float)jsonValue.GetValue<double>();
                }
                catch
                {
                }
            }

            return 0f;
        }

        private static bool CanAssignNull(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        private static object GetDefaultValue(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

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
                        if (type.Name != typeName)
                            continue;

                        _typeCache[typeName] = type;
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
}
