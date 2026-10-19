using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameLogic;
using GameLogic.AI;
using GameLogic.Goap;
using Godot;

public partial class NpcFrameworkValidation
{
    void ValidateResidentStrollPlans(NpcProgram program)
    {
        var goal=program.Goals.Single(g=>g.Id=="gray_green_resident.goal.activity");
        var move=goal.Actions.Single(a=>a.Name=="前往散步地点");var stroll=goal.Actions.Single(a=>a.Name=="散步");var inPlace=goal.Actions.Single(a=>a.Name=="原地散步");
        Check(move.Predictions.Count==1&&move.Predictions[0].Key=="self.location"&&move.Results.Count==0&&move.Success!=null&&stroll.Precondition.FactKeys().Contains("input.地点.at"),"Stroll movement still contains activity effects");
        Check(move.Inputs[0].Variable==stroll.Inputs[0].Variable&&move.Inputs[0].Source==NpcBindingSource.PlanVariable&&inPlace.Inputs.Count==0,"Stroll target binding/no-target variant is misconfigured");
        Check(stroll.Execution.Nodes.OfType<FlowActionNodeData>().SelectMany(n=>n.Actions).OfType<NpcRoamAction>().Single().Radius>0&&
            inPlace.Execution.Nodes.OfType<FlowActionNodeData>().SelectMany(n=>n.Actions).OfType<NpcRoamAction>().Single().Radius==0,"Activity/rain flows do not distinguish local walking and walking in place");
        var life=GD.Load<PackedScene>("res://assets/scenes/npc/riverside_life.tscn").Instantiate<Node3D>();
        var points=new List<NpcTargetCandidate>();
        foreach(string id in new[]{"town_stroll","lakeside_stroll"})
        {
            var node=life.GetNode<GameObject3D>(id);var config=node.Components.OfType<AiInteractionPointComponent3D>().Single();
            points.Add(new(id,config.Tags.ToArray(),node.Position,node.Position.Length(),true,true,true,true));
            Check(program.KnownPoints.Contains(id),"Resident does not know a configured stroll destination");
        }
        life.Free();
        NpcPlanSearch Search(double rain,string location="outside",IReadOnlyList<NpcTargetCandidate> candidates=null)
        {
            var state=new GoapWorldState(program.Facts.Values.Select(f=>new KeyValuePair<string,GoapValue>(f.Key,program.Encode(f.Key,f.Initial))))
                .With(GoapEffect.Set("needs.fun.value",program.Encode("needs.fun.value",NpcValue.Numeric(.8))),GoapEffect.Set("world.rain",program.Encode("world.rain",NpcValue.Numeric(rain))),GoapEffect.Set("self.location",GoapValue.Id(location)));
            foreach(var point in points)state=state.With(GoapEffect.Set("point."+point.Id+".available",GoapValue.Bool(true)),GoapEffect.Set("point."+point.Id+".open",GoapValue.Bool(true)),GoapEffect.Set("point."+point.Id+".stock",program.Encode("target.stock",NpcValue.Numeric(10000))));
            var grounded=NpcTargetQuery.Ground(program,goal,state,candidates??points);
            var search=new NpcPlanSearch(program,goal,state,grounded.Select(a=>new KeyValuePair<string,GoapAction>(a.PlanActionKey,a.Search)));
            for(int i=0;i<4096&&search.Status==GoapSearchStatus.Pending;i++)search.Advance(1);
            return search;
        }
        foreach(string id in new[]{"town_stroll","lakeside_stroll"})
        {
            var dry=Search(0,candidates:points.Where(p=>p.Id==id).ToArray());
            Check(dry.Status==GoapSearchStatus.Found&&dry.Result.Steps.Select(s=>s.Id.Split("::").Last()).SequenceEqual(new[]{move.Id+"@"+id,stroll.Id+"@"+id}),"Dry stroll did not plan travel and activity at "+id);
            Check(Search(0,id).Result?.Steps.Single().Id.Split("::").Last()==stroll.Id+"@"+id,"Already at a stroll destination still requires travel");
        }
        var wet=Search(1,candidates:Array.Empty<NpcTargetCandidate>());
        Check(wet.Status==GoapSearchStatus.Found&&wet.Result.Steps.Single().Id.Split("::").Last()==inPlace.Id+"@","Rain plan tried to find/travel to a destination");
        Check(Search(0,candidates:Array.Empty<NpcTargetCandidate>()).Status==GoapSearchStatus.NoPlan,"Dry plan incorrectly fell back to rain-only activity");
        Check(Search(.05).ChosenPlan.ActionIds.Contains(move.Id)&&Search(.051).ChosenPlan.ActionIds.SequenceEqual(new[]{inPlace.Id}),"Configured rain threshold has a gap or overlap");
    }

