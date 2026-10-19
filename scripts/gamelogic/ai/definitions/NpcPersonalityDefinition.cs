using Godot;

namespace GameLogic.AI;

[Tool,GlobalClass]
public partial class NpcPersonalityDefinition : NpcDefinitionResource
{
    [Export] public Godot.Collections.Array<NpcScalarDefinition> Dimensions {get;set;}=new();
    [Export] public string[] Traits {get;set;}=System.Array.Empty<string>();
    [Export] public Godot.Collections.Array<NpcScalarDefinition> Preferences {get;set;}=new();
    public override string Category=>"性格与偏好";
}
