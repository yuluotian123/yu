using Godot;

namespace GameLogic
{
    public abstract class AbilitySlashActionBase : GraphActionBase
    {
        public string VisualRootPath { get; set; } = "VisualRoot";
        public string SlashNodeName { get; set; } = "AttackSlash";
        public Vector2 SlashOffset { get; set; } = new(0.24f, 0.06f);
        public Vector2 SlashScale { get; set; } = new(1f, 1f);
        public Color SlashColor { get; set; } = new(1f, 0.42f, 0.18f, 0.72f);

        protected MeshInstance3D ResolveSlash(GraphExecutionContext context)
        {
            Node3D root = AbilityActionRuntimeHelper.GetGameObject(context)?.GetNodeOrNull<Node3D>(new NodePath(VisualRootPath));
            return root == null ? null : EnsureSlashVisual(root);
        }

        private MeshInstance3D EnsureSlashVisual(Node3D visualRoot)
        {
            MeshInstance3D slash = visualRoot.GetNodeOrNull<MeshInstance3D>(SlashNodeName);
            if (slash != null)
                return slash;

            var mesh = new ImmediateMesh();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            Vector3[] points = { new(0f, 0.24f, 0f), new(0.52f, 0.14f, 0f),
                new(0.72f, 0f, 0f), new(0.52f, -0.14f, 0f), new(0f, -0.24f, 0f) };
            for (int i = 1; i < points.Length - 1; i++)
            { mesh.SurfaceAddVertex(points[0]); mesh.SurfaceAddVertex(points[i]); mesh.SurfaceAddVertex(points[i + 1]); }
            mesh.SurfaceEnd();
            slash = new MeshInstance3D
            {
                Name = SlashNodeName, Mesh = mesh, Visible = false,
                MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled }
            };

            visualRoot.AddChild(slash);
            return slash;
        }

        protected void UpdateSlash(MeshInstance3D slash, float progress)
        {
            progress = Mathf.Clamp(progress, 0f, 1f);
            float alpha = Mathf.Lerp(0.75f, 0.2f, progress);
            slash.Position = new Vector3(SlashOffset.X, SlashOffset.Y, 0.01f);
            slash.Scale = new Vector3(SlashScale.X, SlashScale.Y, 1f) * Mathf.Lerp(0.9f, 1.18f, progress);
            ((StandardMaterial3D)slash.MaterialOverride).AlbedoColor = new Color(SlashColor.R, SlashColor.G, SlashColor.B, alpha);
            slash.Visible = true;
        }

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 4);

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

    [GraphCallable("Slash Visibility", "动画与表现 / 特效", GraphCallableUsage.Timeline, ChineseName = "显示或隐藏刀光")]
    public class AbilitySlashVisualAction : AbilitySlashActionBase
    {
        public AbilitySlashVisualMode Mode { get; set; } = AbilitySlashVisualMode.Show;
        public override string Description => Mode == AbilitySlashVisualMode.Show ? "显示刀光" : "隐藏刀光";
        public override void Execute(GraphExecutionContext context)
        {
            MeshInstance3D slash = ResolveSlash(context);
            if (slash == null) return;
            if (Mode == AbilitySlashVisualMode.Show) UpdateSlash(slash, 0);
            else slash.Visible = false;
        }
        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = (VBoxContainer)base.CreateEditUI(context);
            root.AddChild(GraphEditorUi.BuildEnumRow("显示 / 隐藏", Mode, value => Mode = value));
            return root;
        }
    }

    [GraphCallable("Slash Clip", "动画与表现 / 特效", GraphCallableUsage.Timeline,
        TimelineKind = GraphTimelineActionKind.Clip, ChineseName = "持续刀光")]
    public class AbilitySlashClipAction : AbilitySlashActionBase
    {
        public override string Description => "片段内播放刀光，结束或取消时隐藏";
        public override void Execute(GraphExecutionContext context)
        {
            FlowTimelineContext timeline = context?.GetUserData<FlowTimelineContext>();
            if (timeline == null) return;
            MeshInstance3D slash = ResolveSlash(context);
            if (slash == null) return;
            if (timeline.Phase is FlowTimelinePhase.Complete or FlowTimelinePhase.Cancel) slash.Visible = false;
            else UpdateSlash(slash, timeline.ClipNormalizedTime);
        }
    }
}
