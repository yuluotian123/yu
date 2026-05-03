using Godot;

namespace GameLogic
{
    public class SkillSetVisualModulateAction : GraphActionBase
    {
        public string VisualRootPath { get; set; } = "VisualRoot";
        public bool Enabled { get; set; }
        public Color ActiveColor { get; set; } = new(0.55f, 0.95f, 1f, 1f);

        public override string Description => Enabled ? "Set Skill Visual" : "Restore Skill Visual";

        public override void Execute(GraphExecutionContext context)
        {
            CanvasItem visual = SkillActionRuntimeHelper.GetGameObject(context)?.GetNodeOrNull<CanvasItem>(VisualRootPath);
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

        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 4);

            root.AddChild(SkillActionEditorHelper.BuildLineEditRow(
                "Visual Root",
                VisualRootPath,
                "VisualRoot",
                value => VisualRootPath = value));
            root.AddChild(SkillActionEditorHelper.BuildCheckRow(
                "Enabled",
                Enabled,
                value => Enabled = value));
            root.AddChild(BuildColorRow(
                "Active Color",
                ActiveColor,
                value => ActiveColor = value));

            return root;
        }

        private static Control BuildColorRow(string label, Color color, System.Action<Color> onChanged)
        {
            var picker = new ColorPickerButton
            {
                Color = color,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            picker.ColorChanged += changed => onChanged(changed);
            return SkillActionEditorHelper.BuildRow(label, picker);
        }
    }
}
