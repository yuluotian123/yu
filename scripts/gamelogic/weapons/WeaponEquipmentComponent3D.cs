using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace GameLogic
{
    [Tool, GlobalClass]
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
        private AnimatedSprite3D _profileSprite;
        private readonly Dictionary<string, WeaponDefinition> _catalog = new(StringComparer.Ordinal);
        private readonly Dictionary<WeaponAnimationProfile, SpriteFrames> _playbackFrames = new();
        private int _currentSlot = -1;
        private int _pendingSlot = -1;
        private const string TransitionRequest = "weapon:stance-transition";
        private string _transitionAnimation = string.Empty;
        private FlowGraphRuntime _stanceEffectRuntime;

        public IReadOnlyList<WeaponDefinition> Slots => _slots;
        public int CurrentSlot => _currentSlot;
        public int PendingSlot => _pendingSlot;
        public WeaponDefinition CurrentWeapon => _currentSlot >= 0 && _currentSlot < _slots.Count ? _slots[_currentSlot] : null;
        public AbilityResource CurrentPrimaryAttack => CurrentWeapon?.PrimaryAttackFor(CurrentStance);
        [GraphValue("CurrentWeaponId", Access = GraphValueAccess.ReadOnly)]
        public string CurrentWeaponId => CurrentWeapon?.WeaponId ?? string.Empty;
        [GraphValue("CurrentStance", Access = GraphValueAccess.ReadOnly)]
        public string CurrentStance { get; private set; } = string.Empty;
        public string PendingStance { get; private set; } = string.Empty;
        public string TransitionTarget { get; private set; } = string.Empty;
        [GraphValue("IsTransitioning", Access = GraphValueAccess.ReadOnly)]
        public bool IsTransitioning => !string.IsNullOrEmpty(TransitionTarget);
        public event Action<string> StanceSwitchStarted;
        public event Action<string> StanceChanged;
        public event Action<WeaponDefinition> WeaponChanged;
        public event Action LoadoutChanged;
        public bool IsBusy => IsAbilityRunning() || IsTransitioning;

        public override void OnInit()
        {
            _abilities = Owner.GetComponent<AbilitySystemComponent3D>();
            _animation = Owner.GetComponent<CharacterAnimationComponent3D>();
            _movement = Owner.GetComponent<CharacterMovementComponent3D>();
            _inventory = Owner.GetComponent<InventoryComponent3D>();
            _slots.Clear();
            _catalog.Clear();
            _playbackFrames.Clear();
            CurrentStance = PendingStance = string.Empty;
            TransitionTarget = _transitionAnimation = string.Empty;
            for (int i = 0; i < WeaponSlotCount; i++) _slots.Add(null);
            _currentSlot = _pendingSlot = -1;
            if (_abilities == null || _animation?.Sprite == null)
            {
                GD.PushWarning("[WeaponEquipmentComponent3D] Missing ability or animation component.");
                return;
            }

            _profileSprite = _animation.Sprite;
            _profileSprite.AnimationChanged += RefreshVisualGeometry;
            _profileSprite.AnimationFinished += OnTransitionFinished;
            _abilities.AbilityCancelled += OnAbilityCancelled;
            _abilities.AbilityActivated += OnAbilityActivated;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (WeaponDefinition weapon in Weapons.Concat(_inventory?.Items ?? Array.Empty<WeaponDefinition>()))
            {
                if (weapon != null && ids.Contains(weapon.WeaponId)) continue;
                if (weapon == null || string.IsNullOrWhiteSpace(weapon.WeaponId) ||
                    weapon.PrimaryAttack?.Graph == null || string.IsNullOrWhiteSpace(weapon.PrimaryAttack.AbilityId) ||
                    weapon.AnimationProfile?.IsValid() != true || !weapon.HasValidStances() || !ids.Add(weapon.WeaponId))
                {
                    GD.PushWarning("[WeaponEquipmentComponent3D] Skipped an invalid or duplicate weapon definition.");
                    continue;
                }
                // Grant once. Switching must never recreate a runtime or reset its cooldown.
                if (GrantWeaponAttacks(weapon))
                    _catalog[weapon.WeaponId] = weapon;
            }
            int initialSlot = 0;
            foreach (WeaponDefinition weapon in Weapons)
                if (initialSlot < WeaponSlotCount && weapon != null && _catalog.ContainsKey(weapon.WeaponId) &&
                    (_inventory == null || _inventory.Contains(weapon.WeaponId)) && !_slots.Contains(weapon))
                    _slots[initialSlot++] = weapon;
            ApplySlot(DefaultSlot());
        }

        public override void OnPhysicsUpdate(double delta)
        {
            _stanceEffectRuntime?.Update(delta);
            if (_stanceEffectRuntime?.IsCompleted == true) StopStanceEffect();
            FlushPendingSwitch();
        }

        public override void OnDestroy()
        {
            StopStanceEffect();
            CancelTransition();
            if (GodotObject.IsInstanceValid(_profileSprite))
            {
                _profileSprite.AnimationChanged -= RefreshVisualGeometry;
                _profileSprite.AnimationFinished -= OnTransitionFinished;
            }
            if (_abilities != null)
            {
                _abilities.AbilityCancelled -= OnAbilityCancelled;
                _abilities.AbilityActivated -= OnAbilityActivated;
            }
            _profileSprite = null;
            WeaponChanged = null;
            LoadoutChanged = null;
            StanceChanged = null;
            StanceSwitchStarted = null;
            _slots.Clear();
            _catalog.Clear();
            _playbackFrames.Clear();
            CurrentStance = PendingStance = string.Empty;
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
            AbilityResource attack = CurrentPrimaryAttack;
            AbilityRuntime runtime = attack == null ? null : _abilities.GetRuntime(attack.AbilityId);
            if (!IsTransitioning && runtime?.IsRunning == true)
                return string.IsNullOrEmpty(PendingStance) && _pendingSlot < 0 && runtime.TryBufferInput("primary_attack");
            return !IsTransitioning && attack != null && _abilities.TryActivateAbility(
                attack.AbilityId, "EquippedWeapon") == AbilityActivationResult.Activated;
        }

        public bool CanRequestPrimaryAttack => !IsTransitioning && string.IsNullOrEmpty(PendingStance) && _pendingSlot < 0;

        private bool GrantWeaponAttacks(WeaponDefinition weapon)
        {
            foreach (var attack in weapon.GetPrimaryAttacks())
                if (_abilities.GetRuntime(attack.AbilityId) == null && !_abilities.GrantAbility(attack)) return false;
            return true;
        }

        [GraphAction("RequestStance", DisplayName = "切换武器架势")]
        public bool RequestStance(string stance)
        {
            if (string.IsNullOrWhiteSpace(stance) || CurrentWeapon?.StanceIdleAnimations.ContainsKey(stance) != true)
                return false;
            if (stance != CurrentStance) _abilities.GetRuntime(CurrentPrimaryAttack?.AbilityId)?.ClearInputWindows();
            if (IsTransitioning)
            {
                // Finish the visible motion before taking the latest requested edge.
                PendingStance = stance == TransitionTarget ? string.Empty : stance;
                return true;
            }
            if (stance == CurrentStance)
            {
                PendingStance = string.Empty;
                return true;
            }
            if (IsAbilityRunning() || _movement?.IsOnFloor == false)
            {
                PendingStance = stance;
                return true;
            }
            StartStanceTransition(stance);
            return true;
        }

        private void ApplyStance(string stance, bool refreshPlayback = true)
        {
            CurrentStance = stance;
            string idle = CurrentWeapon?.StanceIdleAnimations.TryGetValue(stance, out string value) == true ? value : null;
            string move = CurrentWeapon?.StanceMoveAnimations.TryGetValue(stance, out string moveValue) == true ? moveValue : null;
            string start = CurrentWeapon?.StanceMoveStartAnimations.TryGetValue(stance, out string startValue) == true ? startValue : null;
            _animation.SetAnimationAlias("idle", idle, false);
            _animation.SetAnimationAlias("run", move, false);
            _animation.AnimationInstance?.SetAnimationLeadIn("run", start);
            if (refreshPlayback) _animation.AnimationInstance?.RefreshPlayback();
            StanceChanged?.Invoke(stance);
        }

        private void OnAbilityCancelled(AbilityRuntime runtime) => PendingStance = string.Empty;

        private void StartStanceTransition(string stance)
        {
            PendingStance = string.Empty;
            if (stance == CurrentStance) return;
            if (!CurrentWeapon.StanceTransitions.TryGetValue(CurrentStance + ">" + stance, out string clip))
            {
                ApplyStance(stance);
                PlayStanceEffect();
                StanceSwitchStarted?.Invoke(stance);
                return;
            }
            TransitionTarget = stance;
            _transitionAnimation = clip;
            _movement?.SetControlLock(TransitionRequest, true, true, 50);
            _animation.RequestAnimation(TransitionRequest, clip, 50);
            _animation.AnimationInstance?.RefreshPlayback();
            PlayStanceEffect();
            StanceSwitchStarted?.Invoke(stance);
        }

        private void PlayStanceEffect()
        {
            StopStanceEffect();
            var graph = CurrentWeapon?.StanceEffectGraph;
            if (graph == null) return;
            var context = new GraphExecutionContext(graph, new GraphBlackboardRuntime());
            context.UserData.Add(Owner);
            context.UserData.Add(_abilities);
            _stanceEffectRuntime = new FlowGraphRuntime(graph, context);
            if (!_stanceEffectRuntime.Start()) StopStanceEffect();
        }

        private void StopStanceEffect()
        {
            _stanceEffectRuntime?.Stop();
            _stanceEffectRuntime = null;
        }

        private void OnTransitionFinished()
        {
            if (!IsTransitioning || _profileSprite.Animation != _transitionAnimation) return;
            string target = TransitionTarget;
            CancelTransition();
            ApplyStance(target);
            FlushPendingSwitch();
        }

        private void CancelTransition()
        {
            TransitionTarget = _transitionAnimation = string.Empty;
            _animation?.ClearAnimationRequest(TransitionRequest);
            _movement?.ClearControlLock(TransitionRequest);
        }

        private void OnAbilityActivated(AbilityRuntime runtime)
        {
            // Abilities activated outside the weapon input (e.g. a forced hurt) take precedence.
            if (!IsTransitioning) return;
            StopStanceEffect();
            CancelTransition();
            PendingStance = string.Empty;
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
            if (IsBusy)
            {
                _abilities.GetRuntime(CurrentPrimaryAttack?.AbilityId)?.ClearInputWindows();
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
            if (weapon?.AnimationProfile?.IsValid() != true || weapon.PrimaryAttack?.Graph == null || !weapon.HasValidStances()) return false;
            int existing = FindEquippedSlot(weaponId);
            if (slot < 0 && existing >= 0) return !activate || RequestEquipSlot(existing);
            if (slot < 0) slot = _slots.FindIndex(item => item == null);
            if (slot < 0) slot = Math.Max(0, _currentSlot);
            if (slot >= _slots.Count) return false;
            if (!GrantWeaponAttacks(weapon)) return false;
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
            return new JsonObject { ["loadout_version"] = 1, ["equipped_weapon_id"] = CurrentWeaponId, ["slots"] = slots, ["stance"] = CurrentStance };
        }

        public void RestoreDurableState(JsonObject state)
        {
            if (_slots.Count == 0)
                return;
            // Loading restores durable state, not an in-progress attack or queued switch.
            foreach (AbilityRuntime runtime in _abilities.ActiveAbilities.ToArray())
                _abilities.CancelAbility(runtime.AbilityId, "Load");
            CancelTransition();
            _pendingSlot = -1;
            PendingStance = string.Empty;
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
            if (state?["stance"] is JsonValue savedStance && savedStance.TryGetValue(out string stance) &&
                CurrentWeapon?.StanceIdleAnimations.ContainsKey(stance) == true)
                ApplyStance(stance);
        }

        private int DefaultSlot()
        {
            int slot = _slots.FindIndex(item => item?.WeaponId == DefaultWeaponId);
            return slot >= 0 ? slot : _slots.FindIndex(item => item != null);
        }

        private bool IsAbilityRunning() => _abilities?.ActiveAbilities.Any(item => item.IsRunning) == true;

        private void FlushPendingSwitch()
        {
            if (IsBusy) return;
            if (_pendingSlot >= 0) ApplySlot(_pendingSlot);
            if (!string.IsNullOrEmpty(PendingStance) && _movement?.IsOnFloor != false)
                StartStanceTransition(PendingStance);
        }

        private bool ApplySlot(int slot)
        {
            if (slot < 0 || slot >= _slots.Count)
                return false;
            WeaponAnimationProfile profile = _slots[slot]?.AnimationProfile;
            if (profile?.IsValid() != true) return false;
            StopStanceEffect();
            _abilities?.Effects?.Clear();
            _currentSlot = slot;
            _pendingSlot = -1;
            PendingStance = string.Empty;
            CancelTransition();
            // Install the alias and frame set atomically; stance weapons have no legacy idle.
            ApplyStance(CurrentWeapon.StanceIdleAnimations.Count > 0 ? CurrentWeapon.DefaultStance : string.Empty, false);
            if (!_playbackFrames.TryGetValue(profile, out SpriteFrames frames))
                _playbackFrames[profile] = frames = profile.CreatePlaybackFrames();
            _animation.ReplaceSpriteFrames(frames);
            RefreshVisualGeometry();
            WeaponChanged?.Invoke(CurrentWeapon);
            LoadoutChanged?.Invoke();
            return true;
        }

        private void RefreshVisualGeometry()
        {
            WeaponAnimationProfile profile = CurrentWeapon?.AnimationProfile;
            if (profile == null || !GodotObject.IsInstanceValid(_profileSprite)) return;
            float pixelSize = profile.PixelSizeFor(_profileSprite.Animation);
            Vector2I anchor = profile.FootAnchorFor(_profileSprite.Animation);
            Vector2I frameSize = profile.FrameSizeFor(_profileSprite.Animation);
            float halfHeight = _movement?.HalfHeight ?? 0.468f;
            _profileSprite.PixelSize = pixelSize;
            _profileSprite.Scale = Vector3.One * profile.VisualScale;
            _profileSprite.Position = new Vector3(
                (frameSize.X * 0.5f - anchor.X) * pixelSize * profile.VisualScale,
                (anchor.Y - frameSize.Y * 0.5f) * pixelSize * profile.VisualScale - halfHeight, 0f);
        }
    }
}
