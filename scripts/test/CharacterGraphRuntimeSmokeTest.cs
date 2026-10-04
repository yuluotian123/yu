using System;
using System.Linq;
using System.Text.Json.Nodes;
using GameLogic;
using Godot;

public partial class CharacterGraphRuntimeSmokeTest : Node
{
    private const double PhysicsDelta = 1d / 60d;

    public override void _Ready()
    {
        try
        {
            Run();
            GD.Print("[CharacterGraphRuntimeSmokeTest] PASS");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[CharacterGraphRuntimeSmokeTest] FAIL: {exception.Message}\n{exception}");
            GetTree().Quit(1);
        }
    }

    private void Run()
    {
        VerifyComponentMetadata();
        VerifyInputNodes();
        VerifyLifecycleAndNonReentrancy();

        PackedScene playerScene = GD.Load<PackedScene>("res://assets/scenes/player.tscn");
        Require(playerScene != null, "Player scene could not be loaded.");
        GameObject2D player = playerScene.Instantiate<GameObject2D>();
        AddChild(player);
        player.SetProcess(false);
        player.SetPhysicsProcess(false);

        CharacterGraphComponent2D graph = player.GetComponent<CharacterGraphComponent2D>();
        AbilitySystemComponent2D abilities = player.GetComponent<AbilitySystemComponent2D>();
        CharacterMovementComponent2D movement = player.GetComponent<CharacterMovementComponent2D>();
        CharacterAnimationComponent2D animation = player.GetComponent<CharacterAnimationComponent2D>();

        Require(graph?.Runtime?.IsRunning == true, "CharacterGraph did not initialize.");
        Require(graph.CharacterGraph?.ResourceLocalToScene == true,
            "CharacterGraph is not configured as a scene-local inline resource.");
        Require(graph.CharacterGraph.ResourcePath?.Contains("::", StringComparison.Ordinal) == true,
            $"CharacterGraph is not embedded in the player scene: '{graph.CharacterGraph.ResourcePath}'.");
        Require(abilities != null, "AbilitySystem is missing.");
        Require(movement != null, "CharacterMovement is missing.");
        Require(animation?.LocomotionRuntime?.IsRunning == true, "Locomotion graph did not start.");
        Require(animation.AnimationInstance != null, "CharacterAnimation AnimInstance was not created.");
        Require(animation.PlaybackBackend?.IsValid == true, "CharacterAnimation playback backend is unavailable.");
        Require(animation.LocomotionGraph?.ResourceLocalToScene == true,
            "LocomotionGraph is not configured as a scene-local inline resource.");
        Require(animation.LocomotionGraph.ResourcePath?.Contains("::", StringComparison.Ordinal) == true,
            $"LocomotionGraph is not embedded in the player scene: '{animation.LocomotionGraph.ResourcePath}'.");
        VerifyLocomotionBlackboard(animation.LocomotionGraph);
        Require(GraphComponentBindingRuntime.SyncFromComponents(animation.LocomotionRuntime.Context),
            "Locomotion component bindings failed to synchronize.");
        Require(animation.LocomotionRuntime.Context.Blackboard.GetValue<string>(LocomotionBlackboardKeys.MovementMode) == movement.MovementModeName,
            "Locomotion movement mode was not copied from the component.");
        Require(!player.GetAllComponents().Any(value => value.GetType().Name == "CharacterCommandBufferComponent2D"),
            "Legacy CommandBuffer is still mounted.");
        Require(!player.GetAllComponents().Any(value => value.GetType().Name == "SkillManagerComponent2D"),
            "Legacy SkillManager is still mounted.");

        CharacterGraphAsset graphAsset = graph.CharacterGraph;
        Require(graphAsset.Nodes.OfType<HfsmCompositeStateNodeData>().Count() == 0, "CharacterGraph still contains Locomotion.");
        Require(!graphAsset.GetAllowedNodeTypes().Contains(nameof(HfsmStateNodeData)),
            "CharacterGraph editor still exposes HFSM states.");
        Require(!graphAsset.GetAllowedNodeTypes().Contains(nameof(FlowTimelineNodeData)),
            "CharacterGraph editor still exposes Timeline nodes.");
        Require(!graphAsset.GetAllowedNodeTypes().Contains(nameof(FlowEntryNodeData)),
            "CharacterGraph editor still exposes a single-entry Flow node.");
        Require(graphAsset.Nodes.OfType<GraphComponentCallNodeData>()
                .Any(value => value.ComponentTypeName == typeof(CharacterMovementComponent2D).FullName &&
                    value.MemberId == "AddMovementInput"),
            "Movement input is not configured through CharacterMovementComponent2D.");
        CharacterAbilityNodeData attackNode = graphAsset.Nodes.OfType<CharacterAbilityNodeData>()
            .First(value => value.AbilityId == "attack");
        CharacterAbilityNodeData dashNode = graphAsset.Nodes.OfType<CharacterAbilityNodeData>()
            .First(value => value.AbilityId == "dash");
        CharacterGraphConnection attackToDash = graphAsset.Connections.OfType<CharacterGraphConnection>().FirstOrDefault(value =>
            value.FromNode == attackNode.Id && value.ToNode == dashNode.Id &&
            value.RelationKind == CharacterGraphRelationKind.Interrupt);
        Require(attackToDash != null,
            "Attack -> Dash interrupt relationship is missing.");
        Require(abilities.TryActivateAbility("missing", "SmokeTest") == AbilityActivationResult.NotGranted,
            "AbilitySystem activated an Ability that was not granted.");
        Require(abilities.GetRuntime("attack")?.Resource?.Graph?.Nodes
                .OfType<AbilityTimelineNodeData>().Any() == true,
            "Attack Ability Timeline did not deserialize.");

        Require(abilities.TryActivateAbility("attack", "SmokeTest") == AbilityActivationResult.Activated,
            "Attack could not activate.");
        Require(abilities.TryActivateAbility("attack", "SmokeTest") == AbilityActivationResult.AlreadyActive,
            "AbilitySystem did not reject an already active Ability.");
        TickPhysics(player);
        Require(animation.AnimationInstance.Variables.ContainsKey(LocomotionBlackboardKeys.MovementMode),
            "AnimInstance variables did not capture the Movement provider value.");
        Require(animation.ActiveRequestKey == "ability:attack:attack_animation",
            $"Unexpected attack animation key: {animation.ActiveRequestKey}");
        Require(movement.MovementLocked && movement.JumpLocked, "Attack did not apply movement locks.");

        int configuredPriority = attackToDash.RequestPriority;
        attackToDash.RequestPriority = 40;
        Require(graph.Runtime.TryActivateAbility(dashNode) == AbilityActivationResult.BlockedByCurrentAbility,
            "Low-priority graph request interrupted Attack.");
        attackToDash.RequestPriority = configuredPriority;
        Require(graph.Runtime.TryActivateAbility(dashNode) == AbilityActivationResult.Activated,
            "Dash did not interrupt Attack through the graph relationship.");
        TickPhysics(player);
        Require(Mathf.IsEqualApprox(Mathf.Abs(movement.Velocity.X), 760f),
            $"Dash velocity was {movement.Velocity.X}.");
        Require(animation.ActiveRequestKey == "ability:dash:dash_animation",
            $"Unexpected dash animation key: {animation.ActiveRequestKey}");
        Require(graph.Runtime.TryActivateAbility(attackNode) == AbilityActivationResult.BlockedByCurrentAbility,
            "Attack bypassed the missing Dash -> Attack relationship.");

        abilities.GetRuntime("attack").SetCooldownReadyTime(0d);
        var completion = new CharacterGraphConnection
        {
            RelationKind = CharacterGraphRelationKind.Completion,
            RequestPriority = 100,
            FromNode = dashNode.Id,
            FromPort = 0,
            ToNode = attackNode.Id,
            ToPort = 0
        };
        graphAsset.Connections.Add(completion);

        for (int i = 0; i < 60 && abilities.ActiveAbilities.Count > 0; i++)
            TickPhysics(player);
        graphAsset.Connections.Remove(completion);
        Require(abilities.ActiveAbilities.Count == 0, "Completed Ability remained active.");
        Require(abilities.GetRuntime("attack").LastReturnLabel == "Finished",
            "Completion relationship did not activate Attack after Dash.");
        Require(!movement.MovementLocked && !movement.JumpLocked, "Ability movement locks were not released.");
        TickPhysics(player);
        Require(!animation.ActiveRequestKey.StartsWith("ability:", StringComparison.Ordinal),
            "Completed Ability left an animation override active.");

        movement.SubmitCommand(new CharacterCommand2D(-1f, true, true), ComponentPriority.Input);
        TickPhysics(player);
        Require(movement.RawMoveInputX < 0f, "Movement did not consume its internal command buffer.");
        Require(movement.JumpSustainRequested, "Jump sustain did not persist after command consumption.");
        TickPhysics(player);
        Require(movement.JumpSustainRequested, "Jump sustain was cleared without a release request.");
        movement.SetJumpSustain(false, ComponentPriority.Input);
        TickPhysics(player);
        Require(!movement.JumpSustainRequested, "Jump release did not clear persistent sustain.");

        VerifyGraphMovementInput(player, graphAsset, movement);

        CharacterPersistenceComponent2D persistence = player.GetComponent<CharacterPersistenceComponent2D>();
        JsonObject state = persistence.Capture();
        Require(state["abilities"] is JsonObject, "Ability cooldown state was not captured.");
        Require(!state.ContainsKey("input") && !state.ContainsKey("timeline"),
            "Persistence captured transient graph state.");
        VerifyLegacyAbilityPersistence(playerScene, state);

        VerifyAiScene();
    }

