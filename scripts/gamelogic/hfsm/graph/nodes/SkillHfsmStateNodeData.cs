using Framework;
using Godot;
using System.Text.Json.Serialization;

namespace GameLogic
{
    public class SkillHfsmStateNodeData : HfsmStateNodeData
    {
        private SkillResource _skillResource;

        public string SkillResourcePath { get; set; } = string.Empty;

        /// <summary>
        /// 编辑器中的技能资源引用。该属性不写入 GraphJson，保存时只持久化资源路径。
        /// </summary>
        [JsonIgnore]
        public SkillResource SkillResource
        {
            get
            {
                string path = GetSkillResourcePath();
                if (_skillResource != null && _skillResource.ResourcePath == path)
                    return _skillResource;

                if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
                    return _skillResource;

                _skillResource = SkillResource.LoadFromPath(path);
                return _skillResource;
            }
            set
            {
                _skillResource = value;
                SkillResourcePath = value?.ResourcePath ?? string.Empty;
            }
        }

        public override string GetDisplayName()
        {
            string stateName = string.IsNullOrWhiteSpace(StateName) ? "Skill" : StateName;
            string skillName = GetSkillDisplayName();
            return string.IsNullOrWhiteSpace(skillName)
                ? $"{stateName} [Skill]"
                : $"{stateName} [{skillName}]";
        }

        public override Color GetNodeColor() => IsDefault ? new Color(0.35f, 0.8f, 0.5f) : new Color(0.7f, 0.45f, 0.9f);
        public override string GetOutputPortName(int port) => "Completed";

        public override bool CanEnter(HfsmRuntime runtime)
        {
            SkillManagerComponent2D skillManager = runtime?.GetComponent<SkillManagerComponent2D>();
            return skillManager?.CanStart(GetSkillResourcePath()) == true;
        }

        public override void OnEnter(HfsmRuntime runtime)
        {
            base.OnEnter(runtime);
            RemoveExistingRuntime(runtime);

            SkillManagerComponent2D skillManager = runtime?.GetComponent<SkillManagerComponent2D>();
            string skillResourcePath = GetSkillResourcePath();
            SkillRuntime skillRuntime = skillManager?.StartSkill(skillResourcePath, runtime);
            if (skillRuntime == null)
            {
                GD.PushWarning($"[SkillHfsmStateNodeData] Failed to start skill: {skillResourcePath}");
                runtime?.Context?.UserData.Add(new ActiveSkillStateRuntime(Id, null) { Completed = true, ReturnLabel = "Failed" });
                return;
            }

            runtime.Context.UserData.Add(new ActiveSkillStateRuntime(Id, skillRuntime));
        }

        public override void OnUpdate(HfsmRuntime runtime, double delta)
        {
        }

        public override void OnExit(HfsmRuntime runtime)
        {
            ActiveSkillStateRuntime activeSkill = GetActiveRuntime(runtime);
            if (activeSkill?.Runtime != null)
                runtime.GetComponent<SkillManagerComponent2D>()?.StopSkill(activeSkill.Runtime);

            if (activeSkill != null)
                runtime.Context.UserData.Remove(activeSkill);
        }

        public override bool TryGetCompletion(HfsmRuntime runtime, out NodeCompletion completion)
        {
            ActiveSkillStateRuntime activeSkill = GetActiveRuntime(runtime);
            if (activeSkill?.IsCompleted == true)
            {
                completion = NodeCompletion.Completed(activeSkill.GetReturnLabel());
                return true;
            }

            completion = default;
            return false;
        }

        public override void CreateUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(190f, 0f) };
            AddStateFields(root, context);
            root.AddChild(new HSeparator());

#if TOOLS
            root.AddChild(new GraphResourcePathField(
                typeof(SkillResource),
                SkillResourcePath,
                path =>
                {
                    SetSkillResourcePath(path);
                    if (context.GraphNode != null)
                        context.GraphNode.Title = GetDisplayName();
                },
                resource => resource is SkillResource skill && !string.IsNullOrWhiteSpace(skill.DisplayName)
                    ? skill.DisplayName
                    : null));
#else
            root.AddChild(new Label { Text = string.IsNullOrWhiteSpace(SkillResourcePath) ? "No Skill" : SkillResourcePath });
#endif

            context.GraphNode.AddChild(root);
        }

        private void SetSkillResourcePath(string path)
        {
            SkillResourcePath = path ?? string.Empty;
            _skillResource = null;
        }

        private string GetSkillResourcePath()
        {
            if (!string.IsNullOrWhiteSpace(SkillResourcePath))
                return SkillResourcePath;

            return _skillResource?.ResourcePath ?? string.Empty;
        }

        private string GetSkillDisplayName()
        {
            SkillResource skill = SkillResource;
            if (!string.IsNullOrWhiteSpace(skill?.DisplayName))
                return skill.DisplayName;

            string path = GetSkillResourcePath();
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.GetFile().GetBaseName();
        }

        private ActiveSkillStateRuntime GetActiveRuntime(HfsmRuntime runtime)
        {
            if (runtime?.Context == null)
                return null;

            var activeSkills = runtime.Context.GetUserDataAll<ActiveSkillStateRuntime>();
            for (int i = 0; i < activeSkills.Count; i++)
            {
                if (activeSkills[i]?.NodeId == Id)
                    return activeSkills[i];
            }

            return null;
        }

        private void RemoveExistingRuntime(HfsmRuntime runtime)
        {
            ActiveSkillStateRuntime activeSkill = GetActiveRuntime(runtime);
            if (activeSkill == null)
                return;

            if (activeSkill.Runtime != null)
                runtime.GetComponent<SkillManagerComponent2D>()?.StopSkill(activeSkill.Runtime);

            runtime.Context.UserData.Remove(activeSkill);
        }

        private sealed class ActiveSkillStateRuntime
        {
            public ActiveSkillStateRuntime(string nodeId, SkillRuntime runtime)
            {
                NodeId = nodeId;
                Runtime = runtime;
            }

            public string NodeId { get; }
            public SkillRuntime Runtime { get; }
            public bool Completed { get; set; }
            public string ReturnLabel { get; set; } = "Finished";

            public bool IsCompleted => Completed || Runtime?.IsCompleted == true;

            public string GetReturnLabel()
            {
                if (!string.IsNullOrWhiteSpace(Runtime?.LastReturnLabel))
                    return Runtime.LastReturnLabel;

                return string.IsNullOrWhiteSpace(ReturnLabel) ? "Finished" : ReturnLabel;
            }
        }
    }
}
