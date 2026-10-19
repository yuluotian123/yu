using Godot;
namespace GameLogic;

public enum CharacterFeatureKind { Personality, Trait, Preference, Affect, Relation }

/// <summary>Shared meaning and defaults; individual values belong to character profiles and state.</summary>
[Tool,GlobalClass]
public partial class CharacterFeatureDefinition : CharacterDefinitionResource
{
    CharacterFeatureKind _kind;
    [Export] public CharacterFeatureKind Kind {get=>_kind;set{_kind=value;NotifyPropertyListChanged();}}
    [Export(PropertyHint.MultilineText)] public string Description {get;set;}="";
    [Export] public double Minimum {get;set;}
    [Export] public double Maximum {get;set;}=1;
    [Export] public double DefaultValue {get;set;}=.5;
    [Export] public double RecoveryPerHour {get;set;}=.1;
    public override string Category=>KindName(Kind);
    public static string KindName(CharacterFeatureKind kind)=>kind switch
    {CharacterFeatureKind.Personality=>"性格",CharacterFeatureKind.Trait=>"特质",CharacterFeatureKind.Preference=>"偏好",CharacterFeatureKind.Affect=>"情绪",CharacterFeatureKind.Relation=>"关系",_=>"未知类别"};
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        base._ValidateProperty(property);
        string name=property["name"].AsString();
        if(Kind==CharacterFeatureKind.Trait&&name is nameof(Minimum) or nameof(Maximum) or nameof(DefaultValue)||Kind!=CharacterFeatureKind.Affect&&name==nameof(RecoveryPerHour))
            property["usage"]=(long)PropertyUsageFlags.Storage;
    }
}
