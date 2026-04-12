using Framework;
using GameLogic;
using Godot;

[GlobalClass]
public partial class MovementComponent : Component
{
    [Export] public float MoveSpeed { get; set; } = 240f;
    [Export] public float StopDistance { get; set; } = 4f;
    [Export] public bool DrawMovePathOnlyWhenSelected { get; set; } = true;
    [Export] public Color MovePathColor { get; set; } = new Color(0.35f, 0.95f, 0.55f, 0.95f);
    [Export] public float MovePathWidth { get; set; } = 3f;

    private PlayerUnit _unit;
    private Node2D _worldAnchor;
    private Line2D _movePathIndicator;
    private Vector2? _moveTarget;

    /// <summary>
    /// 获取组件执行优先级。
    /// </summary>
    public override int Priority => ComponentPriority.Movement;

    /// <summary>
    /// 获取当前是否存在有效移动目标。
    /// </summary>
    public bool HasMoveTarget => _moveTarget.HasValue;

    /// <summary>
    /// 获取当前移动目标点。
    /// </summary>
    public Vector2? CurrentMoveTarget => _moveTarget;

    /// <summary>
    /// 初始化移动组件，并准备移动路径显示节点。
    /// </summary>
    public override void OnInit()
    {
        _unit = Owner as PlayerUnit;
        _worldAnchor = _unit?.GetWorldAnchor();
        _movePathIndicator = SetupMovePathIndicator();
        RefreshMovePathVisual();
    }

    /// <summary>
    /// 在物理帧中推动单位朝目标点移动。
    /// </summary>
    public override void OnPhysicsUpdate(double delta)
    {

        if ((_movePathIndicator?.Visible ?? false) || _moveTarget.HasValue)
            RefreshMovePathVisual();

        if (!_moveTarget.HasValue || _worldAnchor == null)
            return;

        Vector2 currentPosition = _worldAnchor.GlobalPosition;
        Vector2 targetPosition = _moveTarget.Value;
        float distance = currentPosition.DistanceTo(targetPosition);

        if (distance <= StopDistance)
        {
            _worldAnchor.GlobalPosition = targetPosition;
            ClearMoveTarget();
            return;
        }

        Vector2 direction = (targetPosition - currentPosition).Normalized();
        float step = MoveSpeed * (float)delta;
        _worldAnchor.GlobalPosition = currentPosition + direction * Mathf.Min(step, distance);
        RefreshMovePathVisual();
    }

    /// <summary>
    /// 设置新的移动目标点。
    /// </summary>
    public void SetMoveTarget(Vector2 target)
    {
        _moveTarget = target;
        RefreshMovePathVisual();
    }

    /// <summary>
    /// 清空当前移动目标。
    /// </summary>
    public void ClearMoveTarget()
    {
        _moveTarget = null;
        RefreshMovePathVisual();
    }

    /// <summary>
    /// 刷新移动路径线的显示状态和终点位置。
    /// </summary>
    public void RefreshMovePathVisual()
    {
        if (_movePathIndicator == null)
            _movePathIndicator = SetupMovePathIndicator();

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
        _movePathIndicator.SetPointPosition(1, _worldAnchor.ToLocal(moveTarget.Value));
    }

    private bool ShouldDrawMovePath()
    {
        if (!_moveTarget.HasValue)
            return false;

        return !DrawMovePathOnlyWhenSelected || (_unit?.IsSelected ?? false);
    }

    private Line2D SetupMovePathIndicator()
    {
        if (_worldAnchor == null)
            return null;

        var existingIndicator = _worldAnchor.GetNodeOrNull<Line2D>("MovePathIndicator");
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

        _worldAnchor.AddChild(indicator);
        indicator.AddPoint(Vector2.Zero);
        indicator.AddPoint(Vector2.Zero);
        return indicator;
    }
}
