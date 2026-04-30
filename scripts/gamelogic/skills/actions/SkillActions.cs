using Godot;

namespace GameLogic
{
    public class SkillResolveDashDirectionAction : GraphActionBase
    {
        public string DirectionBlackboardKey { get; set; } = "Skill.DashDirection";
        public string MoveAxisBlackboardKey { get; set; } = CharacterHfsmBlackboardKeys.MoveAxisX;

        public override string Description => "Resolve Dash Direction";

        public override void Execute(GraphExecutionContext context)
        {
            if (context == null)
                return;

            float axisX = context.Blackboard.GetValue(MoveAxisBlackboardKey, 0f);
            float direction = Mathf.Abs(axisX) > 0.01f ? Mathf.Sign(axisX) : 0f;

            if (Mathf.IsZeroApprox(direction))
            {
                CharacterMoveComponent2D move = GetGameObject(context)?.GetComponent<CharacterMoveComponent2D>();
                if (move != null)
                    direction = move.Facing >= 0 ? 1f : -1f;
            }

            if (Mathf.IsZeroApprox(direction))
                direction = 1f;

            context.Blackboard.SetValue(DirectionBlackboardKey, direction);
        }

        private static GameObject2D GetGameObject(GraphExecutionContext context)
        {
            return context.GetUserData<GameObject2D>() ?? context.GetUserData<HfsmRuntime>()?.GameObject;
        }
    }

    public class SkillApplyDashVelocityAction : GraphActionBase
    {
        public string DirectionBlackboardKey { get; set; } = "Skill.DashDirection";
        public float Speed { get; set; } = 760f;
        public bool StopVerticalVelocity { get; set; } = true;

        public override string Description => "Apply Dash Velocity";

        public override void Execute(GraphExecutionContext context)
        {
            CharacterBodyMotorComponent2D motor = GetGameObject(context)?.GetComponent<CharacterBodyMotorComponent2D>();
            if (motor == null)
                return;

            float direction = context.Blackboard.GetValue(DirectionBlackboardKey, 1f);
            if (Mathf.IsZeroApprox(direction))
                direction = 1f;

            float velocityY = StopVerticalVelocity ? 0f : motor.Velocity.Y;
            motor.Velocity = new Vector2(Mathf.Sign(direction) * Speed, velocityY);
        }

        private static GameObject2D GetGameObject(GraphExecutionContext context)
        {
            return context.GetUserData<GameObject2D>() ?? context.GetUserData<HfsmRuntime>()?.GameObject;
        }
    }

    public class SkillSetVisualModulateAction : GraphActionBase
    {
        public string VisualRootPath { get; set; } = "VisualRoot";
        public bool Enabled { get; set; }
        public Color ActiveColor { get; set; } = new(0.55f, 0.95f, 1f, 1f);

        public override string Description => Enabled ? "Set Skill Visual" : "Restore Skill Visual";

        public override void Execute(GraphExecutionContext context)
        {
            CanvasItem visual = GetGameObject(context)?.GetNodeOrNull<CanvasItem>(VisualRootPath);
            SkillRuntime runtime = context.GetUserData<SkillRuntime>();
            if (visual == null || runtime == null)
                return;

            string dataKey = $"VisualModulate:{VisualRootPath}";
            if (Enabled)
            {
                if (!runtime.TryGetData(dataKey, out Color _))
                    runtime.SetData(dataKey, visual.Modulate);

                visual.Modulate = ActiveColor;
                return;
            }

            if (runtime.TryGetData(dataKey, out Color original))
                visual.Modulate = original;
            else
                visual.Modulate = Colors.White;
        }

        private static GameObject2D GetGameObject(GraphExecutionContext context)
        {
            return context.GetUserData<GameObject2D>() ?? context.GetUserData<HfsmRuntime>()?.GameObject;
        }
    }

    public enum SkillSlashVisualMode
    {
        Show,
        Update,
        Hide
    }

    public class SkillSlashVisualAction : GraphActionBase
    {
        public SkillSlashVisualMode Mode { get; set; } = SkillSlashVisualMode.Show;
        public string VisualRootPath { get; set; } = "VisualRoot";
        public string SlashNodeName { get; set; } = "AttackSlash";
        public Vector2 SlashOffset { get; set; } = new(24f, -6f);
        public Vector2 SlashScale { get; set; } = new(1f, 1f);
        public Color SlashColor { get; set; } = new(1f, 0.42f, 0.18f, 0.72f);

        public override string Description => $"{Mode} Slash";

        public override void Execute(GraphExecutionContext context)
        {
            Node2D visualRoot = GetGameObject(context)?.GetNodeOrNull<Node2D>(VisualRootPath);
            if (visualRoot == null)
                return;

            Polygon2D slash = EnsureSlashVisual(visualRoot);
            if (slash == null)
                return;

            switch (Mode)
            {
                case SkillSlashVisualMode.Show:
                    slash.Visible = true;
                    UpdateSlash(slash, 0f);
                    break;
                case SkillSlashVisualMode.Update:
                    FlowTimelineContext timeline = context.GetUserData<FlowTimelineContext>();
                    UpdateSlash(slash, timeline?.NormalizedTime ?? 0f);
                    break;
                case SkillSlashVisualMode.Hide:
                    slash.Visible = false;
                    break;
            }
        }

        private Polygon2D EnsureSlashVisual(Node2D visualRoot)
        {
            Polygon2D slash = visualRoot.GetNodeOrNull<Polygon2D>(SlashNodeName);
            if (slash != null)
                return slash;

            slash = new Polygon2D
            {
                Name = SlashNodeName,
                Polygon = new[]
                {
                    new Vector2(0f, -24f),
                    new Vector2(52f, -14f),
                    new Vector2(72f, 0f),
                    new Vector2(52f, 14f),
                    new Vector2(0f, 24f)
                },
                ZIndex = 20,
                Visible = false
            };

            visualRoot.AddChild(slash);
            return slash;
        }

        private void UpdateSlash(Polygon2D slash, float progress)
        {
            progress = Mathf.Clamp(progress, 0f, 1f);
            float alpha = Mathf.Lerp(0.75f, 0.2f, progress);
            slash.Position = SlashOffset;
            slash.Scale = SlashScale * Mathf.Lerp(0.9f, 1.18f, progress);
            slash.Color = new Color(SlashColor.R, SlashColor.G, SlashColor.B, alpha);
            slash.Visible = true;
        }

        private static GameObject2D GetGameObject(GraphExecutionContext context)
        {
            return context.GetUserData<GameObject2D>() ?? context.GetUserData<HfsmRuntime>()?.GameObject;
        }
    }
}
