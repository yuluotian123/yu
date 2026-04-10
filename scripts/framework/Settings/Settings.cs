using Godot;


namespace Framework
{
    [GlobalClass]
    public partial class Settings : Resource
    {
        [Export]
        public ResourceSetting resourceSetting { get; set; }

        [Export]
        public ConfigSetting configSetting { get; set; }
    }
}
