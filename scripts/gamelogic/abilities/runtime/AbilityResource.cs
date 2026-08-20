using Godot;

namespace GameLogic
{
    [Tool]
    [GlobalClass]
    public partial class AbilityResource : Resource
    {
        [Export] public string AbilityId { get; set; } = string.Empty;
        [Export] public string DisplayName { get; set; } = string.Empty;
        [Export] public float Cooldown { get; set; }
        [Export] public AbilityActivationPolicy ActivationPolicy { get; set; }
        [Export] public AbilityFlowGraphAsset Graph { get; set; }

        public AbilityActivationPolicy Policy => ActivationPolicy ??= AbilityActivationPolicy.Default;

        public static AbilityResource LoadFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
                return null;

            Resource resource = ResourceLoader.Load(path, nameof(AbilityResource)) ?? ResourceLoader.Load(path);
            if (resource is AbilityResource ability)
                return ability;

            GD.PushWarning($"[AbilityResource] Resource is not an AbilityResource: {path}");
            return null;
        }
    }
}