    private static void VerifyLocomotionBlackboard(HfsmGraphAsset graph)
    {
        Require(graph?.BlackboardEntries?.Count == 4, "Locomotion graph blackboard is incomplete.");
        Require(GraphBlackboardValidator.FindEntry(graph.BlackboardEntries, LocomotionBlackboardKeys.MovementMode)?.Value
                is GraphStringBlackboardValue,
            "Locomotion movement mode is not a String blackboard value.");
        Require(GraphBlackboardValidator.FindEntry(graph.BlackboardEntries, LocomotionBlackboardKeys.MovementIsOnFloor)?.Value
                is GraphBoolBlackboardValue,
            "Locomotion grounded state is not a Bool blackboard value.");
        Require(GraphBlackboardValidator.FindEntry(graph.BlackboardEntries, LocomotionBlackboardKeys.MovementMoveAxisX)?.Value
                is GraphFloatBlackboardValue,
            "Locomotion move axis is not a Float blackboard value.");
        Require(GraphBlackboardValidator.FindEntry(graph.BlackboardEntries, LocomotionBlackboardKeys.MovementVelocityY)?.Value
                is GraphFloatBlackboardValue,
            "Locomotion vertical velocity is not a Float blackboard value.");

        VerifyBinding(graph, LocomotionBlackboardKeys.MovementMode, "MovementModeName");
        VerifyBinding(graph, LocomotionBlackboardKeys.MovementIsOnFloor, "IsOnFloor");
        VerifyBinding(graph, LocomotionBlackboardKeys.MovementMoveAxisX, "MoveInputX");
        VerifyBinding(graph, LocomotionBlackboardKeys.MovementVelocityY, "VelocityY");
    }

