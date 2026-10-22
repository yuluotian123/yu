#if TOOLS
using System;
using System.Linq;
using System.Threading.Tasks;
using Framework;
using GameLogic;
using GameLogic.AI;
using Godot;

public partial class WorldSimulationValidation : Node
{
    const string Folder="res://.godot/world-simulation-validation/";
    int _checks;
    bool _driving=true;
    WorldModule _world;
    void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);_checks++;}
    public override void _Process(double delta){if(_driving&&_world?.CurrentContext==null)ModuleSystem.Process(delta,delta);}
    static CharacterExpressionDefinition Fact(string key)=>new(){Kind=CharacterExpressionKind.Fact,Key=key};
    static NpcEffectDefinition Set(string key,CharacterExpressionDefinition value)=>new(){Key=key,Value=value};
    static FlowGraphAsset Flow(params GraphSharedAction[] actions)
    {
        var f=new FlowGraphAsset();var entry=new FlowEntryNodeData();var work=new FlowActionNodeData{Actions=actions.Cast<GraphActionBase>().ToList()};var done=new FlowReturnNodeData{Label="Success"};
        f.Nodes.Add(entry);f.Nodes.Add(work);f.Nodes.Add(done);f.ConnectNodes(entry.Id,0,work.Id,0);f.ConnectNodes(work.Id,0,done.Id,0);f.SaveJsonFields();return f;
    }
    public override async void _Ready()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Folder));AddChild(new GraphBlackboardNode());
            ModuleSystem.RegisterModule<ISaveModule>(new SaveModule(Folder+"saves",Folder+"no-legacy"));_world=(WorldModule)ModuleSystem.GetModule<IWorldModule>();
            var home=new WorldLocationDefinition{Id="home",DisplayName="Home"};var work=new WorldLocationDefinition{Id="work",DisplayName="Work"};
            var a=new WorldLevelDefinition{Id="sim_a",ScenePath=Folder+"a.tscn",Location=home};var b=new WorldLevelDefinition{Id="sim_b",ScenePath=Folder+"b.tscn",Location=work};
            var middle=new WorldLevelDefinition{Id="sim_middle",ScenePath=Folder+"middle.tscn",Location=home};
            var route=new WorldRouteDefinition{Id="road",From=a,To=middle,FromEntry="road",ToEntry="road",TravelHours=.15};
            var secondRoad=new WorldRouteDefinition{Id="second_road",From=middle,To=b,FromEntry="exit",ToEntry="road",TravelHours=.05};
            var type=WorldTestObjects.StockType(5);type.Capability<WorldInteractionCapability>().Operations.Add(new WorldTakeStockInteraction());
            var fixture=new NpcSelectorBindingValidation.Fixture();fixture.Selector.Source=NpcTargetSource.WorldDirectory;fixture.Selector.Scope=NpcWorldScope.World;fixture.Selector.Information=NpcWorldInformation.Authoritative;
            fixture.First.ExecutionGraph=Flow(new NpcMoveToTargetAction{Input=new(){Name="target"}});fixture.First.Predictions.Add(Set("self.location",Fact("input.target.id")));fixture.First.SuccessConditions=Fact("input.target.at");
            fixture.First.Cost=Fact("input.target.travel_hours");fixture.First.TimeoutSeconds=100;fixture.First.Results.Add(Set("private.started",new(){ValueType=CharacterValueType.Boolean,Boolean=true}));
            fixture.Second.ExecutionGraph=Flow(new NpcWaitAction{Seconds=NpcGraphNumber.Constant(10)},new NpcInteractAction{Input=new(){Name="target"},OperationId="take_stock",Amount=NpcGraphNumber.Constant(2)});
            fixture.Second.Cost=new(){Number=1};fixture.Second.Results.Add(Set("private.done",new(){ValueType=CharacterValueType.Boolean,Boolean=true}));fixture.SecondInput.ResourceRequirements=new[]{"capacity"};
            var window=new NpcScheduleWindow{WindowId="work",StartHour=0,EndHour=0,Location=work,Goal=(NpcGoalDefinition)fixture.Graph.Resolve(fixture.GoalNode.DefinitionId),AllowedOperations=new[]{"take_stock"},CommitmentSeconds=2};
            fixture.Profile.Facts.Facts.Add(new(){Key="private.fallback",Type=CharacterValueType.Boolean,Initial=new(){ValueType=CharacterValueType.Boolean}});
            var fallbackGoal=new NpcGoalDefinition{DisplayName="Fallback",Utility=new(){Number=1},Completion=Fact("private.fallback")};
            var fallbackAction=new NpcActionDefinition{DisplayName="Wait fallback",ExecutionGraph=Flow(new NpcWaitAction{Seconds=NpcGraphNumber.Constant(.1)})};
            fallbackAction.Predictions.Add(Set("private.fallback",new(){ValueType=CharacterValueType.Boolean,Boolean=true}));fallbackAction.Results.Add(Set("private.fallback",new(){ValueType=CharacterValueType.Boolean,Boolean=true}));
            var fg=new NpcGoalNodeData();var fa=new NpcActionNodeData();var fp=new NpcTemplateNodeData();fixture.Add(fg,fallbackGoal);fixture.Add(fa,fallbackAction);fixture.Add(fp,new NpcPlanTemplateDefinition{ActionIds=new[]{fallbackAction.DefinitionId}});
            fixture.Graph.ConnectNodes(fixture.Root.Id,1,fg.Id,0);fixture.Graph.ConnectNodes(fg.Id,0,fa.Id,0);fixture.Graph.ConnectNodes(fg.Id,1,fp.Id,0);window.FallbackGoal=fallbackGoal;
            var scheduleNode=new NpcScheduleNodeData();fixture.Add(scheduleNode,new NpcScheduleDefinition{Windows=new(){window}});fixture.Graph.ConnectNodes(fixture.Root.Id,0,scheduleNode.Id,0);
            Check(NpcExpressionEditorCatalog.Collect(fixture.Graph).Any(s=>s.Key=="activity.active")&&NpcExpressionEditorCatalog.ExpectedType(fixture.Graph,window,nameof(NpcScheduleWindow.Activation))==CharacterValueType.Boolean,"The expression editor exposes activity facts and boolean activation");
            fixture.Graph.SaveJsonFields();ResourceSaver.Save(fixture.Profile,Folder+"profile.tres");ResourceSaver.Save(fixture.Graph,Folder+"graph.tres");
            var npc=new GameObject3D{Name="Worker",PersistentId="worker",Components=new(){new WorldActorComponent3D{DetailedDistance=4,AbstractDistance=8,MinimumResidencySeconds=0},new CharacterDataComponent3D(),new AiAgentComponent3D{Graph=new()},new CharacterPersistenceComponent3D()}};
            Save(npc,Folder+"worker.tscn");
            void Level(WorldLevelDefinition level,bool worker,bool crate,int population=1)
            {
                var root=new GameObject3D{Name="Level",Components=new(){new WorldLevelContextComponent3D{Level=level}}};
                void Child(Node parent,Node child){parent.AddChild(child);child.Owner=root;}
                Child(root,new GameObject3D{Name="World",Components=new(){new AiWorldComponent3D()}});
                var spawn=new GameObject3D{Name="Spawn",Components=new(){new WorldSpawnPointComponent3D()}};Child(root,spawn);Child(spawn,new Marker3D{Name="Arrival",Position=new(100,0,0)});
                var road=new GameObject3D{Name="Road",Components=new(){new WorldSpawnPointComponent3D{Entry="road"}}};Child(root,road);Child(road,new Marker3D{Name="Arrival"});
                var exit=new GameObject3D{Name="Exit",Components=new(){new WorldSpawnPointComponent3D{Entry="exit",MarkerPath="Gate/Arrival"}}};Child(root,exit);
                var gate=new Node3D{Name="Gate",Position=new(1,0,0)};Child(exit,gate);Child(gate,new Marker3D{Name="Arrival",Position=new(3,0,0)});
                if(worker)for(int index=0;index<population;index++)
                {
                    var instance=ResourceLoader.Load<PackedScene>(Folder+"worker.tscn").Instantiate<GameObject3D>();
                    instance.Components=new(instance.Components.Select(c=>c is CharacterDataComponent3D?(Component3D)new CharacterDataComponent3D{Profile=fixture.Profile}:c is AiAgentComponent3D?new AiAgentComponent3D{Graph=fixture.Graph}:c));
                    if(population>1){instance.Name="Worker"+index;instance.PersistentId="worker_"+index.ToString("D2");}Child(root,instance);
                }
                if(crate)
                {
                    var obj=WorldTestObjects.WithArea(new GameObject3D{Name="Crate",Position=new(2,0,0),Components=new(WorldTestObjects.Components(type))});obj.SetMeta(WorldIdentity.PlacementKey,"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");Child(root,obj);
                    foreach(var child in obj.FindChildren("*","",true,false))child.Owner=root;
                }
                Save(root,level.ScenePath);
            }
            Level(a,true,false);Level(b,false,true);Level(middle,false,false);
            Save(new GameObject3D{Name="Player",PersistentId="player",Components=new(){new CharacterDataComponent3D(),new CharacterPersistenceComponent3D()}},Folder+"player.tscn");
            var definition=new WorldDefinition{Id="simulation",Locations=new(){home,work},Levels=new(){a,middle,b},ObjectTypes=new(){type},Routes=new(){route,secondRoad},InitialLevel=a,PlayerScene=Folder+"player.tscn",Calendar=new(){DayLengthSeconds=2400,StartHour=6}};
            WorldAuthoringCompiler.CompileAll(definition);Check(new WorldCatalog(definition).Actors.Count==1,"The editor builds a lightweight actor directory from the reusable character scene");
            Check(new WorldCatalog(definition).Entrances[(middle.Id,"exit")].X==4,"Compiled entrances include every nested transform");
            Check(await _world.OpenAsync(this,definition,false),"Simulation fixture must open: "+_world.LastError);_world.Population.Clock.Paused=true;
            var actor=_world.Population.Actors["worker"];string target=_world.Catalog.Objects.Values.Single().Id;
            Check(Math.Abs(actor.Hours-_world.Population.Clock.TotalHours)<.01&&actor.Hours>=6&&actor.Hours<7,"New actors start at the world's configured calendar epoch");
            for(int frame=0;frame<20&&actor.Travel==null;frame++){_world.Population.Process();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            Check(actor.IsProxy&&actor.Agent.Program!=null,"Distant characters use a lightweight proxy without loading their remote level");
            var query=actor.Agent.Program.TargetSelectors.Values.Single();var candidates=NpcBindingAdapters.Find("point").Candidates(actor.Agent,query).ToArray();
            Check(candidates.Length==1&&candidates[0].LevelId==b.Id&&candidates[0].Distance==null&&candidates[0].TravelHours>=.2272,"Remote candidates distinguish route time including local transfers from unknown spatial distance");
            Check(actor.Agent.CurrentActivity=="work"&&actor.Agent.ActivityPlace==work.Id,"The same AI graph receives its activity Goal and place context");
            Check(actor.Travel!=null&&actor.Level==a.Id,"An NPC starts autonomous cross-level travel without switching the player");
            Check(_world.CurrentLevelId==a.Id&&_world.ResolvePoint(target)==null,"Travel must not load the destination scene");
            Check(actor.Travel.Routes.Count==2,"The planner uses connected multi-leg routes and local transfers between entrances");
            _world.Population.Clock.AdvanceHours(.1);await CatchUp();
            Check(actor.Travel is {ElapsedHours:>0}&&actor.Level==a.Id,"Travel has persistent intermediate progress");
            ModuleSystem.GetModule<ISaveModule>().Save("mid-travel");
            _world.Population.Clock.AdvanceHours(.3);await CatchUp();
            Check(actor.Level==b.Id&&actor.Agent.Read("private.done").IsTrue,"The NPC arrives and finishes its scheduled work offscreen: "+actor.Agent.Describe());
            Check(_world.ObjectState(target).Capability<WorldStockState>("stock").Amount==3,"Offscreen execution commits the same business operation exactly once");
            Check(await _world.SwitchAsync(b.Id),"The player can later enter the NPC's destination");
            _world.Player.GlobalPosition=actor.Position;_world.Population.RequestInterest("worker",10);
            for(int frame=0;frame<90&&actor.IsProxy;frame++){_world.Population.Process();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            Check(!actor.IsProxy&&actor.Host.Owner.SceneFilePath==Folder+"worker.tscn","The arrived NPC materializes as its original character scene");
            Check(_world.CurrentContext.Owner.FindChildren("*","",true,false).OfType<GameObject3D>().Count(h=>h.PersistentId=="worker")==1,"Exactly one scene instance represents the actor");
            Check(_world.ResolveObject(target).Owner.GetComponent<WorldStockComponent3D>().Amount==3&&actor.Agent.Read("private.done").IsTrue,"Precision handoff keeps object and character effects");
            Check(await _world.SwitchAsync(a.Id),"Return to the authored origin level");
            Check(!_world.CurrentContext.Owner.FindChildren("*","",true,false).OfType<GameObject3D>().Any(h=>h.PersistentId=="worker"),"The origin placement cannot respawn a migrated NPC");
            _world.Close();Check(await _world.OpenAsync(this,definition,true,"mid-travel"),"Resume a mid-travel save");_world.Population.Clock.Paused=true;actor=_world.Population.Actors["worker"];
            Check(actor.Travel is {ElapsedHours:>0}&&actor.Level==a.Id,"Continue restores route and progress");
            _world.Population.Clock.AdvanceHours(.3);await CatchUp();
            Check(actor.Agent.Read("private.done").IsTrue&&_world.ObjectState(target).Capability<WorldStockState>("stock").Amount==3,"Resume finishes travel and work without duplicate effects");
            var invalid=_world.Population.Capture();invalid["worker"]["travel"]=System.Text.Json.JsonSerializer.SerializeToNode(new WorldTravelState{Target=target,Routes=new(){"missing"}});
            bool rejected=false;var previousHost=actor.Host;
            try{_world.Population.Restore(invalid);}catch(ArgumentException){rejected=true;}
            Check(rejected&&actor.Host==previousHost,"Invalid travel checkpoints are rejected before replacing live hosts");
            Check(ModuleSystem.GetModule<ISaveModule>().Load("mid-travel"),"Live read of a managed population succeeds");
            for(int frame=0;frame<90;frame++){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(!_world.IsSwitching&&frame>2)break;}
            actor=_world.Population.Actors["worker"];_world.Population.Clock.Paused=true;
            Check(actor.Travel is {ElapsedHours:>0}&&!_world.Population.IsCatchingUp,"Live read rewinds the simulation epoch and restores travel");
            _world.Population.Clock.AdvanceHours(.3);await CatchUp();
            Check(actor.Agent.Read("private.done").IsTrue&&_world.ObjectState(target).Capability<WorldStockState>("stock").Amount==3,"Live restore replays only remaining work");
            Check(!_world.Population.RequestInterest("worker",double.PositiveInfinity),"Interest requests have bounded lifetimes");
            actor.Agent.Character.WorldKnowledge.Observe(target,work.Id,actor.Hours,3,true,"dialogue",.01);
            Check(actor.Agent.Character.WorldKnowledge.Observations[target].Fresh(actor.Hours)&&!actor.Agent.Character.WorldKnowledge.Observations[target].Fresh(actor.Hours+.02),"Observation sources and expiry are explicit");
            _world.Routes.SetOpen(route.Id,false);Check(_world.Routes.Find(a.Id,b.Id,actor.Agent)==null,"Closed roads cannot be treated as reachable");_world.Routes.SetOpen(route.Id,true);
            route.Public=false;Check(_world.Routes.Find(a.Id,b.Id,actor.Agent)==null,"Private roads require route knowledge");actor.Agent.Character.WorldKnowledge.KnownRoutes.Add(route.Id);
            Check(_world.Routes.Find(a.Id,b.Id,actor.Agent)?.Legs.Count==2,"Granted route knowledge unlocks only the authorized road");route.Public=true;
            string goal=fixture.Graph.Resolve(fixture.GoalNode.DefinitionId).DefinitionId;
            Check(actor.Agent.RequestActivity(new("committed",goal,work.Id,50,actor.Hours+2,Operations:new[]{"take_stock"},CommitmentSeconds:100)),"A task can request the same activity context as a schedule");_world.Population.Process();
            Check(actor.Agent.CurrentActivity=="committed"&&!actor.Agent.ActivityAllows("pour_liquid"),"Requested activities apply Goal, location and operation limits");
            actor.Agent.RequestActivity(new("normal",goal,work.Id,100,actor.Hours+2));_world.Population.Process();Check(actor.Agent.CurrentActivity=="committed","Commitment prevents routine priority churn");
            actor.Agent.RequestActivity(new("emergency",goal,work.Id,1,actor.Hours+2,Emergency:true));_world.Population.Process();Check(actor.Agent.CurrentActivity=="emergency","Emergency activities interrupt commitment using the same Goal executor");
            Check(!actor.Agent.RequestActivity(new("bad","missing",work.Id,10,actor.Hours+1)),"Unknown activity Goals are rejected");
            _world.Close();a.ScenePath=Folder+"scale_a.tscn";Level(a,true,false,24);WorldAuthoringCompiler.CompileAll(definition);
            Check(await _world.OpenAsync(this,definition,false),"A population fixture opens from reusable scenes: "+_world.LastError);_world.Population.Clock.Paused=true;
            Check(_world.Population.Actors.Count==24,"The roster indexes every authored actor exactly once");_world.ObjectState(target).Capability<WorldStockState>("stock").Amount=60;
            _world.Population.EventsPerFrame=4;_world.Population.Clock.AdvanceHours(4);_world.Population.Process();
            Check(_world.Population.LastEvents<=4&&_world.Population.Backlog>0,"Fast-forward leaves bounded backlog instead of replaying all work in one frame");
            var samples=new System.Collections.Generic.List<double>();int frames=0,events=0,maxEvents=0;
            while((_world.Population.Backlog>0||_world.Population.IsCatchingUp)&&frames++<10000)
            {_world.Population.Process();maxEvents=Math.Max(maxEvents,_world.Population.LastEvents);samples.Add(_world.Population.LastMilliseconds);events+=_world.Population.LastEvents;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            Check(maxEvents<=4,"Every simulation slice honors its event cap");
            Check(_world.Population.Backlog==0&&_world.Population.Actors.Values.All(r=>r.Agent.Read("private.done").IsTrue),"All actors finish their offscreen work without starvation: "+string.Join(";",_world.Population.Actors.Values.Where(r=>!r.Agent.Read("private.done").IsTrue).Take(2).Select(r=>r.Definition.Id+" "+r.Mode+" "+r.Hours+" "+r.Agent.Describe())));
            Check(_world.ObjectState(target).Capability<WorldStockState>("stock").Amount==12,"Shared capacity serializes 24 workers and commits 48 units once");
            Check(_world.Population.Actors.Values.Any(r=>r.Agent.Read("private.fallback").IsTrue),"Blocked activities execute the configured fallback Goal");
            samples.Sort();double p95=samples.Count==0?0:samples[Math.Min(samples.Count-1,(int)Math.Ceiling(samples.Count*.95)-1)];
            GD.Print($"WORLD_SIMULATION_SCALE: actors=24, fast_forward_hours=4, frames={frames}, events={events}, slice_p95_ms={p95:0.000}, state_kib={_world.Population.Capture().ToJsonString().Length/1024d:0.0}");
            _world.Close();_driving=false;ModuleSystem.Shutdown();GC.Collect();GC.WaitForPendingFinalizers();GD.Print($"WORLD_SIMULATION_OK: {_checks} checks");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());_driving=false;ModuleSystem.Shutdown();GetTree().Quit(1);}
    }
    async Task CatchUp()
    {
        for(int frame=0;frame<300;frame++){_world.Population.Process();if(_world.Population.Backlog==0&&!_world.Population.IsCatchingUp)return;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        throw new InvalidOperationException("Simulation backlog did not drain");
    }
    static void Save(Node node,string path){var packed=new PackedScene();WorldCatalog.Require(packed.Pack(node)==Error.Ok&&ResourceSaver.Save(packed,path)==Error.Ok,"Cannot save fixture "+path);packed.TakeOverPath(path);node.Free();}
}
#endif
