using Framework;
using GameLogic;
using Godot;
using System.Text.Json.Serialization;

public enum MovementMode
{
    None,
    RaceOnce,
    Follow
}
[GlobalClass]
public partial class MovementComponent : Component2D
{
    [Export] public float MoveSpeed { get; set; } = 240f;
    [Export] public float StopDistance { get; set; } = 4f;
    [Export] public bool DrawMovePathOnlyWhenSelected { get; set; } = true;
    [Export] public Color MovePathColor { get; set; } = new Color(0.35f, 0.95f, 0.55f, 0.95f);
    [Export] public float MovePathWidth { get; set; } = 3f;

    private SelectionComponent _selectionComponent;
    private Node2D _transformNode2D;
    private Line2D _movePathIndicator;
    [JsonInclude] private Vector2 _moveTarget;
    [JsonInclude] private string _followId;
    [JsonInclude] private MovementMode movementMode = MovementMode.None;

    private GameObject2D _followTargetObject = null;

    public override int Priority => ComponentPriority.Movement;

    public bool HasMoveTarget => movementMode != MovementMode.None;

    public override void OnInit()
    {
        _selectionComponent = Owner?.GetComponent<SelectionComponent>();
        _transformNode2D = Owner;
        _movePathIndicator = SetupMovePathIndicator();
        RefreshMovePathVisual();
    }

    public override void OnPhysicsUpdate(double delta)
    {
        switch (movementMode)
        {
            case MovementMode.None:
                return;
            case MovementMode.RaceOnce:
                MoveTo(_moveTarget, delta);
                break;
            case MovementMode.Follow:
                UpdateFollowMovement(delta);
                break;
        }

        RefreshMovePathVisual();
    }

    private void UpdateFollowMovement(double delta)
    {
        if (string.IsNullOrEmpty(_followId) || _transformNode2D == null)
            return;

        if (_followTargetObject == null)
        {
            _followTargetObject = RootModule.Instance.GameState.GetRegisteredSeriableGameObject(_followId);
        }

        Vector2 targetPosition = _followTargetObject.GlobalPosition;
        MoveTo(targetPosition, delta);
    }

    private void MoveTo(Vector2 targetPosition, double delta)
    {
        if (_transformNode2D == null)
            return;

        Vector2 currentPosition = _transformNode2D.GlobalPosition;

        float distance = currentPosition.DistanceTo(targetPosition);

        if (distance <= StopDistance)
        {
            _transformNode2D.GlobalPosition = targetPosition;
            ClearMoveTarget();
            return;
        }

        Vector2 direction = (targetPosition - currentPosition).Normalized();
        float step = MoveSpeed * (float)delta;
        _transformNode2D.GlobalPosition = currentPosition + direction * Mathf.Min(step, distance);

    }

    public void SetMoveTarget(Vector2 target)
    {
        movementMode = MovementMode.RaceOnce;
        _moveTarget = target;
        RefreshMovePathVisual();
    }

    public void SetFollowTarget(string targetPersistentId)
    {
        movementMode = MovementMode.Follow;
        _followId = targetPersistentId;
        _followTargetObject = RootModule.Instance.GameState.GetRegisteredSeriableGameObject(_followId);
        RefreshMovePathVisual();
    }

    public void ClearMoveTarget()
    {
        _moveTarget = Vector2.Zero;
        _followId = null;
        _followTargetObject = null;
        movementMode = MovementMode.None;
        RefreshMovePathVisual();
    }



    #region DrawMovePath

    public void RefreshMovePathVisual()
    {
        if (_movePathIndicator == null)
            _movePathIndicator = SetupMovePathIndicator();

        if (_movePathIndicator == null || _transformNode2D == null)
            return;

        _movePathIndicator.Width = MovePathWidth;
        _movePathIndicator.DefaultColor = MovePathColor;

        if (!ShouldDrawMovePath())
        {
            _movePathIndicator.Visible = false;
            return;
        }

        var moveTarget = movementMode == MovementMode.Follow ? _followTargetObject?.GlobalPosition : (Vector2?)_moveTarget;


        _movePathIndicator.Visible = true;
        _movePathIndicator.SetPointPosition(0, Vector2.Zero);
        _movePathIndicator.SetPointPosition(1, _transformNode2D.ToLocal(moveTarget.Value));
    }

    private bool ShouldDrawMovePath()
    {
        if (movementMode == MovementMode.None)
            return false;

        _selectionComponent ??= Owner?.GetComponent<SelectionComponent>();
        return !DrawMovePathOnlyWhenSelected || (_selectionComponent?.IsSelected ?? false);
    }

    private Line2D SetupMovePathIndicator()
    {
        if (_transformNode2D == null)
            return null;

        var existingIndicator = _transformNode2D.GetNodeOrNull<Line2D>("MovePathIndicator");
        if (existingIndicator != null)
            return existingIndicator;

        var indicator = new Line2D
        {
            Name = "MovePathIndicator",
            Visible = false,
            Width = MovePathWidth,
            DefaultColor = MovePathColor,
            Antialiased = true,
            ZIndex = 1,
        };

        _transformNode2D.AddChild(indicator);
        indicator.AddPoint(Vector2.Zero);
        indicator.AddPoint(Vector2.Zero);
        return indicator;
    }

    #endregion
}
