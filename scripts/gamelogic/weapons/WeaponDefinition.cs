using Godot;
using System.Collections.Generic;

namespace GameLogic
{
    public enum WeaponCategory { Greatsword, Katana }

    [Tool, GlobalClass]
    public partial class WeaponDefinition : Resource
    {
        [Export] public string WeaponId { get; set; } = string.Empty;
        [Export] public string DisplayName { get; set; } = string.Empty;
        [Export] public WeaponCategory Category { get; set; }
        [Export] public AbilityResource PrimaryAttack { get; set; }
        [Export] public Godot.Collections.Dictionary<string, AbilityResource> StancePrimaryAttacks { get; set; } = new();
        [Export] public WeaponAnimationProfile AnimationProfile { get; set; }
        [Export] public string DefaultStance { get; set; } = "mid";
        [Export] public Godot.Collections.Dictionary<string, string> StanceIdleAnimations { get; set; } = new();
        [Export] public Godot.Collections.Dictionary<string, string> StanceMoveAnimations { get; set; } = new();
        [Export] public Godot.Collections.Dictionary<string, string> StanceMoveStartAnimations { get; set; } = new();
        // Directed edges, for example "high>mid". Empty keeps legacy weapons instant.
        [Export] public Godot.Collections.Dictionary<string, string> StanceTransitions { get; set; } = new();
        [Export] public Texture2D Icon { get; set; }
        [Export] public Texture2D Portrait { get; set; }
        [Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;

        public AbilityResource PrimaryAttackFor(string stance) =>
            stance != null && StancePrimaryAttacks.TryGetValue(stance, out var attack) ? attack : PrimaryAttack;

        public IEnumerable<AbilityResource> GetPrimaryAttacks()
        {
            yield return PrimaryAttack;
            foreach (var attack in StancePrimaryAttacks.Values)
                if (attack != PrimaryAttack) yield return attack;
        }

        public bool HasValidStances()
        {
            if (StanceIdleAnimations.Count == 0) return AnimationProfile?.HasAnimation("idle") == true && StanceTransitions.Count == 0
                && StanceMoveAnimations.Count == 0 && StanceMoveStartAnimations.Count == 0 && StancePrimaryAttacks.Count == 0;
            if (!StanceIdleAnimations.ContainsKey(DefaultStance)) return false;
            foreach (var stance in StanceIdleAnimations)
                if (string.IsNullOrWhiteSpace(stance.Key) || string.IsNullOrWhiteSpace(stance.Value) ||
                    AnimationProfile?.HasAnimation(stance.Value) != true)
                    return false;
            if (StancePrimaryAttacks.Count > 0)
            {
                if (StancePrimaryAttacks.Count != StanceIdleAnimations.Count) return false;
                foreach (string stance in StanceIdleAnimations.Keys)
                    if (!StancePrimaryAttacks.TryGetValue(stance, out var attack) ||
                        attack?.Graph == null || string.IsNullOrWhiteSpace(attack.AbilityId)) return false;
            }
            if (StanceMoveAnimations.Count > 0)
            {
                if (StanceMoveAnimations.Count != StanceIdleAnimations.Count) return false;
                foreach (string stance in StanceIdleAnimations.Keys)
                    if (!StanceMoveAnimations.TryGetValue(stance, out string clip) ||
                        string.IsNullOrWhiteSpace(clip) || AnimationProfile?.IsLoop(clip) != true) return false;
            }
            foreach (var start in StanceMoveStartAnimations)
                if (!StanceMoveAnimations.ContainsKey(start.Key) || AnimationProfile?.IsOneShot(start.Value) != true)
                    return false;
            if (StanceTransitions.Count > 0)
                foreach (string from in StanceIdleAnimations.Keys)
                    foreach (string to in StanceIdleAnimations.Keys)
                        if (from != to && (!StanceTransitions.TryGetValue(from + ">" + to, out string clip) ||
                            AnimationProfile?.IsOneShot(clip) != true))
                            return false;
            return true;
        }

    }
}
