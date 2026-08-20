using Godot;

namespace GameLogic
{
    public class HfsmAnimationStateNodeData : HfsmStateNodeData
    {
        public string AnimationName { get; set; } = string.Empty;
        public string RequestKey { get; set; } = string.Empty;
        public int AnimationPriority { get; set; }
        public float Speed { get; set; } = 1f;
        public bool FromEnd { get; set; }
        public bool RestartIfPlaying { get; set; } = true;

        public override string GetDisplayName()
        {
            string stateName = string.IsNullOrWhiteSpace(StateName) ? "Animation State" : StateName;
            return string.IsNullOrWhiteSpace(AnimationName)
                ? $"{stateName} [Animation]"
                : $"{stateName} [{AnimationName}]";
        }

        public override string GetMenuName() => "Animation State";
        public override string GetCategory() => "HFSM";
        public override Color GetNodeColor() => IsDefault ? new Color(0.3f, 0.75f, 0.45f) : new Color(0.25f, 0.62f, 0.88f);
        public override System.Collections.Generic.List<string> GetSearchKeywords() => new() { "animation", "animator", "sprite" };

        protected override void AddCompactFields(VBoxContainer root)
        {
            root.AddChild(new Label
            {
                Text = string.IsNullOrWhiteSpace(AnimationName) ? "Animation: state name" : $"Animation: {AnimationName}",
                ClipText = true,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            });
        }

        public override void OnEnter(HfsmRuntime runtime)
        {
            base.OnEnter(runtime);
            RequestAnimation(runtime);
        }

        public override void OnUpdate(HfsmRuntime runtime, double delta)
        {
            RequestAnimation(runtime);
        }

        public override void OnExit(HfsmRuntime runtime)
        {
            runtime?.Context.GetUserData<CharacterAnimationComponent2D>()?.ClearAnimationRequest(GetRequestKey());
            base.OnExit(runtime);
        }

        public override void CreateUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(210f, 0f) };
            AddStateFields(root, context);

            root.AddChild(new HSeparator());
            AddAnimationFields(root, context);

            context.GraphNode.AddChild(root);
        }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(260f, 0f) };
            root.AddThemeConstantOverride("separation", 6);
            AddStateFields(root, context);
            root.AddChild(new HSeparator());
            AddAnimationFields(root, context);
            return root;
        }

        private void AddAnimationFields(VBoxContainer root, GraphEditorContext context)
        {
            root.AddChild(new Label { Text = "Animation Request" });
            root.AddChild(GraphEditorUi.BuildLineEditRow(
                "Animation",
                AnimationName,
                "SpriteFrames animation",
                value =>
                {
                    AnimationName = value;
                    if (context.GraphNode != null)
                        context.GraphNode.Title = GetDisplayName();
                }));
            root.AddChild(GraphEditorUi.BuildLineEditRow(
                "Request Key",
                RequestKey,
                "Empty = state id",
                value => RequestKey = value));
            root.AddChild(GraphEditorUi.BuildSpinRow("Priority", AnimationPriority, -1000, 1000, 1, value => AnimationPriority = (int)value));
            root.AddChild(GraphEditorUi.BuildSpinRow("Speed", Speed, -20, 20, 0.05, value => Speed = (float)value));
            root.AddChild(GraphEditorUi.BuildCheckRow("From End", FromEnd, value => FromEnd = value));
            root.AddChild(GraphEditorUi.BuildCheckRow("Restart If Playing", RestartIfPlaying, value => RestartIfPlaying = value));
        }

        private void RequestAnimation(HfsmRuntime runtime)
        {
            runtime?.Context.GetUserData<CharacterAnimationComponent2D>()?.RequestAnimation(
                GetRequestKey(),
                GetAnimationName(),
                AnimationPriority,
                Speed,
                FromEnd,
                RestartIfPlaying);
        }

        private string GetAnimationName()
        {
            if (!string.IsNullOrWhiteSpace(AnimationName))
                return AnimationName;

            return StateName;
        }

        private string GetRequestKey()
        {
            if (!string.IsNullOrWhiteSpace(RequestKey))
                return RequestKey;

            return string.IsNullOrWhiteSpace(Id) ? StateName : $"hfsm:{Id}";
        }
    }
}
