using Godot;

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
        [Export] public WeaponAnimationProfile AnimationProfile { get; set; }
        [Export] public Texture2D Icon { get; set; }
        [Export] public Texture2D Portrait { get; set; }
        [Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
    }
}
