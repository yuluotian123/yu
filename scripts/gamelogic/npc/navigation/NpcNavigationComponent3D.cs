using System;
using Godot;

namespace GameLogic;

public enum NpcNavigationStatus { Idle, Moving, Arrived, Failed }

[Tool, GlobalClass]
public partial class NpcNavigationComponent3D : Component3D
{
    public override int Priority => ComponentPriority.State;
    [Export] public NodePath AgentPath { get; set; } = new("PhysicsBody/NavigationAgent3D");
    [Export(PropertyHint.Range, "0.5,20,0.5")] public double StuckSeconds { get; set; } = 4;
    [Export] public bool EnableAvoidance { get; set; } = true;
    public NavigationAgent3D Agent { get; private set; }
    public NpcNavigationStatus Status { get; private set; }
    public string Failure { get; private set; } = "";
    public string TargetId { get; private set; } = "";
    public string Diagnostics => $"position={_movement?.Body?.GlobalPosition} velocity={_movement?.Velocity} safe={_safeVelocity} ready={_safeReady} agent={Agent?.Velocity} offset={Agent?.PathHeightOffset} index={Agent?.GetCurrentNavigationPathIndex()} target={Agent?.TargetPosition}";
    public bool MapReady => Agent != null && Agent.GetNavigationMap().IsValid
        && NavigationServer3D.MapGetIterationId(Agent.GetNavigationMap()) > 0;
    private CharacterMovementComponent3D _movement;
    private Vector3 _safeVelocity, _lastProgress;
    private bool _safeReady;
    private double _stuck, _elapsed;
    private Vector3 _positionTarget;
    private float _positionRadius = .5f;
    public bool TryEstimateBetween(Vector3 from, Vector3 to, out double distance)
    {
        distance = 0;
        if (!MapReady) return false;
        var path = NavigationServer3D.MapGetPath(Agent.GetNavigationMap(), from, to, true);
        if (path.Length == 0 || HorizontalDistance(path[^1], to) > .55f) return false;
        for (int i = 1; i < path.Length; i++) distance += path[i - 1].DistanceTo(path[i]);
        return true;
    }
    public bool BeginPosition(Vector3 floorPosition, string targetId, float radius = .5f)
    {
        Stop();
        if (!MapReady) { Fail("NavigationUnavailable"); return false; }
        Failure = "";
        _positionTarget = floorPosition; TargetId = targetId; _positionRadius = radius;
        _lastProgress = Owner.GlobalPosition; _stuck = _elapsed = 0;
        Status = NpcNavigationStatus.Moving;
        Agent.TargetPosition = floorPosition + Vector3.Up * _movement.HalfHeight;
        return true;
    }

    public override void OnInit()
    {
        _movement = Owner.GetComponent<CharacterMovementComponent3D>();
        Agent = Owner.GetNodeOrNull<NavigationAgent3D>(AgentPath);
        if (Agent == null || _movement == null) { Failure = "MissingNavigationOrMovement"; return; }
        Agent.AvoidanceEnabled = EnableAvoidance;
        Agent.Use3DAvoidance = false;
        Agent.MaxSpeed = _movement.Profile?.MoveSpeed ?? 2.8f;
        // Body origin is at its centre; navmesh points are on the floor.
        // Godot subtracts this offset when testing whether a waypoint was reached.
        Agent.PathHeightOffset = -_movement.HalfHeight;
        Agent.VelocityComputed += OnSafeVelocity;
        Status = NpcNavigationStatus.Idle;
    }
    public override void OnPhysicsUpdate(double delta)
    {
        if (Agent == null || _movement?.Body == null) return;
        if (Status != NpcNavigationStatus.Moving) { Agent.Velocity = Vector3.Zero; return; }
        if (!MapReady) { Fail("NavigationUnavailable"); return; }
        _elapsed += delta;
        if (HorizontalDistance(Owner.GlobalPosition, _positionTarget) <= _positionRadius) { Status = NpcNavigationStatus.Arrived; Halt(); return; }
        Vector3 next = Agent.GetNextPathPosition();
        // Godot's reachability check compares the offset path end against TargetDesiredDistance.
        // Our business point can legally lie within UseRadius of a baked edge; verify the real
        // interaction distance instead of rejecting a valid path on its first frame.
        if (Agent.IsNavigationFinished()) { Fail("TargetUnreachable"); return; }
        if (HorizontalDistance(Owner.GlobalPosition, _lastProgress) > .08f) { _lastProgress = Owner.GlobalPosition; _stuck = 0; }
        else _stuck += delta;
        if (_stuck >= StuckSeconds || _elapsed >= 90) { Fail("NavigationStuck"); return; }
        Vector3 direction = next - _movement.Body.GlobalPosition;
        direction.Y = 0;
        float speed = _movement.Profile?.MoveSpeed ?? 2.8f;
        Vector3 desired = direction.Normalized() * speed;
        Agent.MaxSpeed = speed;
        Agent.Velocity = desired;
        Vector3 velocity = EnableAvoidance ? (_safeReady ? _safeVelocity : Vector3.Zero) : desired;
        _movement.SubmitCommand(new(velocity.X / speed, false, false, velocity.Z / speed), ComponentPriority.AI);
        if (EnableAvoidance) _movement.RequestVelocityOverride(new(velocity, overrideHorizontal: true, overrideVertical: false, priority: ComponentPriority.AI));
    }
    private void OnSafeVelocity(Vector3 velocity) { _safeVelocity = velocity; _safeReady = true; }
    private void Fail(string reason) { Failure = reason; Status = NpcNavigationStatus.Failed; Halt(); }
    private void Halt()
    {
        if (Agent != null) Agent.Velocity = Vector3.Zero;
        _movement?.SubmitCommand(CharacterCommand3D.None, ComponentPriority.AI);
        _movement?.RequestVelocityOverride(new(Vector3.Zero, overrideHorizontal: true, overrideVertical: false, priority: ComponentPriority.AI));
    }
    public void Stop()
    {
        Halt();
        Status = NpcNavigationStatus.Idle;
        TargetId = "";
        _safeReady = false;
    }
    private static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
    public override void OnDestroy()
    {
        Stop();
        if (GodotObject.IsInstanceValid(Agent)) Agent.VelocityComputed -= OnSafeVelocity;
        Agent = null;
        _movement = null;
    }
}
