using Godot;

namespace GameLogic
{
    [GraphCallable("Apply Dash Velocity", "移动", GraphCallableUsage.Flow | GraphCallableUsage.Timeline, TimelineKind = GraphTimelineActionKind.Clip, ChineseName = "施加冲刺速度")]
    public class AbilityApplyDashVelocityAction : GraphActionBase
    {
        public GraphActionComponentReference Movement { get; set; } = new();
        public float Speed { get; set; } = 20f;
        public bool StopVerticalVelocity { get; set; } = true;

        public override string Description => "Apply Dash Velocity";

        public override void Execute(GraphExecutionContext context)
        {
            Execute(new GraphActionInvocation(context));
        }

        public override void Execute(GraphActionInvocation invocation)
        {
            CharacterMovementComponent3D movement = null;
            string error = string.Empty;
            bool hasInput = invocation.Execution?.ActionDependencyMode == GraphActionDependencyMode.HostBound &&
                            invocation.TryGetInput("Component", out movement);
            if (!hasInput &&
                !GraphActionComponentResolver.TryResolve(invocation.Execution, Movement, nameof(AbilityApplyDashVelocityAction), out movement, out error))
            {
                GD.PushError($"[AbilityApplyDashVelocityAction] {error}");
                return;
            }

            FlowTimelineContext timeline = invocation.Execution?.GetUserData<FlowTimelineContext>();
            AbilityRuntime runtime = invocation.Execution?.GetUserData<AbilityRuntime>();
            string velocityKey = $"DashVelocityX:{timeline?.Node?.Id}:{timeline?.ClipId}";
            if (timeline?.Phase == FlowTimelinePhase.Start)
                runtime?.SetData(velocityKey, movement.Velocity.X);

            if (timeline?.Phase is FlowTimelinePhase.Complete or FlowTimelinePhase.Cancel)
            {
                // 只继承冲刺前的 X 速度；Z 不继承，Y 继续由当前跳跃和重力处理。
                if (runtime != null && runtime.TryGetData(velocityKey, out float velocityXBeforeDash))
                {
                    movement.RequestVelocityOverride(new CharacterMovementOverride3D(
                        new Vector3(velocityXBeforeDash, 0f, 0f),
                        overrideHorizontal: true,
                        overrideVertical: false,
                        priority: 100,
                        restoreBeforeMovement: true));
                    runtime.SetData(velocityKey, null);
                }
                return;
            }

            Vector3 direction = movement.FacingDirection;
            float velocityY = StopVerticalVelocity ? 0f : movement.Velocity.Y;
            movement.RequestVelocityOverride(new CharacterMovementOverride3D(
                new Vector3(direction.X * Speed, velocityY, direction.Z * Speed),
                overrideHorizontal: true,
                overrideVertical: StopVerticalVelocity,
                priority: 100));
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 4);
            root.AddChild(Movement.CreateEditUI("Movement Component", context, () => { }));

            root.AddChild(GraphEditorUi.BuildSpinRow(
                "Speed",
                Speed,
                0,
                999999,
                0.1,
                value => Speed = (float)value));
            root.AddChild(GraphEditorUi.BuildCheckRow(
                "Stop Vertical Velocity",
                StopVerticalVelocity,
                value => StopVerticalVelocity = value));

            return root;
        }
    }
}
