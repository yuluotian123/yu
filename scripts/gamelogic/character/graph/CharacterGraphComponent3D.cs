using System;
using System.Linq;
using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class CharacterGraphComponent3D : Component3D
    {
        private CharacterGraphAsset _characterGraph;

        [Export]
        public CharacterGraphAsset CharacterGraph
        {
            get => _characterGraph;
            set
            {
                _characterGraph = value;
                if (_characterGraph != null && IsSceneLocalResource(_characterGraph))
                    _characterGraph.ResourceLocalToScene = true;
            }
        }

        /// <summary>
        /// Ensures the graph edited from this component is scene-local. Existing
        /// external CharacterGraph resources are duplicated so saving the host
        /// scene no longer writes to or depends on a separate .tres file.
        /// </summary>
        public CharacterGraphAsset PrepareGraphForEditor()
        {
            if (CharacterGraph == null)
            {
                CharacterGraph = new CharacterGraphAsset();
                return CharacterGraph;
            }

            if (!CharacterGraph.TryLoadDocument(out _))
                return CharacterGraph;

            if (!IsSceneLocalResource(CharacterGraph) && !string.IsNullOrWhiteSpace(CharacterGraph.ResourcePath))
            {
                CharacterGraphAsset migrated = CharacterGraph.Duplicate(true) as CharacterGraphAsset;
                if (migrated != null)
                {
                    migrated.ResourcePath = string.Empty;
                    CharacterGraph = migrated;
                }
            }

            CharacterGraph.ResourceLocalToScene = true;
            CharacterGraph.InitializeComponentArguments();
            return CharacterGraph;
        }

        public override int Priority => ComponentPriority.State;
        public CharacterGraphRuntime Runtime { get; private set; }

        public override void OnInit()
        {
            if (CharacterGraph == null)
            {
                GD.PushWarning("[CharacterGraphComponent3D] CharacterGraph is not assigned.");
                return;
            }

            ICharacterInputProvider input = Owner?.GetAllComponents()
                .OfType<ICharacterInputProvider>()
                .FirstOrDefault();
            if (!CharacterGraph.TryLoadDocument(out string loadError))
            {
                GD.PushWarning($"[CharacterGraphComponent3D] {loadError}");
                return;
            }
            Runtime = new CharacterGraphRuntime(CharacterGraph, Owner, input);
        }

        public override void OnUpdate(double delta)
        {
            if (Runtime == null)
                return;
            GraphComponentBindingRuntime.SyncFromComponents(Runtime.Context);
            Runtime.Update(delta, physics: false);
            GraphComponentBindingRuntime.SyncToComponents(Runtime.Context);
        }

        public override void OnPhysicsUpdate(double delta)
        {
            if (Runtime == null)
                return;
            GraphComponentBindingRuntime.SyncFromComponents(Runtime.Context);
            Runtime.Update(delta, physics: true);
            GraphComponentBindingRuntime.SyncToComponents(Runtime.Context);
        }

        public override void OnDestroy()
        {
            Runtime?.Stop();
            Runtime = null;
        }

        public void PublishEvent(string eventName) => Runtime?.Context.Events.Publish(eventName);

        private static bool IsSceneLocalResource(CharacterGraphAsset graph)
        {
            if (graph == null)
                return false;

            string path = graph.ResourcePath;
            return graph.IsBuiltIn() ||
                   path?.Contains("::", StringComparison.Ordinal) == true ||
                   (graph.ResourceLocalToScene && string.IsNullOrWhiteSpace(path));
        }
    }
}
