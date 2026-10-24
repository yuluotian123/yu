#if TOOLS
using System;
using System.Linq;
using System.Threading.Tasks;
using Framework;
using GameLogic;
using GameLogic.AI;
using Godot;

/// <summary>Exercises the shipped resident and levels, including remote completion and materialization.</summary>
public partial class NpcDailyGoalsValidation : Node
{
    const string Folder="res://.godot/npc-daily-goals/";
    WorldModule _world;
    bool _driving=true;
    int _checks;
    ulong _observedHost;
    Vector3 _previousPosition;
    float _largestVisibleStep;
    int _visibleSamples;
    public override void _PhysicsProcess(double delta)
    {
        if(_world?.IsSwitching!=false||!_world.Population.Actors.TryGetValue("town_resident_01",out var actor)||actor.Host==null||actor.IsProxy||actor.Travel!=null)
        {_observedHost=0;return;}
        var host=actor.Host.Owner;var position=host.GlobalPosition;
        if(_observedHost==host.GetInstanceId())
        {
            _largestVisibleStep=Math.Max(_largestVisibleStep,new Vector2(position.X-_previousPosition.X,position.Z-_previousPosition.Z).Length());
            _visibleSamples++;
        }
        _observedHost=host.GetInstanceId();_previousPosition=position;
    }
    void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);_checks++;GD.Print("DAILY_CHECK: "+message);}
    public override void _Process(double delta){if(_driving&&_world?.CurrentContext==null)ModuleSystem.Process(delta,delta);}
    async Task CatchUp()
    {
        for(int n=0;n<1000;n++)
        {
            _world.Population.Process();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(n>5&&!_world.Population.IsCatchingUp&&_world.Population.Backlog==0)return;
        }
        throw new Exception("World catch-up did not finish");
    }
    public override async void _Ready()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Folder));AddChild(new GraphBlackboardNode());
            var definition=ResourceLoader.Load<WorldDefinition>(WorldDefinition.CommonPath);
            WorldAuthoringCompiler.CompileAll(definition);
            var catalog=new WorldCatalog(definition);
            Check(definition.InitialLevel.ScenePath=="res://assets/scenes/levels/forest_town_level.tscn","The formal town is the direct world entry");
            var entry=catalog.Actors["town_resident_01"];
            Check(entry.LevelId==definition.InitialLevel.Id&&entry.ScenePath=="res://assets/scenes/npc/resident.tscn","The formal town registers the reusable resident exactly once");
            var program=NpcProgram.Compile(entry.Graph,entry.Profile);
            Check(program.Goals.Count==2&&entry.Graph.Definitions.OfType<NpcActionDefinition>().Count()==4,$"The resident graph compiles with two Goals and four Actions (actual {program.Goals.Count}/{entry.Graph.Definitions.OfType<NpcActionDefinition>().Count()}, {entry.Graph.ResourcePath}, nodes {entry.Graph.Nodes.Count}, edges {entry.Graph.Connections.Count})");
            var water=catalog.Objects.Values.Single(o=>o.AllTags.Contains("drinking_water"));
            Check(definition.Routes.Count==2&&definition.Routes.All(route=>route.SourcePortalId.Length>0&&!route.Bidirectional&&route.UseExplicitPositions&&route.TravelHours==0),
                "Both formal portals export directed zero-duration routes without manual road resources");
            var crates=catalog.Objects.Values.Where(o=>o.Type.Id=="storage_type").ToArray();
            Check(entry.Profile.World.Locations["work"].Id==water.Location.Id,"Both selectors use the authored work location binding");
            ModuleSystem.RegisterModule<ISaveModule>(new SaveModule(Folder+"saves",Folder+"no-legacy"));
            _world=(WorldModule)ModuleSystem.GetModule<IWorldModule>();
            Check(await _world.OpenAsync(this,definition,false),"The actual formal town opens: "+_world.LastError);
            _world.Population.Clock.Paused=true;
            var actor=_world.Population.Actors[entry.Id];
            Check(!actor.IsProxy&&actor.Level==entry.LevelId&&actor.Travel==null&&actor.Host.Owner.Visible
                &&actor.Host.Owner.GlobalPosition.DistanceTo(entry.Position)<.2f,
                $"New game shows the resident at its authored town spawn before it starts travelling (mode={actor.Mode}, position={actor.Position}, travel={actor.Travel?.Target})");
            double stock=crates.Sum(c=>_world.ObjectState(c.Id).Capability<WorldStockState>("stock").Amount);
            double liquid=_world.ObjectState(water.Id).Capability<WorldLiquidState>("liquid").Volume;
            // First exercise real town navigation to the departure; then fast-forward the remote leg.
            _world.Population.Clock.Paused=false;
            for(int n=0;n<900&&actor.Travel==null&&actor.Level==entry.LevelId;n++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            _world.Population.Clock.Paused=true;
            Check(actor.Travel!=null||actor.Level!=entry.LevelId,$"The visible resident walks to the portal and starts or completes its exported route (position={actor.Host?.Owner.GlobalPosition}, navigation={actor.Agent?.Navigation?.Status}, {actor.Agent?.Navigation?.Diagnostics})");
            _world.Population.Clock.AdvanceHours(.2);await CatchUp();
            Check(actor.Level==water.Level.Id&&actor.Travel==null,"The resident reaches the workshop without moving the player");
            Check(actor.Agent.Read("private.cargo").Number==2,"The resident completes the authored two-unit collection Goal");
            Check(actor.Agent.Read("needs.thirst.value").Number<.3,"The drink result lowers thirst");
            Check(Math.Abs(_world.ObjectState(water.Id).Capability<WorldLiquidState>("liquid").Volume-(liquid-.25))<1e-6,"Drinking consumes exactly 0.25 liquid");
            Check(Math.Abs(crates.Sum(c=>_world.ObjectState(c.Id).Capability<WorldStockState>("stock").Amount)-(stock-2))<1e-6,"Collection consumes exactly two stock units");
            Check(_world.CurrentLevelId==definition.InitialLevel.Id&&_world.ResolvePoint(water.Id)==null,"The remote workshop remains unloaded while its actions complete");
            var saves=ModuleSystem.GetModule<ISaveModule>();saves.Save("daily-complete");Check(saves.Exists("daily-complete"),"The completed population saves");
            Check(await _world.SwitchAsync(water.Level.Id),"The player can enter the real workshop");
            var playerArrival=_world.Player.GlobalPosition;
            _world.Population.Clock.Paused=true;_world.Population.RequestInterest(entry.Id,20);
            for(int n=0;n<120&&actor.IsProxy;n++){_world.Population.Process();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            Check(!actor.IsProxy&&actor.Host.Owner.SceneFilePath==entry.ScenePath,"The resident materializes from its reusable scene");
            Check(actor.Agent.Read("private.cargo").Number==2,"Materialization preserves the collection result");
            for(int n=0;n<8;n++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            Check(_world.Player.GlobalPosition.DistanceTo(playerArrival)<.2f,"Materialization does not spawn a temporary collider at the origin or displace the player");
            // Trigger another drink with a visible host and exercise physical navigation and interaction.
            actor.Agent.State.Apply(new[]{new System.Collections.Generic.KeyValuePair<string,CharacterValue>("needs.thirst.value",CharacterValue.Numeric(.8))});
            actor.Agent.RequestReplan();_world.Population.Clock.Paused=false;
            for(int n=0;n<2400&&actor.Agent.Read("needs.thirst.value").Number>.3;n++)
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            _world.Population.Clock.Paused=true;
            Check(!actor.IsProxy&&actor.Agent.Read("needs.thirst.value").Number<.3,"The visible resident navigates and drinks through the same Action graph: "+actor.Agent.LastFailure);
            Check(Math.Abs(_world.ObjectState(water.Id).Capability<WorldLiquidState>("liquid").Volume-(liquid-.5))<1e-6,"The visible drink consumes one additional serving");
            actor.Agent.State.Apply(new[]{new System.Collections.Generic.KeyValuePair<string,CharacterValue>("private.cargo",CharacterValue.Numeric(0))});
            actor.Agent.RequestReplan();_world.Population.Clock.Paused=false;
            for(int n=0;n<1200&&actor.Agent.Read("private.cargo").Number<2;n++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            _world.Population.Clock.Paused=true;
            Check(!actor.IsProxy&&actor.Agent.Read("private.cargo").Number==2,"The visible resident also navigates and collects through its authored Action graph");
            Check(Math.Abs(crates.Sum(c=>_world.ObjectState(c.Id).Capability<WorldStockState>("stock").Amount)-(stock-4))<1e-6,"The visible collection consumes exactly two additional stock units");
            // Player E and the NPC consume the same bottle. Exhaust it through the public entry.
            var waterPoint=_world.ResolvePoint(water.Id);
            var waterState=_world.ObjectState(water.Id).Capability<WorldLiquidState>("liquid");
            var playerPosition=_world.Player.GlobalPosition;
            _world.Player.GlobalPosition=waterPoint.UsePosition;
            var playerUse=WorldInteraction.Execute(_world.Player,waterPoint,"pour_liquid");
            _world.Player.GlobalPosition=playerPosition;
            Check(playerUse.Success&&Math.Abs(waterState.Volume)<1e-6,"Player interaction consumes the last serving from the shared bottle: "+playerUse.Message);
            actor.Agent.State.Apply(new[]{new System.Collections.Generic.KeyValuePair<string,CharacterValue>("needs.thirst.value",CharacterValue.Numeric(.8))});
            actor.Agent.RequestReplan();_world.Population.Clock.Paused=false;
            for(int n=0;n<180&&actor.Agent.LastFailure!="NoPlan";n++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            _world.Population.Clock.Paused=true;
            Check(actor.Agent.LastFailure=="NoPlan"&&actor.Agent.Read("needs.thirst.value").Number>.3,"An empty bottle makes the drink Goal infeasible without granting thirst relief");
            var drinkGoal=actor.Agent.Program.Goals.Single(g=>g.Name=="解渴");
            double cooldown=actor.Agent.GoalCooldown(drinkGoal.Id);
            Check(cooldown>0&&cooldown<=actor.Agent.Program.Policy.Retry&&actor.Agent.RetryRemaining<=actor.Agent.Program.Policy.Retry,$"Cooldown diagnostics use the actor simulation clock (goal {cooldown:0.000}s, retry {actor.Agent.RetryRemaining:0.000}s)");
            Check(waterPoint.Owner.GetComponent<WorldLiquidComponent3D>().TryFill("water",.25),"The shared bottle can be refilled through its liquid capability");
            _world.Population.Clock.Paused=false;
            for(int n=0;n<1200&&actor.Agent.Read("needs.thirst.value").Number>.3;n++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            _world.Population.Clock.Paused=true;
            Check(actor.Agent.Read("needs.thirst.value").Number<.3&&Math.Abs(waterState.Volume)<1e-6,"After refill the NPC retries automatically and consumes exactly one serving");
            Check(actor.Agent.GoalCooldown(drinkGoal.Id)==0&&actor.Agent.RetryRemaining==0,"Expired retry and Goal cooldown diagnostics return zero");
            Check(await _world.SwitchAsync(definition.InitialLevel.Id),"The player can return to the formal town");
            Check(!_world.CurrentContext.Owner.FindChildren("*","",true,false).OfType<GameObject3D>().Any(h=>h.PersistentId==entry.Id),"Returning to the origin does not respawn the resident");
            _world.Close();Check(await _world.OpenAsync(this,definition,true,"daily-complete"),"The real-world save resumes");_world.Population.Clock.Paused=true;
            actor=_world.Population.Actors[entry.Id];_world.Population.Clock.AdvanceHours(.1);await CatchUp();
            Check(actor.Agent.Read("private.cargo").Number==2&&Math.Abs(crates.Sum(c=>_world.ObjectState(c.Id).Capability<WorldStockState>("stock").Amount)-(stock-2))<1e-6,"Resume does not duplicate collection rewards or consumption");
            await MidPlanHandoff(definition,entry,water);
            Check(_visibleSamples>60&&_largestVisibleStep<.2f,$"Detailed residents move continuously across {_visibleSamples} physics samples (largest step {_largestVisibleStep:0.000}m)");
            _world.Close();_driving=false;ModuleSystem.Shutdown();GD.Print($"NPC_DAILY_GOALS_OK: {_checks} checks");GetTree().Quit();
        }
        catch(Exception e)
        {
            GD.PushError(e+"\n"+string.Join("\n",_world?.Population?.Actors.Values.Select(a=>a.Agent?.Describe()+"\n"+string.Join("\n",a.Agent?.Trace??Array.Empty<string>()))??Array.Empty<string>()));
            _driving=false;ModuleSystem.Shutdown();GetTree().Quit(1);
        }
    }
    async Task MidPlanHandoff(WorldDefinition definition,WorldActorEntry entry,WorldCatalogEntry water)
    {
        _world.Close();Check(await _world.OpenAsync(this,definition,false),"A fresh world opens for the mid-plan handoff regression");
        var actor=_world.Population.Actors[entry.Id];
        for(int n=0;n<900&&actor.Travel==null;n++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        Check(actor.Travel!=null,"The resident reaches the portal before the handoff scenario");
        _world.Population.Clock.Paused=true;
        // Process one scheduled distant event at a time, stopping between movement and interaction.
        for(int n=0;n<200&&actor.Travel!=null;n++)
        {
            _world.Population.Clock.AdvanceHours(Math.Max(0,actor.NextHours-_world.Population.Clock.TotalHours));
            _world.Population.Process();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        Check(actor.IsProxy&&actor.Level==water.Level.Id&&actor.Travel==null&&actor.Agent.Capture()["checkpoint"]!=null,
            "The distant resident has a real unfinished plan at the destination");
        Check(await _world.SwitchAsync(water.Level.Id),"The player enters while the distant plan is unfinished");
        _world.Population.RequestInterest(entry.Id,20);
        for(int n=0;n<240&&actor.IsProxy;n++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        for(int n=0;n<5;n++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        Check(!actor.IsProxy&&!actor.Agent.IsAbstract,"Restoring a distant checkpoint on a visible host restores detailed navigation");
        _world.Population.Clock.Paused=false;
        for(int n=0;n<1800&&(actor.Agent.Read("private.cargo").Number<2||actor.Agent.Read("needs.thirst.value").Number>.3);n++)
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        Check(actor.Agent.Read("private.cargo").Number==2&&actor.Agent.Read("needs.thirst.value").Number<.3,
            "The materialized mid-plan resident finishes drinking and walks to collect stock");
    }
}
#endif
