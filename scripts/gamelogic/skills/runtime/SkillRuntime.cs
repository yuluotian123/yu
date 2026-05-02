using Godot;
using System;
using System.Collections.Generic;

namespace GameLogic
{
    public sealed class SkillRuntime
    {
        private readonly Dictionary<string, object> _data = new(StringComparer.Ordinal);
        private FlowGraphRuntime _flowRuntime;
        private GraphRuntimeDebugHandle _debugHandle;

        public SkillRuntime(SkillManagerComponent2D manager, SkillResource resource, string resourcePath)
        {
            Manager = manager;
            Resource = resource;
            ResourcePath = resourcePath ?? string.Empty;
            SkillKey = ResolveSkillKey(resource, resourcePath);
        }

        public SkillManagerComponent2D Manager { get; }
        public SkillResource Resource { get; }
        public string ResourcePath { get; }
        public string SkillKey { get; }
        public double CooldownReadyTime { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsCompleted { get; private set; }
        public string LastReturnLabel { get; private set; } = string.Empty;
        public FlowGraphRuntime FlowRuntime => _flowRuntime;

        public bool IsCooldownReady(double now)
        {
            return now >= CooldownReadyTime;
        }

        public bool CanStart(double now)
        {
            return Resource?.Graph != null && !IsRunning && IsCooldownReady(now);
        }

        public bool Start(HfsmRuntime hfsmRuntime, double now)
        {
            if (!CanStart(now))
                return false;

            Stop();
            _data.Clear();
            IsRunning = true;
            IsCompleted = false;
            LastReturnLabel = string.Empty;
            CooldownReadyTime = now + Mathf.Max(0f, Resource.Cooldown);

            var context = new GraphExecutionContext(Resource.Graph, hfsmRuntime.Blackboard.ForkSharedLocals());
            context.UserData.Add(Manager);
            context.UserData.Add(this);
            context.UserData.Add(Resource);
            context.UserData.Add(hfsmRuntime);
            if (hfsmRuntime.Owner != null)
                context.UserData.Add(hfsmRuntime.Owner);
            if (hfsmRuntime.GameObject != null)
                context.UserData.Add(hfsmRuntime.GameObject);

            _flowRuntime = new FlowGraphRuntime(Resource.Graph, context);
            _flowRuntime.Returned += OnFlowReturned;
            Node ownerNode = hfsmRuntime.GameObject ?? Manager?.Owner;
            _debugHandle = GraphRuntimeDebugRegistry.Register(
                ownerNode,
                _flowRuntime,
                Resource.Graph,
                $"Skill:{SkillKey}",
                CreateRuntimeDebugMetadata);

            if (!_flowRuntime.Start())
            {
                Stop();
                return false;
            }

            if (_flowRuntime.IsCompleted)
                MarkCompleted("Finished");

            return true;
        }

        public void Update(double delta)
        {
            if (!IsRunning || _flowRuntime == null)
                return;

            _flowRuntime.Update(delta);
            if (_flowRuntime.IsCompleted)
                MarkCompleted(string.IsNullOrWhiteSpace(LastReturnLabel) ? "Finished" : LastReturnLabel);
        }

        public void Stop()
        {
            bool wasCompleted = IsCompleted;
            if (_flowRuntime != null)
            {
                _flowRuntime.Returned -= OnFlowReturned;
                _flowRuntime.Stop();
                _flowRuntime = null;
            }

            _debugHandle?.Dispose();
            _debugHandle = null;

            if (IsRunning && !wasCompleted && string.IsNullOrWhiteSpace(LastReturnLabel))
                LastReturnLabel = "Interrupted";

            IsRunning = false;
            _data.Clear();
        }

        public void SetData(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (value == null)
                _data.Remove(key);
            else
                _data[key] = value;
        }

        public T GetData<T>(string key, T defaultValue = default)
        {
            return TryGetData(key, out T value) ? value : defaultValue;
        }

        public bool TryGetData<T>(string key, out T value)
        {
            if (!string.IsNullOrWhiteSpace(key) &&
                _data.TryGetValue(key, out object raw) &&
                raw is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        private void OnFlowReturned(FlowGraphRuntime flowRuntime, string label)
        {
            MarkCompleted(string.IsNullOrWhiteSpace(label) ? "Finished" : label);
        }

        private void MarkCompleted(string label)
        {
            LastReturnLabel = string.IsNullOrWhiteSpace(label) ? "Finished" : label;
            IsCompleted = true;
            IsRunning = false;
            if (_flowRuntime != null)
            {
                GraphRuntimeDebugRegistry.RecordEvent(_flowRuntime, "SkillCompleted", LastReturnLabel, Resource?.Graph);
                GraphRuntimeDebugRegistry.CaptureContext(_flowRuntime, _flowRuntime.Context, true);
            }

            _debugHandle?.Dispose();
            _debugHandle = null;
        }

        private static string ResolveSkillKey(SkillResource resource, string resourcePath)
        {
            if (!string.IsNullOrWhiteSpace(resource?.SkillId))
                return resource.SkillId;

            if (!string.IsNullOrWhiteSpace(resourcePath))
                return resourcePath;

            return resource?.ResourcePath ?? string.Empty;
        }

        private IEnumerable<string> CreateRuntimeDebugMetadata()
        {
            yield return $"SkillKey={SkillKey}";
            if (!string.IsNullOrWhiteSpace(ResourcePath))
                yield return $"ResourcePath={ResourcePath}";

            yield return $"Running={IsRunning}";
            yield return $"Completed={IsCompleted}";
            if (!string.IsNullOrWhiteSpace(LastReturnLabel))
                yield return $"Return={LastReturnLabel}";

            double now = Time.GetTicksMsec() * 0.001d;
            yield return $"CooldownRemaining={Mathf.Max(0f, (float)(CooldownReadyTime - now)):0.###}";
        }
    }
}
