using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class WeaponEquipmentComponent3D : Component3D
    {
        [Export] public Godot.Collections.Array<WeaponDefinition> Weapons { get; set; } = new();
        [Export] public string DefaultWeaponId { get; set; } = "greatsword";
        public const int WeaponSlotCount = 2;

        // Animation (20) must initialize before the equipped profile is applied.
        public override int Priority => ComponentPriority.VFX;
        private readonly List<WeaponDefinition> _slots = new();
        private AbilitySystemComponent3D _abilities;
        private CharacterAnimationComponent3D _animation;
        private CharacterMovementComponent3D _movement;
        private InventoryComponent3D _inventory;
        private readonly Dictionary<string, WeaponDefinition> _catalog = new(StringComparer.Ordinal);
        private int _currentSlot = -1;
        private int _pendingSlot = -1;

        public IReadOnlyList<WeaponDefinition> Slots => _slots;
        public int CurrentSlot => _currentSlot;
        public int PendingSlot => _pendingSlot;
        public WeaponDefinition CurrentWeapon => _currentSlot >= 0 && _currentSlot < _slots.Count ? _slots[_currentSlot] : null;
        [GraphValue("CurrentWeaponId", Access = GraphValueAccess.ReadOnly)]
        public string CurrentWeaponId => CurrentWeapon?.WeaponId ?? string.Empty;
        public event Action<WeaponDefinition> WeaponChanged;
        public event Action LoadoutChanged;
        public bool IsBusy => IsAbilityRunning();

        public override void OnInit()
        {
            _abilities = Owner.GetComponent<AbilitySystemComponent3D>();
            _animation = Owner.GetComponent<CharacterAnimationComponent3D>();
            _movement = Owner.GetComponent<CharacterMovementComponent3D>();
            _inventory = Owner.GetComponent<InventoryComponent3D>();
            _slots.Clear();
            _catalog.Clear();
            for (int i = 0; i < WeaponSlotCount; i++) _slots.Add(null);
            _currentSlot = _pendingSlot = -1;
            if (_abilities == null || _animation?.Sprite == null)
            {
                GD.PushWarning("[WeaponEquipmentComponent3D] Missing ability or animation component.");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (WeaponDefinition weapon in Weapons.Concat(_inventory?.Items ?? Array.Empty<WeaponDefinition>()))
            {
                if (weapon != null && ids.Contains(weapon.WeaponId)) continue;
                if (weapon == null || string.IsNullOrWhiteSpace(weapon.WeaponId) ||
                    weapon.PrimaryAttack?.Graph == null || string.IsNullOrWhiteSpace(weapon.PrimaryAttack.AbilityId) ||
                    weapon.AnimationProfile?.IsValid() != true || !ids.Add(weapon.WeaponId))
                {
                    GD.PushWarning("[WeaponEquipmentComponent3D] Skipped an invalid or duplicate weapon definition.");
                    continue;
                }
                // Grant once. Switching must never recreate a runtime or reset its cooldown.
                if (_abilities.GetRuntime(weapon.PrimaryAttack.AbilityId) != null || _abilities.GrantAbility(weapon.PrimaryAttack))
                    _catalog[weapon.WeaponId] = weapon;
            }
            int initialSlot = 0;
            foreach (WeaponDefinition weapon in Weapons)
                if (initialSlot < WeaponSlotCount && weapon != null && _catalog.ContainsKey(weapon.WeaponId) &&
                    (_inventory == null || _inventory.Contains(weapon.WeaponId)) && !_slots.Contains(weapon))
                    _slots[initialSlot++] = weapon;
            ApplySlot(DefaultSlot());
        }

        public override void OnPhysicsUpdate(double delta) => FlushPendingSwitch();

        public override void OnDestroy()
        {
            WeaponChanged = null;
            LoadoutChanged = null;
            _slots.Clear();
            _catalog.Clear();
            _currentSlot = _pendingSlot = -1;
            _abilities = null;
            _animation = null;
            _movement = null;
            _inventory = null;
        }

        [GraphAction("TryPrimaryAttack", DisplayName = "当前武器普攻")]
        public bool TryPrimaryAttack()
        {
            FlushPendingSwitch();
            return CurrentWeapon != null && _abilities.TryActivateAbility(
                CurrentWeapon.PrimaryAttack.AbilityId, "EquippedWeapon") == AbilityActivationResult.Activated;
        }

        [GraphAction("RequestNextWeapon", DisplayName = "切换下一把武器")]
        public bool RequestNextWeapon()
        {
            if (_slots.Count(item => item != null) < 2)
                return false;
            int from = _pendingSlot >= 0 ? _pendingSlot : _currentSlot;
            for (int step = 1; step <= _slots.Count; step++)
            {
                int next = (from + step) % _slots.Count;
                if (_slots[next] != null) return RequestEquipSlot(next);
            }
            return false;
        }

        public bool RequestEquipSlot(int slot)
        {
            if (slot < 0 || slot >= _slots.Count || _slots[slot] == null)
                return false;
            if (slot == _currentSlot)
            {
                _pendingSlot = -1;
                LoadoutChanged?.Invoke();
                return true;
            }
            if (IsAbilityRunning())
            {
                _pendingSlot = slot;
                LoadoutChanged?.Invoke();
                return true;
            }
            return ApplySlot(slot);
        }

        public int FindEquippedSlot(string weaponId) => string.IsNullOrWhiteSpace(weaponId) ? -1 : _slots.FindIndex(item => item?.WeaponId == weaponId);

        public bool EquipWeapon(string weaponId, int slot = -1, bool activate = true)
        {
            if (string.IsNullOrWhiteSpace(weaponId) || IsBusy || _inventory?.Contains(weaponId) == false) return false;
            WeaponDefinition weapon = _inventory?.Find(weaponId) ?? _catalog.GetValueOrDefault(weaponId);
            if (weapon?.AnimationProfile?.IsValid() != true || weapon.PrimaryAttack?.Graph == null) return false;
            int existing = FindEquippedSlot(weaponId);
            if (slot < 0 && existing >= 0) return !activate || RequestEquipSlot(existing);
            if (slot < 0) slot = _slots.FindIndex(item => item == null);
            if (slot < 0) slot = Math.Max(0, _currentSlot);
            if (slot >= _slots.Count) return false;
            if (_abilities.GetRuntime(weapon.PrimaryAttack.AbilityId) == null && !_abilities.GrantAbility(weapon.PrimaryAttack)) return false;
            string previouslyHeld = CurrentWeaponId;
            _catalog[weaponId] = weapon;
            if (existing >= 0 && existing != slot) _slots[existing] = _slots[slot];
            _slots[slot] = weapon;
            int heldSlot = activate ? slot : FindEquippedSlot(previouslyHeld);
            return ApplySlot(heldSlot >= 0 ? heldSlot : slot);
        }

        public bool UnequipSlot(int slot)
        {
            if (IsBusy || slot < 0 || slot >= _slots.Count || _slots[slot] == null) return false;
            if (_slots.Count(item => item != null) <= 1) return false;
            _slots[slot] = null;
            _pendingSlot = -1;
            int next = _currentSlot == slot ? _slots.FindIndex(item => item != null) : _currentSlot;
            return ApplySlot(next);
        }

        public JsonObject CaptureDurableState()
        {
            var slots = new JsonArray();
            foreach (WeaponDefinition weapon in _slots)
                slots.Add(weapon == null ? null : JsonValue.Create(weapon.WeaponId));
            return new JsonObject { ["loadout_version"] = 1, ["equipped_weapon_id"] = CurrentWeaponId, ["slots"] = slots };
        }

        public void RestoreDurableState(JsonObject state)
        {
            if (_slots.Count == 0)
                return;
            // Loading restores durable state, not an in-progress attack or queued switch.
            foreach (AbilityRuntime runtime in _abilities.ActiveAbilities.ToArray())
                _abilities.CancelAbility(runtime.AbilityId, "Load");
            _pendingSlot = -1;
            if (state?["slots"] is JsonArray savedSlots)
            {
                var ordered = new List<WeaponDefinition>();
                bool explicitLoadout = state["loadout_version"] != null;
                foreach (JsonNode node in savedSlots)
                {
                    string id = node?.GetValue<string>();
                    WeaponDefinition weapon = id == null ? null : _catalog.GetValueOrDefault(id);
                    if (weapon != null && (_inventory?.Contains(id) == false || ordered.Contains(weapon))) weapon = null;
                    if (explicitLoadout || weapon != null) ordered.Add(weapon);
                    if (ordered.Count == WeaponSlotCount) break;
                }
                if (!explicitLoadout)
                    foreach (WeaponDefinition weapon in Weapons)
                        if (ordered.Count < WeaponSlotCount && weapon != null && _catalog.ContainsKey(weapon.WeaponId) &&
                            _inventory?.Contains(weapon.WeaponId) != false && !ordered.Contains(weapon)) ordered.Add(weapon);
                while (ordered.Count < WeaponSlotCount) ordered.Add(null);
                _slots.Clear();
                _slots.AddRange(ordered);
            }
            else
            {
                // Old saves without equipment initialize from the original scene loadout.
                for (int i = 0; i < _slots.Count; i++)
                    _slots[i] = i < Weapons.Count && _inventory?.Contains(Weapons[i].WeaponId) != false ? Weapons[i] : null;
            }
            string equipped = state?["equipped_weapon_id"]?.GetValue<string>();
            if (_slots.All(item => item == null))
            {
                WeaponDefinition fallback = _catalog.GetValueOrDefault(DefaultWeaponId);
                if (fallback == null || _inventory?.Contains(fallback.WeaponId) == false)
                    fallback = _catalog.Values.FirstOrDefault(item => _inventory?.Contains(item.WeaponId) != false);
                _slots[0] = fallback;
            }
            int slot = _slots.FindIndex(item => item != null && item.WeaponId == equipped);
            ApplySlot(slot >= 0 ? slot : DefaultSlot());
        }

        private int DefaultSlot()
        {
            int slot = _slots.FindIndex(item => item?.WeaponId == DefaultWeaponId);
            return slot >= 0 ? slot : _slots.FindIndex(item => item != null);
        }

        private bool IsAbilityRunning() => _abilities?.ActiveAbilities.Any(item => item.IsRunning) == true;

        private void FlushPendingSwitch()
        {
            if (_pendingSlot >= 0 && !IsAbilityRunning())
                ApplySlot(_pendingSlot);
        }

        private bool ApplySlot(int slot)
        {
            if (slot < 0 || slot >= _slots.Count)
                return false;
            WeaponAnimationProfile profile = _slots[slot]?.AnimationProfile;
            if (profile?.IsValid() != true) return false;
            AnimatedSprite3D sprite = _animation.Sprite;
            float halfHeight = _movement?.HalfHeight ?? 0.468f;
            sprite.PixelSize = profile.PixelSize;
            sprite.Scale = Vector3.One * profile.VisualScale;
            sprite.Position = new Vector3(
                (profile.FrameSize.X * 0.5f - profile.FootAnchor.X) * profile.PixelSize * profile.VisualScale,
                (profile.FootAnchor.Y - profile.FrameSize.Y * 0.5f) * profile.PixelSize * profile.VisualScale - halfHeight, 0f);
            _animation.ReplaceSpriteFrames(profile.Frames);
            _currentSlot = slot;
            _pendingSlot = -1;
            WeaponChanged?.Invoke(CurrentWeapon);
            LoadoutChanged?.Invoke();
            return true;
        }
    }
}
