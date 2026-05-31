using System;
using System.Collections.Generic;
using Framework;
using Godot;

namespace GameLogic
{
    public class HfsmCompositeStateNodeData : CompositeStateNodeData, IHfsmStateNodeData
    {
        private HfsmGraphAsset _cachedSubGraph;

        public string BehaviourKey { get; set; } = string.Empty;
        public string MetadataJson { get; set; } = "{}";

        public override List<string> GetGraphTypes() => new() { HfsmGraphAsset.GraphTypeName };
        public override string GetMenuName() => "Composite State";

        public override string GetDisplayName()
        {
            string stateName = string.IsNullOrWhiteSpace(StateName) ? "Composite" : StateName;
            return string.IsNullOrWhiteSpace(SubGraphPath)
                ? $"{stateName} [Composite]"
                : $"{stateName} [{SubGraphPath.GetFile().GetBaseName()}]";
        }

        public override Color GetNodeColor() => IsDefault ? new Color(0.35f, 0.8f, 0.5f) : new Color(0.45f, 0.45f, 0.95f);
        public override int GetInputMaxConnections(int port) => -1;
        public override int GetOutputMaxConnections(int port) => -1;

        public override HfsmGraphAsset GetSubGraph()
        {
            if (_cachedSubGraph != null)
                return _cachedSubGraph;

            if (string.IsNullOrWhiteSpace(SubGraphPath))
                return null;

            _cachedSubGraph = ModuleSystem
                .GetModule<IResourceModule>()
                .LoadAssetOnce<HfsmGraphAsset>(SubGraphPath);
            return _cachedSubGraph;
        }

        public override void InvalidateCache()
        {
            _cachedSubGraph = null;
            base.InvalidateCache();
        }

        public override GraphAsset CreateSubGraphAsset()
        {
            return new HfsmGraphAsset();
        }

        public override Type GetSubGraphType()
        {
            return typeof(HfsmGraphAsset);
        }

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

        public override void CreateNodeUI(GraphEditorContext context)
        {
            var root = new VBoxContainer
            {
                Name = "SubGraphContent",
                CustomMinimumSize = new Vector2(180f, 0f)
            };

            HfsmStateNodeUi.AddStateSummary(root, this);
            AddSubGraphSummary(root);
            context.GraphNode.AddChild(root);
        }

        public override void CreateUI(GraphEditorContext context)
        {
            var root = new VBoxContainer
            {
                Name = "SubGraphContent",
                CustomMinimumSize = new Vector2(190f, 0f)
            };

            HfsmStateNodeUi.AddStateFields(root, this, context);
            root.AddChild(new HSeparator());

            var pathLabel = new Label
            {
                Name = "PathLabel",
                Text = string.IsNullOrEmpty(SubGraphPath) ? "Unbound HFSM SubGraph" : GetDisplayName(),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            root.AddChild(pathLabel);

            context.GraphNode.AddChild(root);
        }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(260f, 0f) };
            root.AddThemeConstantOverride("separation", 6);

            HfsmStateNodeUi.AddStateFields(root, this, context);
            root.AddChild(new HSeparator());
            root.AddChild(CreateInspectorInfoRow("SubGraph", string.IsNullOrWhiteSpace(SubGraphPath) ? "Unbound" : SubGraphPath));
            return root;
        }

        private void AddSubGraphSummary(VBoxContainer root)
        {
            var pathLabel = new Label
            {
                Name = "PathLabel",
                Text = string.IsNullOrEmpty(SubGraphPath) ? "SubGraph: unbound" : $"SubGraph: {SubGraphPath.GetFile().GetBaseName()}",
                ClipText = true,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            root.AddChild(pathLabel);
        }

    }
}
