using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class SkillResource : Resource
    {
        [Export] public string SkillId { get; set; } = string.Empty;
        [Export] public string DisplayName { get; set; } = string.Empty;
        [Export] public float Cooldown { get; set; }
        [Export] public SkillFlowGraphAsset Graph { get; set; }
    }
}
