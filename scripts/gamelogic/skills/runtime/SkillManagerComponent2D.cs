using Framework;
using Godot;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace GameLogic
{
    [GlobalClass]
    public partial class SkillManagerComponent2D : Component2D
    {
        private readonly System.Collections.Generic.Dictionary<string, SkillResource> _resources = new(System.StringComparer.Ordinal);
        private readonly System.Collections.Generic.Dictionary<string, SkillRuntime> _runtimes = new(System.StringComparer.Ordinal);
        private readonly List<SkillRuntime> _activeRuntimes = new();

        public override int Priority => ComponentPriority.Movement + 5;

        public bool ActiveSkillBlocksMovement => HasActivePolicy(movement: true);
        public bool ActiveSkillBlocksJump => HasActivePolicy(movement: false);

        public override void OnInit()
        {
            RegisterCharacterGraphSkills();
        }

        public override void OnPhysicsUpdate(double delta)
        {
            for (int i = _activeRuntimes.Count - 1; i >= 0; i--)
            {
                SkillRuntime runtime = _activeRuntimes[i];
                if (runtime == null)
                {
                    _activeRuntimes.RemoveAt(i);
                    continue;
                }

                runtime.Update(delta);
                if (!runtime.IsRunning)
                    _activeRuntimes.RemoveAt(i);
            }
        }

        public override void OnDestroy()
        {
            for (int i = _activeRuntimes.Count - 1; i >= 0; i--)
                _activeRuntimes[i]?.Stop();

            _activeRuntimes.Clear();
            _runtimes.Clear();
            _resources.Clear();
        }

        public bool CanStart(string skillResourcePath, SkillExecutionPolicy policy)
        {
            SkillRuntime runtime = GetRuntime(skillResourcePath);
            return runtime != null &&
                runtime.CanStart(GetTimeSeconds()) &&
                CanInterruptActive(policy, stopActive: false);
        }

        public SkillRuntime StartSkill(
            string skillResourcePath,
            HfsmRuntime hfsmRuntime,
            SkillExecutionPolicy policy)
        {
            if (hfsmRuntime == null)
                return null;

            return StartSkill(
                skillResourcePath,
                new SkillExecutionContext { Hfsm = hfsmRuntime, Manager = this },
                policy);
        }

        public SkillRuntime StartSkill(
            string skillResourcePath,
            SkillExecutionContext context,
            SkillExecutionPolicy policy)
        {
            SkillRuntime runtime = GetRuntime(skillResourcePath);
            if (runtime == null ||
                context == null ||
                !runtime.CanStart(GetTimeSeconds()) ||
                !CanInterruptActive(policy, stopActive: false))
                return null;

            StopInterruptibleSkills(policy);
            if (!runtime.Start(context, policy, GetTimeSeconds()))
                return null;

            if (!_activeRuntimes.Contains(runtime))
                _activeRuntimes.Add(runtime);

            return runtime;
        }

        public bool RegisterSkillPath(string skillResourcePath)
        {
            if (string.IsNullOrWhiteSpace(skillResourcePath))
                return false;

            SkillResource skill = LoadSkillResource(skillResourcePath);
            return skill != null && CacheSkill(skillResourcePath, skill);
        }

        public JsonObject CaptureDurableState()
        {
            double now = GetTimeSeconds();
            var skills = new JsonArray();
            foreach (SkillRuntime runtime in _runtimes.Values)
            {
                if (runtime?.Resource == null || string.IsNullOrWhiteSpace(runtime.SkillKey))
                    continue;

                skills.Add(new JsonObject
                {
                    ["id"] = runtime.SkillKey,
                    ["cooldown_remaining"] = runtime.CooldownRemaining(now)
                });
            }

            return new JsonObject { ["skills"] = skills };
        }

        public void RestoreDurableState(JsonObject state)
        {
            if (state?["skills"] is not JsonArray skills)
                return;

            double now = GetTimeSeconds();
            foreach (JsonNode node in skills)
            {
                if (node is not JsonObject item || item["id"] == null)
                    continue;

                string id = item["id"]?.GetValue<string>();
                SkillRuntime runtime = GetRuntime(id);
                if (runtime == null)
                    continue;

                float remaining = item["cooldown_remaining"]?.GetValue<float>() ?? 0f;
                runtime.SetCooldownReadyTime(now + Mathf.Max(0f, remaining));
            }
        }

        public void StopSkill(SkillRuntime runtime)
        {
            if (runtime == null)
                return;

            runtime.Stop();
            _activeRuntimes.Remove(runtime);
        }

        public SkillRuntime GetRuntime(string skillResourcePath)
        {
            SkillResource resource = LoadSkill(skillResourcePath);
            if (resource == null)
                return null;

            string key = GetSkillKey(resource, skillResourcePath);
            if (string.IsNullOrWhiteSpace(key))
                return null;

            if (!_runtimes.TryGetValue(key, out SkillRuntime runtime))
            {
                runtime = new SkillRuntime(this, resource, skillResourcePath);
                _runtimes[key] = runtime;
            }

            return runtime;
        }

        private void RegisterCharacterGraphSkills()
        {
            CharacterGraphComponent2D component = Owner?.GetComponent<CharacterGraphComponent2D>();
            HfsmGraphAsset graph = component?.Runtime?.Graph ?? component?.CharacterGraph ?? component?.Graph;
            if (graph == null)
                return;

            var visited = new HashSet<HfsmGraphAsset>();
            RegisterGraphSkills(graph, visited);
        }

        private void RegisterGraphSkills(HfsmGraphAsset graph, HashSet<HfsmGraphAsset> visited)
        {
            if (graph == null || !visited.Add(graph))
                return;

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                GraphNodeData node = graph.Nodes[i];
                if (node is CharacterSkillChainNodeData action)
                {
                    if (action.SkillResourcePaths != null)
                    {
                        for (int pathIndex = 0; pathIndex < action.SkillResourcePaths.Count; pathIndex++)
                            RegisterSkillPath(action.SkillResourcePaths[pathIndex]);
                    }
                    continue;
                }

                if (node is HfsmCompositeStateNodeData composite)
                    RegisterGraphSkills(composite.GetSubGraph(), visited);
            }
        }

        private SkillResource LoadSkill(string skillResourcePath)
        {
            if (string.IsNullOrWhiteSpace(skillResourcePath))
                return null;

            if (_resources.TryGetValue(skillResourcePath, out SkillResource cached))
                return cached;

            SkillResource skill = LoadSkillResource(skillResourcePath);
            if (skill == null || !CacheSkill(skillResourcePath, skill))
                return null;

            return skill;
        }

        private static SkillResource LoadSkillResource(string skillResourcePath)
        {
            SkillResource skill = null;
            try
            {
                var resourceModule = ModuleSystem.GetModule<IResourceModule>();
                if (resourceModule != null)
                    skill = resourceModule.LoadAssetOnce<SkillResource>(skillResourcePath);
            }
            catch
            {
            }

            if (skill == null)
                skill = SkillResource.LoadFromPath(skillResourcePath);

            return skill;
        }

        private bool CacheSkill(string lookupPath, SkillResource skill)
        {
            if (skill == null)
                return false;

            string key = GetSkillKey(skill, lookupPath);
            if (!string.IsNullOrWhiteSpace(key) &&
                _resources.TryGetValue(key, out SkillResource existing) &&
                existing != skill &&
                !string.Equals(existing.ResourcePath, skill.ResourcePath, System.StringComparison.Ordinal))
            {
                GD.PushError(
                    $"[SkillManagerComponent2D] Duplicate SkillId '{key}' is used by " +
                    $"'{existing.ResourcePath}' and '{skill.ResourcePath}'.");
                return false;
            }

            _resources[lookupPath] = skill;
            if (!string.IsNullOrWhiteSpace(skill.ResourcePath))
                _resources[skill.ResourcePath] = skill;
            if (!string.IsNullOrWhiteSpace(key))
                _resources[key] = skill;
            return true;
        }

        private static double GetTimeSeconds()
        {
            return Time.GetTicksMsec() * 0.001d;
        }

        private static string GetSkillKey(SkillResource skill, string resourcePath)
        {
            if (!string.IsNullOrWhiteSpace(skill?.SkillId))
                return skill.SkillId;

            if (!string.IsNullOrWhiteSpace(resourcePath))
                return resourcePath;

            return skill?.ResourcePath ?? string.Empty;
        }

        private bool HasActivePolicy(bool movement)
        {
            for (int i = 0; i < _activeRuntimes.Count; i++)
            {
                SkillRuntime runtime = _activeRuntimes[i];
                if (runtime?.Resource == null || !runtime.IsRunning)
                    continue;

                if (movement
                    ? runtime.ExecutionPolicy.BlocksMovement
                    : runtime.ExecutionPolicy.BlocksJump)
                    return true;
            }

            return false;
        }

        private bool CanInterruptActive(SkillExecutionPolicy requested, bool stopActive)
        {
            for (int i = _activeRuntimes.Count - 1; i >= 0; i--)
            {
                SkillRuntime active = _activeRuntimes[i];
                if (active?.Resource == null || !active.IsRunning)
                    continue;

                if (active.ExecutionPolicy.Priority > requested.Priority || !requested.CanInterrupt)
                    return false;

                if (stopActive)
                    active.Stop();
            }

            return true;
        }

        private void StopInterruptibleSkills(SkillExecutionPolicy requested)
        {
            CanInterruptActive(requested, stopActive: true);
            for (int i = _activeRuntimes.Count - 1; i >= 0; i--)
            {
                if (_activeRuntimes[i]?.IsRunning != true)
                    _activeRuntimes.RemoveAt(i);
            }
        }
    }
}
