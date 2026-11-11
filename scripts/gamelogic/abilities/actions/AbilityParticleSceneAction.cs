using Godot;

namespace GameLogic
{
    [GraphCallable("Particle Scene", "动画与表现 / 特效", GraphCallableUsage.Timeline,
        TimelineKind = GraphTimelineActionKind.Clip, ChineseName = "粒子场景")]
    public class AbilityParticleSceneAction : GraphActionBase
    {
        public string ScenePath { get; set; } = string.Empty;
        public string EffectKey { get; set; } = "particles";
        public override string Description => $"Particles: {EffectKey}";
        public override void Execute(GraphExecutionContext context)
        {
            var system = context?.GetUserData<AbilitySystemComponent3D>();
            var timeline = context?.GetUserData<FlowTimelineContext>();
            if (system?.Effects == null || timeline == null) return;
            if (timeline.Phase == FlowTimelinePhase.Start)
                system.Effects.PlayParticles(EffectKey, GD.Load<PackedScene>(ScenePath), context, timeline.ClipDuration);
            else if (timeline.Phase is FlowTimelinePhase.Complete or FlowTimelinePhase.Cancel)
                system.Effects.StopParticles(EffectKey, context);
        }
        public override void Validate(GraphAsset graph, string nodeId, GraphValidationResult result)
        {
            base.Validate(graph, nodeId, result);
            if (string.IsNullOrWhiteSpace(EffectKey) || !ResourceLoader.Exists(ScenePath))
                result.AddError("Particle Scene requires an effect key and a scene path.", nodeId);
        }
#if TOOLS
        public override Control CreateEditUI(GraphEditorContext context)
        {
            var root = new VBoxContainer();
            root.AddChild(GraphEditorUi.BuildLineEditRow("Scene", ScenePath, "res://...tscn", value => ScenePath = value));
            root.AddChild(GraphEditorUi.BuildLineEditRow("Effect Key", EffectKey, "particles", value => EffectKey = value));
            return root;
        }
#endif
    }
}
