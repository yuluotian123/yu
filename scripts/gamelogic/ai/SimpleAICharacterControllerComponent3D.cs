using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class SimpleAICharacterControllerComponent3D : Component3D
    {
        public override int Priority => ComponentPriority.AI;

        [Export] public BehaviorTreeGraphAsset Graph { get; set; }
        [Export] public bool UpdateInPhysics { get; set; } = true;

        [ExportGroup("Patrol")]
        [Export] public float PatrolDistance { get; set; } = 1.2f;
        [Export] public int StartDirection { get; set; } = 1;
        [Export] public bool ReverseAtEdges { get; set; } = true;
        [Export] public float EdgeLookAhead { get; set; } = 0.18f;
        [Export] public float TurnPauseDuration { get; set; } = 0.12f;

        [ExportGroup("Jump")]
        [Export] public float JumpInterval { get; set; } = 1.8f;
        [Export] public float JumpSustainDuration { get; set; } = 0.12f;

        public BehaviorTreeRuntime Runtime { get; private set; }

        public override void OnInit()
        {
            if (Graph == null) { GD.PushWarning("AI behavior graph is not assigned."); return; }
            Runtime = new BehaviorTreeRuntime(Graph);
            Runtime.Context.UserData.Add(Owner);
            if (!Runtime.Start()) { GD.PushWarning($"Failed to start BehaviorTree: {Graph.ResourcePath}"); return; }
            // Per-host configuration; action progress belongs to each graph task.
            Runtime.SetValue("Patrol.Distance", PatrolDistance);
            Runtime.SetValue("Patrol.StartDirection", StartDirection);
            Runtime.SetValue("Patrol.ReverseAtEdges", ReverseAtEdges);
            Runtime.SetValue("Patrol.LookAhead", EdgeLookAhead);
            Runtime.SetValue("Patrol.TurnPause", TurnPauseDuration);
            Runtime.SetValue("Jump.Interval", JumpInterval);
            Runtime.SetValue("Jump.HoldDuration", JumpSustainDuration);
        }

        public override void OnUpdate(double delta) { if (!UpdateInPhysics) Tick(delta); }
        public override void OnPhysicsUpdate(double delta) { if (UpdateInPhysics) Tick(delta); }
        public override void OnDestroy() { Runtime?.Stop(); Runtime = null; }
        private void Tick(double delta)
        {
            GraphComponentBindingRuntime.SyncFromComponents(Runtime?.Context);
            Runtime?.Update(delta);
            GraphComponentBindingRuntime.SyncToComponents(Runtime?.Context);
        }
    }
}
