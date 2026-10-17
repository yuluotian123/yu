using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Framework;
using Framework.UI;
using Godot;

namespace GameLogic;

public enum InventoryDragArea { Bag, Equipment, CurrentWeapon }

[GlobalClass]
public partial class InventoryComponent3D : Component3D
{
    [Export] public Godot.Collections.Array<WeaponDefinition> StartingWeapons { get; set; } = new();
    [Export(PropertyHint.Range, "2,48,1")] public int Capacity { get; set; } = 12;
    public override int Priority => ComponentPriority.Input + 10;
    private readonly List<WeaponDefinition> _items = new();
    private readonly Dictionary<string, int> _cellPositions = new(StringComparer.Ordinal);
    private PlayerCharacterInputComponent3D _playerInput;
    private IInputModule _input;
    private IUIModule _ui;
    private CanvasLayer _hud;
    private double _releaseDelay;
    private bool _waitForToggleRelease;
    public IReadOnlyList<WeaponDefinition> Items => _items;
    public bool IsOpen { get; private set; }
    public WeaponEquipmentComponent3D Equipment => Owner?.GetComponent<WeaponEquipmentComponent3D>();
    public event Action ContentsChanged;

    public override void OnInit()
    {
        _input = ModuleSystem.GetModule<IInputModule>();
        _ui = ModuleSystem.GetModule<IUIModule>();
        StandaloneModuleDriver.Ensure(Owner.GetTree());
        _playerInput = Owner.GetComponent<PlayerCharacterInputComponent3D>();
        _items.Clear();
        _cellPositions.Clear();
        foreach (WeaponDefinition weapon in StartingWeapons) TryAddWeapon(weapon);
        Owner.SetMeta("inventory", this);
        _hud = new CanvasLayer { Name = "InventoryHUD", Layer = 20 };
        Owner.AddChild(_hud);
        var button = InventoryUiSkin.Button("背包  [ B ]");
        button.Name = "BackpackButton";
        button.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        button.Position = new Vector2(-164, -72);
        button.Size = new Vector2(140, 48);
        button.Pressed += Toggle;
        _hud.AddChild(button);
    }

    public bool Contains(string weaponId) => _items.Any(item => item.WeaponId == weaponId);
    public WeaponDefinition Find(string weaponId) => _items.Find(item => item.WeaponId == weaponId);

    public bool TryAddWeapon(WeaponDefinition weapon)
    {
        if (weapon == null || string.IsNullOrWhiteSpace(weapon.WeaponId) || weapon.AnimationProfile?.IsValid() != true ||
            weapon.PrimaryAttack?.Graph == null || string.IsNullOrWhiteSpace(weapon.PrimaryAttack.AbilityId) ||
            Contains(weapon.WeaponId) || _items.Count >= Capacity)
            return false;
        _items.Add(weapon);
        _cellPositions[weapon.WeaponId] = Enumerable.Range(0, Capacity).First(index => !_cellPositions.ContainsValue(index));
        ContentsChanged?.Invoke();
        return true;
    }

    public int GetCellIndex(string weaponId) => weaponId != null && _cellPositions.TryGetValue(weaponId, out int index) ? index : -1;

    public WeaponDefinition GetWeaponAtCell(int index)
        => index < 0 || index >= Capacity ? null : _items.Find(item => GetCellIndex(item.WeaponId) == index);

    public bool MoveWeaponToCell(string weaponId, int targetIndex)
    {
        int sourceIndex = GetCellIndex(weaponId);
        if (sourceIndex < 0 || targetIndex < 0 || targetIndex >= Capacity) return false;
        if (sourceIndex == targetIndex) return true;
        WeaponDefinition other = GetWeaponAtCell(targetIndex);
        _cellPositions[weaponId] = targetIndex;
        if (other != null) _cellPositions[other.WeaponId] = sourceIndex;
        ContentsChanged?.Invoke();
        return true;
    }

    public bool CanDropWeapon(string weaponId, InventoryDragArea source, int sourceIndex, InventoryDragArea target, int targetIndex)
    {
        WeaponEquipmentComponent3D gear = Equipment;
        if (!Contains(weaponId) || gear == null || gear.IsBusy) return false;
        if (source == InventoryDragArea.Bag)
        {
            if (GetWeaponAtCell(sourceIndex)?.WeaponId != weaponId) return false;
        }
        else if (source is InventoryDragArea.Equipment or InventoryDragArea.CurrentWeapon)
        {
            if (sourceIndex < 0 || sourceIndex >= gear.Slots.Count || gear.Slots[sourceIndex]?.WeaponId != weaponId) return false;
            if (source == InventoryDragArea.CurrentWeapon && gear.CurrentSlot != sourceIndex) return false;
        }
        else return false;
        return target switch
        {
            InventoryDragArea.Bag => targetIndex >= 0 && targetIndex < Capacity &&
                (source == InventoryDragArea.Bag || gear.Slots.Count(item => item != null) > 1),
            InventoryDragArea.Equipment => targetIndex >= 0 && targetIndex < gear.Slots.Count,
            InventoryDragArea.CurrentWeapon => true,
            _ => false
        };
    }

