using Godot;

namespace GameLogic
{
    public class AbilityPlayAnimationAction : GraphActionBase
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
            GameObject2D owner = AbilityActionRuntimeHelper.GetGameObject(context);
            if (owner == null)
                return;

            CharacterAnimationComponent2D animationComponent =
                owner.GetComponent<CharacterAnimationComponent2D>();
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

            root.AddChild(GraphEditorUi.BuildLineEditRow(
                "Animation",
                AnimationName,
                "Animation name",
                value => AnimationName = value));

            var advancedContent = new VBoxContainer { Visible = false };
            advancedContent.AddChild(GraphEditorUi.BuildLineEditRow(
                "Request Key",
                RequestKey,
                "Empty = automatic ability/clip key",
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

            root.AddChild(GraphEditorUi.BuildSpinRow("Priority", AnimationPriority, -1000, 1000, 1, value => AnimationPriority = (int)value));
            root.AddChild(GraphEditorUi.BuildSpinRow("Speed", Speed, -20, 20, 0.05, value => Speed = (float)value));
            root.AddChild(GraphEditorUi.BuildCheckRow("From End", FromEnd, value => FromEnd = value));
            root.AddChild(GraphEditorUi.BuildCheckRow("Restart If Playing", RestartIfPlaying, value => RestartIfPlaying = value));

            return root;
        }

        internal string ResolveAnimationRequestKey(GraphExecutionContext context)
        {
            if (!string.IsNullOrWhiteSpace(RequestKey))
                return RequestKey.Trim();

            FlowTimelineContext timeline = context?.GetUserData<FlowTimelineContext>();
            AbilityResource ability = context?.GetUserData<AbilityResource>();
            if (timeline != null && !string.IsNullOrWhiteSpace(timeline.ClipId))
            {
                string abilityKey = !string.IsNullOrWhiteSpace(ability?.AbilityId)
                    ? ability.AbilityId.Trim()
                    : ability?.ResourcePath?.Trim();
                if (!string.IsNullOrWhiteSpace(abilityKey))
                    return $"ability:{abilityKey}:{timeline.ClipId.Trim()}";
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
