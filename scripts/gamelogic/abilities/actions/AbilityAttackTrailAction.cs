using Godot;

namespace GameLogic
{
    [GraphCallable("Weapon Trail", "动画与表现 / 特效", GraphCallableUsage.Timeline,
        TimelineKind = GraphTimelineActionKind.Clip, ChineseName = "武器刀光")]
    public class AbilityAttackTrailAction : GraphActionBase
    {
        public string EffectScenePath { get; set; } = string.Empty;
        public string TrailPath { get; set; } = string.Empty;
        public override string Description => $"Weapon Trail: {TrailPath.GetFile()}";

        public override void Execute(GraphExecutionContext context)
        {
            var timeline = context?.GetUserData<FlowTimelineContext>();
            if (timeline == null || timeline.Phase == FlowTimelinePhase.Update) return;
            var runtime = context.GetUserData<AbilityRuntime>();
            var effects = runtime?.System.Effects;
            if (effects == null) return;
            string key = runtime.AbilityId + ":" + runtime.ActivationVersion + ":weapon-trail";
            if (timeline.Phase == FlowTimelinePhase.Complete || timeline.Phase == FlowTimelinePhase.Cancel)
                effects.EndTrail(key, timeline.Phase == FlowTimelinePhase.Cancel);
            else if (timeline.Phase == FlowTimelinePhase.Start &&
                !effects.PlayTrail(ResourceLoader.Load<WeaponAttackTrail>(TrailPath), ResourceLoader.Load<PackedScene>(EffectScenePath), key))
                GD.PushError($"[AbilityAttackTrailAction] Invalid sword path: {TrailPath}");
        }

        public override void Validate(GraphAsset graph, string nodeId, GraphValidationResult result)
        {
            base.Validate(graph, nodeId, result);
            if (string.IsNullOrWhiteSpace(TrailPath) || !ResourceLoader.Exists(TrailPath))
                result.AddError("Weapon Trail requires a valid trail resource path.", nodeId);
            if (string.IsNullOrWhiteSpace(EffectScenePath) || !ResourceLoader.Exists(EffectScenePath))
                result.AddError("Weapon Trail requires a particle scene.", nodeId);
        }

#if TOOLS
        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddChild(GraphEditorUi.BuildLineEditRow("Particles", EffectScenePath, "res://...tscn", value => EffectScenePath = value));
            root.AddChild(GraphEditorUi.BuildLineEditRow("Trail", TrailPath, "res://...trail.tres", value => TrailPath = value));
            return root;
        }
#endif
    }
}
