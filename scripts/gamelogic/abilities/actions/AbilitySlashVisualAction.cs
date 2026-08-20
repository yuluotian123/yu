using Godot;

namespace GameLogic
{
    public class AbilitySlashVisualAction : GraphActionBase
    {
        public AbilitySlashVisualMode Mode { get; set; } = AbilitySlashVisualMode.Show;
        public string VisualRootPath { get; set; } = "VisualRoot";
        public string SlashNodeName { get; set; } = "AttackSlash";
        public Vector2 SlashOffset { get; set; } = new(24f, -6f);
        public Vector2 SlashScale { get; set; } = new(1f, 1f);
        public Color SlashColor { get; set; } = new(1f, 0.42f, 0.18f, 0.72f);

        public override string Description => $"{Mode} Slash";

        public override void Execute(GraphExecutionContext context)
        {
            Node2D visualRoot = AbilityActionRuntimeHelper.GetGameObject(context)?.GetNodeOrNull<Node2D>(VisualRootPath);
            if (visualRoot == null)
                return;

            Polygon2D slash = EnsureSlashVisual(visualRoot);
            if (slash == null)
                return;

            switch (Mode)
            {
                case AbilitySlashVisualMode.Show:
                    slash.Visible = true;
                    UpdateSlash(slash, 0f);
                    break;
                case AbilitySlashVisualMode.Update:
                    FlowTimelineContext timeline = context.GetUserData<FlowTimelineContext>();
                    UpdateSlash(slash, timeline?.ClipDuration > 0f ? timeline.ClipNormalizedTime : timeline?.NormalizedTime ?? 0f);
                    break;
                case AbilitySlashVisualMode.Hide:
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

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 4);

            root.AddChild(GraphEditorUi.BuildEnumRow(
                "Mode",
                Mode,
                value => Mode = value));
            root.AddChild(GraphEditorUi.BuildLineEditRow(
                "Visual Root",
                VisualRootPath,
                "VisualRoot",
                value => VisualRootPath = value));
            root.AddChild(GraphEditorUi.BuildLineEditRow(
                "Slash Node",
                SlashNodeName,
                "AttackSlash",
                value => SlashNodeName = value));
            root.AddChild(BuildVector2Row(
                "Offset",
                SlashOffset,
                value => SlashOffset = value));
            root.AddChild(BuildVector2Row(
                "Scale",
                SlashScale,
                value => SlashScale = value));
            root.AddChild(BuildColorRow(
                "Color",
                SlashColor,
                value => SlashColor = value));

            return root;
        }

        private static Control BuildVector2Row(string label, Vector2 value, System.Action<Vector2> onChanged)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label
            {
                Text = label,
                CustomMinimumSize = new Vector2(120, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            var xSpin = new SpinBox
            {
                MinValue = -999999,
                MaxValue = 999999,
                Step = 0.1,
                Value = value.X,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            var ySpin = new SpinBox
            {
                MinValue = -999999,
                MaxValue = 999999,
                Step = 0.1,
                Value = value.Y,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };

            xSpin.ValueChanged += changed => onChanged(new Vector2((float)changed, (float)ySpin.Value));
            ySpin.ValueChanged += changed => onChanged(new Vector2((float)xSpin.Value, (float)changed));
            row.AddChild(xSpin);
            row.AddChild(ySpin);
            return row;
        }

        private static Control BuildColorRow(string label, Color color, System.Action<Color> onChanged)
        {
            var picker = new ColorPickerButton
            {
                Color = color,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            picker.ColorChanged += changed => onChanged(changed);
            return GraphEditorUi.BuildRow(label, picker);
        }
    }
}
