using System.Collections.Generic;
using Framework;
using GameLogic;
using Godot;

/// <summary>
/// 玩家旗下单位管理组件，负责恢复、选择、移动和存档同步。
/// </summary>
[GlobalClass]
public partial class PlayerArmyComponent : Component
{
    private const string DefaultUnitScenePath = "res://assets/scenes/playerunit.tscn";

    [Export] public PackedScene DefaultUnitScene { get; set; }
    [Export] public NodePath UnitsRootPath { get; set; } = new NodePath("Player/Units");
    [Export] public int DefaultSpawnCount { get; set; } = 3;
    [Export] public Vector2 DefaultSpawnOrigin { get; set; } = Vector2.Zero;
    [Export] public float DefaultSpawnSpacing { get; set; } = 72f;

    private readonly List<PlayerUnit> _units = new();

    private InputComponent _inputComponent;
    private CameraComponent _cameraComponent;
    private PlayerState _playerState;
    private Node _unitsRoot;
    private PlayerUnit _selectedUnit;

    /// <summary>
    /// 获取组件执行优先级。
    /// </summary>
    public override int Priority => ComponentPriority.Input - 1;

    /// <summary>
    /// 获取当前玩家旗下单位列表。
    /// </summary>
    public IReadOnlyList<PlayerUnit> Units => _units;

    /// <summary>
    /// 获取当前选中的单位。
    /// </summary>
    public PlayerUnit SelectedUnit => _selectedUnit;

    /// <summary>
    /// 初始化玩家军队组件并尝试从存档恢复单位。
    /// </summary>
    public override void OnInit()
    {
        _inputComponent = Owner.GetComponent<InputComponent>();
        _cameraComponent = Owner.GetComponent<CameraComponent>();
        _playerState = RootModule.Instance?.GameState?._PlayerState;

        if (!UnitsRootPath.IsEmpty)
        {
            var explicitRoot = Owner.GetNodeOrNull(UnitsRootPath);
            if (explicitRoot != null)
                _unitsRoot = explicitRoot;
        }
        else
        {
            _unitsRoot = Owner.GetParent();
        }

        if (_playerState != null && _playerState.OwnedUnits.Count > 0)
            RestoreUnitsFromState();
        else
            BootstrapDefaultUnits();

        SyncStateToPlayerState();
    }

    /// <summary>
    /// 在每帧中处理玩家军队的单选与移动命令。
    /// </summary>
    public override void OnUpdate(double delta)
    {
        if (_inputComponent == null)
        {
            Debugger.Warn("[PlayerArmyComponent] Missing InputComponent. PlayerArmyComponent will not process input.");
            return;
        }

        if (_inputComponent.IsPointerBlockedByUI)
            return;


        HandleSelectInput();
        HandleMoveInput();
    }

    /// <summary>
    /// 在组件销毁时清理运行时缓存。
    /// </summary>
    public override void OnDestroy()
    {
        _units.Clear();
        _selectedUnit = null;
        _unitsRoot = null;
        _inputComponent = null;
        _cameraComponent = null;
        _playerState = null;
    }

    /// <summary>
    /// 注册一个玩家单位到当前军队列表中。
    /// </summary>
    public void RegisterUnit(PlayerUnit unit)
    {
        if (unit == null || _units.Contains(unit))
            return;

        _units.Add(unit);
        SyncStateToPlayerState();
        NotifyRosterChanged();
    }

    /// <summary>
    /// 将一个玩家单位从当前军队列表中注销。
    /// </summary>
    public void UnregisterUnit(PlayerUnit unit)
    {
        if (unit == null)
            return;

        if (_selectedUnit == unit)
            ClearSelection();

        if (_units.Remove(unit))
        {
            SyncStateToPlayerState();
            NotifyRosterChanged();
        }
    }

    /// <summary>
    /// 选中指定单位，并可选地将摄像机聚焦到该单位。
    /// </summary>
    public void SelectUnit(PlayerUnit unit, bool focusCamera = false)
    {
        if (_selectedUnit == unit)
            return;

        if (_selectedUnit != null)
            _selectedUnit.SetSelected(false);

        _selectedUnit = unit;

        if (_selectedUnit != null)
        {
            _selectedUnit.SetSelected(true);
            _playerState?.SetSelectedUnitId(_selectedUnit.UnitId);
        }
        else
        {
            _playerState?.ClearSelectedUnit();
        }

        SyncStateToPlayerState();
        NotifySelectionChanged();

        if (focusCamera)
            FocusCameraOnUnit(unit);
    }
    /// <summary>
    /// 将摄像机聚焦到指定单位。
    /// </summary>
    private void FocusCameraOnUnit(PlayerUnit unit)
    {
        if (unit == null)
            return;

        if (_cameraComponent == null)
            _cameraComponent = Owner?.GetComponent<CameraComponent>();

        _cameraComponent?.FocusOn(unit.WorldPosition);
    }

    /// <summary>
    /// 根据单位唯一 ID 选中对应单位，并可选地聚焦摄像机。
    /// </summary>
    public void SelectUnitById(string unitId, bool focusCamera = false)
    {
        if (string.IsNullOrEmpty(unitId))
        {
            ClearSelection();
            return;
        }

        for (int i = 0; i < _units.Count; i++)
        {
            if (_units[i] != null && _units[i].UnitId == unitId)
            {
                SelectUnit(_units[i], focusCamera);
                return;
            }
        }

        ClearSelection();
    }

