using Godot;

namespace GameLogic
{
    public class SkillPlayAnimationAction : GraphActionBase
    {
        public string AnimationName { get; set; } = string.Empty;
        public string RequestKey { get; set; } = string.Empty;
        public int AnimationPriority { get; set; } = 100;
        public float Speed { get; set; } = 1f;
        public bool FromEnd { get; set; }
        public bool RestartIfPlaying { get; set; } = true;

        public override string Description
        {
            get
            {
                string animation = string.IsNullOrWhiteSpace(AnimationName) ? "(empty)" : AnimationName;
                return $"Request Animation {animation} [P{AnimationPriority}]";
            }
        }

        public override void Execute(GraphExecutionContext context)
        {
            GameObject2D owner = SkillActionRuntimeHelper.GetGameObject(context);
            if (owner == null)
                return;

            SpriteAnimationComponent2D animationComponent =
                owner.GetComponent<SpriteAnimationComponent2D>();
            if (animationComponent == null)
                return;

            string requestKey = GetAnimationRequestKey(context);
            FlowTimelineContext timeline = context?.GetUserData<FlowTimelineContext>();
            if (timeline?.Phase is FlowTimelinePhase.Complete or FlowTimelinePhase.Cancel)
            {
                animationComponent.ClearAnimationRequest(requestKey);
                return;
            }

            animationComponent.RequestAnimation(
                requestKey,
                AnimationName,
                AnimationPriority,
                Speed,
                FromEnd,
                RestartIfPlaying);
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 4);

            root.AddChild(SkillActionEditorHelper.BuildLineEditRow(
                "Animation",
                AnimationName,
                "Animation name",
                value => AnimationName = value));
            root.AddChild(SkillActionEditorHelper.BuildLineEditRow(
                "Request Key",
                RequestKey,
                "Empty = clip or animation name",
                value => RequestKey = value));
            root.AddChild(SkillActionEditorHelper.BuildSpinRow("Priority", AnimationPriority, -1000, 1000, 1, value => AnimationPriority = (int)value));
            root.AddChild(SkillActionEditorHelper.BuildSpinRow("Speed", Speed, -20, 20, 0.05, value => Speed = (float)value));
            root.AddChild(SkillActionEditorHelper.BuildCheckRow("From End", FromEnd, value => FromEnd = value));
            root.AddChild(SkillActionEditorHelper.BuildCheckRow("Restart If Playing", RestartIfPlaying, value => RestartIfPlaying = value));

            return root;
        }

        private string GetAnimationRequestKey(GraphExecutionContext context)
        {
            if (!string.IsNullOrWhiteSpace(RequestKey))
                return RequestKey.Trim();

            FlowTimelineContext timeline = context?.GetUserData<FlowTimelineContext>();
            if (timeline != null && !string.IsNullOrWhiteSpace(timeline.ClipName))
            {
                string track = string.IsNullOrWhiteSpace(timeline.TrackName)
                    ? "timeline"
                    : timeline.TrackName.Trim();
                return $"clip:{track}/{timeline.ClipName.Trim()}";
            }

            return AnimationName;
        }
    }
}
