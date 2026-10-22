using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldLocationDefinition : WorldRecord
{
    [Export] public WorldLocationDefinition Parent {get;set;}
    [Export] public string[] Tags {get;set;}=System.Array.Empty<string>();
    [Export] public string[] PublicServices {get;set;}=System.Array.Empty<string>();
}
