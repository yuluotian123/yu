using System;
using System.Collections.Generic;
using Godot;

namespace GameLogic
{
    public interface IHfsmStateNodeData
    {
        string Id { get; set; }
        string StateName { get; set; }
        bool IsDefault { get; set; }
        string Tags { get; set; }
        string BehaviourKey { get; set; }
        string MetadataJson { get; set; }

        bool HasTag(string tag);
        void OnEnter(HfsmRuntime runtime);
        void OnUpdate(HfsmRuntime runtime, double delta);
        void OnExit(HfsmRuntime runtime);
    }

    public class HfsmStateNodeData : GraphNodeData, IHfsmStateNodeData
    {
        public string StateName { get; set; } = "State";
        public bool IsDefault { get; set; }
        public string Tags { get; set; } = string.Empty;
        public string BehaviourKey { get; set; } = string.Empty;
        public string MetadataJson { get; set; } = "{}";

        public override List<string> GetGraphTypes() => new() { HfsmGraphAsset.GraphTypeName };
        public override string GetDisplayName() => string.IsNullOrWhiteSpace(StateName) ? "State" : StateName;
        public override Color GetNodeColor() => IsDefault ? new Color(0.3f, 0.75f, 0.45f) : new Color(0.35f, 0.55f, 0.9f);
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

        public virtual void OnEnter(HfsmRuntime runtime)
        {
            if (runtime != null)
                Execute(runtime.Context);
        }

        public virtual void OnUpdate(HfsmRuntime runtime, double delta)
        {
        }

        public virtual void OnExit(HfsmRuntime runtime)
        {
        }

        public override void CreateUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(190f, 0f) };

            AddStateFields(root);
            AddExtraFields(root);

            context.GraphNode.AddChild(root);
        }

        protected virtual void AddExtraFields(VBoxContainer root)
        {
        }

        protected void AddStateFields(VBoxContainer root)
        {
            HfsmStateNodeUi.AddStateFields(root, this);
        }
    }

    internal static class HfsmStateNodeUi
    {
        public static void AddStateFields(VBoxContainer root, IHfsmStateNodeData state)
        {
            var nameEdit = new LineEdit
            {
                PlaceholderText = "State name",
                Text = state.StateName
            };
            nameEdit.TextChanged += value => state.StateName = value;
            root.AddChild(nameEdit);

            var defaultCheck = new CheckBox
            {
                Text = "Default",
                ButtonPressed = state.IsDefault
            };
            defaultCheck.Toggled += value => state.IsDefault = value;
            root.AddChild(defaultCheck);

            var behaviourEdit = new LineEdit
            {
                PlaceholderText = "Behaviour key",
                Text = state.BehaviourKey
            };
            behaviourEdit.TextChanged += value => state.BehaviourKey = value;
            root.AddChild(behaviourEdit);

            var tagsEdit = new LineEdit
            {
                PlaceholderText = "Tags, comma separated",
                Text = state.Tags
            };
            tagsEdit.TextChanged += value => state.Tags = value;
            root.AddChild(tagsEdit);
        }
    }
}
