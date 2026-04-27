using System;
using System.Collections.Generic;
using Godot;

namespace Framework
{
    public partial class GraphFsmStateNodeData : global::GraphNodeData
    {
        public string StateName { get; set; } = "State";
        public bool IsDefault { get; set; }
        public string Tags { get; set; } = string.Empty;
        public string MetadataJson { get; set; } = "{}";

        public override List<string> GetGraphTypes() => new() { GraphFsmGraphAsset.GraphTypeName };
        public override string GetDisplayName() => string.IsNullOrWhiteSpace(StateName) ? "State" : StateName;
        public override Color GetNodeColor() => IsDefault ? new Color(0.35f, 0.8f, 0.45f) : new Color(0.35f, 0.55f, 0.9f);
        public override int GetInputCount() => 1;
        public override int GetOutputCount() => 1;
        public override int GetInputMaxConnections(int port) => -1;
        public override int GetOutputMaxConnections(int port) => -1;

        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(Tags))
                return false;

            foreach (string rawTag in Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.Equals(rawTag, tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public override void CreateUI(GraphNode node)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(180f, 0f) };

            var nameEdit = new LineEdit
            {
                PlaceholderText = "State name",
                Text = StateName
            };
            nameEdit.TextChanged += value => StateName = value;
            root.AddChild(nameEdit);

            var defaultCheck = new CheckBox
            {
                Text = "Default",
                ButtonPressed = IsDefault
            };
            defaultCheck.Toggled += value => IsDefault = value;
            root.AddChild(defaultCheck);

            var tagsEdit = new LineEdit
            {
                PlaceholderText = "Tags, comma separated",
                Text = Tags
            };
            tagsEdit.TextChanged += value => Tags = value;
            root.AddChild(tagsEdit);

            node.AddChild(root);
        }
    }
}
