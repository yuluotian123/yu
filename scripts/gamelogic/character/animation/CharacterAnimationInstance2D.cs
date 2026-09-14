using System;
using System.Collections.Generic;
using Framework;
using Godot;

namespace GameLogic
{
    /// <summary>
    /// Runtime equivalent of an Unreal AnimInstance for a 2D character.
    /// Locomotion owns the state-machine requests while ability requests act as
    /// a higher-priority overlay before the selected clip reaches the backend.
    /// </summary>
    public sealed class CharacterAnimationInstance2D
    {
        private sealed class AnimationRequest
        {
            public string Key;
            public string Animation;
            public int Priority;
            public float Speed;
            public bool FromEnd;
            public bool RestartIfPlaying;
            public ulong Sequence;
        }

        private readonly Dictionary<string, AnimationRequest> _animationRequests = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _variables = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _variableSources = new(StringComparer.Ordinal);
        private readonly HashSet<string> _missingAnimationWarnings = new(StringComparer.Ordinal);
        private ulong _requestSequence;
        private bool _backendWarningShown;
        private string _activeRequestKey = string.Empty;
        private string _activeAnimation = string.Empty;
        private int _activeRequestPriority;

        public GameObject2D Owner { get; private set; }
        public HfsmGraphAsset LocomotionGraph { get; private set; }
        public HfsmRuntime LocomotionRuntime { get; private set; }
        public IAnimationPlaybackBackend PlaybackBackend { get; private set; }
        public IReadOnlyDictionary<string, object> Variables => _variables;
        public IReadOnlyDictionary<string, string> VariableSources => _variableSources;
        public string ActiveRequestKey => _activeRequestKey;
        public string ActiveAnimation => _activeAnimation;
        public int ActiveRequestPriority => _activeRequestPriority;

        public string CurrentStatePath => LocomotionRuntime?.CurrentStatePath ?? string.Empty;
        public int ActiveRequestCount => _animationRequests.Count;

        public void Initialize(GameObject2D owner, HfsmGraphAsset graph, IAnimationPlaybackBackend backend)
        {
            if (LocomotionRuntime != null || PlaybackBackend != null)
                Dispose();

            Owner = owner;
            LocomotionGraph = graph;
            PlaybackBackend = backend;
            if (PlaybackBackend?.IsValid != true && !_backendWarningShown)
            {
                _backendWarningShown = true;
                Debugger.Warn("[CharacterAnimationInstance2D] No valid animation playback backend is attached.");
            }

            if (graph == null)
                return;

            LocomotionRuntime = new HfsmRuntime(graph);
            LocomotionRuntime.Context.UserData.Add(this);
            if (owner != null)
                LocomotionRuntime.Context.UserData.Add(owner);

            if (!LocomotionRuntime.Start())
                Debugger.Warn("[CharacterAnimationInstance2D] Failed to start LocomotionGraph.");
        }

        public void Update(double delta)
        {
            if (LocomotionRuntime == null)
            {
                ApplyBestAnimationRequest();
                return;
            }

            GraphComponentBindingRuntime.SyncFromComponents(LocomotionRuntime.Context);
            CaptureVariables();
            LocomotionRuntime.Update(delta);
            GraphComponentBindingRuntime.SyncToComponents(LocomotionRuntime.Context);
            ApplyBestAnimationRequest();
        }

        public void Dispose()
        {
            LocomotionRuntime?.Stop();
            LocomotionRuntime = null;
            LocomotionGraph = null;
            PlaybackBackend = null;
            Owner = null;
            _animationRequests.Clear();
            _variables.Clear();
            _variableSources.Clear();
            _missingAnimationWarnings.Clear();
            _requestSequence = 0;
            _backendWarningShown = false;
            _activeRequestKey = string.Empty;
            _activeAnimation = string.Empty;
            _activeRequestPriority = 0;
        }

        public bool TryGetAnimationVariable(string key, out object value)
        {
            if (!string.IsNullOrWhiteSpace(key) && _variables.TryGetValue(key.Trim(), out value))
                return true;
            value = null;
            return false;
        }

        public void RequestAnimation(
            string key,
            string animation,
            int priority,
            float speed = 1f,
            bool fromEnd = false,
            bool restartIfPlaying = true)
        {
            if (string.IsNullOrWhiteSpace(animation))
                return;

            key = NormalizeRequestKey(key, animation);
            _animationRequests[key] = new AnimationRequest
            {
                Key = key,
                Animation = animation.Trim(),
                Priority = priority,
                Speed = speed,
                FromEnd = fromEnd,
                RestartIfPlaying = restartIfPlaying,
                Sequence = ++_requestSequence
            };
        }

        public void ClearAnimationRequest(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                _animationRequests.Clear();
            else
                _animationRequests.Remove(key.Trim());
        }

        private void CaptureVariables()
        {
            _variables.Clear();
            _variableSources.Clear();
            if (LocomotionRuntime?.Context?.Graph?.BlackboardEntries == null)
                return;

            foreach (GraphBlackboardEntry entry in LocomotionRuntime.Context.Graph.BlackboardEntries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                    continue;

                if (LocomotionRuntime.Context.Blackboard.TryGetValue(entry.Key, out object value))
                    _variables[entry.Key] = value;

                GraphComponentBinding binding = entry.Binding;
                _variableSources[entry.Key] = binding == null
                    ? "Local"
                    : $"Component: {binding.ComponentTypeName}.{binding.MemberId}";
            }
        }

        private void ApplyBestAnimationRequest()
        {
            AnimationRequest best = null;
            foreach (AnimationRequest request in _animationRequests.Values)
            {
                if (!CanPlayAnimation(request.Animation))
                    continue;
                if (best == null || request.Priority > best.Priority ||
                    request.Priority == best.Priority && request.Sequence > best.Sequence)
                    best = request;
            }

            if (best == null)
            {
                _activeRequestKey = string.Empty;
                _activeAnimation = string.Empty;
                _activeRequestPriority = 0;
                PlaybackBackend?.Stop();
                return;
            }

            bool sameRequest = _activeRequestKey == best.Key;
            bool sameAnimation = _activeAnimation == best.Animation;
            _activeRequestKey = best.Key;
            _activeAnimation = best.Animation;
            _activeRequestPriority = best.Priority;
            bool restart = sameRequest && sameAnimation
                ? false
                : !sameAnimation || best.RestartIfPlaying;
            PlaybackBackend?.Play(best.Animation, best.Speed, best.FromEnd, restart);
        }

        private bool CanPlayAnimation(string animation)
        {
            if (PlaybackBackend?.IsValid != true || string.IsNullOrWhiteSpace(animation))
                return false;
            if (PlaybackBackend.HasAnimation(animation))
                return true;
            if (_missingAnimationWarnings.Add(animation))
                Debugger.Warn($"[CharacterAnimationInstance2D] Missing animation '{animation}'.");
            return false;
        }

        private static string NormalizeRequestKey(string key, string animation) =>
            !string.IsNullOrWhiteSpace(key) ? key.Trim() : animation.Trim();
    }
}