    public bool TryDropWeapon(string weaponId, InventoryDragArea source, int sourceIndex, InventoryDragArea target, int targetIndex)
    {
        if (!CanDropWeapon(weaponId, source, sourceIndex, target, targetIndex)) return false;
        if (target == InventoryDragArea.Equipment) return Equipment.EquipWeapon(weaponId, targetIndex, activate: false);
        if (target == InventoryDragArea.CurrentWeapon) return Equipment.EquipWeapon(weaponId);
        // Validate both operations before changing anything. Dragging starts without removing an item.
        if (source != InventoryDragArea.Bag && !Equipment.UnequipSlot(sourceIndex)) return false;
        return MoveWeaponToCell(weaponId, targetIndex);
    }

    public override void OnUpdate(double delta)
    {
        if (_releaseDelay > 0)
        {
            _releaseDelay -= delta;
            if (_releaseDelay <= 0 && !IsOpen && _playerInput != null)
                _playerInput.GameplayBlocked = false;
        }
        // Respect other modal tools, which disable the UI input layer.
        if (_input?.IsLayerEnabled("UI") != true) return;
        // CharacterGraph opens the bag. While it is modal, close keys bypass the blocked character provider.
        if (!IsOpen) return;
        if (_waitForToggleRelease && !_input.IsPressed("player_inventory", "UI")) _waitForToggleRelease = false;
        if (_input.IsJustPressed("ui_cancel", "UI") ||
            (!_waitForToggleRelease && _input.IsJustPressed("player_inventory", "UI"))) Close();
    }

    [GraphAction("ToggleInventory", DisplayName = "打开或关闭背包")]
    public void Toggle()
    {
        if (IsOpen) Close(); else Open();
    }

    public void Open()
    {
        if (IsOpen || _ui == null || _input?.IsLayerEnabled("UI") == false) return;
        IsOpen = true;
        // The graph can open in physics before this component's frame update sees the same press.
        _waitForToggleRelease = _input?.IsPressed("player_inventory", "UI") == true;
        _releaseDelay = 0;
        if (_playerInput != null) _playerInput.GameplayBlocked = true;
        _input?.ClearBuffer();
        _ui.ShowUIAsync<InventoryWindow>(window =>
        {
            if (window.Owner == null) { Close(); return; }
            // A close or scene change may have happened while the scene was loading.
            if (!IsOpen || Owner == null || !GodotObject.IsInstanceValid(Owner))
                _ui?.CloseUI(window);
        }, this);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        _releaseDelay = 0.15;
        _input?.ClearBuffer();
        _ui?.CloseUI<InventoryWindow>();
        Owner?.GetViewport().GuiReleaseFocus();
    }

    internal void WindowDestroyed()
    {
        if (!IsOpen) return;
        IsOpen = false;
        _releaseDelay = 0.15;
        _input?.ClearBuffer();
    }

    public JsonObject CaptureDurableState()
    {
        var positions = new JsonObject();
        foreach (var item in _cellPositions) positions[item.Key] = item.Value;
        return new JsonObject
        {
            ["weapon_ids"] = new JsonArray(_items.Select(item => (JsonNode)JsonValue.Create(item.WeaponId)).ToArray()),
            ["positions"] = positions
        };
    }

    public void RestoreDurableState(JsonObject state)
    {
        // Missing state is a pre-inventory save: retain the scene's starter items.
        if (state?["weapon_ids"] is not JsonArray ids) return;
        _items.Clear();
        _cellPositions.Clear();
        foreach (JsonNode node in ids)
        {
            string id = node?.GetValue<string>();
            WeaponDefinition definition = StartingWeapons.FirstOrDefault(item => item?.WeaponId == id);
            if (definition != null) TryAddWeapon(definition);
        }
        // Recover malformed/obsolete saves without leaving the player weaponless.
        if (_items.Count == 0)
            foreach (WeaponDefinition weapon in StartingWeapons) TryAddWeapon(weapon);
        if (state["positions"] is JsonObject positions)
        {
            _cellPositions.Clear();
            var occupied = new HashSet<int>();
            foreach (WeaponDefinition item in _items)
                if (positions[item.WeaponId] is JsonValue value && value.TryGetValue<int>(out int index) &&
                    index >= 0 && index < Capacity && occupied.Add(index))
                    _cellPositions[item.WeaponId] = index;
            foreach (WeaponDefinition item in _items)
                if (!_cellPositions.ContainsKey(item.WeaponId))
                {
                    int index = Enumerable.Range(0, Capacity).First(cell => !occupied.Contains(cell));
                    occupied.Add(index);
                    _cellPositions[item.WeaponId] = index;
                }
        }
        ContentsChanged?.Invoke();
    }

    public override void OnDestroy()
    {
        Close();
        if (_playerInput != null) _playerInput.GameplayBlocked = false;
        if (GodotObject.IsInstanceValid(_hud)) _hud.QueueFree();
        _hud = null;
        _items.Clear();
        _cellPositions.Clear();
        ContentsChanged = null;
        _input = null;
        _ui = null;
        _playerInput = null;
    }
}