    async Task ValidateResidentStrollRuntime(AiAgentComponent3D resident,AiAgentComponent3D courier,AiWorldComponent3D world)
    {
        Check(world.Points.ContainsKey("town_stroll")&&world.Points.ContainsKey("lakeside_stroll"),"Missing real stroll interaction points");
        var town=world.Points["town_stroll"];var lake=world.Points["lakeside_stroll"];
        Check(resident.Navigation.TryEstimateBetween(resident.Owner.GlobalPosition,town.UsePosition,out _)&&resident.Navigation.TryEstimateBetween(resident.Owner.GlobalPosition,lake.UsePosition,out _),"Stroll destinations are not reachable on the real navigation map");
        courier.Running=false;courier.CancelPlan("StrollValidation");resident.Running=true;
        int initialAnimationRequests=resident.Owner.GetComponent<CharacterAnimationComponent3D>().ActiveRequestCount;
        void Start(){resident.CancelPlan("StrollValidation");resident.State.Needs.Values["hunger"]=.15;resident.State.Needs.Values["fatigue"]=.15;resident.State.Needs.Values["fun"]=.8;resident.State.Needs.Refresh();}
        float Horizontal(Vector3 a,Vector3 b)=>new Vector2(a.X-b.X,a.Z-b.Z).Length();
        foreach(var destination in new[]{lake,town})
        {
            world.RainOverride=0;town.SetOpen(destination==town);lake.SetOpen(destination==lake);Start();
            bool arrived=false,walked=false,localMotion=false,bindingConsistent=true;Vector3 activityStart=default;
            for(int frame=0;frame<3000&&resident.State.Needs.Values["fun"]>.25;frame++)
            {
                await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                if(resident.CurrentAction!="散步")continue;
                if(!arrived){activityStart=resident.Owner.GlobalPosition;arrived=true;}
                walked|=resident.Owner.GetComponent<CharacterAnimationComponent3D>().ActiveAnimation=="walk";
                localMotion|=Horizontal(activityStart,resident.Owner.GlobalPosition)>.12f;
                bindingConsistent&=resident.PlanBindings.Count==1&&resident.PlanBindings.Values.Single().Symbol==destination.PointId;
            }
            Check(arrived&&walked&&localMotion&&bindingConsistent&&resident.State.Needs.Values["fun"]<.25,resident.Describe()+"\nStroll failed at "+destination.PointId+"\n"+resident.Navigation.Diagnostics);
        }
        // Change weather during a real trip; the rainy plan must stay at its current position.
        town.SetOpen(false);lake.SetOpen(true);world.RainOverride=0;Start();
        bool travelling=false;
        for(int frame=0;frame<600&&!travelling;frame++){await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);travelling=resident.CurrentAction=="前往散步地点";}
        Check(travelling,"Could not start the travel step for weather interruption");world.RainOverride=1;
        bool stationary=false,animated=false,noTravel=true;Vector3 rainPosition=default;float drift=0;
        for(int frame=0;frame<1800&&resident.State.Needs.Values["fun"]>.25;frame++)
        {
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            if(resident.CurrentAction!="原地散步")continue;
            if(!stationary){stationary=true;rainPosition=resident.Owner.GlobalPosition;}
            drift=Math.Max(drift,Horizontal(rainPosition,resident.Owner.GlobalPosition));
            animated|=resident.Owner.GetComponent<CharacterAnimationComponent3D>().ActiveAnimation=="walk";
            noTravel&=resident.PlanBindings.Count==0&&resident.Navigation.Status==NpcNavigationStatus.Idle;
        }
        Check(stationary&&animated&&noTravel&&drift<.04f&&resident.State.Needs.Values["fun"]<.25,resident.Describe()+"\nRain activity moved or failed, drift="+drift);
        Check(resident.Trace.Any(s=>s.Contains("PlanEligibilityChanged")),"Changing rain did not cancel the outdoor plan");
        for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        Check(world.Points.Values.All(p=>p.Reservations==0)&&resident.PlanBindings.Count==0&&resident.Owner.GetComponent<CharacterAnimationComponent3D>().ActiveRequestCount<=initialAnimationRequests,"Finished stroll leaked a reservation, binding or animation request");
        world.RainOverride=0;town.SetOpen(true);lake.SetOpen(true);
    }
}
