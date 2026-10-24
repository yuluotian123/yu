using Godot;
namespace GameLogic;

[Tool,GlobalClass]
public partial class WorldRouteDefinition : WorldRecord
{
    [Export] public WorldLevelDefinition From {get;set;}
    [Export] public string FromEntry {get;set;}="default";
    [Export] public Vector3 FromPosition {get;set;}
    [Export] public WorldLevelDefinition To {get;set;}
    [Export] public string ToEntry {get;set;}="default";
    [Export] public Vector3 ToPosition {get;set;}
    [Export] public bool UseExplicitPositions {get;set;}
    [Export] public double TravelHours {get;set;}=.1;
    [Export] public bool Bidirectional {get;set;}=true;
    [Export] public bool Enabled {get;set;}=true;
    [Export] public bool Public {get;set;}=true;
    [Export] public CharacterExpressionDefinition Permission {get;set;}
    [Export] public string SourcePortalId {get;set;}="";
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        base._ValidateProperty(property);
        if(property["name"].AsString()==nameof(SourcePortalId))property["usage"]=(long)PropertyUsageFlags.Storage;
    }
}
