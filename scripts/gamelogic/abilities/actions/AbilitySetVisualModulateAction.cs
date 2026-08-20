using Godot;

namespace GameLogic
{
    public class AbilitySetVisualModulateAction : GraphActionBase
    {
        public string VisualRootPath { get; set; } = "VisualRoot";
        public bool Enabled { get; set; }
        public Color ActiveColor { get; set; } = new(0.55f, 0.95f, 1f, 1f);

        public override string Description => Enabled ? "Set Ability Visual" : "Restore Ability Visual";

        public override void Execute(GraphExecutionContext context)
        {
            CanvasItem visual = AbilityActionRuntimeHelper.GetGameObject(context)?.GetNodeOrNull<CanvasItem>(VisualRootPath);
            AbilityRuntime runtime = context.GetUserData<AbilityRuntime>();
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

            root.AddChild(GraphEditorUi.BuildLineEditRow(
                "Visual Root",
                VisualRootPath,
                "VisualRoot",
                value => VisualRootPath = value));
            root.AddChild(GraphEditorUi.BuildCheckRow(
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
            return GraphEditorUi.BuildRow(label, picker);
        }
    }
}
