using Godot;

namespace Framework
{
    [GlobalClass]
    public partial class Settings: Resource
    {
        [Export]
        public ResourceSetting resourceSetting{get;set;}
    }
}