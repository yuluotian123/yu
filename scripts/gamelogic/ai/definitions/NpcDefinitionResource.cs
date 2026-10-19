using System;
using Godot;

namespace GameLogic.AI;

[Tool,GlobalClass]
public abstract partial class NpcDefinitionResource : Resource
{
    [Export] public string DefinitionId {get;set;}=Guid.NewGuid().ToString("N");
    [Export] public string DisplayName {get;set;}="";
    [Export] public bool Enabled {get;set;}=true;
    public virtual string Category=>GetType().Name;
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if(property["name"].AsString()==nameof(DefinitionId))
            property["usage"]=(long)PropertyUsageFlags.Storage;
    }
}
