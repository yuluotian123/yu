using System.Collections.Generic;
using System;
using Godot;

namespace GameLogic
{
    public interface IHfsmStateNodeData : IStateNodeData
    {
        string BehaviourKey { get; set; }
        string MetadataJson { get; set; }

        void OnEnter(HfsmRuntime runtime);
        void OnUpdate(HfsmRuntime runtime, double delta);
        void OnExit(HfsmRuntime runtime);
    }

    public class HfsmStateNodeData : StateNodeData, IHfsmStateNodeData
    {
        public string BehaviourKey { get; set; } = string.Empty;
        public string MetadataJson { get; set; } = "{}";

        public override List<string> GetGraphTypes() => new() { HfsmGraphAsset.GraphTypeName };
        public override string GetMenuName() => "HFSM State";
        public override string GetDisplayName() => string.IsNullOrWhiteSpace(StateName) ? "State" : StateName;
        public override Color GetNodeColor() => IsDefault ? new Color(0.3f, 0.75f, 0.45f) : new Color(0.35f, 0.55f, 0.9f);
        public override int GetInputCount() => 1;
        public override int GetOutputCount() => 1;
        public override int GetInputMaxConnections(int port) => -1;
        public override int GetOutputMaxConnections(int port) => -1;

        public override bool HasTag(string tag)
        {
            return HfsmTagUtility.ContainsTag(Tags, tag);
        }

        public override IReadOnlyList<string> GetTags()
        {
            return HfsmTagUtility.ParseTags(Tags);
        }

        public override bool CanEnter(StateGraphRuntime runtime)
        {
            HfsmRuntime hfsmRuntime = runtime as HfsmRuntime ?? runtime?.Context?.GetUserData<HfsmRuntime>();
            return hfsmRuntime == null || CanEnter(hfsmRuntime);
        }

        public override void OnEnter(StateGraphRuntime runtime)
        {
            HfsmRuntime hfsmRuntime = runtime as HfsmRuntime ?? runtime?.Context?.GetUserData<HfsmRuntime>();
            if (hfsmRuntime != null)
            {
                OnEnter(hfsmRuntime);
                return;
            }

            base.OnEnter(runtime);
        }

        public override void OnUpdate(StateGraphRuntime runtime, double delta)
        {
            HfsmRuntime hfsmRuntime = runtime as HfsmRuntime ?? runtime?.Context?.GetUserData<HfsmRuntime>();
            if (hfsmRuntime != null)
                OnUpdate(hfsmRuntime, delta);
        }

        public override void OnExit(StateGraphRuntime runtime)
        {
            HfsmRuntime hfsmRuntime = runtime as HfsmRuntime ?? runtime?.Context?.GetUserData<HfsmRuntime>();
            if (hfsmRuntime != null)
                OnExit(hfsmRuntime);
        }

        public override bool TryGetCompletion(StateGraphRuntime runtime, out NodeCompletion completion)
        {
            HfsmRuntime hfsmRuntime = runtime as HfsmRuntime ?? runtime?.Context?.GetUserData<HfsmRuntime>();
            if (hfsmRuntime != null)
                return TryGetCompletion(hfsmRuntime, out completion);

            completion = default;
            return false;
        }

        public virtual void OnEnter(HfsmRuntime runtime)
        {
            if (runtime != null)
                Execute(runtime.Context);
        }

        public virtual bool CanEnter(HfsmRuntime runtime)
        {
            return true;
        }

        public virtual void OnUpdate(HfsmRuntime runtime, double delta)
        {
        }

        public virtual void OnExit(HfsmRuntime runtime)
        {
        }

        public virtual bool TryGetCompletion(HfsmRuntime runtime, out NodeCompletion completion)
        {
            completion = default;
            return false;
        }

        public override void CreateNodeUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(170f, 0f) };
            HfsmStateNodeUi.AddStateSummary(root, this);
            AddCompactFields(root);
            context.GraphNode.AddChild(root);
        }

        public override void CreateUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(190f, 0f) };

            AddStateFields(root, context);
            AddExtraFields(root);

            context.GraphNode.AddChild(root);
        }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(260f, 0f) };
            root.AddThemeConstantOverride("separation", 6);

            AddStateFields(root, context);
            AddExtraInspectorFields(root, context);
            return root;
        }

        protected virtual void AddCompactFields(VBoxContainer root)
        {
        }

        protected virtual void AddExtraFields(VBoxContainer root)
        {
        }

        protected virtual void AddExtraInspectorFields(VBoxContainer root, GraphEditorContext context)
        {
            AddExtraFields(root);
        }

        protected void AddStateFields(VBoxContainer root, GraphEditorContext context)
        {
            HfsmStateNodeUi.AddStateFields(root, this, context);
        }
    }

    internal static class HfsmStateNodeUi
    {
        public static void AddStateSummary(VBoxContainer root, IHfsmStateNodeData state)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);

            if (state.IsDefault)
            {
                row.AddChild(new Label
                {
                    Text = "Default",
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            int tagCount = HfsmTagUtility.ParseTags(state.Tags).Count;
            if (tagCount > 0)
            {
                row.AddChild(new Label
                {
                    Text = $"Tags {tagCount}",
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            if (!string.IsNullOrWhiteSpace(state.BehaviourKey))
            {
                row.AddChild(new Label
                {
                    Text = state.BehaviourKey,
                    ClipText = true,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            if (row.GetChildCount() == 0)
            {
                row.AddChild(new Label
                {
                    Text = "State",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                });
            }

            root.AddChild(row);
        }

        public static void AddStateFields(VBoxContainer root, IHfsmStateNodeData state, GraphEditorContext context)
        {
            var nameEdit = new LineEdit
            {
                PlaceholderText = "State name",
                Text = state.StateName
            };
            nameEdit.TextChanged += value =>
            {
                state.StateName = value;
                RefreshGraphNodeTitle(state, context);
            };
            root.AddChild(nameEdit);

            var defaultCheck = new CheckBox
            {
                Text = "Default",
                ButtonPressed = state.IsDefault
            };
            defaultCheck.Toggled += value =>
            {
                state.IsDefault = value;
                RefreshGraphNodeTitle(state, context);
            };
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
            tagRoot.AddChild(new HfsmTagMultiSelectDropdown(registry, state.Tags, tags => state.Tags = tags));
            root.AddChild(tagRoot);
        }

        private static void RefreshGraphNodeTitle(IHfsmStateNodeData state, GraphEditorContext context)
        {
            if (context?.GraphNode == null || state is not GraphNodeData nodeData)
                return;

            context.GraphNode.Title = nodeData.GetDisplayName();
        }
    }
}
