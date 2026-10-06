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
            string stateName = string.IsNullOrWhiteSpace(StateName) ? "动画状态" : StateName;
            return string.IsNullOrWhiteSpace(AnimationName)
                ? $"{stateName} [Animation]"
                : $"{stateName} [{AnimationName}]";
        }

        public override string GetMenuName() => "动画状态 / Animation State";
        public override string GetCategory() => "HFSM";
        public override Color GetNodeColor() => IsDefault ? new Color(0.3f, 0.75f, 0.45f) : new Color(0.25f, 0.62f, 0.88f);
        public override System.Collections.Generic.List<string> GetSearchKeywords() => new() { "animation", "animator", "sprite", "动画", "播放" };

        public override void CreateNodeUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(170, 0) };
            root.AddChild(new Label { Text = IsDefault ? "默认动画状态" : "动画状态" });
            AddCompactFields(root);
            context.GraphNode.AddChild(root);
        }

        protected override void AddCompactFields(VBoxContainer root)
        {
            root.AddChild(new Label
            {
                Text = string.IsNullOrWhiteSpace(AnimationName) ? $"动画：跟随状态名 ({StateName})" : $"动画：{AnimationName}",
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
            GetAnimationInstance(runtime)?.ClearAnimationRequest(GetRequestKey());
            base.OnExit(runtime);
        }

        public override void CreateUI(GraphEditorContext context)
        {
            context.GraphNode.AddChild(CreateInspectorUI(context));
        }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
#if TOOLS
            return new GraphAnimationStateInspector(this, context);
#else
            return base.CreateInspectorUI(context);
#endif
        }

        private void RequestAnimation(HfsmRuntime runtime)
        {
            GetAnimationInstance(runtime)?.RequestAnimation(
                GetRequestKey(),
                GetAnimationName(),
                AnimationPriority,
                Speed,
                FromEnd,
                RestartIfPlaying);
        }

        private static CharacterAnimationInstance GetAnimationInstance(HfsmRuntime runtime)
        {
            if (runtime?.Context == null)
                return null;
            return runtime.Context.GetUserData<CharacterAnimationInstance>() ??
                   runtime.Context.GetUserData<CharacterAnimationComponent3D>()?.AnimationInstance;
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
