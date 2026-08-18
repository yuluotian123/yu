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

            string requestKey = ResolveAnimationRequestKey(context);
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

            var advancedContent = new VBoxContainer { Visible = false };
            advancedContent.AddChild(SkillActionEditorHelper.BuildLineEditRow(
                "Request Key",
                RequestKey,
                "Empty = automatic skill/clip key",
                value => RequestKey = value));

            var advancedButton = new Button
            {
                Text = "Advanced >",
                ToggleMode = true,
                TooltipText = "Show optional animation request overrides"
            };
            advancedButton.Toggled += expanded =>
            {
                advancedButton.Text = expanded ? "Advanced v" : "Advanced >";
                advancedContent.Visible = expanded;
            };
            root.AddChild(advancedButton);
            root.AddChild(advancedContent);

            root.AddChild(SkillActionEditorHelper.BuildSpinRow("Priority", AnimationPriority, -1000, 1000, 1, value => AnimationPriority = (int)value));
            root.AddChild(SkillActionEditorHelper.BuildSpinRow("Speed", Speed, -20, 20, 0.05, value => Speed = (float)value));
            root.AddChild(SkillActionEditorHelper.BuildCheckRow("From End", FromEnd, value => FromEnd = value));
            root.AddChild(SkillActionEditorHelper.BuildCheckRow("Restart If Playing", RestartIfPlaying, value => RestartIfPlaying = value));

            return root;
        }

        internal string ResolveAnimationRequestKey(GraphExecutionContext context)
        {
            if (!string.IsNullOrWhiteSpace(RequestKey))
                return RequestKey.Trim();

            FlowTimelineContext timeline = context?.GetUserData<FlowTimelineContext>();
            SkillResource skill = context?.GetUserData<SkillResource>();
            if (timeline != null && !string.IsNullOrWhiteSpace(timeline.ClipId))
            {
                string skillKey = !string.IsNullOrWhiteSpace(skill?.SkillId)
                    ? skill.SkillId.Trim()
                    : skill?.ResourcePath?.Trim();
                if (!string.IsNullOrWhiteSpace(skillKey))
                    return $"skill:{skillKey}:{timeline.ClipId.Trim()}";
            }

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
