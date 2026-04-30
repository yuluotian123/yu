using Godot;

namespace GameLogic
{
    [Tool]
    [GlobalClass]
    public partial class HfsmTagDefinition : Resource
    {
        [Export] public string Key { get; set; } = string.Empty;
        [Export] public string DisplayName { get; set; } = string.Empty;
        [Export] public string Layer { get; set; } = string.Empty;
        [Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
        [Export] public int DisplayOrder { get; set; }

        public string DisplayText => string.IsNullOrWhiteSpace(DisplayName) ? Key : DisplayName;
    }
}
