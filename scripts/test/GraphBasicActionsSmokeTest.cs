using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameLogic;
using Godot;

public partial class GraphBasicActionsSmokeTest : Node
{
    private int _checks;
    private void Check(bool valid, string message)
    {
        if (!valid) throw new Exception(message);
        _checks++; GD.Print("PASS: " + message);
    }
    public override async void _Ready()
    {
        try
        {
            AddChild(new GraphBlackboardNode());
            CheckSequences(); CheckTargets(); CheckAbilities(); CheckEditorsAndSerialization();
            CheckMovementActions(); await CheckGroundedJump();
            CheckGeneralFlow(); CheckCharacterActivation();
            GD.Print($"GRAPH_BASIC_ACTIONS_SMOKE_OK ({_checks} checks)");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static FlowGraphAsset Flow(GraphNodeData node, FlowGraphAsset graph = null)
    {
        graph ??= new FlowGraphAsset { ActionDependencyMode = GraphActionDependencyMode.Reusable };
        var entry = new FlowEntryNodeData();
        graph.Nodes.Add(entry); graph.Nodes.Add(node);
        graph.Connections.Add(new FlowConnection { FromNode = entry.Id, ToNode = node.Id });
        return graph;
    }
    private static BehaviorTreeGraphAsset Tree(BehaviorTreeNodeData node)
    {
        var graph = new BehaviorTreeGraphAsset { ActionDependencyMode = GraphActionDependencyMode.Reusable };
        var root = new BehaviorRootNodeData(); graph.Nodes.Add(root); graph.Nodes.Add(node);
        graph.Connections.Add(new BehaviorTreeConnection { FromNode = root.Id, ToNode = node.Id });
        return graph;
    }
    private static GraphExecutionContext Context(GraphAsset graph, GameObject3D host = null, bool local = false)
    {
        var context = new GraphExecutionContext(graph, new GraphBlackboardRuntime());
        if (host != null) context.UserData.Add(host);
        if (local) context.Blackboard.PushLocal(graph);
        return context;
    }
    private void CheckSequences()
    {
        var actions = new List<GraphActionBase>
        {
            new AddBlackboardNumberAction { Key = new() { Key = "Count" } },
            new WaitSecondsAction { Seconds = GraphActionValue.Number(0.5f) },
            new SetBlackboardAction { Key = new() { Key = "Done" }, Value = new() { Constant = new GraphBoolBlackboardValue { Value = true } } }
        };
        var node = new FlowActionNodeData { Actions = actions };
        var graph = Flow(node);
        graph.BlackboardEntries.Add(new() { Key = "Count", Value = new GraphIntBlackboardValue() });
        var first = new FlowGraphRuntime(graph); var second = new FlowGraphRuntime(graph);
        Check(first.Start() && second.Start(), "Two runtimes can start the same shared action asset");
        first.Update(0.25); first.Update(0.25);
        Check(first.IsCompleted && !second.IsCompleted, "Wait progress is isolated per runtime");
        Check(first.Context.Blackboard.GetValue<int>("Count") == 1 && first.Context.Blackboard.GetValue<bool>("Done"), "Flow executes instant actions once around a wait");
        second.Update(0.1);
        Check(second.Context.Blackboard.GetValue<int>("Count") == 1, "A running action does not repeat preceding actions");
        second.Stop(); first.Stop();

        var behaviorNode = new BehaviorActionNodeData { Actions = actions };
        var behaviorGraph = Tree(behaviorNode);
        behaviorGraph.BlackboardEntries.Add(new() { Key = "Count", Value = new GraphIntBlackboardValue() });
        var behavior = new BehaviorTreeRuntime(behaviorGraph);
        Check(behavior.Start(), "Behavior tree accepts shared action definitions");
        Check(behavior.Update(0.1) == BehaviorTreeStatus.Running && behavior.Update(0.2) == BehaviorTreeStatus.Running,
            "Behavior tree keeps waiting instead of completing early");
        Check(behavior.Context.Blackboard.GetValue<int>("Count") == 1, "Behavior tree preserves its action-list cursor");
        Check(behavior.Update(0.3) == BehaviorTreeStatus.Success, "Behavior tree finishes the same shared wait");
        behavior.Update(0.1);
        Check(behavior.Context.Blackboard.GetValue<int>("Count") == 2, "A completed behavior can start a fresh activation");
        behavior.Stop();

        var failureNode = new FlowActionNodeData { Actions = new() { new SetBlackboardAction { Value = GraphActionValue.Key("Missing") } } };
        var failureGraph = Flow(failureNode);
        var failed = new FlowReturnNodeData { Label = "FailedBranch" }; failureGraph.Nodes.Add(failed);
        failureGraph.Connections.Add(new FlowConnection { FromNode = failureNode.Id, FromPort = 1, ToNode = failed.Id });
        var failure = new FlowGraphRuntime(failureGraph);
        Check(failure.Start() && failure.ReturnLabels.Contains("FailedBranch"), "Action failure follows the Flow failure port"); failure.Stop();

        var single = new GraphActionNodeData { Action = new WaitSecondsAction { Seconds = GraphActionValue.Number(0.2f) } };
        var singleRuntime = new FlowGraphRuntime(Flow(single)); singleRuntime.Start();
        Check(!singleRuntime.IsCompleted, "Single Action nodes also retain ongoing tasks");
        singleRuntime.Update(0.3); Check(singleRuntime.IsCompleted, "Single Action tasks complete after elapsed time"); singleRuntime.Stop();
    }

    private GameObject3D Host(string name)
    {
        var host = new GameObject3D { Name = name };
        host.AddChild(new CharacterBody3D { Name = "PhysicsBody" });
        host.AddChild(new Node3D { Name = "VisualRoot" });
        host.AddComponent<CharacterMovementComponent3D>(); host.AddComponent<AbilitySystemComponent3D>();
        AddChild(host); host.SetProcess(false); host.SetPhysicsProcess(false);
        return host;
    }
    private void CheckTargets()
    {
        var host = Host("TargetHost");
        var target = new Node3D { Name = "Enemy", Position = new Vector3(100, 0, 0) }; AddChild(target); target.AddToGroup("smoke_targets");
        var graph = new FlowGraphAsset { ActionDependencyMode = GraphActionDependencyMode.Reusable };
        var context = Context(graph, host, true); var call = new GraphActionInvocation(context);
        Check(new FindNearestTargetAction { Group = "smoke_targets", Radius = GraphActionValue.Number(600) }.CreateTask(call).Tick(0) == BehaviorTreeStatus.Success,
            "FindNearestTarget writes a target into the blackboard");
        Check(new TargetValidCondition().IsMet(context) && new TargetDistanceCondition { Distance = GraphActionValue.Number(101) }.IsMet(context),
            "Target validity and distance conditions consume that target");
        var movement = host.GetComponent<CharacterMovementComponent3D>();
        using var ai = new SimpleAICharacterControllerComponent3D();
        movement.RestoreFacing(-1);
        Check(new FaceTargetAction().CreateTask(call).Tick(0) == BehaviorTreeStatus.Success && movement.Facing == 1,
            "Reusable FaceTarget resolves the current host's movement component");
        var action = new MoveToTargetAction { ArrivalDistance = GraphActionValue.Number(5) };
        var task = action.CreateTask(call);
        Check(task.Tick(0.1) == BehaviorTreeStatus.Running, "MoveTo remains running while the target is distant");
        task.Cancel();
        var pending = (CharacterCommand3D)typeof(CharacterMovementComponent3D).GetField("_pendingCommand", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(movement)!;
        Check(pending.MoveAxisX == 0, "Cancelling MoveTo clears its pending movement input");
        task = action.CreateTask(call); task.Tick(0.1); host.Position = new Vector3(99, 0, 0);
        Check(task.Tick(0.1) == BehaviorTreeStatus.Success, "MoveTo finishes within arrival distance");
        host.Position = Vector3.Zero;
        task = new MoveToTargetAction { Timeout = GraphActionValue.Number(0.1f) }.CreateTask(call);
        Check(task.Tick(0.2) == BehaviorTreeStatus.Failure, "MoveTo fails on timeout");
        graph.ActionDependencyMode = GraphActionDependencyMode.HostBound;
        Check(new FaceTargetAction().CreateTask(call).Tick(0) == BehaviorTreeStatus.Failure, "HostBound fails without a component dependency");
        var bound = new FaceTargetAction { Movement = new() { ComponentTypeName = typeof(CharacterMovementComponent3D).FullName } };
        Check(bound.CreateTask(call).Tick(0) == BehaviorTreeStatus.Success, "HostBound resolves an explicit type and slot");
        var wired = new GraphActionInvocation(context, new Dictionary<string, object> { ["Component"] = movement });
        Check(new FaceTargetAction().CreateTask(wired).Tick(0) == BehaviorTreeStatus.Success, "HostBound accepts a component supplied by a graph input");
        graph.ActionDependencyMode = GraphActionDependencyMode.Reusable;
        var integratedLeaf = new BehaviorActionNodeData { Actions = new() { new MoveToTargetAction() } };
        ai.Owner = host; ai.Graph = Tree(integratedLeaf); ai.OnInit();
        ai.Runtime.SetValue("Target", target.GetPath().ToString()); ai.OnPhysicsUpdate(0.1);
        pending = (CharacterCommand3D)typeof(CharacterMovementComponent3D).GetField("_pendingCommand", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(movement)!;
        Check(pending.MoveAxisX > 0, "The actual AI controller preserves MoveTo input without controller-specific action coupling");
        ai.OnDestroy();
        var leaf = new BehaviorActionNodeData { Actions = new() { new MoveToTargetAction() } };
        var timeout = new BehaviorTimeoutNodeData { Seconds = 0.25f };
        var tree = Tree(timeout); tree.Nodes.Add(leaf);
        tree.Connections.Add(new BehaviorTreeConnection { FromNode = timeout.Id, ToNode = leaf.Id });
        var behavior = new BehaviorTreeRuntime(tree, Context(tree, host)); behavior.Start();
        behavior.SetValue("Target", target.GetPath().ToString());
        Check(behavior.Update(0.1) == BehaviorTreeStatus.Running && behavior.Update(0.2) == BehaviorTreeStatus.Failure,
            "Timeout decorator aborts a running child"); behavior.Stop();
        target.Free();
        Check(!new TargetValidCondition().IsMet(context) && !new TargetDistanceCondition().IsMet(context), "Freed targets are rejected safely");
        Check(new FindNearestTargetAction { Group = "smoke_targets" }.CreateTask(call).Tick(0) == BehaviorTreeStatus.Failure
            && context.Blackboard.GetValue<string>("Target") == "", "No target clears the previous target and returns failure");
        context.Blackboard.PopLocal(); host.Free();
    }

    private static CharacterCommand3D Pending(CharacterMovementComponent3D movement) =>
        (CharacterCommand3D)typeof(CharacterMovementComponent3D).GetField("_pendingCommand", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(movement)!;

    private void CheckMovementActions()
    {
        var host = Host("GenericMovement"); var movement = host.GetComponent<CharacterMovementComponent3D>();
        var graph = new FlowGraphAsset { ActionDependencyMode = GraphActionDependencyMode.Reusable };
        var context = Context(graph, host, true); var call = new GraphActionInvocation(context);
        Check(new SetFacingAction { Direction = GraphActionValue.Number(-1) }.CreateTask(call).Tick(0) == BehaviorTreeStatus.Success
            && movement.Facing == -1, "Generic facing works with no AI controller");
        context.Blackboard.SetValue("Direction", 1f);
        var move = new MoveInDirectionAction { Direction = GraphActionValue.Key("Direction"), Duration = GraphActionValue.Number(1) }.CreateTask(call);
        movement.RequestJumpStart(ComponentPriority.AI);
        Check(move.Tick(0.2) == BehaviorTreeStatus.Running && Pending(movement).MoveAxisX == 1 && Pending(movement).JumpStartRequested,
            "Movement preserves parallel jump input");
        context.Blackboard.SetValue("Direction", -1f); move.Tick(0.2);
        Check(Pending(movement).MoveAxisX == -1, "Directional movement reads updated blackboard input");
        Check(move.Tick(0.7) == BehaviorTreeStatus.Success && Pending(movement).MoveAxisX == 0, "Timed movement stops on completion");
        var definition = new PatrolAction { Distance = GraphActionValue.Number(10), TurnPause = GraphActionValue.Number(0.1f) };
        var first = definition.CreateTask(call); first.Tick(0);
        host.Position = new Vector3(11, 0, 0);
        Check(first.Tick(0.01) == BehaviorTreeStatus.Running && Pending(movement).MoveAxisX == 0 && movement.Facing == -1,
            "Patrol reverses at its bound and pauses");
        var second = definition.CreateTask(call); second.Tick(0);
        Check(Pending(movement).MoveAxisX == 1, "Shared patrol definition has isolated direction and origin per task");
        first.Tick(0.2); first.Tick(0.01);
        Check(Pending(movement).MoveAxisX == -1, "Patrol resumes in its own direction after the pause");
        first.Cancel(); second.Cancel();
        Check(Pending(movement).MoveAxisX == 0, "Cancelling patrol stops movement");
        Check(!new IsOnFloorCondition().IsMet(context), "Floor condition detects an airborne host without AI state");
        Check(new JumpAction().CreateTask(call).Tick(0) == BehaviorTreeStatus.Failure, "Ground-required jump rejects an airborne host");
        movement.AddMovementInput(1, ComponentPriority.AI);
        var jump = new JumpAction { RequireGround = false }.CreateTask(call); jump.Tick(0); jump.Cancel();
        Check(Pending(movement).MoveAxisX == 1 && !Pending(movement).JumpStartRequested && !Pending(movement).JumpSustainRequested,
            "Cancelling a jump clears start and hold while preserving movement");
        graph.ActionDependencyMode = GraphActionDependencyMode.HostBound;
        Check(new MoveInDirectionAction().CreateTask(call).Tick(0) == BehaviorTreeStatus.Failure, "Generic movement respects HostBound explicit binding");
        var wired = new GraphActionInvocation(context, new Dictionary<string, object> { ["Component"] = movement });
        move = new MoveInDirectionAction().CreateTask(wired);
        Check(move.Tick(0) == BehaviorTreeStatus.Running, "Generic movement accepts wired components"); move.Cancel();
        var migrated = ResourceLoader.Load<BehaviorTreeGraphAsset>("res://assets/graphs/ai_patrol_behavior_tree.tres", cacheMode: ResourceLoader.CacheMode.Ignore);
        var runtime = new BehaviorTreeRuntime(migrated); runtime.Context.UserData.Add(host);
        Check(runtime.Start() && runtime.Update(0.1) == BehaviorTreeStatus.Running, "Migrated patrol resource runs without an AI controller");
        Check(migrated.Nodes.Count == 4 && migrated.Nodes.OfType<BehaviorActionNodeData>().SelectMany(n => n.Actions).All(a => a is PatrolAction or PeriodicJumpAction),
            "Migrated graph contains only the generic patrol and jump branches"); runtime.Stop();
        context.Blackboard.PopLocal(); host.Free();
    }

    private async System.Threading.Tasks.Task CheckGroundedJump()
    {
        var floor = new StaticBody3D { Position = new Vector3(0, -0.3f, 0) };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(10, 0.2f, 10) } }); AddChild(floor);
        var host = Host("JumpPhysics"); var movement = host.GetComponent<CharacterMovementComponent3D>();
        movement.BodySize = new Vector3(0.1f, 0.1f, 0.1f);
        movement.Body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.1f, 0.1f, 0.1f) } });
        for (int i = 0; i < 60 && !movement.IsOnFloor; i++)
        { await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); movement.OnPhysicsUpdate(1.0 / 60); }
        var graph = new FlowGraphAsset { ActionDependencyMode = GraphActionDependencyMode.Reusable };
        var context = Context(graph, host, true); var call = new GraphActionInvocation(context);
        Check(new IsOnFloorCondition().IsMet(context) && new GroundAheadCondition().IsMet(context), "Generic floor and edge queries use actual physics");
        var definition = new PeriodicJumpAction { Interval = GraphActionValue.Number(0.5f) };
        var a = definition.CreateTask(call); var b = definition.CreateTask(call);
        movement.ClearJumpInput(ComponentPriority.AI); a.Tick(0.4);
        Check(!Pending(movement).JumpStartRequested, "Periodic jump waits for its interval");
        b.Tick(0.2); Check(!Pending(movement).JumpStartRequested, "Periodic jump cooldown is isolated per task");
        a.Tick(0.2); Check(Pending(movement).JumpStartRequested && Pending(movement).JumpSustainRequested, "Periodic jump requests a grounded jump and hold");
        a.Cancel(); b.Cancel();
        var jump = new JumpAction { HoldDuration = GraphActionValue.Number(0.1f) }.CreateTask(call);
        Check(jump.Tick(0) == BehaviorTreeStatus.Running, "Grounded Jump starts");
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); movement.OnPhysicsUpdate(1.0 / 60);
        Check(movement.Body.Velocity.Y > 0 && jump.Tick(0.2) == BehaviorTreeStatus.Success && !Pending(movement).JumpSustainRequested,
            "Jump produces upward velocity and releases hold after its duration");
        context.Blackboard.PopLocal(); host.Free(); floor.Free();
    }

    private void CheckGeneralFlow()
    {
        var wait = new FlowWaitEventNodeData { EventName = "Ready", Timeout = 0.2f };
        var graph = Flow(wait);
        var received = new FlowReturnNodeData { Label = "Received" }; var timedOut = new FlowReturnNodeData { Label = "TimedOut" };
        graph.Nodes.Add(received); graph.Nodes.Add(timedOut);
        graph.Connections.Add(new FlowConnection { FromNode = wait.Id, ToNode = received.Id });
        graph.Connections.Add(new FlowConnection { FromNode = wait.Id, FromPort = 1, ToNode = timedOut.Id });
        var first = new FlowGraphRuntime(graph); var second = new FlowGraphRuntime(graph);
        first.Context.Events.Publish("Ready");
        Check(first.Start() && second.Start() && !first.IsCompleted, "Event wait ignores events published before entering");
        first.Context.Events.Publish("Ready"); first.Update(0.1); second.Update(0.1);
        Check(first.ReturnLabels.Contains("Received") && !second.IsCompleted, "Event streams and wait progress are isolated per runtime");
        second.Update(0.2); Check(second.ReturnLabels.Contains("TimedOut"), "Event wait follows its timeout output"); first.Stop(); second.Stop();
        var sequence = new FlowSequenceNodeData(); var branches = Flow(sequence);
        var awaiting = new FlowWaitEventNodeData { EventName = "Signal" }; var publish = new FlowPublishEventNodeData { EventName = "Signal" };
        branches.Nodes.Add(awaiting); branches.Nodes.Add(publish);
        branches.Connections.Add(new FlowConnection { FromNode = sequence.Id, ToNode = awaiting.Id });
        branches.Connections.Add(new FlowConnection { FromNode = sequence.Id, FromPort = 1, ToNode = publish.Id });
        var flow = new FlowGraphRuntime(branches);
        Check(flow.Start() && flow.Context.Events.GetVersion("Signal") == 1, "Sequence starts the second branch while the first waits");
        flow.Update(0); Check(flow.IsCompleted, "Publish Event wakes a sibling flow branch"); flow.Stop();
        var begin = new CharacterLifecycleEventNodeData { Event = CharacterLifecycleEvent.BeginPlay };
        var characterWait = new FlowWaitEventNodeData { EventName = "External" };
        var done = new GraphActionNodeData { Action = new SetBlackboardAction { Key = new() { Key = "Done" }, Value = GraphActionValue.Number(1) } };
        var character = new CharacterGraphAsset { Nodes = new List<GraphNodeData> { begin, characterWait, done }, Connections = new List<GraphConnection>()
        {
            new CharacterGraphConnection { FromNode = begin.Id, ToNode = characterWait.Id },
            new CharacterGraphConnection { FromNode = characterWait.Id, ToNode = done.Id }
        } };
        var runtime = new CharacterGraphRuntime(character, null, null); runtime.Update(0, false);
        runtime.Context.Events.Publish("External"); runtime.Update(0.01, false);
        Check(runtime.ActiveExecutionCount == 0 && runtime.Context.Blackboard.GetValue<float>("Done") == 1,
            "Character event executions share their owner's generic event stream"); runtime.Stop();
        foreach (var node in new GraphNodeData[] { sequence, wait, publish })
            Check(GraphJsonHelper.Deserialize<GraphNodeData>(GraphJsonHelper.Serialize(node)).GetType() == node.GetType(), node.GetType().Name + " serializes");
        var invalid = new GraphValidationResult(); new FlowWaitEventNodeData { EventName = "Missing", Timeout = 0 }.Validate(graph, invalid);
        Check(!invalid.IsValid, "Wait Event requires a finite positive timeout");
    }

    private void CheckCharacterActivation()
    {
        var host = Host("CharacterActivation"); var system = host.GetComponent<AbilitySystemComponent3D>();
        system.GrantAbility(Ability("attack", 0.5f));
        var node = new CharacterAbilityNodeData { AbilityId = "attack" };
        var graph = new CharacterGraphAsset { Nodes = new List<GraphNodeData> { node } };
        var character = new CharacterGraphRuntime(graph, host, null);
        var flow = new FlowGraphRuntime(graph, character.Context);
        node.Enter(flow, character.Context);
        Check(!node.TryGetCompletion(flow, character.Context, out _), "Character ability waits for its own running activation");
        system.CancelAbility("attack"); system.TryActivateAbility("attack");
        Check(node.TryGetCompletion(flow, character.Context, out var replaced) && replaced.OutputPort == 2,
            "A restarted ability sends the old Character node to Cancelled");
        node.Exit(flow, character.Context);
        Check(system.GetRuntime("attack").IsRunning, "Exiting an old Character node leaves the newer activation intact");
        system.CancelAbility("attack"); node.Enter(flow, character.Context); system.OnPhysicsUpdate(0.6);
        Check(node.TryGetCompletion(flow, character.Context, out var completed) && completed.OutputPort == 1,
            "Character ability still uses Completed for its own successful activation");
        node.Exit(flow, character.Context); character.Stop(); host.Free();
    }

    private static AbilityResource Ability(string id, float duration)
    {
        var graph = new AbilityFlowGraphAsset();
        Flow(new FlowActionNodeData { Actions = new() { new WaitSecondsAction { Seconds = GraphActionValue.Number(duration) } } }, graph);
        return new AbilityResource { AbilityId = id, Graph = graph };
    }
    private void CheckAbilities()
    {
        var host = Host("AbilityHost"); var system = host.GetComponent<AbilitySystemComponent3D>();
        system.GrantAbility(Ability("attack", 0.5f));
        var graph = new BehaviorTreeGraphAsset { ActionDependencyMode = GraphActionDependencyMode.Reusable };
        var context = Context(graph, host, true); var call = new GraphActionInvocation(context);
        Check(new CanUseAbilityCondition().IsMet(context) && !system.GetRuntime("attack").IsRunning,
            "CanUseAbility validates without activating the skill");
        var definition = new UseAbilityAction(); var task = definition.CreateTask(call);
        Check(task.Tick(0) == BehaviorTreeStatus.Running && system.GetRuntime("attack").IsRunning, "UseAbility starts and waits for its activation");
        Check(!new CanUseAbilityCondition().IsMet(context), "CanUseAbility detects an already active skill");
        system.OnPhysicsUpdate(0.6);
        Check(task.Tick(0.6) == BehaviorTreeStatus.Success, "UseAbility succeeds on actual skill completion");
        task = definition.CreateTask(call); task.Tick(0); task.Cancel();
        Check(!system.GetRuntime("attack").IsRunning, "Aborting UseAbility cancels its owned activation");
        task = definition.CreateTask(call); task.Tick(0);
        system.CancelAbility("attack"); system.TryActivateAbility("attack"); task.Cancel();
        Check(system.GetRuntime("attack").IsRunning, "An old task cannot cancel a newer activation of the same skill");
        Check(task.Tick(0) == BehaviorTreeStatus.Failure, "Replaced activations do not count as successful completion");
        Check(new CancelAbilityAction().CreateTask(call).Tick(0) == BehaviorTreeStatus.Success, "CancelAbility explicitly cancels the requested skill");
        task = new UseAbilityAction { WaitForCompletion = false }.CreateTask(call);
        Check(task.Tick(0) == BehaviorTreeStatus.Success && system.GetRuntime("attack").IsRunning, "Request-only mode completes without waiting");
        system.CancelAbility("attack");
        task = new UseAbilityAction { Timeout = GraphActionValue.Number(0.1f) }.CreateTask(call); task.Tick(0);
        Check(task.Tick(0.2) == BehaviorTreeStatus.Failure && !system.GetRuntime("attack").IsRunning, "Ability wait timeout cancels the owned activation");
        task = new UseAbilityAction { CancelOnAbort = false }.CreateTask(call); task.Tick(0); task.Cancel();
        Check(system.GetRuntime("attack").IsRunning, "CancelOnAbort can leave a skill running intentionally");
        system.CancelAbility("attack");
        Check(new UseAbilityAction { AbilityId = GraphActionValue.Text("unknown") }.CreateTask(call).Tick(0) == BehaviorTreeStatus.Failure,
            "Missing abilities return failure");
        var leaf = new BehaviorActionNodeData { Actions = new() { new UseAbilityAction() } }; var tree = Tree(leaf);
        var behavior = new BehaviorTreeRuntime(tree, Context(tree, host)); behavior.Start(); behavior.Update(0);
        Check(system.GetRuntime("attack").IsRunning, "Behavior tree starts UseAbility through the shared runner");
        behavior.Stop(); Check(!system.GetRuntime("attack").IsRunning, "Stopping the behavior runtime cancels its active task");
        var flowNode = new GraphActionNodeData { Action = new UseAbilityAction() }; var flow = Flow(flowNode);
        var flowRuntime = new FlowGraphRuntime(flow, Context(flow, host)); flowRuntime.Start(); flowRuntime.Stop();
        Check(!system.GetRuntime("attack").IsRunning, "Stopping a Flow runtime cancels its active task");
        context.Blackboard.PopLocal(); host.Free();
    }

    private void CheckEditorsAndSerialization()
    {
        var graph = new FlowGraphAsset { ActionDependencyMode = GraphActionDependencyMode.Reusable };
        var context = Context(graph, local: true);
        context.Blackboard.SetValue("Count", 3);
        Check(new BlackboardCompareCondition { Left = GraphActionValue.Key("Count"), Right = GraphActionValue.Number(2),
            Comparison = GraphValueComparison.Greater }.IsMet(context), "Blackboard comparison resolves numeric operands");
        Check(!new BlackboardCompareCondition { Left = GraphActionValue.Key("Missing") }.IsMet(context), "Missing blackboard inputs do not pass conditions");
        var actions = SubTypeCache.GetSubTypes<GraphSharedAction>();
        Check(actions.Count >= 9 && SubTypeCache.GetSubTypes<BehaviorTreeActionBase>().Contains(typeof(UseAbilityAction)),
            "Shared actions are discoverable from the behavior tree action picker");
        foreach (Type type in actions)
        {
            var action = (GraphActionBase)Activator.CreateInstance(type)!;
            var copy = GraphJsonHelper.Deserialize<GraphActionBase>(GraphJsonHelper.Serialize(action));
            Check(copy.GetType() == type, type.Name + " survives graph serialization");
            Control ui = action.CreateEditUI(new GraphEditorContext { CurrentGraph = graph }); ui.Free();
        }
        foreach (Type type in SubTypeCache.GetSubTypes<GraphSharedCondition>())
        {
            var condition = (GraphConditionBase)Activator.CreateInstance(type)!;
            Check(GraphJsonHelper.Deserialize<GraphConditionBase>(GraphJsonHelper.Serialize(condition)).GetType() == type,
                type.Name + " survives graph serialization");
            Control ui = condition.CreateEditUI(new GraphEditorContext { CurrentGraph = graph }); ui.Free();
        }
        var timeline = new FlowTimelineNodeData(); graph.Nodes.Add(timeline);
        var validation = new GraphValidationResult(); new WaitSecondsAction().Validate(graph, timeline.Id, validation);
        Check(!validation.IsValid, "Timeline rejects actions that require an ongoing task lifecycle");
        var phase = new FlowTimelineContext(); context.UserData.Add(phase);
        var increment = new AddBlackboardNumberAction { Key = new() { Key = "Count" } };
        foreach (var item in new[] { FlowTimelinePhase.Start, FlowTimelinePhase.Update, FlowTimelinePhase.Complete, FlowTimelinePhase.Cancel })
        { phase.Phase = item; increment.Execute(context); }
        Check(context.Blackboard.GetValue<int>("Count") == 4, "Instant timeline actions execute once at clip start");
        context.Blackboard.PopLocal();
    }
}
