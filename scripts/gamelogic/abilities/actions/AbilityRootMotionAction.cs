using System.Collections.Generic;
using Godot;

namespace GameLogic
{
    [GraphCallable("Animation Root Motion", "动画与表现 / 动画", GraphCallableUsage.Timeline,
        TimelineKind = GraphTimelineActionKind.Clip, ChineseName = "动画前向位移")]
    public class AbilityRootMotionAction : GraphActionBase
    {
        public GraphActionComponentReference Movement { get; set; } = new();
        // One cumulative distance per uniform animation frame, in metres.
        public List<float> DistanceSamples { get; set; } = new();
        public int MotionPriority { get; set; } = 100;
        public override string Description => $"Animation Motion ({DistanceSamples?.Count ?? 0} frames)";

        public override void Execute(GraphExecutionContext context)
        {
            FlowTimelineContext timeline = context?.GetUserData<FlowTimelineContext>();
            AbilityRuntime runtime = context?.GetUserData<AbilityRuntime>();
            if (timeline == null || runtime == null || DistanceSamples == null || DistanceSamples.Count < 2)
                return;
            if (!GraphActionComponentResolver.TryResolve(context, Movement, nameof(AbilityRootMotionAction),
                    out CharacterMovementComponent3D movement, out string error))
            {
                GD.PushError($"[AbilityRootMotionAction] {error}");
                return;
            }

            string key = $"RootMotion:{timeline.ClipId}";
            if (timeline.Phase == FlowTimelinePhase.Cancel)
            {
                // Discard any motion submitted earlier in this physics tick.
                movement.RequestVelocityOverride(new(Vector3.Zero, true, false, MotionPriority,
                    restoreAfterMovement: true));
                runtime.SetData(key, null);
                return;
            }
            if (timeline.Phase == FlowTimelinePhase.Complete)
            {
                // The last Update still needs to reach MoveAndSlide this tick.
                runtime.SetData(key, null);
                return;
            }
            if (!runtime.TryGetData(key, out MotionState state))
            {
                Vector3 direction = movement.MovementSpace == CharacterMovementSpace.SideView
                    ? Vector3.Right * movement.Facing
                    : movement.FacingDirection;
                direction.Y = 0f;
                state = new MotionState { Direction = direction.Normalized(), Distance = DistanceSamples[0] };
                runtime.SetData(key, state);
            }
            if (timeline.Phase != FlowTimelinePhase.Update || timeline.Delta <= 0f)
                return;

            float distance = Sample(timeline.ClipTime, timeline.ClipDuration);
            float advance = distance - state.Distance;
            state.Distance = distance;
            movement.RequestVelocityOverride(new(state.Direction * (advance / timeline.Delta),
                true, false, MotionPriority, restoreAfterMovement: true));
        }

        private float Sample(float time, float duration)
        {
            // Frame N starts at N / frameCount of the clip; the last frame holds.
            float frame = duration > 0f ? Mathf.Clamp(time / duration * DistanceSamples.Count, 0f, DistanceSamples.Count - 1) : 0f;
            int left = Mathf.FloorToInt(frame);
            return Mathf.Lerp(DistanceSamples[left], DistanceSamples[Mathf.Min(left + 1, DistanceSamples.Count - 1)], frame - left);
        }

        private sealed class MotionState
        {
            public Vector3 Direction;
            public float Distance;
        }
#if TOOLS
        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddChild(Movement.CreateEditUI("Movement Component", context, () => { }));
            root.AddChild(new Label { Text = $"Authored samples: {DistanceSamples?.Count ?? 0}" });
            root.AddChild(GraphEditorUi.BuildSpinRow("Priority", MotionPriority, -1000, 1000, 1,
                value => MotionPriority = (int)value));
            return root;
        }
#endif
    }
}
