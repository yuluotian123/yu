using Godot;

namespace GameLogic.AI;

[Tool,GlobalClass]
public partial class NpcMemoryDefinition : NpcDefinitionResource
{
    [Export] public Godot.Collections.Array<NpcMemoryEntryDefinition> Entries {get;set;}=new();
    public override string Category=>"记忆规则";
}