    private static void VerifyComponentMetadata()
    {
        Require(GraphComponentRegistry.TryGet(typeof(CharacterMovementComponent2D), out GraphComponentTypeDescriptor descriptor),
            "CharacterMovementComponent2D was not registered.");
        Require(descriptor.Values.Any(value => value.MemberId == "MoveInputX"), "MoveInputX metadata is missing.");
        Require(descriptor.Values.Any(value => value.MemberId == "IsOnFloor"), "IsOnFloor metadata is missing.");
        Require(descriptor.Actions.Any(value => value.MemberId == "AddMovementInput"), "AddMovementInput metadata is missing.");
        Require(descriptor.Actions.First(value => value.MemberId == "AddMovementInput").UseInputEventValue,
            "AddMovementInput is not marked as an input-event component action.");
        Require(!GraphComponentInvoker.TryRead(null, descriptor.TypeName, "MoveInputX", out _, out _),
            "Missing component owner did not fail a component read.");

        var node = new GraphComponentCallNodeData
        {
            ComponentTypeName = descriptor.TypeName,
            MemberId = "AddMovementInput",
            Arguments = new System.Collections.Generic.List<GraphComponentArgument>
            {
                new GraphComponentArgument { Name = "axis", Value = new GraphFloatBlackboardValue { Value = 1f } }
            }
        };
        GraphComponentCallNodeData restored = GraphJsonHelper.Deserialize<GraphComponentCallNodeData>(GraphJsonHelper.Serialize(node));
        Require(restored?.ComponentTypeName == descriptor.TypeName && restored.MemberId == node.MemberId &&
                restored.Arguments?.Count == 1, "Component Call node did not round-trip through GraphJson.");
    }

