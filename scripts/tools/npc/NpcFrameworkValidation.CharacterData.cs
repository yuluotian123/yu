using System;
using System.Linq;
using System.Threading.Tasks;
using GameLogic;
using GameLogic.AI;
using Godot;

public partial class NpcFrameworkValidation
{
    void ValidateSharedCharacterData(NpcGraphAsset graph,NpcProgram program)
    {
        Check(!typeof(CharacterState).GetProperties().Any(p=>p.PropertyType==typeof(NpcProgram)),"Character state depends on an AI program");
        Check(!typeof(CharacterProfile).GetProperties().Any(p=>typeof(NpcDefinitionResource).IsAssignableFrom(p.PropertyType)),"Character profile contains AI configuration");
        Check(graph.Definitions.All(d=>d is NpcGoalDefinition or NpcActionDefinition or NpcPlanTemplateDefinition or NpcTargetSelectorDefinition or NpcScheduleDefinition or NpcPerceptionDefinition or NpcMemoryDefinition or NpcPolicyDefinition or NpcNeedPolicyDefinition),"AI graph contains a shared character definition");
        Check(typeof(CharacterState).GetProperty("Memory")==null&&typeof(NpcDecisionState).GetProperty("Memory")!=null,"Memory moved out of the AI controller");
#if TOOLS
        var rule=graph.Definitions.OfType<NpcNeedPolicyDefinition>().First();
        var picker=graph.CreateCustomPropertyEditor(rule,nameof(rule.NeedId));
        Check(picker is NpcNeedPolicyProperty,"AI need rules do not select from character needs");picker.Free();
#endif
        var first=new CharacterState(CharacterProfileSpec.Compile(graph.CharacterContext));
        var second=new CharacterState(CharacterProfileSpec.Compile(graph.CharacterContext));
        first.Advance(12);first.Advance(13);
        double initial=second.Needs.Values["hunger"],rate=second.Profile.Needs.Single(n=>n.Id=="hunger").Rate;
        Check(Math.Abs(first.Needs.Values["hunger"]-Math.Clamp(initial+rate,0,1))<1e-8&&second.Needs.Values["hunger"]==initial,"Character time advancement is shared or controller-dependent");
        Check(first.Profile.Parameter("home").Symbol.Length>0,"Character object parameter was lost");
        var decision=new NpcDecisionState(program,first);var policy=program.Needs.Single(n=>n.Id=="hunger");
        first.Needs.Values["hunger"]=policy.Enter;decision.Refresh();Check(decision.Read("needs.hunger.urgent").IsTrue,"AI need enter threshold was lost");
        first.Needs.Values["hunger"]=(policy.Enter+policy.Exit)/2;decision.Refresh();Check(decision.Read("needs.hunger.urgent").IsTrue,"AI need hysteresis was lost");
        first.Needs.Values["hunger"]=policy.Exit;decision.Refresh();Check(!decision.Read("needs.hunger.urgent").IsTrue,"AI need exit threshold was lost");
        var saved=first.Capture();Check(saved["memory"]==null&&saved["urgent"]==null,"Shared persistence contains AI state");
        Check(decision.Capture()["needs"]==null&&decision.Capture()["relations"]==null,"AI persistence duplicates character data");
        var copy=graph.CloneForCharacter();copy.CharacterContext.Identity.Parameters.Single(p=>p.Key=="home").Symbol="different_home";
        Check(graph.CharacterContext.Identity.Parameters.Single(p=>p.Key=="home").Symbol!="different_home","Graph authoring contexts share mutable profile copies");
        graph.SaveJsonFields();string path="res://.godot/npc/decision_only_graph.tres";
        Check(ResourceSaver.Save(graph,path)==Error.Ok,"Could not save decision-only graph");
        using var file=Godot.FileAccess.Open(path,Godot.FileAccess.ModeFlags.Read);
        Check(!file.GetAsText().Contains("CharacterProfile")&&!file.GetAsText().Contains("CharacterNeedsDefinition"),"Saving an AI graph serialized its character context");
    }
    async Task ValidateSharedCharacterRuntime(Node3D level,AiAgentComponent3D resident,AiAgentComponent3D courier,AiWorldComponent3D world)
    {
        var player=level.GetNode<GameObject3D>("Player");var data=player.GetComponent<CharacterDataComponent3D>();
        Check(data?.State!=null&&player.GetComponent<AiAgentComponent3D>()==null,"Player cannot use character data without AI");
        Check(!ReferenceEquals(data.State,resident.State)&&!ReferenceEquals(resident.State,courier.State),"Player and NPC share a mutable character state");
        for(int frame=0;frame<2;frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        var playerSave=data.Capture();var residentSave=resident.Character.Capture();var courierSave=courier.Character.Capture();
        double hours=world.Clock.TotalHours;bool running=resident.Running;resident.Running=false;
        double hunger=data.State.Needs.Values["hunger"],npcHunger=resident.State.Needs.Values["hunger"];
        world.Clock.AdvanceHours(.25);
        for(int frame=0;frame<2;frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        Check(data.State.Needs.Values["hunger"]>hunger&&resident.State.Needs.Values["hunger"]>npcHunger,"Character needs require a running AI controller");
        var aiSave=resident.Capture();resident.Restore(aiSave,resident.SchemaVersion);
        Check(resident.State.Needs.Values["hunger"]>npcHunger,"Restoring AI overwrote shared character state");
        var stateBefore=resident.State;var programBefore=resident.Program;
        resident.CancelPlan("CharacterOwnershipCheck");
        Check(ReferenceEquals(stateBefore,resident.State)&&ReferenceEquals(programBefore,resident.Program),"Cancelling a plan recreated character data");
        world.Clock.AdvanceHours(hours-world.Clock.TotalHours);
        data.Restore(playerSave,data.SchemaVersion);resident.Character.Restore(residentSave,1);courier.Character.Restore(courierSave,1);resident.Running=running;
        Check(Math.Abs(data.State.Needs.Values["hunger"]-hunger)<1e-8,"Player character save restore failed");
    }
}
