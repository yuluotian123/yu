using System;
using System.Collections.Generic;
using Godot;
namespace GameLogic;

/// <summary>Shared operation configuration. Runtime execution always goes through WorldInteraction.</summary>
[Tool, GlobalClass]
public abstract partial class WorldInteractionDefinition : Resource
{
    [Export] public string Id {get;set;}="";
    [Export] public string DisplayName {get;set;}="";
    [Export] public bool RequireCapacity {get;set;}
    public abstract IReadOnlyList<string> RequiredCapabilities {get;}
    public virtual string ConsumedProperty=>"";
    public virtual bool SupportsSemantic=>false;
    public virtual void Validate()=>WorldCatalog.Require(!string.IsNullOrWhiteSpace(Id)&&!string.IsNullOrWhiteSpace(DisplayName),"交互操作需要 Id 和显示名称。");
    internal abstract WorldInteractionResult Check(WorldInteractionContext context);
    internal abstract WorldInteractionResult Apply(WorldInteractionContext context);
}

public readonly record struct WorldInteractionContext(GameObject3D Actor,WorldObjectComponent3D Target,double? Amount=null,WorldObjectState LogicalState=null)
{public WorldObjectState State=>Target?.State??LogicalState;}

public readonly record struct WorldInteractionResult(bool Success,string Message)
{
    public static WorldInteractionResult Ok(string message="")=>new(true,message);
    public static WorldInteractionResult Fail(string message)=>new(false,message);
}
