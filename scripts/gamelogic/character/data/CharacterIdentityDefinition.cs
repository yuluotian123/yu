using Godot;

namespace GameLogic;

[Tool,GlobalClass]
public partial class CharacterIdentityDefinition : CharacterDefinitionResource
{
    [Export] public string[] Tags {get;set;}=System.Array.Empty<string>();
    [Export] public Godot.Collections.Array<CharacterScalarDefinition> Parameters {get;set;}=new();
    public override string Category=>"身份与实例参数";
}
