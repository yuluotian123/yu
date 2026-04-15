using Framework;
using GameLogic;
using Godot;
using System.Text.Json.Serialization;

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
    [JsonInclude] private Vector2? _moveTarget;

    public override int Priority => ComponentPriority.Movement;

    public bool HasMoveTarget => _moveTarget.HasValue;

    public Vector2? CurrentMoveTarget => _moveTarget;

    public override void OnInit()
    {
        _selectionComponent = Owner?.GetComponent<SelectionComponent>();
        _transformNode2D = Owner;
        _movePathIndicator = SetupMovePathIndicator();
        RefreshMovePathVisual();
    }

    public override void OnPhysicsUpdate(double delta)
    {
        if ((_movePathIndicator?.Visible ?? false) || _moveTarget.HasValue)
            RefreshMovePathVisual();

        if (!_moveTarget.HasValue || _transformNode2D == null)
            return;

        Vector2 currentPosition = _transformNode2D.GlobalPosition;
        Vector2 targetPosition = _moveTarget.Value;
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
        RefreshMovePathVisual();
    }

    public void SetMoveTarget(Vector2 target)
    {
        _moveTarget = target;
        RefreshMovePathVisual();
    }

    public void ClearMoveTarget()
    {
        _moveTarget = null;
        RefreshMovePathVisual();
    }

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

        var moveTarget = CurrentMoveTarget;
        if (!moveTarget.HasValue)
        {
            _movePathIndicator.Visible = false;
            return;
        }

        _movePathIndicator.Visible = true;
        _movePathIndicator.SetPointPosition(0, Vector2.Zero);
        _movePathIndicator.SetPointPosition(1, _transformNode2D.ToLocal(moveTarget.Value));
    }

    private bool ShouldDrawMovePath()
    {
        if (!_moveTarget.HasValue)
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
}
