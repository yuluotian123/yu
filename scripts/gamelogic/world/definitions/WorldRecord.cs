using System;
using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public abstract partial class WorldRecord : Resource
{
    [Export] public string Id {get;set;}=Guid.NewGuid().ToString("N");
    [Export] public string DisplayName {get;set;}="";
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if(property["name"].AsString()==nameof(Id))property["usage"]=(long)PropertyUsageFlags.Storage;
    }
}
