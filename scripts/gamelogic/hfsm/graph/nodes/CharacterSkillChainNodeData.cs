using System.Collections.Generic;
using Godot;

namespace GameLogic
{
    public class CharacterSkillChainNodeData : HfsmStateNodeData
    {
        public List<string> SkillResourcePaths { get; set; } = new();
        public string ActionId { get; set; } = string.Empty;
        public int Priority { get; set; }
        public bool BlocksMovement { get; set; } = true;
        public bool BlocksJump { get; set; } = true;
        public bool CanInterrupt { get; set; } = true;

        public SkillExecutionPolicy ExecutionPolicy => new(
            Priority,
            BlocksMovement,
            BlocksJump,
            CanInterrupt);

        public override List<string> GetGraphTypes() => new() { CharacterGraphAsset.CharacterGraphTypeName };
        public override string GetMenuName() => "Skill Chain";
        public override string GetCategory() => "Character / Skills";
        public override Color GetNodeColor() => new(0.75f, 0.45f, 0.9f);
        public override string GetDisplayName() => string.IsNullOrWhiteSpace(StateName)
            ? "Skill Chain"
            : StateName;

        public override bool CanEnter(HfsmRuntime runtime)
        {
            if (runtime == null || SkillResourcePaths == null || SkillResourcePaths.Count == 0)
                return false;

            SkillManagerComponent2D manager = runtime.GetComponent<SkillManagerComponent2D>();
            return manager?.CanStart(SkillResourcePaths[0], ExecutionPolicy) == true;
        }

        public override void Validate(GraphAsset graph, GraphValidationResult result)
        {
            if (SkillResourcePaths == null || SkillResourcePaths.Count == 0)
            {
                result.AddError("Character Action requires at least one SkillResource.", Id);
                return;
            }

            for (int i = 0; i < SkillResourcePaths.Count; i++)
            {
                string path = SkillResourcePaths[i];
                string error = GetSkillResourceError(path);
                if (!string.IsNullOrWhiteSpace(error))
                    result.AddError(error, Id);
            }

            ValidateUniqueSkillIds(graph, result);
        }

        public override void OnEnter(HfsmRuntime runtime)
        {
            base.OnEnter(runtime);
            runtime.SetValue(CharacterGraphBlackboardKeys.LastSkillCompletionLabel, string.Empty);

            var chain = new ChainRuntime(this, runtime);
            runtime.Context.UserData.Add(chain);
            chain.StartNext();
        }

        public override void OnUpdate(HfsmRuntime runtime, double delta)
        {
            ChainRuntime chain = FindRuntime(runtime);
            chain?.Update();
        }

        public override void OnExit(HfsmRuntime runtime)
        {
            ChainRuntime chain = FindRuntime(runtime);
            chain?.Stop();
            if (chain != null)
                runtime.Context.UserData.Remove(chain);

            base.OnExit(runtime);
        }

        public override bool TryGetCompletion(HfsmRuntime runtime, out NodeCompletion completion)
        {
            ChainRuntime chain = FindRuntime(runtime);
            if (chain == null || !chain.IsCompleted)
            {
                completion = default;
                return false;
            }

            completion = NodeCompletion.Completed(chain.ReturnLabel);
            return true;
        }

        protected override void AddCompactFields(VBoxContainer root)
        {
            root.AddChild(new Label { Text = $"Skills: {SkillResourcePaths?.Count ?? 0}" });
            if (!string.IsNullOrWhiteSpace(ActionId))
                root.AddChild(new Label { Text = $"Action: {ActionId}" });

            if (SkillResourcePaths == null || SkillResourcePaths.Count == 0)
            {
                AddErrorLabel(root, "Select at least one SkillResource.");
                return;
            }

            for (int i = 0; i < SkillResourcePaths.Count; i++)
            {
                string error = GetSkillResourceError(SkillResourcePaths[i]);
                if (!string.IsNullOrWhiteSpace(error))
                    AddErrorLabel(root, error);
            }
        }

