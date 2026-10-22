using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class CharacterWorldDefinition : Resource
{
    [Export] public string[] KnownRoutes {get;set;}=System.Array.Empty<string>();
    [Export] public Godot.Collections.Dictionary<string,WorldLocationDefinition> Locations {get;set;}=new();
    [Export] public Godot.Collections.Dictionary<string,string> Objects {get;set;}=new();
    [Export] public Godot.Collections.Array<WorldLocationDefinition> KnownLocations {get;set;}=new();
    [Export] public string[] KnownObjects {get;set;}=System.Array.Empty<string>();
}
