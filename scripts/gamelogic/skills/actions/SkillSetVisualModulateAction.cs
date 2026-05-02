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
    }
}
