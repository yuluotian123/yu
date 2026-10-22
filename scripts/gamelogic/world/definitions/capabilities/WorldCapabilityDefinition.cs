using System;
using Godot;
namespace GameLogic;

/// <summary>A typed, optional capability default or instance override. Contains no scene references.</summary>
[Tool, GlobalClass]
public abstract partial class WorldCapabilityDefinition : Resource
{
    public abstract string Key {get;}
    public abstract Type ComponentType {get;}
    public virtual string Adapter=>"";
    public abstract void Validate();
    public abstract WorldCapabilityState CreateState(Action changed);
}
