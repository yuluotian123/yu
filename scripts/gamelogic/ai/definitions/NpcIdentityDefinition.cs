using Godot;

namespace GameLogic.AI;

[Tool,GlobalClass]
public partial class NpcIdentityDefinition : NpcDefinitionResource
{
    [Export] public string[] Tags {get;set;}=System.Array.Empty<string>();
    [Export] public SpriteFrames SpriteFrames {get;set;}
    [Export] public string IdleAnimation {get;set;}="idle";
    [Export] public string WalkAnimation {get;set;}="walk";
    [Export] public Godot.Collections.Array<NpcScalarDefinition> Parameters {get;set;}=new();
    public override string Category=>"身份与实例参数";
}
