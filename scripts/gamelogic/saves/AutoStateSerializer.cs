using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Framework;

namespace GameLogic
{
    internal static class AutoStateSerializer
    {
        private readonly struct MetadataKey : IEquatable<MetadataKey>
        {
            public MetadataKey(Type type, bool includePublicProperties)
            {
                Type = type;
                IncludePublicProperties = includePublicProperties;
            }

            public Type Type { get; }

            public bool IncludePublicProperties { get; }

            public bool Equals(MetadataKey other)
            {
                return Type == other.Type && IncludePublicProperties == other.IncludePublicProperties;
            }

            public override bool Equals(object obj)
            {
                return obj is MetadataKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Type, IncludePublicProperties);
            }
        }

        private sealed class StatePropertyMetadata
        {
            public PropertyInfo Property { get; init; }

            public bool CanSerialize { get; init; }

            public bool CanDeserialize { get; init; }
        }

        private sealed class StateMetadata
        {
            public IReadOnlyList<StatePropertyMetadata> Properties { get; init; }

            public IReadOnlyList<FieldInfo> Fields { get; init; }
        }

        private static readonly Dictionary<MetadataKey, StateMetadata> _metadataCache = new();

        public static JsonObject SerializeObject(object target, bool includePublicProperties = true)
        {
            if (target == null)
                return new JsonObject();

            var metadata = GetMetadata(target.GetType(), includePublicProperties);
            var result = new JsonObject();

            for (int i = 0; i < metadata.Properties.Count; i++)
            {
                var property = metadata.Properties[i];
                if (!property.CanSerialize)
                    continue;

                var value = property.Property.GetValue(target);
                result[property.Property.Name] = JsonNode.Parse(JsonHelper.Serialize(value));
            }

            for (int i = 0; i < metadata.Fields.Count; i++)
            {
                var field = metadata.Fields[i];
                var value = field.GetValue(target);
                result[field.Name] = JsonNode.Parse(JsonHelper.Serialize(value));
            }

            return result;
        }

        public static void DeserializeInto(object target, JsonObject data, bool includePublicProperties = true)
        {
            if (target == null || data == null)
                return;

            var metadata = GetMetadata(target.GetType(), includePublicProperties);

            for (int i = 0; i < metadata.Properties.Count; i++)
            {
                var property = metadata.Properties[i];
                if (!property.CanDeserialize)
                    continue;

                if (!data.TryGetPropertyValue(property.Property.Name, out var node))
                    continue;

                var value = JsonHelper.Deserialize(node?.ToJsonString(), property.Property.PropertyType);
                if (value != null)
                    property.Property.SetValue(target, value);
            }

            for (int i = 0; i < metadata.Fields.Count; i++)
            {
                var field = metadata.Fields[i];
                if (!data.TryGetPropertyValue(field.Name, out var node))
                    continue;

                var value = JsonHelper.Deserialize(node?.ToJsonString(), field.FieldType);
                if (value != null)
                    field.SetValue(target, value);
            }
        }

        private static StateMetadata GetMetadata(Type type, bool includePublicProperties)
        {
            var key = new MetadataKey(type, includePublicProperties);
            if (_metadataCache.TryGetValue(key, out var cached))
                return cached;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            var properties = new List<StatePropertyMetadata>();
            foreach (var property in type.GetProperties(flags))
            {
                if (property.GetIndexParameters().Length > 0)
                    continue;

                if (property.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                    continue;

                var include = property.GetCustomAttribute<JsonIncludeAttribute>() != null;
                var isPublicProperty = includePublicProperties && property.GetMethod?.IsPublic == true;
                var canSerialize = property.CanRead && (include || isPublicProperty);
                var canDeserialize = property.CanWrite && (include || isPublicProperty);

                if (!canSerialize && !canDeserialize)
                    continue;

                properties.Add(new StatePropertyMetadata
                {
                    Property = property,
                    CanSerialize = canSerialize,
                    CanDeserialize = canDeserialize,
                });
            }

            var fields = new List<FieldInfo>();
            foreach (var field in type.GetFields(flags))
            {
                if (field.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                    continue;

                if (field.GetCustomAttribute<JsonIncludeAttribute>() == null)
                    continue;

                fields.Add(field);
            }

            var metadata = new StateMetadata
            {
                Properties = properties,
                Fields = fields,
            };

            _metadataCache[key] = metadata;
            return metadata;
        }
    }
}
