using System;
using System.Collections.Generic;
using Godot;
namespace GameLogic;

public sealed record CharacterFeatureSpec(string Id,string Name,CharacterFeatureKind Kind,double Minimum,double Maximum,double DefaultValue,double RecoveryPerHour);

[Tool,GlobalClass]
public partial class CharacterFeatureLibrary : Resource
{
    public const string CommonPath="res://assets/resources/character/definitions/common_features.tres";
    [Export] public Godot.Collections.Array<CharacterFeatureDefinition> Definitions {get;set;}=new();
    public CharacterFeatureDefinition Find(string id)
    {
        foreach(var definition in Definitions)if(definition?.DefinitionId==id)return definition;
        return null;
    }
    public Dictionary<string,CharacterFeatureSpec> Compile()
    {
        var result=new Dictionary<string,CharacterFeatureSpec>(StringComparer.Ordinal);var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var d in Definitions)
        {
            CharacterProfileSpec.Require(d!=null&&!string.IsNullOrWhiteSpace(d.DefinitionId)&&ids.Add(d.DefinitionId)&&Enum.IsDefined(d.Kind),"特征定义为空、ID 重复或类别无效。");
            if(!d.Enabled)continue;
            CharacterProfileSpec.Require(d.Kind==CharacterFeatureKind.Trait||double.IsFinite(d.Minimum)&&double.IsFinite(d.Maximum)&&d.Minimum<d.Maximum&&double.IsFinite(d.DefaultValue)&&d.DefaultValue>=d.Minimum&&d.DefaultValue<=d.Maximum,"特征定义的范围或默认值无效："+d.DisplayName);
            CharacterProfileSpec.Require(d.Kind!=CharacterFeatureKind.Affect||double.IsFinite(d.RecoveryPerHour)&&d.RecoveryPerHour>=0,"情绪恢复速度无效："+d.DisplayName);
            result.Add(d.DefinitionId,new(d.DefinitionId,d.DisplayName,d.Kind,d.Minimum,d.Maximum,d.DefaultValue,d.RecoveryPerHour));
        }
        return result;
    }
    public Godot.Collections.Array<CharacterFeatureDefinition> AddDefinition(CharacterFeatureKind kind)
    {
        var result=new Godot.Collections.Array<CharacterFeatureDefinition>(Definitions);
        result.Add(new(){Kind=kind,DisplayName="新"+CharacterFeatureDefinition.KindName(kind),Minimum=kind is CharacterFeatureKind.Affect or CharacterFeatureKind.Relation?-1:0,DefaultValue=kind is CharacterFeatureKind.Personality or CharacterFeatureKind.Preference?.5:0});
        return result;
    }
}
