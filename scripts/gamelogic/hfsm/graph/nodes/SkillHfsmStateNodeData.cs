using Framework;
using Godot;

namespace GameLogic
{
    public class SkillHfsmStateNodeData : HfsmStateNodeData
    {
        public string SkillResourcePath { get; set; } = string.Empty;

        public override string GetDisplayName()
        {
            string stateName = string.IsNullOrWhiteSpace(StateName) ? "Skill" : StateName;
            return string.IsNullOrWhiteSpace(SkillResourcePath)
                ? $"{stateName} [Skill]"
                : $"{stateName} [{SkillResourcePath.GetFile().GetBaseName()}]";
        }

        public override Color GetNodeColor() => IsDefault ? new Color(0.35f, 0.8f, 0.5f) : new Color(0.7f, 0.45f, 0.9f);
        public override string GetOutputPortName(int port) => "Completed";

        public override bool CanEnter(HfsmRuntime runtime)
        {
            SkillManagerComponent2D skillManager = runtime?.GetComponent<SkillManagerComponent2D>();
            return skillManager?.CanStart(SkillResourcePath) == true;
        }

        public override void OnEnter(HfsmRuntime runtime)
        {
            base.OnEnter(runtime);
            RemoveExistingRuntime(runtime);

            SkillManagerComponent2D skillManager = runtime?.GetComponent<SkillManagerComponent2D>();
            SkillRuntime skillRuntime = skillManager?.StartSkill(SkillResourcePath, runtime);
            if (skillRuntime == null)
            {
                GD.PushWarning($"[SkillHfsmStateNodeData] Failed to start skill: {SkillResourcePath}");
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

            var skillPathEdit = new LineEdit
            {
                PlaceholderText = "Skill resource path",
                Text = SkillResourcePath
            };
            skillPathEdit.TextChanged += value => SkillResourcePath = value;
            root.AddChild(skillPathEdit);

            context.GraphNode.AddChild(root);
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
