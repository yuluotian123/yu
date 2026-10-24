using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldLevelDefinition : WorldRecord
{
    [Export(PropertyHint.File,"*.tscn,*.scn")] public string ScenePath {get;set;}="";
    [Export] public WorldLocationDefinition Location {get;set;}
    [Export] public string DefaultEntry {get;set;}="default";
    [Export] public bool ExportPortalRoutes {get;set;}
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        base._ValidateProperty(property);
        if(property["name"].AsString()==nameof(ExportPortalRoutes))property["usage"]=(long)PropertyUsageFlags.Storage;
    }
    public string CatalogPath=>ScenePath.StartsWith("res://.godot/")?ScenePath.GetBaseDir()+"/catalog_"+Id+".tres":"res://assets/resources/world/generated/"+Id+".tres";
}
