using System;
using System.Collections.Generic;
using Framework;
using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class CharacterAnimationComponent2D : Component2D
    {
        private HfsmGraphAsset _locomotionGraph;
        private AnimatedSprite2D _sprite;
        public override int Priority => 20;

        [Export] public NodePath SpritePath { get; set; } = new("VisualRoot/AnimatedSprite2D");
        [Export]
        public HfsmGraphAsset LocomotionGraph
        {
            get => _locomotionGraph;
            set
            {
                _locomotionGraph = value;
                if (_locomotionGraph != null && IsSceneLocalResource(_locomotionGraph))
                    _locomotionGraph.ResourceLocalToScene = true;
            }
        }

        public CharacterAnimationInstance2D AnimationInstance { get; private set; }
        public IReadOnlyDictionary<string, object> AnimationVariables => AnimationInstance?.Variables;
        public string CurrentStatePath => AnimationInstance?.CurrentStatePath ?? string.Empty;
        public IAnimationPlaybackBackend PlaybackBackend => AnimationInstance?.PlaybackBackend;
        public int ActiveRequestCount => AnimationInstance?.ActiveRequestCount ?? 0;
        public int ActiveRequestPriority => AnimationInstance?.ActiveRequestPriority ?? 0;
        public string ActiveRequestKey => AnimationInstance?.ActiveRequestKey ?? string.Empty;
        public string ActiveAnimation => AnimationInstance?.ActiveAnimation ?? string.Empty;
        public AnimatedSprite2D Sprite => _sprite;
        public HfsmRuntime LocomotionRuntime => AnimationInstance?.LocomotionRuntime;

        public HfsmGraphAsset PrepareLocomotionGraphForEditor()
        {
            if (LocomotionGraph == null)
            {
                LocomotionGraph = new HfsmGraphAsset();
                LocomotionGraph.ResourceLocalToScene = true;
                return LocomotionGraph;
            }

            if (!IsSceneLocalResource(LocomotionGraph) &&
                !string.IsNullOrWhiteSpace(LocomotionGraph.ResourcePath))
            {
                HfsmGraphAsset migrated = LocomotionGraph.Duplicate(true) as HfsmGraphAsset;
                if (migrated != null)
                {
                    migrated.ResourcePath = string.Empty;
                    LocomotionGraph = migrated;
                }
            }

            LocomotionGraph.ResourceLocalToScene = true;
            GraphComponentBindingRuntime.ApplyDefaultBindings(LocomotionGraph);
            return LocomotionGraph;
        }

        public override void OnInit()
        {
            if (LocomotionGraph == null)
                Debugger.Warn("[CharacterAnimationComponent2D] LocomotionGraph is not assigned.");
            _sprite = Owner?.GetNodeOrNull<AnimatedSprite2D>(SpritePath);
            if (_sprite == null)
                Debugger.Warn("[CharacterAnimationComponent2D] Missing AnimatedSprite2D.");
            AnimationInstance = new CharacterAnimationInstance2D();
            AnimationInstance.Initialize(Owner, LocomotionGraph, new AnimatedSprite2DPlaybackBackend(_sprite));
            if (LocomotionRuntime != null)
                LocomotionRuntime.Context.UserData.Add(this);
        }

        public override void OnPhysicsUpdate(double delta)
        {
            AnimationInstance?.Update(delta);
        }

        public override void OnDestroy()
        {
            AnimationInstance?.Dispose();
            AnimationInstance = null;
            _sprite = null;
        }

        public void RequestAnimation(
            string key,
            string animation,
            int priority,
            float speed = 1f,
            bool fromEnd = false,
            bool restartIfPlaying = true)
        {
            AnimationInstance?.RequestAnimation(key, animation, priority, speed, fromEnd, restartIfPlaying);
        }

        public void ClearAnimationRequest(string key)
        {
            AnimationInstance?.ClearAnimationRequest(key);
        }

        private static bool IsSceneLocalResource(HfsmGraphAsset graph)
        {
            if (graph == null)
                return false;
            string path = graph.ResourcePath;
            return graph.IsBuiltIn() || path?.Contains("::", StringComparison.Ordinal) == true ||
                   (graph.ResourceLocalToScene && string.IsNullOrWhiteSpace(path));
        }
    }
}
