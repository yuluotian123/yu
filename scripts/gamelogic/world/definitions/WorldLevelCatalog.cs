using Godot;
namespace GameLogic;

/// <summary>Generated once per level; never references a PackedScene or a live node.</summary>
[Tool, GlobalClass]
public partial class WorldLevelCatalog : Resource
{
    [Export] public int Version {get;set;}=1;
    [Export] public string LevelId {get;set;}="";
    [Export] public string SourceSignature {get;set;}="";
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> Entries {get;set;}=new();
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> Actors {get;set;}=new();
    [Export] public Godot.Collections.Dictionary<string,Vector3> Entrances {get;set;}=new();
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> Portals {get;set;}=new();
}
