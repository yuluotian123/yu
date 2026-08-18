using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using GameLogic;
using Godot;

public partial class CharacterGraphRuntimeSmokeTest : Node
{
    private const double PhysicsDelta = 1d / 60d;
    private const float DashSpeed = 760f;

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
        VerifyTimelineClipKeys();

        PackedScene playerScene = GD.Load<PackedScene>("res://assets/scenes/player.tscn");
        Require(playerScene != null, "Player scene could not be loaded.");

        GameObject2D player = playerScene.Instantiate<GameObject2D>();
        AddChild(player);
        player.SetProcess(false);
        player.SetPhysicsProcess(false);

        CharacterGraphComponent2D graph = player.GetComponent<CharacterGraphComponent2D>();
        CharacterCommandBufferComponent2D commands = player.GetComponent<CharacterCommandBufferComponent2D>();
        CharacterMovementComponent2D movement = player.GetComponent<CharacterMovementComponent2D>();
        SkillManagerComponent2D skills = player.GetComponent<SkillManagerComponent2D>();
        SpriteAnimationComponent2D animations = player.GetComponent<SpriteAnimationComponent2D>();
        Require(graph?.Runtime?.IsRunning == true, "Character graph did not start.");
        Require(commands != null, "Character command buffer is missing.");
        Require(movement != null, "Character movement component is missing.");
        Require(skills != null, "Skill manager is missing.");
        Require(animations != null, "Sprite animation component is missing.");
        Require(graph.PhysicalInputEnabled, "Player physical input is disabled.");
        Require(skills.GetRuntime("dash") != null, "Graph skill index did not register dash by SkillId.");
        Require(graph.CurrentStateName == "Locomotion", $"Expected Locomotion, got {graph.CurrentStateName}.");
        Require(graph.Runtime.ChildRuntime?.Graph?.Connections.Count == 9,
            "Locomotion graph was not simplified to nine connections.");

        graph.SubmitAction(new CharacterActionRequest("attack", 50));
        Tick(player);
        Require(graph.CurrentStateName == "Attack", $"Attack request entered {graph.CurrentStateName}.");
        Require(animations.ActiveRequestKey == "skill:attack:attack_animation",
            $"Attack used unexpected animation request key '{animations.ActiveRequestKey}'.");
        Tick(player);
        Require(animations.ActiveRequestKey == "skill:attack:attack_animation",
            "Attack animation request key changed between timeline phases.");
        Require(skills.ActiveSkillBlocksMovement && skills.ActiveSkillBlocksJump,
            "Attack Action policy was not applied by SkillManager.");
        Require(movement.MovementLocked && movement.JumpLocked,
            "Movement did not consume the active Action policy.");

        commands.Submit(new CharacterCommand2D(1f, false, false), int.MaxValue);
        graph.SubmitAction(new CharacterActionRequest("dash", 100));
        Tick(player);
        Require(graph.CurrentStateName == "Dash", $"Dash did not interrupt Attack; current={graph.CurrentStateName}.");
        Require(animations.ActiveRequestKey == "skill:dash:dash_animation",
            $"Dash used unexpected animation request key '{animations.ActiveRequestKey}'.");
        Require(Mathf.IsEqualApprox(movement.Velocity.X, DashSpeed),
            $"Dash same-frame velocity was {movement.Velocity.X}, expected {DashSpeed}.");

        graph.SubmitAction(new CharacterActionRequest("attack", 50));
        Tick(player);
        Require(graph.CurrentStateName == "Dash", "Lower-priority Attack interrupted Dash.");

        for (int i = 0; i < 24 && graph.CurrentStateName == "Dash"; i++)
            Tick(player);

        Require(graph.CurrentStateName == "Locomotion",
            $"Completed Dash did not resume Locomotion; current={graph.CurrentStateName}.");
        Require(animations.ActiveRequestKey != "skill:attack:attack_animation" &&
            animations.ActiveRequestKey != "skill:dash:dash_animation",
            "Completed or cancelled timeline left a skill animation request active.");

