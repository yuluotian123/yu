using System;
using System.Collections.Generic;
using Godot;

namespace GameLogic
{
    public class HfsmCompositeStateNodeData : SubGraphNodeData, IHfsmStateNodeData
    {
        private HfsmGraphAsset _cachedSubGraph;

        public string StateName { get; set; } = "Composite";
        public bool IsDefault { get; set; }
        public string Tags { get; set; } = string.Empty;
        public string BehaviourKey { get; set; } = string.Empty;
        public string MetadataJson { get; set; } = "{}";

        public override List<string> GetGraphTypes() => new() { HfsmGraphAsset.GraphTypeName };

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

            if (!ResourceLoader.Exists(SubGraphPath))
            {
                GD.PushWarning($"[HFSM] Sub graph resource does not exist: {SubGraphPath}");
                return null;
            }

            _cachedSubGraph = ResourceLoader.Load<HfsmGraphAsset>(SubGraphPath);
            if (_cachedSubGraph == null)
                GD.PushWarning($"[HFSM] Resource is not a HfsmGraphAsset: {SubGraphPath}");

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

        public override bool AcceptsSubGraph(GraphAsset graph)
        {
            return graph is HfsmGraphAsset;
        }

        public override string GetSubGraphTypeName()
        {
            return nameof(HfsmGraphAsset);
        }

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
    }
}
