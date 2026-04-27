using System.Collections.Generic;
using System;
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
        IReadOnlyList<string> GetTags();
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
            return HfsmTagUtility.ContainsTag(Tags, tag);
        }

        public IReadOnlyList<string> GetTags()
        {
            return HfsmTagUtility.ParseTags(Tags);
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

            AddStateFields(root, context);
            AddExtraFields(root);

            context.GraphNode.AddChild(root);
        }

        protected virtual void AddExtraFields(VBoxContainer root)
        {
        }

        protected void AddStateFields(VBoxContainer root, GraphEditorContext context)
        {
            HfsmStateNodeUi.AddStateFields(root, this, context);
        }
    }

    internal static class HfsmStateNodeUi
    {
        public static void AddStateFields(VBoxContainer root, IHfsmStateNodeData state, GraphEditorContext context)
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

            AddTagFields(root, state);
        }

        private static void AddTagFields(VBoxContainer root, IHfsmStateNodeData state)
        {
            HfsmTagRegistry registry = HfsmTagRegistry.LoadDefault();
            if (registry != null)
                state.Tags = registry.NormalizeTags(state.Tags);

            var tagRoot = new VBoxContainer();
            tagRoot.AddThemeConstantOverride("separation", 4);

            tagRoot.AddChild(new Label { Text = "Tags" });

            var selectedTags = new HashSet<string>(HfsmTagUtility.ParseTags(state.Tags), StringComparer.OrdinalIgnoreCase);
            var rawEdit = new LineEdit
            {
                PlaceholderText = "Raw tags fallback",
                Text = state.Tags,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };

            bool suppressRawChange = false;

            void WriteSelectedTags()
            {
                IEnumerable<string> normalized = registry != null
                    ? registry.NormalizeTagList(selectedTags)
                    : HfsmTagUtility.DistinctTags(selectedTags);

                state.Tags = HfsmTagUtility.FormatTags(normalized);
                suppressRawChange = true;
                rawEdit.Text = state.Tags;
                suppressRawChange = false;
            }

            if (registry == null)
            {
                var warning = new Label
                {
                    Text = $"Global tag registry not found: {HfsmTagRegistry.DefaultResourcePath}",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                warning.AddThemeColorOverride("font_color", new Color(0.9f, 0.65f, 0.25f));
                tagRoot.AddChild(warning);
            }
            else
            {
                foreach (string layer in registry.GetLayerNames())
                {
                    List<HfsmTagDefinition> layerTags = registry.GetLayerTags(layer);
                    if (layerTags.Count == 0)
                        continue;

                    var row = new HBoxContainer();
                    row.AddChild(new Label
                    {
                        Text = layer,
                        CustomMinimumSize = new Vector2(78f, 0f),
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    var option = new OptionButton
                    {
                        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                    };

                    option.AddItem("None", 0);
                    int selectedIndex = 0;
                    for (int i = 0; i < layerTags.Count; i++)
                    {
                        HfsmTagDefinition tag = layerTags[i];
                        option.AddItem(tag.DisplayText, i + 1);
                        if (selectedTags.Contains(tag.Key))
                            selectedIndex = i + 1;
                    }

                    option.Selected = selectedIndex;
                    option.ItemSelected += index =>
                    {
                        foreach (HfsmTagDefinition tag in layerTags)
                            selectedTags.Remove(tag.Key);

                        int tagIndex = (int)index - 1;
                        if (tagIndex >= 0 && tagIndex < layerTags.Count)
                            selectedTags.Add(layerTags[tagIndex].Key);

                        WriteSelectedTags();
                    };

                    row.AddChild(option);
                    tagRoot.AddChild(row);
                }

                List<HfsmTagDefinition> plainTags = registry.GetPlainTags();
                if (plainTags.Count > 0)
                {
                    tagRoot.AddChild(new Label { Text = "Flags" });
                    foreach (HfsmTagDefinition tag in plainTags)
                    {
                        var check = new CheckBox
                        {
                            Text = tag.DisplayText,
                            ButtonPressed = selectedTags.Contains(tag.Key)
                        };

                        check.Toggled += value =>
                        {
                            if (value)
                                selectedTags.Add(tag.Key);
                            else
                                selectedTags.Remove(tag.Key);

                            WriteSelectedTags();
                        };

                        tagRoot.AddChild(check);
                    }
                }
            }

            rawEdit.TextChanged += value =>
            {
                if (suppressRawChange)
                    return;

                state.Tags = value;
                selectedTags = new HashSet<string>(HfsmTagUtility.ParseTags(value), StringComparer.OrdinalIgnoreCase);
            };
            tagRoot.AddChild(rawEdit);

            root.AddChild(tagRoot);
        }
    }
}
