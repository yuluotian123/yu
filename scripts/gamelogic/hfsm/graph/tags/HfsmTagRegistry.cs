using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic
{
    [Tool]
    [GlobalClass]
    public partial class HfsmTagRegistry : Resource
    {
        public const string DefaultResourcePath = "res://assets/config/hfsm_tag_registry.tres";

        [Export] public Godot.Collections.Array<HfsmTagDefinition> Tags { get; set; } = new();

        public static HfsmTagRegistry LoadDefault()
        {
            if (!ResourceLoader.Exists(DefaultResourcePath))
                return null;

            return ResourceLoader.Load<HfsmTagRegistry>(DefaultResourcePath);
        }

        public HfsmTagDefinition FindTag(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            return GetTags().FirstOrDefault(tag => string.Equals(tag.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public List<string> GetLayerNames()
        {
            return GetTags()
                .Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Layer))
                .Select(tag => tag.Layer.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(layer => layer, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public List<HfsmTagDefinition> GetLayerTags(string layer)
        {
            if (string.IsNullOrWhiteSpace(layer))
                return new List<HfsmTagDefinition>();

            return GetTags()
                .Where(tag => tag != null && string.Equals(tag.Layer, layer, StringComparison.OrdinalIgnoreCase))
                .OrderBy(tag => tag.DisplayOrder)
                .ThenBy(tag => tag.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public List<HfsmTagDefinition> GetPlainTags()
        {
            return GetTags()
                .Where(tag => tag != null && string.IsNullOrWhiteSpace(tag.Layer))
                .OrderBy(tag => tag.DisplayOrder)
                .ThenBy(tag => tag.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public string NormalizeTags(string tags)
        {
            return HfsmTagUtility.FormatTags(NormalizeTagList(HfsmTagUtility.ParseTags(tags)));
        }

        public List<string> NormalizeTagList(IEnumerable<string> tags)
        {
            var requested = HfsmTagUtility.DistinctTags(tags);
            var selectedLayerTags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var selectedPlainTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unknownTags = new List<string>();

            foreach (string key in requested)
            {
                HfsmTagDefinition definition = FindTag(key);
                if (definition == null)
                {
                    unknownTags.Add(key);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.Layer))
                {
                    selectedPlainTags.Add(definition.Key);
                    continue;
                }

                string layer = definition.Layer.Trim();
                if (!selectedLayerTags.ContainsKey(layer))
                    selectedLayerTags[layer] = definition.Key;
            }

            var normalized = new List<string>();
            foreach (HfsmTagDefinition definition in GetTags())
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Key))
                    continue;

                if (!string.IsNullOrWhiteSpace(definition.Layer))
                {
                    string layer = definition.Layer.Trim();
                    if (selectedLayerTags.TryGetValue(layer, out string selectedKey) &&
                        string.Equals(selectedKey, definition.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        normalized.Add(definition.Key);
                    }

                    continue;
                }

                if (selectedPlainTags.Contains(definition.Key))
                    normalized.Add(definition.Key);
            }

            foreach (string unknownTag in unknownTags)
            {
                if (!HfsmTagUtility.ContainsTag(normalized, unknownTag))
                    normalized.Add(unknownTag);
            }

            return normalized;
        }

        private IEnumerable<HfsmTagDefinition> GetTags()
        {
            return Tags == null ? Array.Empty<HfsmTagDefinition>() : Tags;
        }
    }

    internal static class HfsmTagUtility
    {
        public static List<string> ParseTags(string tags)
        {
            if (string.IsNullOrWhiteSpace(tags))
                return new List<string>();

            return DistinctTags(tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        public static List<string> DistinctTags(IEnumerable<string> tags)
        {
            var result = new List<string>();
            if (tags == null)
                return result;

            foreach (string rawTag in tags)
            {
                string tag = rawTag?.Trim();
                if (string.IsNullOrWhiteSpace(tag) || ContainsTag(result, tag))
                    continue;

                result.Add(tag);
            }

            return result;
        }

        public static string FormatTags(IEnumerable<string> tags)
        {
            return string.Join(",", DistinctTags(tags));
        }

        public static bool ContainsTag(IEnumerable<string> tags, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || tags == null)
                return false;

            foreach (string existingTag in tags)
            {
                if (string.Equals(existingTag, tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool ContainsTag(string tags, string tag)
        {
            return ContainsTag(ParseTags(tags), tag);
        }
    }
}