        public override void CreateUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(220f, 0f) };
            HfsmStateNodeUi.AddStateFields(root, this, context);
            AddEditorFields(root);
            context.GraphNode.AddChild(root);
        }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(280f, 0f) };
            HfsmStateNodeUi.AddStateFields(root, this, context);
            AddEditorFields(root);
            return root;
        }

        private void AddEditorFields(VBoxContainer root)
        {
            var action = new LineEdit { Text = ActionId, PlaceholderText = "Trigger Action id" };
            action.TextChanged += value => ActionId = value.Trim();
            root.AddChild(action);

#if TOOLS
            root.AddChild(new GraphResourcePathListField(
                typeof(SkillResource),
                SkillResourcePaths,
                paths => SkillResourcePaths = paths,
                resource => resource is SkillResource skill && !string.IsNullOrWhiteSpace(skill.DisplayName)
                    ? skill.DisplayName
                    : null));
#else
            root.AddChild(new Label { Text = $"Skills: {SkillResourcePaths?.Count ?? 0}" });
#endif

            var priority = new SpinBox
            {
                Value = Priority,
                Step = 1,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            var priorityRow = new HBoxContainer();
            priorityRow.AddChild(new Label { Text = "Priority" });
            priorityRow.AddChild(priority);
            priority.ValueChanged += value => Priority = (int)value;
            root.AddChild(priorityRow);

            AddCheckBox(root, "Blocks movement", BlocksMovement, value => BlocksMovement = value);
            AddCheckBox(root, "Blocks jump", BlocksJump, value => BlocksJump = value);
            AddCheckBox(root, "Can interrupt", CanInterrupt, value => CanInterrupt = value);
        }

        private static void AddCheckBox(VBoxContainer root, string text, bool value, System.Action<bool> setter)
        {
            var check = new CheckBox { Text = text, ButtonPressed = value };
            check.Toggled += toggled => setter(toggled);
            root.AddChild(check);
        }

        private static void AddErrorLabel(VBoxContainer root, string message)
        {
            var label = new Label
            {
                Text = message,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            label.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
            root.AddChild(label);
        }

        private static string GetSkillResourceError(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "SkillResource path is empty.";

            if (!ResourceLoader.Exists(path))
                return $"Skill resource does not exist: {path}";

            SkillResource skill = SkillResource.LoadFromPath(path);
            if (skill == null)
                return $"Resource is not a SkillResource: {path}";

            if (skill.Graph == null)
                return $"SkillResource has no SkillFlowGraph: {path}";

            return skill.Graph.Validate(out GraphValidationResult validation)
                ? string.Empty
                : $"SkillResource has an invalid SkillFlowGraph: {path}\n{validation.ToDisplayText()}";
        }

        private void ValidateUniqueSkillIds(GraphAsset graph, GraphValidationResult result)
        {
            if (graph == null)
                return;

            var pathsById = new Dictionary<string, string>(System.StringComparer.Ordinal);
            foreach (GraphNodeData node in graph.Nodes)
            {
                if (node is not CharacterSkillChainNodeData action || action.SkillResourcePaths == null)
                    continue;

                for (int i = 0; i < action.SkillResourcePaths.Count; i++)
                {
                    string path = action.SkillResourcePaths[i];
                    SkillResource skill = SkillResource.LoadFromPath(path);
                    if (skill == null || string.IsNullOrWhiteSpace(skill.SkillId))
                        continue;

                    if (pathsById.TryGetValue(skill.SkillId, out string existingPath) &&
                        !string.Equals(existingPath, path, System.StringComparison.Ordinal))
                    {
                        if (ReferenceEquals(action, this))
                        {
                            result.AddError(
                                $"SkillId '{skill.SkillId}' is used by both '{existingPath}' and '{path}'.",
                                Id);
                        }
                    }
                    else
                    {
                        pathsById[skill.SkillId] = path;
                    }
                }
            }
        }

        private ChainRuntime FindRuntime(HfsmRuntime runtime)
        {
            var values = runtime?.Context?.GetUserDataAll<ChainRuntime>();
            if (values == null)
                return null;

            for (int i = 0; i < values.Count; i++)
            {
                if (values[i]?.Owner == this)
                    return values[i];
            }

            return null;
        }

        private sealed class ChainRuntime
        {
            private readonly HfsmRuntime _hfsm;
            private int _index;
            private SkillRuntime _active;

            public ChainRuntime(CharacterSkillChainNodeData owner, HfsmRuntime hfsm)
            {
                Owner = owner;
                _hfsm = hfsm;
            }

            public CharacterSkillChainNodeData Owner { get; }
            public bool IsCompleted { get; private set; }
            public string ReturnLabel { get; private set; } = "Finished";

            public void StartNext()
            {
                if (_index >= Owner.SkillResourcePaths.Count)
                {
                    IsCompleted = true;
                    _hfsm.SetValue(CharacterGraphBlackboardKeys.LastSkillCompletionLabel, ReturnLabel);
                    return;
                }

                string path = Owner.SkillResourcePaths[_index];
                _active = _hfsm.GetComponent<SkillManagerComponent2D>()?.StartSkill(
                    path,
                    _hfsm,
                    Owner.ExecutionPolicy);
                if (_active == null)
                {
                    IsCompleted = true;
                    ReturnLabel = "Failed";
                    _hfsm.SetValue(CharacterGraphBlackboardKeys.LastSkillCompletionLabel, ReturnLabel);
                }
            }

            public void Update()
            {
                if (IsCompleted || _active == null)
                    return;

                if (!_active.IsCompleted)
                    return;

                string label = _active.LastReturnLabel;
                if (string.Equals(label, "Cancelled", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(label, "Interrupted", System.StringComparison.OrdinalIgnoreCase))
                {
                    IsCompleted = true;
                    ReturnLabel = "Cancelled";
                    _hfsm.SetValue(CharacterGraphBlackboardKeys.LastSkillCompletionLabel, ReturnLabel);
                    return;
                }

                _index++;
                _active = null;
                _hfsm.SetValue(CharacterGraphBlackboardKeys.LastSkillCompletionLabel, label);
                StartNext();
            }

            public void Stop()
            {
                if (_active != null && _active.IsRunning)
                    _hfsm.GetComponent<SkillManagerComponent2D>()?.StopSkill(_active);
            }
        }
    }
}
