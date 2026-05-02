using Godot;

namespace GameLogic
{
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
            Node2D visualRoot = SkillActionRuntimeHelper.GetGameObject(context)?.GetNodeOrNull<Node2D>(VisualRootPath);
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
                    UpdateSlash(slash, timeline?.ClipDuration > 0f ? timeline.ClipNormalizedTime : timeline?.NormalizedTime ?? 0f);
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
    }
}