    private static void VerifyBinding(HfsmGraphAsset graph, string key, string memberId)
    {
        GraphBlackboardEntry entry = GraphBlackboardValidator.FindEntry(graph.BlackboardEntries, key);
        Require(entry?.Binding != null && entry.Binding.ComponentTypeName == typeof(CharacterMovementComponent2D).FullName &&
                entry.Binding.MemberId == memberId && entry.Binding.Direction == GraphComponentBindingDirection.ComponentToBlackboard,
            $"Locomotion binding for {key} is missing or incorrect.");
    }

    private static void VerifyInputNodes()
    {
        var provider = new FakeInputProvider { Negative = 0.8f, Positive = 0.1f };
        var axis = new CharacterInputActionNodeData
        {
            TriggerMode = CharacterInputTriggerMode.Axis1D,
            NegativeAction = "left",
            PositiveAction = "right",
            AxisDeadzone = 0.1f,
            AxisThreshold = 0.1f,
            ConsumeInput = false
        };
        Require(axis.IsTriggered(provider), "Axis1D did not trigger outside its deadzone.");
        Require(Mathf.IsEqualApprox(axis.ReadValue(provider), -0.7f), "Axis1D did not preserve its sign.");

        provider.Negative = 0.1f;
        provider.Positive = 0.05f;
        Require(!axis.IsTriggered(provider), "Axis1D triggered inside its deadzone.");
    }

    private static void VerifyLifecycleAndNonReentrancy()
    {
        var update = new CharacterLifecycleEventNodeData
        {
            Id = "update",
            Event = CharacterLifecycleEvent.Update
        };
        var delay = new FlowDelayNodeData { Id = "delay", Seconds = 0.1f };
        var graph = new CharacterGraphAsset
        {
            Nodes = new System.Collections.Generic.List<GraphNodeData> { update, delay },
            Connections = new System.Collections.Generic.List<GraphConnection>
            {
                new CharacterGraphConnection
                {
                    FromNode = update.Id,
                    FromPort = 0,
                    ToNode = delay.Id,
                    ToPort = 0
                }
            }
        };
        var runtime = new CharacterGraphRuntime(graph, null, null);

        runtime.Update(0.01d, physics: false);
        Require(runtime.GetEventVersion("Lifecycle.BeginPlay") == 1, "BeginPlay did not fire on the first update.");
        Require(runtime.ActiveExecutionCount == 1, "Update flow did not enter Delay.");
        for (int i = 0; i < 4; i++)
            runtime.Update(0.01d, physics: false);
        Require(runtime.GetEventVersion("Lifecycle.BeginPlay") == 1, "BeginPlay fired more than once.");
        Require(runtime.ActiveExecutionCount == 1, "Update event re-entered while its Delay was active.");

        runtime.Update(0.1d, physics: false);
        Require(runtime.ActiveExecutionCount == 1, "Update flow did not restart after the previous execution completed.");
        runtime.Stop();
        Require(runtime.GetEventVersion("Lifecycle.EndPlay") == 1, "EndPlay did not fire when the runtime stopped.");
    }

