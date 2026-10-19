using Godot;
namespace GameLogic;

/// <summary>Reusable initial configuration. Every host creates its own mutable state.</summary>
[Tool, GlobalClass]
public partial class CharacterProfile : Resource
{
    [Export] public CharacterIdentityDefinition Identity {get;set;}=new();
    [Export] public CharacterNeedsDefinition Needs {get;set;}=new();
    [Export] public CharacterPersonalityDefinition Personality {get;set;}=new();
    [Export] public CharacterRelationsDefinition Relations {get;set;}=new();
    [Export] public CharacterAffectDefinition Affect {get;set;}=new();
    [Export] public CharacterFactSchema Facts {get;set;}=new();
}
