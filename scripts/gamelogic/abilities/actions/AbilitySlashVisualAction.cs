using Godot;

namespace GameLogic
{
    public abstract class AbilitySlashActionBase : GraphActionBase
    {
        public string VisualRootPath { get; set; } = "VisualRoot";
        public string SlashNodeName { get; set; } = "AttackSlash";
        public Vector2 SlashOffset { get; set; } = new(0.12f, 0.06f);
        public Vector2 SlashScale { get; set; } = new(0.6f, 0.6f);
        public Color SlashColor { get; set; } = new(1f, 0.76f, 0.35f, 0.8f);
        public float StartAngleDegrees { get; set; } = 130f;
        public float EndAngleDegrees { get; set; } = -55f;
        public float TrailAngleDegrees { get; set; } = 80f;
        public float TrailWidth { get; set; } = 0.14f;

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

            slash = new MeshInstance3D
            {
                Name = SlashNodeName, Mesh = new ImmediateMesh(), Visible = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    VertexColorUseAsAlbedo = true }
            };

            visualRoot.AddChild(slash);
            return slash;
        }

        protected void UpdateSlash(MeshInstance3D slash, float progress)
        {
            progress = Mathf.Clamp(progress, 0f, 1f);
            float alpha = SlashColor.A * Mathf.Lerp(1f, 0.05f, progress);
            var mesh = (ImmediateMesh)slash.Mesh;
            mesh.ClearSurfaces();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            float angle = Mathf.Lerp(StartAngleDegrees, EndAngleDegrees, progress);
            float direction = EndAngleDegrees < StartAngleDegrees ? -1f : 1f;
            float trail = Mathf.Min(Mathf.Abs(EndAngleDegrees - StartAngleDegrees) * progress + 15f,
                Mathf.Abs(TrailAngleDegrees));
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                float a = (float)i / segments, b = (float)(i + 1) / segments;
                Vector3 outerA = ArcPoint(angle - direction * trail * (1f - a), 1f);
                Vector3 outerB = ArcPoint(angle - direction * trail * (1f - b), 1f);
                float width = Mathf.Clamp(TrailWidth, 0.01f, 0.5f);
                Vector3 innerA = outerA * (1f - width * Mathf.Sin(a * Mathf.Pi));
                Vector3 innerB = outerB * (1f - width * Mathf.Sin(b * Mathf.Pi));
                Color rim = new(1f, 0.97f, 0.84f, alpha);
                Color body = new(SlashColor.R, SlashColor.G, SlashColor.B, alpha * 0.32f);
                AddTriangle(mesh, outerA, outerB, innerB, rim, rim, body);
                AddTriangle(mesh, outerA, innerB, innerA, rim, body, body);
            }
            mesh.SurfaceEnd();

            // Match the character's Y-billboard plane; explicit mirroring survives camera rotation.
            var visualRoot = (Node3D)slash.GetParent();
            float facing = visualRoot.Scale.X < 0f ? -1f : 1f;
            Camera3D camera = slash.GetViewport().GetCamera3D();
            if (camera != null)
            {
                Vector3 right = camera.GlobalBasis.X;
                right.Y = 0f;
                right = right.Normalized();
                Vector3 normal = right.Cross(Vector3.Up);
                slash.GlobalTransform = new Transform3D(
                    new Basis(right * (SlashScale.X * facing), Vector3.Up * SlashScale.Y, normal),
                    visualRoot.GlobalPosition + right * (SlashOffset.X * facing) +
                    Vector3.Up * SlashOffset.Y + normal * 0.025f);
            }
            else
            {
                slash.Position = new Vector3(SlashOffset.X, SlashOffset.Y, 0.025f);
                slash.Scale = new Vector3(SlashScale.X, SlashScale.Y, 1f);
            }
            slash.Visible = true;
        }

        private static Vector3 ArcPoint(float degrees, float radius)
        {
            float angle = Mathf.DegToRad(degrees);
            return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        private static void AddTriangle(ImmediateMesh mesh, Vector3 a, Vector3 b, Vector3 c,
            Color colorA, Color colorB, Color colorC)
        {
            mesh.SurfaceSetColor(colorA); mesh.SurfaceAddVertex(a);
            mesh.SurfaceSetColor(colorB); mesh.SurfaceAddVertex(b);
            mesh.SurfaceSetColor(colorC); mesh.SurfaceAddVertex(c);
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
            root.AddChild(GraphEditorUi.BuildRow("起始角度", BuildAngleSpin(StartAngleDegrees, v => StartAngleDegrees = v)));
            root.AddChild(GraphEditorUi.BuildRow("结束角度", BuildAngleSpin(EndAngleDegrees, v => EndAngleDegrees = v)));
            root.AddChild(GraphEditorUi.BuildRow("残影弧长", BuildAngleSpin(TrailAngleDegrees, v => TrailAngleDegrees = v)));
            root.AddChild(GraphEditorUi.BuildRow("刀光宽度", BuildAngleSpin(TrailWidth, v => TrailWidth = v, .01, .5, .01)));

            return root;
        }

        private static SpinBox BuildAngleSpin(float value, System.Action<float> onChanged,
            double min = -360, double max = 360, double step = 1)
        {
            var spin = new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value };
            spin.ValueChanged += changed => onChanged((float)changed);
            return spin;
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
