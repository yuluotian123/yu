using Godot;
namespace GameLogic;

/// <summary>Reusable initial configuration. Every host creates its own mutable state.</summary>
[Tool, GlobalClass]
public partial class CharacterProfile : Resource
{
    // Export initializers also run in generated default-value queries during type registration.
    public CharacterProfile()
    {
        Identity=new();Needs=new();Personality=new();Relations=new();Affect=new();Facts=new();
    }

    /// <summary>留空使用公共特征定义库；仅需另一套定义时指定。</summary>
    [Export] public CharacterFeatureLibrary FeatureLibrary {get;set;}
    [Export] public CharacterIdentityDefinition Identity {get;set;}
    [Export] public CharacterNeedsDefinition Needs {get;set;}
    [Export] public CharacterPersonalityDefinition Personality {get;set;}
    [Export] public CharacterRelationsDefinition Relations {get;set;}
    [Export] public CharacterAffectDefinition Affect {get;set;}
    [Export] public CharacterFactSchema Facts {get;set;}
    [Export] public CharacterWorldDefinition World {get;set;}

    // Called by compilation and editor tools, never by constructors or serialization getters.
    public CharacterFeatureLibrary ResolveFeatureLibrary()=>FeatureLibrary??GD.Load<CharacterFeatureLibrary>(CharacterFeatureLibrary.CommonPath);
}
