using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Framework;
using Godot;

namespace GameLogic;

[Tool, GlobalClass]
public partial class InventoryComponent3D : Component3D
{
    [Export] public Godot.Collections.Array<WeaponDefinition> StartingWeapons { get; set; } = new();
    [Export(PropertyHint.Range, "2,48,1")] public int Capacity { get; set; } = 12;
    public override int Priority => ComponentPriority.Input + 10;
    private readonly List<WeaponDefinition> _items = new();
    private readonly Dictionary<string, int> _cellPositions = new(StringComparer.Ordinal);
    public IReadOnlyList<WeaponDefinition> Items => _items;
    public event Action ContentsChanged;

    public override void OnInit()
    {
        StandaloneModuleDriver.Ensure(Owner.GetTree());
        _items.Clear();
        _cellPositions.Clear();
        foreach (WeaponDefinition weapon in StartingWeapons) TryAddWeapon(weapon);
        Owner.SetMeta("inventory", this);
    }

    public bool Contains(string weaponId) => _items.Any(item => item.WeaponId == weaponId);
    public WeaponDefinition Find(string weaponId) => _items.Find(item => item.WeaponId == weaponId);

    public bool TryAddWeapon(WeaponDefinition weapon)
    {
        if (weapon == null || string.IsNullOrWhiteSpace(weapon.WeaponId) || weapon.AnimationProfile?.IsValid() != true ||
            weapon.PrimaryAttack?.Graph == null || string.IsNullOrWhiteSpace(weapon.PrimaryAttack.AbilityId) ||
            !weapon.HasValidStances() || Contains(weapon.WeaponId) || _items.Count >= Capacity)
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
        _items.Clear();
        _cellPositions.Clear();
        ContentsChanged = null;
    }
}