    private static void VerifyGraphMovementInput(
        GameObject2D player,
        CharacterGraphAsset graphAsset,
        CharacterMovementComponent2D movement)
    {
        var provider = new FakeInputProvider { Negative = 0.8f, Positive = 0.1f };
        var runtime = new CharacterGraphRuntime(graphAsset, player, provider);

        runtime.Update(PhysicsDelta, physics: true);
        movement.OnPhysicsUpdate(PhysicsDelta);
        Require(Mathf.IsEqualApprox(movement.RawMoveInputX, -0.7f),
            "CharacterGraph did not submit the signed movement axis.");

        provider.JustPressedAction = "player_jump";
        runtime.Update(PhysicsDelta, physics: true);
        Require(movement.JumpSustainRequested, "CharacterGraph jump press did not enable sustain.");

        provider.JustPressedAction = null;
        provider.JustReleasedAction = "player_jump";
        runtime.Update(PhysicsDelta, physics: true);
        Require(!movement.JumpSustainRequested, "CharacterGraph jump release did not disable sustain.");
        runtime.Stop();
    }

    private void VerifyAiScene()
    {
        PackedScene scene = GD.Load<PackedScene>("res://assets/scenes/ai_runner.tscn");
        Require(scene != null, "AI scene could not be loaded.");
        GameObject2D ai = scene.Instantiate<GameObject2D>();
        AddChild(ai);
        ai.SetProcess(false);
        ai.SetPhysicsProcess(false);
        Require(ai.GetComponent<CharacterGraphComponent2D>() == null, "Simple AI still mounts CharacterGraph.");
        Require(ai.GetComponent<AbilitySystemComponent2D>() == null, "Simple AI unexpectedly mounts AbilitySystem.");
        Require(!ai.GetAllComponents().Any(value => value.GetType().Name == "CharacterCommandBufferComponent2D"),
            "Simple AI still mounts CommandBuffer.");
        Require(ai.GetComponent<CharacterMovementComponent2D>() != null, "Simple AI has no Movement component.");
        ai.QueueFree();
    }

    private void VerifyLegacyAbilityPersistence(PackedScene playerScene, JsonObject capturedState)
    {
        GameObject2D restored = playerScene.Instantiate<GameObject2D>();
        restored.PersistentId = "player_restore_smoke";
        AddChild(restored);
        restored.SetProcess(false);
        restored.SetPhysicsProcess(false);

        var legacyState = new JsonObject
        {
            ["skills"] = capturedState["abilities"]?.DeepClone()
        };
        restored.GetComponent<CharacterPersistenceComponent2D>()?.Restore(legacyState, schemaVersion: 1);
        AbilityRuntime dash = restored.GetComponent<AbilitySystemComponent2D>()?.GetRuntime("dash");
        double now = Time.GetTicksMsec() * 0.001d;
        Require(dash != null && dash.CooldownRemaining(now) > 0f,
            "Legacy skills cooldown state was not restored by AbilityId.");
        restored.QueueFree();
    }

    private static void TickPhysics(GameObject2D owner)
    {
        foreach (Component2D component in owner.GetAllComponents())
        {
            if (component.IsActive)
                component.OnPhysicsUpdate(PhysicsDelta);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FakeInputProvider : ICharacterInputProvider
    {
        public float Negative { get; set; }
        public float Positive { get; set; }
        public string JustPressedAction { get; set; }
        public string JustReleasedAction { get; set; }
        public bool IsPressed(string action, string handlerLayer = null) => GetActionStrength(action, handlerLayer) > 0f;
        public bool IsJustPressed(string action, string handlerLayer = null) => action == JustPressedAction;
        public bool IsJustReleased(string action, string handlerLayer = null) => action == JustReleasedAction;
        public bool IsBuffered(string action, float bufferTime) => false;
        public float GetActionStrength(string action, string handlerLayer = null) =>
            action == "left" || action?.EndsWith("_left", StringComparison.Ordinal) == true
                ? Negative
                : action == "right" || action?.EndsWith("_right", StringComparison.Ordinal) == true
                    ? Positive
                    : 0f;
        public float GetHoldTime(string action) => 0f;
        public bool ConsumePressed(string action, string handlerLayer = null) => true;
        public bool ConsumeJustPressed(string action, string handlerLayer = null) => true;
        public bool ConsumeJustReleased(string action, string handlerLayer = null) => true;
    }
}
