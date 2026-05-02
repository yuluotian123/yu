using GameLogic;
using Godot;

[Tool]
[GlobalClass]
public partial class SkillResource : Resource
{
    [Export] public string SkillId { get; set; } = string.Empty;
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public float Cooldown { get; set; }
    [Export] public SkillFlowGraphAsset Graph { get; set; }

    /// <summary>
    /// 安全加载技能资源。不要直接使用 ResourceLoader.Load&lt;SkillResource&gt;，
    /// 因为 Godot 有时会先按普通 Resource 载入脚本资源，泛型 Load 会抛 InvalidCastException。
    /// </summary>
    public static SkillResource LoadFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
            return null;

        Resource resource = ResourceLoader.Load(path, nameof(SkillResource));
        if (resource is SkillResource skill)
            return skill;

        resource = ResourceLoader.Load(path);
        if (resource is SkillResource fallbackSkill)
            return fallbackSkill;

        GD.PushWarning($"[SkillResource] Resource is not a SkillResource: {path}");
        return null;
    }
}
