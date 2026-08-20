using System;
using System.Collections.Generic;
using Godot;

namespace GameLogic
{
    public sealed class AbilityRuntime
    {
        private readonly Dictionary<string, object> _data = new(StringComparer.Ordinal);
        private FlowGraphRuntime _flowRuntime;

        public AbilityRuntime(AbilitySystemComponent2D system, AbilityResource resource)
        {
            System = system;
            Resource = resource;
        }

        public AbilitySystemComponent2D System { get; }
        public AbilityResource Resource { get; }
        public string AbilityId => Resource?.AbilityId ?? string.Empty;
        public double CooldownReadyTime { get; private set; }
        public double ElapsedTime { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsCompleted { get; private set; }
        public string LastReturnLabel { get; private set; } = string.Empty;
        public FlowGraphRuntime FlowRuntime => _flowRuntime;
        public float CooldownRemaining(double now) => Mathf.Max(0f, (float)(CooldownReadyTime - now));

        internal void SetCooldownReadyTime(double value) => CooldownReadyTime = Math.Max(0d, value);
        public bool CanStart(double now) => Resource?.Graph != null && !IsRunning && now >= CooldownReadyTime;

        internal bool Start(AbilityExecutionContext executionContext, double now)
        {
            if (!CanStart(now) || executionContext?.GameObject == null)
                return false;

            Stop("Restarted");
            _data.Clear();
            ElapsedTime = 0d;
            IsRunning = true;
            IsCompleted = false;
            LastReturnLabel = string.Empty;
            CooldownReadyTime = now + Mathf.Max(0f, Resource.Cooldown);

            var context = new GraphExecutionContext(Resource.Graph, new GraphBlackboardRuntime());
            context.UserData.Add(System);
            context.UserData.Add(this);
            context.UserData.Add(Resource);
            context.UserData.Add(executionContext);
            context.UserData.Add(executionContext.GameObject);

            _flowRuntime = new FlowGraphRuntime(Resource.Graph, context);
            _flowRuntime.Returned += OnFlowReturned;
            if (!_flowRuntime.Start())
            {
                Stop("Failed");
                return false;
            }

            if (_flowRuntime.IsCompleted)
                MarkCompleted("Finished");
            return true;
        }

        internal void Update(double delta)
        {
            if (!IsRunning || _flowRuntime == null)
                return;

            ElapsedTime += Math.Max(0d, delta);
            _flowRuntime.Update(delta);
            if (_flowRuntime.IsCompleted)
                MarkCompleted(string.IsNullOrWhiteSpace(LastReturnLabel) ? "Finished" : LastReturnLabel);
        }

        internal void Stop(string label = "Interrupted")
        {
            if (_flowRuntime != null)
            {
                _flowRuntime.Returned -= OnFlowReturned;
                _flowRuntime.Stop();
                _flowRuntime = null;
            }

            if (IsRunning && !IsCompleted)
                LastReturnLabel = string.IsNullOrWhiteSpace(label) ? "Interrupted" : label;
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

        public T GetData<T>(string key, T defaultValue = default) =>
            !string.IsNullOrWhiteSpace(key) && _data.TryGetValue(key, out object raw) && raw is T value
                ? value
                : defaultValue;

        public bool TryGetData<T>(string key, out T value)
        {
            if (!string.IsNullOrWhiteSpace(key) &&
                _data.TryGetValue(key, out object raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        private void OnFlowReturned(FlowGraphRuntime runtime, string label) => MarkCompleted(label);

        private void MarkCompleted(string label)
        {
            LastReturnLabel = string.IsNullOrWhiteSpace(label) ? "Finished" : label;
            IsCompleted = true;
            IsRunning = false;
        }
    }
}
