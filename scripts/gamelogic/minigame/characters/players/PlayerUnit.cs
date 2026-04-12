using System;
using Framework;
using Godot;

/// <summary>
/// RTS 玩家单位节点。
/// </summary>
public partial class PlayerUnit : Character
{
    [Export] public string UnitConfigId { get; set; } = "player_unit";
    [Export] public string DisplayName { get; set; } = "玩家单位";
    [Export] public NodePath WorldAnchorPath { get; set; } = new NodePath("WorldAnchor");
    [Export] public NodePath SelectionIndicatorPath { get; set; } = new NodePath("SelectionIndicator");
    [Export] public float SelectionRadius { get; set; } = 36f;

    private Node2D _worldAnchor;
    private CanvasItem _selectionIndicator;
    private MovementComponent _movementComponent;
    private string _unitId = string.Empty;
    private bool _isSelected;

    /// <summary>
    /// 获取单位唯一 ID。
    /// </summary>
    public string UnitId => _unitId;

    /// <summary>
    /// 获取当前单位是否处于选中状态。
    /// </summary>
    public bool IsSelected => _isSelected;

    /// <summary>
    /// 获取当前单位的世界坐标。
    /// </summary>
    public Vector2 WorldPosition => _worldAnchor?.GlobalPosition ?? Vector2.Zero;

    /// <summary>
    /// 在节点就绪后缓存运行时节点，并补齐单位唯一 ID。
    /// </summary>
    public override void _Ready()
    {
        _worldAnchor = ResolveWorldAnchor();
        _selectionIndicator = GetNodeOrNull<CanvasItem>(SelectionIndicatorPath);
        base._Ready();
        _movementComponent = GetComponent<MovementComponent>() ?? AddComponent<MovementComponent>();

        if (string.IsNullOrEmpty(_unitId))
            _unitId = Guid.NewGuid().ToString("N");

        SetSelected(_isSelected);
    }

    /// <summary>
    /// 设置单位的选中状态表现。
    /// </summary>
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (_selectionIndicator != null)
            _selectionIndicator.Visible = selected;

        _movementComponent?.RefreshMovePathVisual();
    }

    /// <summary>
    /// 向单位下发移动命令。
    /// </summary>
    public void MoveTo(Vector2 target)
    {
        _movementComponent?.SetMoveTarget(target);
    }

    /// <summary>
    /// 判断某个世界坐标点是否命中当前单位。
    /// </summary>
    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        return WorldPosition.DistanceTo(worldPoint) <= SelectionRadius;
    }

    /// <summary>
    /// 采集当前单位的可存档快照。
    /// </summary>
    public PlayerUnitSnapshot CaptureSnapshot()
    {
        return new PlayerUnitSnapshot
        {
            UnitId = UnitId,
            UnitConfigId = UnitConfigId,
            DisplayName = DisplayName,
            Position = WorldPosition,
            Rotation = _worldAnchor?.GlobalRotation ?? 0f,
            HasMoveTarget = _movementComponent?.HasMoveTarget ?? false,
            MoveTarget = _movementComponent?.CurrentMoveTarget ?? Vector2.Zero,
        };
    }

    /// <summary>
    /// 将快照数据应用到当前单位。
    /// </summary>
    public void ApplySnapshot(PlayerUnitSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        _unitId = string.IsNullOrEmpty(snapshot.UnitId) ? Guid.NewGuid().ToString("N") : snapshot.UnitId;
        UnitConfigId = snapshot.UnitConfigId ?? UnitConfigId;
        DisplayName = string.IsNullOrEmpty(snapshot.DisplayName) ? DisplayName : snapshot.DisplayName;
        SetWorldPosition(snapshot.Position);

        if (_worldAnchor != null)
            _worldAnchor.GlobalRotation = snapshot.Rotation;

        if (snapshot.HasMoveTarget)
            _movementComponent?.SetMoveTarget(snapshot.MoveTarget);
        else
            _movementComponent?.ClearMoveTarget();

        _movementComponent?.RefreshMovePathVisual();
    }

    /// <summary>
    /// 获取单位的世界锚点节点。
    /// </summary>
    public Node2D GetWorldAnchor()
    {
        return _worldAnchor;
    }

    /// <summary>
    /// 设置单位的世界坐标。
    /// </summary>
    public void SetWorldPosition(Vector2 position)
    {
        if (_worldAnchor == null)
            return;

        _worldAnchor.GlobalPosition = position;
        _movementComponent?.RefreshMovePathVisual();
    }

    private Node2D ResolveWorldAnchor()
    {
        var explicitAnchor = GetNodeOrNull<Node2D>(WorldAnchorPath);
        if (explicitAnchor != null)
            return explicitAnchor;

        var selfAnchor = GetNodeOrNull<Node2D>(new NodePath("."));
        if (selfAnchor != null)
            return selfAnchor;

        Debugger.Warn($"[PlayerUnit] Missing WorldAnchor on unit '{Name}', and self node is not Node2D.");
        return null;
    }
}
