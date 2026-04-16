using System.Collections.Generic;
using Framework;
using GameLogic;
using GameLogic.Input;
using Godot;

[GlobalClass]
public partial class PlayerArmyComponent : Component2D
{
    private const string DefaultUnitScenePath = "res://assets/scenes/character.tscn";

    [Export] public PackedScene DefaultUnitScene { get; set; }
    [Export] public NodePath UnitsRootPath { get; set; } = new NodePath("Player/Units");
    [Export] public int DefaultSpawnCount { get; set; } = 3;
    [Export] public Vector2 DefaultSpawnOrigin { get; set; } = Vector2.Zero;
    [Export] public float DefaultSpawnSpacing { get; set; } = 72f;

    private readonly List<GameObject2D> _units = new();
    public IReadOnlyList<GameObject2D> Units => _units.AsReadOnly();

    private IInputModule _inputModule;
    private IEventModule _eventModule;
    private IResourceModule _resourceModule;
    private Node _unitsRoot;

    public override int Priority => ComponentPriority.Input - 1;

    public override void OnInit()
    {
        _inputModule = ModuleSystem.GetModule<IInputModule>();
        _eventModule = ModuleSystem.GetModule<IEventModule>();
        _resourceModule = ModuleSystem.GetModule<IResourceModule>();
        _unitsRoot = Owner.GetNodeOrNull<Node>(UnitsRootPath) ?? Owner;

        InitializeRoster();
    }

    public override void OnUpdate(double delta)
    {
        var viewport = Owner.GetViewport();
        if (ViewportInputUtility.IsPointerBlockedByUI(viewport))
            return;

        var mouseScreenPosition = viewport?.GetMousePosition() ?? Vector2.Zero;
        var mouseWorldPosition = ViewportInputUtility.ScreenToWorld(viewport, mouseScreenPosition);

        HandleMoveInput(mouseWorldPosition);
    }

    private void HandleMoveInput(Vector2 mouseWorldPosition)
    {
        if (_inputModule.IsActionConsumed("combat_command_move"))
            return;

        if (_inputModule.TryHandleJustPressed("combat_command_move"))
        {
            var selectedSelection = Owner.GetComponent<SelectableManagerComponent>().Selected;
            var selectedUnit = selectedSelection?.Owner;
            if (selectedUnit == null || !(selectedSelection?.CanReceivePlayerCommands ?? false))
                return;

            Debugger.Info($"Selected unit: {selectedUnit.Name}");
            selectedUnit.GetComponent<MovementComponent>()?.SetMoveTarget(mouseWorldPosition);
        }
    }

    public override void OnDestroy()
    {
        _units.Clear();
        _unitsRoot = null;
        _inputModule = null;
        _eventModule = null;
        _resourceModule = null;
    }

    public void RegisterUnit(GameObject2D unit)
    {
        var faction = unit?.GetComponent<FactionComponent>();
        if (!(faction?.IsPlayerFaction ?? false) || _units.Contains(unit))
            return;

        _units.Add(unit);
        _eventModule?.Send(GameRtsEvents.ArmyRosterChanged);
    }

    public void UnregisterUnit(GameObject2D unit)
    {
        if (unit == null)
            return;

        if (_units.Remove(unit))
            _eventModule?.Send(GameRtsEvents.ArmyRosterChanged);
    }

    public void InitializeRoster()
    {
        var saveData = RootModule.Instance.GameState.SaveData;
        if (saveData.HasData())
        {
            Debugger.Info("Initializing player army from saved data.");
            for (int i = 0; i < saveData.GetUnitCount(); i++)
            {
                var unitData = saveData.GetPlayerUnitData(i);
                if (unitData != null)
                {
                    var unit = CreateUnitFromScene();
                    if (unit == null)
                        continue;

                    _unitsRoot.AddChild(unit);
                        RegisterUnit(unit);

                    var serializationComponent = unit.GetComponent<SerializationComponent>();
                    if (serializationComponent != null)
                    {
                        serializationComponent.Load(unitData);
                    }
                    else
                    {
                        Debugger.Warn($"Unit '{unit.Name}' does not have a SerializationComponent to load its state.");
                    }
                }
            }



            return;
        }

        Debugger.Info("No saved data found for player army. Initializing default roster.");

        for (int i = 0; i < DefaultSpawnCount; i++)
        {
            var unit = CreateUnitFromScene();
            if (unit == null)
                continue;

            _unitsRoot.AddChild(unit);
            RegisterUnit(unit);

            unit.SetWorldPosition2D(DefaultSpawnOrigin + new Vector2(i * DefaultSpawnSpacing, 0f));
        }
    }


    private GameObject2D CreateUnitFromScene()
    {
        var scenePath = !string.IsNullOrEmpty(DefaultUnitScene?.ResourcePath)
            ? DefaultUnitScene.ResourcePath
            : DefaultUnitScenePath;

        var handle = _resourceModule.LoadAsset<PackedScene>(scenePath);
        try
        {
            var scene = handle.Asset;
            if (scene != null)
            {
                var sceneInstance = scene.Instantiate();
                if (sceneInstance is GameObject2D gameObject)
                    return gameObject;

                Debugger.Warn("[PlayerArmyComponent] Default unit scene is not a SerializableGameObject2D.");
            }
            else
            {
                Debugger.Warn($"[PlayerArmyComponent] Failed to load PackedScene from '{scenePath}'. Error: {handle.Error}");
            }
        }
        finally
        {
            handle.Release();
        }

        return null;
    }

}
