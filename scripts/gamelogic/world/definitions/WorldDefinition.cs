using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldDefinition : WorldRecord
{
    public const string CommonPath="res://assets/resources/world/default_world.tres";
    [Export] public Godot.Collections.Array<WorldLocationDefinition> Locations {get;set;}=new();
    [Export] public Godot.Collections.Array<WorldLevelDefinition> Levels {get;set;}=new();
    [Export] public Godot.Collections.Array<WorldObjectTypeDefinition> ObjectTypes {get;set;}=new();
    [Export] public Godot.Collections.Array<WorldRouteDefinition> Routes {get;set;}=new();
    [Export] public WorldLevelDefinition InitialLevel {get;set;}
    [Export] public TimeOfDayProfile Calendar {get;set;}
    [Export(PropertyHint.File,"*.tscn,*.scn")] public string PlayerScene {get;set;}="res://assets/scenes/player.tscn";
#if TOOLS
    [ExportToolButton("校验世界目录")] public Callable ValidateCatalog=>Callable.From(()=>
    {
        try{var catalog=new WorldCatalog(this);GD.Print($"世界目录有效：{catalog.Locations.Count} 地点，{catalog.Levels.Count} 关卡，{catalog.Objects.Count} 对象。");}
        catch(System.Exception error){GD.PushError(error.Message);}
    });
#endif
}