    /// <summary>
    /// 清空当前单位选择。
    /// </summary>
    public void ClearSelection()
    {
        if (_selectedUnit != null)
            _selectedUnit.SetSelected(false);

        _selectedUnit = null;
        _playerState?.ClearSelectedUnit();
        SyncStateToPlayerState();
        NotifySelectionChanged();
    }

    /// <summary>
    /// 向当前选中单位下发移动命令。
    /// </summary>
    public void CommandMoveSelected(Vector2 target)
    {
        if (_selectedUnit == null)
            return;

        Debugger.Info($"Selected unit: {_selectedUnit.UnitId}");

        _selectedUnit.MoveTo(target);
        SyncStateToPlayerState();
    }

    /// <summary>
    /// 将当前运行时单位列表和选中状态同步回 PlayerState。
    /// </summary>
    public void SyncStateToPlayerState()
    {
        if (_playerState == null)
            return;

        var snapshots = new List<PlayerUnitSnapshot>(_units.Count);
        for (int i = 0; i < _units.Count; i++)
        {
            if (_units[i] != null)
                snapshots.Add(_units[i].CaptureSnapshot());
        }

        _playerState.ReplaceOwnedUnits(snapshots);

        if (_selectedUnit != null)
            _playerState.SetSelectedUnitId(_selectedUnit.UnitId);
        else
            _playerState.ClearSelectedUnit();
    }

    /// <summary>
    /// 从 PlayerState 中的快照数据恢复单位。
    /// </summary>
    private void RestoreUnitsFromState()
    {
        var snapshots = new List<PlayerUnitSnapshot>(_playerState.OwnedUnits);

        for (int i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (snapshot == null)
                continue;

            var unit = CreateUnitFromScene();
            if (unit == null)
                continue;

            AddUnitToScene(unit);
            unit.ApplySnapshot(snapshot);
            RegisterUnit(unit);
        }

        if (!string.IsNullOrEmpty(_playerState.SelectedUnitId))
            SelectUnitById(_playerState.SelectedUnitId);
    }
    /// <summary>
    /// 在首局进入或空存档情况下生成默认玩家单位。
    /// </summary>
    private void BootstrapDefaultUnits()
    {
        for (int i = 0; i < DefaultSpawnCount; i++)
        {
            var unit = CreateUnitFromScene();
            if (unit == null)
                continue;

            AddUnitToScene(unit);
            unit.SetWorldPosition(DefaultSpawnOrigin + new Vector2(i * DefaultSpawnSpacing, 0f));
            RegisterUnit(unit);
        }

        if (_units.Count > 0)
            SelectUnit(_units[0], true);
    }
    /// <summary>
    /// 创建一个玩家单位实例。
    /// </summary>
    private PlayerUnit CreateUnitFromScene()
    {
        var scene = DefaultUnitScene;
        if (scene == null)
            scene = ResourceLoader.Load<PackedScene>(DefaultUnitScenePath);

        if (scene != null)
        {
            var sceneInstance = scene.Instantiate();
            if (sceneInstance is PlayerUnit sceneUnit)
                return sceneUnit;

            Debugger.Warn("[PlayerArmyComponent] Default unit scene is not a PlayerUnit. Fallback to runtime-created unit.");
        }

        return new PlayerUnit();
    }
    /// <summary>
    /// 将单位加入场景并补齐最小运行时初始化。
    /// </summary>
    private void AddUnitToScene(PlayerUnit unit)
    {
        if (unit == null)
            return;

        var parent = _unitsRoot ?? Owner?.GetParent();
        parent?.AddChild(unit);
    }

    /// <summary>
    /// 在当前单位列表中查找鼠标命中的单位。
    /// </summary>
    private PlayerUnit FindUnitAtWorldPosition(Vector2 worldPosition)
    {
        for (int i = _units.Count - 1; i >= 0; i--)
        {
            var unit = _units[i];
            if (unit != null && unit.ContainsWorldPoint(worldPosition))
                return unit;
        }

        return null;
    }

    /// <summary>
    /// 处理左键单选输入。
    /// </summary>
    private void HandleSelectInput()
    {
        if (!_inputComponent.SelectPressedThisFrame)
            return;

        var unit = FindUnitAtWorldPosition(_inputComponent.MouseWorldPosition);
        if (unit == null)
            return;

        if (unit == _selectedUnit)
            ClearSelection();
        else
            SelectUnit(unit);
    }

    /// <summary>
    /// 处理右键移动输入。
    /// </summary>
    private void HandleMoveInput()
    {
        if (_inputComponent.CommandMovePressedThisFrame)
            CommandMoveSelected(_inputComponent.MouseWorldPosition);
    }

    /// <summary>
    /// 发送玩家单位列表变化事件。
    /// </summary>
    private void NotifyRosterChanged()
    {
        ModuleSystem.GetModule<IEventModule>()?.Send(GameRtsEvents.ArmyRosterChanged);
    }

    /// <summary>
    /// 发送当前选中单位变化事件。
    /// </summary>
    private void NotifySelectionChanged()
    {
        ModuleSystem.GetModule<IEventModule>()?.Send(GameRtsEvents.ArmySelectionChanged);
    }
}