        VerifySaveV2(playerScene, player, movement);
        VerifyAiInputIsolation();
    }

    private static void VerifyTimelineClipKeys()
    {
        var firstClip = new FlowTimelineClip { Id = string.Empty, Name = "First" };
        var secondClip = new FlowTimelineClip { Name = "Second" };
        var timeline = new SkillTimelineNodeData
        {
            Id = "timeline_clip_key_test",
            Duration = 1f,
            Tracks = new List<FlowTimelineTrack>
            {
                new()
                {
                    Name = "Animation",
                    Clips = new List<FlowTimelineClip> { firstClip, secondClip }
                }
            }
        };

        timeline.NormalizeTimelineData();
        Require(!string.IsNullOrWhiteSpace(firstClip.Id), "Timeline normalization did not create an empty clip Id.");

        secondClip.Id = firstClip.Id;
        var validation = new GraphValidationResult();
        timeline.Validate(new SkillFlowGraphAsset(), validation);
        Require(validation.Errors.Any(issue => issue.Message.Contains("duplicated", StringComparison.OrdinalIgnoreCase)),
            "Timeline validation accepted duplicate clip Ids.");

        var executionContext = new GraphExecutionContext(
            new SkillFlowGraphAsset(),
            new GraphBlackboardRuntime());
        executionContext.UserData.Add(new SkillResource { SkillId = "test_skill" });
        var timelineContext = new FlowTimelineContext { ClipId = "clip_a", ClipName = "A", TrackName = "Animation" };
        executionContext.UserData.Add(timelineContext);

        var action = new SkillPlayAnimationAction { AnimationName = "attack" };
        Require(action.ResolveAnimationRequestKey(executionContext) == "skill:test_skill:clip_a",
            "Animation action did not generate a SkillId/ClipId request key.");

        timelineContext.ClipId = "clip_b";
        Require(action.ResolveAnimationRequestKey(executionContext) == "skill:test_skill:clip_b",
            "Two clips in the same skill generated the same request key.");

        action.RequestKey = "manual:animation";
        Require(action.ResolveAnimationRequestKey(executionContext) == "manual:animation",
            "Explicit animation request key did not override the automatic key.");
    }

    private void VerifySaveV2(
        PackedScene playerScene,
        GameObject2D player,
        CharacterMovementComponent2D movement)
    {
        CharacterPersistenceComponent2D persistence = player.GetComponent<CharacterPersistenceComponent2D>();
        Require(persistence != null, "Character persistence component is missing.");

        JsonObject state = persistence.Capture();
        Require(state["skills"] is JsonObject, "Skill cooldown state was not captured.");
        Require(!state.ContainsKey("velocity") &&
            !state.ContainsKey("action") &&
            !state.ContainsKey("input") &&
            !state.ContainsKey("timeline"),
            "Save V2 captured transient character runtime state.");

        Vector2 savedPosition = player.GlobalPosition;
        int savedFacing = movement.Facing;
        player.GlobalPosition += new Vector2(1000f, 500f);
        movement.RestoreFacing(-savedFacing);
        persistence.Restore(state, persistence.SchemaVersion);

        Require(player.GlobalPosition.IsEqualApprox(savedPosition), "Save V2 did not restore character position.");
        Require(movement.Facing == savedFacing, "Save V2 did not restore character facing.");

        GameObject2D restoredPlayer = playerScene.Instantiate<GameObject2D>();
        AddChild(restoredPlayer);
        restoredPlayer.SetProcess(false);
        restoredPlayer.SetPhysicsProcess(false);
        SkillManagerComponent2D restoredSkills = restoredPlayer.GetComponent<SkillManagerComponent2D>();
        restoredSkills.RestoreDurableState(state["skills"] as JsonObject);
        SkillRuntime restoredDash = restoredSkills.GetRuntime("dash");
        double now = Time.GetTicksMsec() * 0.001d;
        Require(restoredDash != null && restoredDash.CooldownRemaining(now) > 0f,
            "Fresh graph skill index did not restore dash cooldown by SkillId.");
        restoredPlayer.QueueFree();
    }

    private void VerifyAiInputIsolation()
    {
        PackedScene aiScene = GD.Load<PackedScene>("res://assets/scenes/ai_runner.tscn");
        Require(aiScene != null, "AI scene could not be loaded.");

        GameObject2D ai = aiScene.Instantiate<GameObject2D>();
        AddChild(ai);
        ai.SetProcess(false);
        ai.SetPhysicsProcess(false);
        CharacterGraphComponent2D graph = ai.GetComponent<CharacterGraphComponent2D>();
        Require(graph != null && !graph.PhysicalInputEnabled,
            "AI character accepts physical player input.");
        Require(ai.GetComponent(typeof(ICharacterInputProvider)) == null,
            "AI still exposes a physical input provider.");
        ai.QueueFree();
    }

    private static void Tick(GameObject2D player)
    {
        foreach (Component2D component in player.GetAllComponents())
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
}
