using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class AbilitySystemComponent2D : Component2D
    {
        [Export] public AbilitySetResource AbilitySet { get; set; }

        private readonly Dictionary<string, AbilityResource> _granted = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AbilityRuntime> _runtimes = new(StringComparer.Ordinal);
        private readonly List<AbilityRuntime> _active = new();
        private CharacterMovementComponent2D _movement;

        public override int Priority => ComponentPriority.Movement + 5;
        public IReadOnlyList<AbilityRuntime> ActiveAbilities => _active;

        public event Action<AbilityRuntime> AbilityActivated;
        public event Action<AbilityRuntime> AbilityCompleted;
        public event Action<AbilityRuntime> AbilityCancelled;
        public event Action<string, AbilityActivationResult> AbilityRejected;

        public override void OnInit()
        {
            _movement = Owner?.GetComponent<CharacterMovementComponent2D>();
            GrantAbilitySet(AbilitySet);
        }

        public override void OnPhysicsUpdate(double delta)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                AbilityRuntime runtime = _active[i];
                runtime?.Update(delta);
                if (runtime?.IsRunning == true)
                    continue;

                _active.RemoveAt(i);
                ReleaseMovementLock(runtime);
                AbilityCompleted?.Invoke(runtime);
            }
        }

        public override void OnDestroy()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ReleaseMovementLock(_active[i]);
                _active[i]?.Stop("Destroyed");
            }
            _active.Clear();
            _runtimes.Clear();
            _granted.Clear();
            _movement = null;
        }

        public void GrantAbilitySet(AbilitySetResource set)
        {
            if (set?.Abilities == null)
                return;
            foreach (AbilityResource ability in set.Abilities)
                GrantAbility(ability);
        }

        public bool GrantAbility(AbilityResource ability)
        {
            if (ability == null || string.IsNullOrWhiteSpace(ability.AbilityId))
                return false;
            string id = ability.AbilityId.Trim();
            if (_granted.TryGetValue(id, out AbilityResource existing) && existing != ability)
            {
                GD.PushError($"[AbilitySystemComponent2D] Duplicate AbilityId '{id}'.");
                return false;
            }
            _granted[id] = ability;
            return true;
        }

        public AbilityActivationResult TryActivateAbility(
            string abilityId,
            string source = "CharacterGraph",
            int? requestPriority = null)
        {
            AbilityActivationResult validation = ValidateActivation(
                abilityId,
                requestPriority,
                out AbilityRuntime runtime,
                out int effectivePriority);
            if (validation != AbilityActivationResult.Activated)
            {
                AbilityRejected?.Invoke(abilityId ?? string.Empty, validation);
                return validation;
            }

            AbilityActivationPolicy requested = runtime.Resource.Policy;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                AbilityRuntime active = _active[i];
                if (active?.IsRunning != true || requested.AllowConcurrent)
                    continue;
                if (active.Resource.Policy.Priority > effectivePriority)
                    continue;
                CancelAbility(active.AbilityId, "Interrupted");
            }

            var context = new AbilityExecutionContext
            {
                GameObject = Owner,
                AbilitySystem = this,
                Source = source ?? string.Empty
            };
            if (!runtime.Start(context, GetTimeSeconds()))
            {
                AbilityRejected?.Invoke(abilityId, AbilityActivationResult.InvalidContext);
                return AbilityActivationResult.InvalidContext;
            }

            if (!_active.Contains(runtime))
                _active.Add(runtime);
            ApplyMovementLock(runtime);
            AbilityActivated?.Invoke(runtime);
            return AbilityActivationResult.Activated;
        }

        public bool CancelAbility(string abilityId, string reason = "Cancelled")
        {
            AbilityRuntime runtime = GetRuntime(abilityId);
            if (runtime?.IsRunning != true)
                return false;
            runtime.Stop(reason);
            _active.Remove(runtime);
            ReleaseMovementLock(runtime);
            AbilityCancelled?.Invoke(runtime);
            return true;
        }

        public AbilityRuntime GetRuntime(string abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId) || !_granted.TryGetValue(abilityId.Trim(), out AbilityResource resource))
                return null;
            if (!_runtimes.TryGetValue(abilityId.Trim(), out AbilityRuntime runtime))
            {
                runtime = new AbilityRuntime(this, resource);
                _runtimes[abilityId.Trim()] = runtime;
            }
            return runtime;
        }

        public JsonObject CaptureDurableState()
        {
            double now = GetTimeSeconds();
            var values = new JsonArray();
            foreach (AbilityRuntime runtime in _runtimes.Values)
            {
                values.Add(new JsonObject
                {
                    ["id"] = runtime.AbilityId,
                    ["cooldown_remaining"] = runtime.CooldownRemaining(now)
                });
            }
            return new JsonObject { ["abilities"] = values };
        }

        public void RestoreDurableState(JsonObject state)
        {
            JsonArray values = state?["abilities"] as JsonArray ?? state?["skills"] as JsonArray;
            if (values == null)
                return;
            double now = GetTimeSeconds();
            foreach (JsonNode node in values)
            {
                if (node is not JsonObject item)
                    continue;
                string id = item["id"]?.GetValue<string>();
                AbilityRuntime runtime = GetRuntime(id);
                if (runtime == null)
                    continue;
                float remaining = item["cooldown_remaining"]?.GetValue<float>() ?? 0f;
                runtime.SetCooldownReadyTime(now + Mathf.Max(0f, remaining));
            }
        }

        private AbilityActivationResult ValidateActivation(
            string abilityId,
            int? requestPriority,
            out AbilityRuntime runtime,
            out int effectivePriority)
        {
            runtime = null;
            effectivePriority = int.MinValue;
            if (string.IsNullOrWhiteSpace(abilityId))
                return AbilityActivationResult.InvalidAbilityId;
            runtime = GetRuntime(abilityId);
            if (runtime == null)
                return AbilityActivationResult.NotGranted;
            if (runtime.IsRunning)
                return AbilityActivationResult.AlreadyActive;
            if (!runtime.CanStart(GetTimeSeconds()))
                return AbilityActivationResult.OnCooldown;

            AbilityActivationPolicy requested = runtime.Resource.Policy;
            effectivePriority = requestPriority ?? requested.Priority;
            for (int i = 0; i < _active.Count; i++)
            {
                AbilityRuntime active = _active[i];
                if (active?.IsRunning != true || requested.AllowConcurrent)
                    continue;
                if (!requested.CanInterrupt || active.Resource.Policy.Priority > effectivePriority)
                    return AbilityActivationResult.BlockedByCurrentAbility;
            }
            return AbilityActivationResult.Activated;
        }

        private void ApplyMovementLock(AbilityRuntime runtime)
        {
            AbilityActivationPolicy policy = runtime?.Resource?.Policy;
            if (policy != null)
                _movement?.SetControlLock(runtime.AbilityId, policy.BlocksMovement, policy.BlocksJump, policy.Priority);
        }

        private void ReleaseMovementLock(AbilityRuntime runtime)
        {
            if (runtime != null)
                _movement?.ClearControlLock(runtime.AbilityId);
        }

        private static double GetTimeSeconds() => Time.GetTicksMsec() * 0.001d;
    }
}
