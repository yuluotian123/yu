using Godot;

namespace GameLogic
{
    public class SkillPlayAnimationAction : GraphActionBase
    {
        public SkillAnimationTargetKind TargetKind { get; set; } = SkillAnimationTargetKind.Auto;
        public SkillAnimationCommand Command { get; set; } = SkillAnimationCommand.Play;
        public string TargetPath { get; set; } = string.Empty;
        public string AnimationName { get; set; } = string.Empty;
        public float BlendTime { get; set; } = -1f;
        public float Speed { get; set; } = 1f;
        public bool FromEnd { get; set; }
        public bool RestartIfPlaying { get; set; } = true;
        public bool KeepStateOnStop { get; set; }
        public bool AdvanceImmediately { get; set; }

        public override string Description
        {
            get
            {
                string animation = string.IsNullOrWhiteSpace(AnimationName) ? "(current)" : AnimationName;
                return $"{Command} Animation {animation}";
            }
        }

        public override void Execute(GraphExecutionContext context)
        {
            if (ShouldSkipForTimelineUpdate(context))
                return;

            GameObject2D owner = SkillActionRuntimeHelper.GetGameObject(context);
            if (owner == null)
                return;

            Node target = ResolveTarget(owner);
            switch (target)
            {
                case AnimationPlayer animationPlayer:
                    ExecuteAnimationPlayer(context, animationPlayer);
                    break;
                case AnimatedSprite2D animatedSprite:
                    ExecuteAnimatedSprite(animatedSprite);
                    break;
            }
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 4);

            root.AddChild(SkillActionEditorHelper.BuildEnumRow("Command", Command, value => Command = value));
            root.AddChild(SkillActionEditorHelper.BuildEnumRow("Target", TargetKind, value => TargetKind = value));
            root.AddChild(SkillActionEditorHelper.BuildLineEditRow(
                "Target Path",
                TargetPath,
                "Empty = first matching animation node",
                value => TargetPath = value));
            root.AddChild(SkillActionEditorHelper.BuildLineEditRow(
                "Animation",
                AnimationName,
                "Animation name",
                value => AnimationName = value));
            root.AddChild(SkillActionEditorHelper.BuildSpinRow("Blend", BlendTime, -1, 60, 0.01, value => BlendTime = (float)value));
            root.AddChild(SkillActionEditorHelper.BuildSpinRow("Speed", Speed, -20, 20, 0.05, value => Speed = (float)value));
            root.AddChild(SkillActionEditorHelper.BuildCheckRow("From End", FromEnd, value => FromEnd = value));
            root.AddChild(SkillActionEditorHelper.BuildCheckRow("Restart If Playing", RestartIfPlaying, value => RestartIfPlaying = value));
            root.AddChild(SkillActionEditorHelper.BuildCheckRow("Keep State On Stop", KeepStateOnStop, value => KeepStateOnStop = value));
            root.AddChild(SkillActionEditorHelper.BuildCheckRow("Advance Immediately", AdvanceImmediately, value => AdvanceImmediately = value));

            return root;
        }

        private bool ShouldSkipForTimelineUpdate(GraphExecutionContext context)
        {
            FlowTimelineContext timeline = context?.GetUserData<FlowTimelineContext>();
            return timeline?.Phase == FlowTimelinePhase.Update &&
                   Command != SkillAnimationCommand.SeekToTimeline;
        }

        private void ExecuteAnimationPlayer(GraphExecutionContext context, AnimationPlayer animationPlayer)
        {
            switch (Command)
            {
                case SkillAnimationCommand.Play:
                    PlayAnimationPlayer(animationPlayer);
                    break;
                case SkillAnimationCommand.Stop:
                    animationPlayer.Stop(KeepStateOnStop);
                    break;
                case SkillAnimationCommand.Pause:
                    animationPlayer.Pause();
                    break;
                case SkillAnimationCommand.SeekToTimeline:
                    SeekAnimationPlayerToTimeline(context, animationPlayer);
                    break;
            }
        }

        private void PlayAnimationPlayer(AnimationPlayer animationPlayer)
        {
            StringName animation = GetAnimationName();
            if (!RestartIfPlaying &&
                animationPlayer.IsPlaying() &&
                IsSameAnimation(animationPlayer.CurrentAnimation, animation))
            {
                return;
            }

            animationPlayer.Play(animation, BlendTime, Speed, FromEnd);
            if (AdvanceImmediately)
                animationPlayer.Advance(0);
        }

        private void SeekAnimationPlayerToTimeline(GraphExecutionContext context, AnimationPlayer animationPlayer)
        {
            FlowTimelineContext timeline = context.GetUserData<FlowTimelineContext>();
            if (timeline == null || animationPlayer.CurrentAnimationLength <= 0f)
                return;

            float normalizedTime = timeline.ClipDuration > 0f
                ? timeline.ClipNormalizedTime
                : timeline.NormalizedTime;
            double position = animationPlayer.CurrentAnimationLength * normalizedTime;
            animationPlayer.Seek(position, true);
            if (AdvanceImmediately)
                animationPlayer.Advance(0);
        }

        private void ExecuteAnimatedSprite(AnimatedSprite2D animatedSprite)
        {
            StringName animation = GetAnimationName();
            switch (Command)
            {
                case SkillAnimationCommand.Play:
                    if (!RestartIfPlaying &&
                        animatedSprite.IsPlaying() &&
                        IsSameAnimation(animatedSprite.Animation, animation))
                    {
                        return;
                    }

                    animatedSprite.Play(animation, Speed, FromEnd);
                    break;
                case SkillAnimationCommand.Stop:
                    animatedSprite.Stop();
                    break;
                case SkillAnimationCommand.Pause:
                    animatedSprite.Pause();
                    break;
            }
        }

        private Node ResolveTarget(GameObject2D owner)
        {
            Node root = owner;
            if (!string.IsNullOrWhiteSpace(TargetPath))
            {
                root = owner.GetNodeOrNull<Node>(TargetPath);
                if (root == null)
                    return null;
            }

            if (TargetMatches(root))
                return root;

            return TargetKind switch
            {
                SkillAnimationTargetKind.AnimationPlayer => SkillActionRuntimeHelper.FindFirst<AnimationPlayer>(root),
                SkillAnimationTargetKind.AnimatedSprite2D => SkillActionRuntimeHelper.FindFirst<AnimatedSprite2D>(root),
                _ => ResolveFirstAnimationNode(root)
            };
        }

        private static Node ResolveFirstAnimationNode(Node root)
        {
            Node animationPlayer = SkillActionRuntimeHelper.FindFirst<AnimationPlayer>(root);
            return animationPlayer ?? SkillActionRuntimeHelper.FindFirst<AnimatedSprite2D>(root);
        }

        private bool TargetMatches(Node node)
        {
            return TargetKind switch
            {
                SkillAnimationTargetKind.AnimationPlayer => node is AnimationPlayer,
                SkillAnimationTargetKind.AnimatedSprite2D => node is AnimatedSprite2D,
                _ => node is AnimationPlayer or AnimatedSprite2D
            };
        }

        private StringName GetAnimationName()
        {
            return string.IsNullOrWhiteSpace(AnimationName)
                ? default
                : new StringName(AnimationName);
        }

        private static bool IsSameAnimation(StringName current, StringName target)
        {
            if (target == default)
                return true;

            return current == target;
        }
    }
}
