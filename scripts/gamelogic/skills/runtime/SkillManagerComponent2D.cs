using Framework;
using Godot;
using Godot.Collections;
using System.Collections.Generic;

namespace GameLogic
{
    [GlobalClass]
    public partial class SkillManagerComponent2D : Component2D
    {
        private readonly System.Collections.Generic.Dictionary<string, SkillResource> _resources = new(System.StringComparer.Ordinal);
        private readonly System.Collections.Generic.Dictionary<string, SkillRuntime> _runtimes = new(System.StringComparer.Ordinal);
        private readonly List<SkillRuntime> _activeRuntimes = new();

        [Export] public Array<SkillResource> Skills { get; set; } = new();

        public override int Priority => ComponentPriority.Motor + 1;

        public override void OnInit()
        {
            RegisterConfiguredSkills();
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

        public bool CanStart(string skillResourcePath)
        {
            SkillRuntime runtime = GetRuntime(skillResourcePath);
            return runtime != null && runtime.CanStart(GetTimeSeconds());
        }

        public SkillRuntime StartSkill(string skillResourcePath, HfsmRuntime hfsmRuntime)
        {
            if (hfsmRuntime == null)
                return null;

            SkillRuntime runtime = GetRuntime(skillResourcePath);
            if (runtime == null)
                return null;

            if (!runtime.Start(hfsmRuntime, GetTimeSeconds()))
                return null;

            if (!_activeRuntimes.Contains(runtime))
                _activeRuntimes.Add(runtime);

            return runtime;
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

        private void RegisterConfiguredSkills()
        {
            if (Skills == null)
                return;

            for (int i = 0; i < Skills.Count; i++)
            {
                SkillResource skill = Skills[i];
                if (skill == null)
                    continue;

                string key = GetSkillKey(skill, skill.ResourcePath);
                if (!string.IsNullOrWhiteSpace(key))
                    _resources[key] = skill;
            }
        }

        private SkillResource LoadSkill(string skillResourcePath)
        {
            if (string.IsNullOrWhiteSpace(skillResourcePath))
                return null;

            if (_resources.TryGetValue(skillResourcePath, out SkillResource cached))
                return cached;

            SkillResource skill = null;
            try
            {
                var resourceModule = ModuleSystem.GetModule<IResourceModule>();
                if (resourceModule != null)
                {
                    var handle = resourceModule.LoadAsset<SkillResource>(skillResourcePath);
                    skill = handle.Asset;
                    handle.Release();
                }
            }
            catch
            {
            }

            if (skill == null)
                skill = SkillResource.LoadFromPath(skillResourcePath);

            if (skill == null)
                return null;

            _resources[skillResourcePath] = skill;
            string key = GetSkillKey(skill, skillResourcePath);
            if (!string.IsNullOrWhiteSpace(key))
                _resources[key] = skill;

            return skill;
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
    }
}
