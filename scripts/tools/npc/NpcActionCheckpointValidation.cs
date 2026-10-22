using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Framework;
using GameLogic;
using GameLogic.AI;
using Godot;

public partial class NpcActionCheckpointValidation : Node
{
    const string Folder="res://.godot/npc-checkpoint-validation/";
    int _checks;
    bool _driving;
    public override void _Process(double delta)
    {if(_driving&&ModuleSystem.GetModule<IWorldModule>().CurrentContext==null)ModuleSystem.Process(delta,delta);}
    void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);_checks++;}
    sealed class Fixture
    {
        public readonly NpcSelectorBindingValidation.Fixture Outer=new();
        public readonly NpcWaitAction Wait=new(){Seconds=NpcGraphNumber.Fact("local.units")};
        public Fixture()
        {
            Outer.Selector.RequireReachable=false;Outer.FirstInput.ResourceRequirements=new[]{"capacity"};
            Outer.Profile.Facts.Facts.Add(new(){Key="private.received",Type=CharacterValueType.Number,Minimum=0,Maximum=100,Initial=new(){Number=0}});
            var calculation=new NpcCalculateNumberAction{Local=new(){Key="units"},Value=new(){Kind=CharacterExpressionKind.Minimum,Inputs=new(){NpcGraphNumber.Fact("input.target.stock"),NpcGraphNumber.Constant(2)}}};
            Wait.Seconds=new(){Kind=CharacterExpressionKind.Multiply,Inputs=new(){NpcGraphNumber.Fact("local.units"),NpcGraphNumber.Constant(5)}};
            Outer.First.ExecutionGraph=Flow(calculation,Wait,new NpcInteractAction{Input=new(){Name="target"},OperationId="take_stock",Amount=NpcGraphNumber.Fact("local.units")});
            Outer.First.ExecutionGraph.BlackboardEntries.Add(new(){Key="units",Value=new GraphFloatBlackboardValue()});
            Outer.Second.ExecutionGraph=Flow(new NpcWaitAction{Seconds=NpcGraphNumber.Constant(2)});
            Outer.First.Results.Add(new(){Key="private.started",Value=new(){ValueType=CharacterValueType.Boolean,Boolean=true}});
            Outer.Second.Results.Add(new(){Key="private.done",Value=new(){ValueType=CharacterValueType.Boolean,Boolean=true}});
            foreach(var action in new[]{Outer.First,Outer.Second})action.Results.Add(new(){Key="private.received",Mode=NpcEffectMode.Add,Value=new(){Number=1}});
        }
    }
    static FlowGraphAsset Flow(params GraphSharedAction[] actions)
    {
        var graph=new FlowGraphAsset();var entry=new FlowEntryNodeData();var work=new FlowActionNodeData{Actions=actions.Cast<GraphActionBase>().ToList()};var done=new FlowReturnNodeData{Label="Success"};
        graph.Nodes.Add(entry);graph.Nodes.Add(work);graph.Nodes.Add(done);graph.ConnectNodes(entry.Id,0,work.Id,0);graph.ConnectNodes(work.Id,0,done.Id,0);graph.SaveJsonFields();return graph;
    }
    GameObject3D Host(Node parent,string id,params Component3D[] components)
    {
        var host=new GameObject3D{Name=id,PersistentId=id,Components=new(components)};WorldTestObjects.WithArea(host);parent.AddChild(host);host.SetProcess(false);return host;
    }
    WorldInteractionComponent3D Point(Node parent,string id,double amount)
    {
        var type=WorldTestObjects.StockType(amount);type.Capability<WorldInteractionCapability>().Operations.Add(new WorldTakeStockInteraction{Id="take_stock"});
        var host=Host(parent,id,WorldTestObjects.Components(type));host.Position=id=="B"?Vector3.Right:Vector3.Zero;
        var point=host.GetComponent<WorldInteractionComponent3D>();point.RequireLineOfSight=false;point.OnUpdate(0);return point;
    }
    AiAgentComponent3D Actor(Node parent,Fixture fixture,string id="CheckpointActor",bool persist=false)
    {
        var host=Host(parent,id,new CharacterDataComponent3D{Profile=fixture.Outer.Profile,Persist=persist},
            new AiAgentComponent3D{Graph=fixture.Outer.Graph,Persist=persist},new CharacterPersistenceComponent3D{Persist=persist});
        host.Position=Vector3.Right;return host.GetComponent<AiAgentComponent3D>();
    }
    void Start(AiAgentComponent3D agent)
    {
        agent.OnUpdate(0);for(int i=0;i<100&&agent.SelectorBindings.Count==0;i++)agent.AdvancePlanning(100);
        agent.OnUpdate(0);Check(agent.CurrentAction.Length>0,"A valid runtime plan must start: "+agent.LastFailure);
    }
    static JsonObject Clone(JsonObject data)=>(JsonObject)data.DeepClone();
    static double Progress(JsonObject data)=>data["checkpoint"]?["Current"]?["Execution"]?["Progress"]?.AsObject().Select(p=>p.Value["Elapsed"].GetValue<double>()).DefaultIfEmpty(0).Max()??0;
    public override async void _Ready()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Folder));
            await NavigationRecovery();
            SemanticRecovery();
            await WorldLifecycle();
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            GD.Print($"NPC_ACTION_CHECKPOINTS_OK: {_checks} checks");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());_driving=false;ModuleSystem.Shutdown();GetTree().Quit(1);}
    }
    void SemanticRecovery()
    {
        var root=new Node3D();AddChild(root);root.AddChild(new GraphBlackboardNode());
        Host(root,"CheckpointWorld",new AiWorldComponent3D());var a=Point(root,"A",10);var b=Point(root,"B",3);
        var fixture=new Fixture();var agent=Actor(root,fixture);var stock=b.Owner.GetComponent<WorldStockComponent3D>();var capacity=b.Owner.GetComponent<WorldUsageCapacityComponent3D>();
        try
        {
            Check(agent.Program!=null,"Checkpoint actions must compile through the existing NPC graph");
            Start(agent);agent.OnUpdate(4);
            var saved=agent.Capture();var character=agent.Character.Capture();
            foreach(var action in new[]{fixture.Outer.First,fixture.Outer.Second})action.ExecutionGraph.SaveJsonFields();
            fixture.Outer.Graph.SaveJsonFields();const string graphPath=Folder+"checkpoint_graph.tres";
            const string profilePath=Folder+"checkpoint_profile.tres";
            Check(ResourceSaver.Save(fixture.Outer.Graph,graphPath)==Error.Ok&&ResourceSaver.Save(fixture.Outer.Profile,profilePath)==Error.Ok,"A checkpoint's authored graph and character profile must save");
            var reloaded=ResourceLoader.Load<NpcGraphAsset>(graphPath,cacheMode:ResourceLoader.CacheMode.Ignore);
            var reloadedProfile=ResourceLoader.Load<CharacterProfile>(profilePath,cacheMode:ResourceLoader.CacheMode.Ignore);
            Check(NpcCheckpointLayout.Signature(NpcProgram.Compile(reloaded,reloadedProfile))==saved["checkpoint"]["Signature"].GetValue<string>(),"Definition signatures must survive a fresh resource reload");
            Check(Progress(saved)==4&&saved["checkpoint"]["Current"]["Inputs"]["target"]["Symbol"].GetValue<string>()=="B","Save the current semantic timer and chosen object");
            Check(capacity.Reservations==1&&stock.Amount==3&&agent.Read("private.received").Number==0,"Waiting owns capacity but commits no stock or reward");
            Check(saved["checkpoint"]["Remaining"].AsArray().Count==1,"Save the remaining plan, not just the current action");
            var independent=Actor(root,fixture,"IndependentActor");Start(independent);independent.OnUpdate(2);
            Check(Progress(independent.Capture())==2&&Progress(agent.Capture())==4
                &&independent.SelectorBindings.Values.Single().Symbol=="A"&&independent.Capture()["checkpoint"]["InstanceId"].GetValue<string>()!=saved["checkpoint"]["InstanceId"].GetValue<string>(),"Two NPCs sharing the graph keep independent progress, runtime IDs and selected objects");
            agent.Restore(Clone(saved),agent.SchemaVersion);
            Check(capacity.Reservations==0&&agent.Capture()["checkpoint"]!=null,"Loading releases leases and retains a pending checkpoint until initialization finishes");
            agent.Character.Restore(Clone(character),agent.Character.SchemaVersion); // Exercise the opposite section-restore order.
            a.Owner.GetComponent<WorldStockComponent3D>().Amount=0;
            agent.OnUpdate(0);
            Check(agent.CheckpointStatus=="已恢复"&&agent.ActionElapsed==4&&Progress(agent.Capture())==4,"Restore timer progress after either section ordering: "+agent.LastFailure);
            Check(agent.SelectorBindings.Values.Single().Symbol=="B"&&capacity.Reservations==1,"A changed cheaper candidate must not replace the selected target; reacquire a live lease");
            stock.Amount=5;agent.OnUpdate(5.9);
            Check(agent.CompletedActions==0&&stock.Amount==5,"Restored wait must not complete before its remaining time");
            agent.OnUpdate(.1);
            Check(agent.CompletedActions==1&&stock.Amount==3&&agent.Read("private.received").Number==1,"Complete only the remaining second and commit the original frozen amount exactly once");
            Check(capacity.Reservations==0&&agent.QueuedActions.Count==1,"Successful restored action releases its lease and preserves the next step");
            var after=agent.Capture();
            Check(after["checkpoint"]["Current"]==null&&after["last_committed_action"].GetValue<string>().Length==32,"Committed action leaves a durable marker and is absent from the remaining plan");
            var committed=Clone(saved);committed["completed_actions"]=after["completed_actions"].DeepClone();committed["last_committed_action"]=after["last_committed_action"].DeepClone();
            committed["checkpoint"]["Current"]["Committed"]=true;
            agent.Restore(committed,agent.SchemaVersion);agent.OnUpdate(0);
            Check(stock.Amount==3&&agent.Read("private.received").Number==1&&agent.CurrentAction==fixture.Outer.Second.DisplayName,"A checkpoint at the committed boundary skips the finished action");
            agent.OnUpdate(2);agent.OnUpdate(0);
            Check(agent.CompletedActions==2&&agent.Read("private.done").IsTrue&&agent.Read("private.received").Number==2&&stock.Amount==3,"Complete the remaining shared-target plan without replaying stock or actor effects");
            Check(agent.Capture()["checkpoint"]==null&&agent.SelectorBindings.Count==0,"A completed plan has no checkpoint or bindings");
            void RestoreOriginal(JsonObject data)
            {agent.Character.Restore(Clone(character),1);stock.Amount=3;agent.Restore(data,agent.SchemaVersion);agent.OnUpdate(0);}
            var corrupt=Clone(saved);corrupt["checkpoint"]["Signature"]="changed";RestoreOriginal(corrupt);
            Check(agent.LastFailure.Contains("DefinitionOrWorldChanged")&&capacity.Reservations==0,"Changed definitions cancel recovery without leaking occupancy");
            corrupt=Clone(saved);corrupt["checkpoint"]["Current"]["Elapsed"]=-1;RestoreOriginal(corrupt);
            Check(agent.LastFailure.Contains("InvalidActionProgress"),"Invalid elapsed time is rejected");
            corrupt=Clone(saved);corrupt["checkpoint"]["Bindings"].AsObject().First().Value["Symbol"]="A";RestoreOriginal(corrupt);
            Check(agent.LastFailure.Contains("InvalidSelectorBindings"),"Contradictory shared targets are rejected");
            corrupt=Clone(saved);corrupt["checkpoint"]["Current"]["Committed"]=true;RestoreOriginal(corrupt);
            Check(agent.LastFailure.Contains("InvalidCommitMarker"),"A commit flag without the matching event identity is rejected");
            capacity.Reserve("other_actor");RestoreOriginal(Clone(saved));
            Check(agent.LastFailure=="ResourceBusy"&&capacity.Reservations==1,"Conflicting live occupancy cancels recovery without releasing another actor's lease");capacity.Release("other_actor");
            b.Owner.GetComponent<WorldOpeningHoursComponent3D>().SetOpen(false);RestoreOriginal(Clone(saved));
            Check(agent.CurrentAction.Length==0&&agent.LastFailure=="InputUnavailable"&&capacity.Reservations==0,"A closed target cancels recovery instead of silently changing the object");
            b.Owner.GetComponent<WorldOpeningHoursComponent3D>().SetOpen(true);
            agent.CancelPlan();Check(agent.Capture()["checkpoint"]==null,"Explicit cancellation discards pending recovery");
            var unsupported=new Fixture();unsupported.Outer.First.ExecutionGraph=Flow(new NpcWaitAction{Seconds=NpcGraphNumber.Constant(10)},new NpcRememberAction{Category="test"});
            var custom=Actor(root,unsupported,"UnsupportedActor");Start(custom);custom.OnUpdate(1);
            Check(custom.Capture()["checkpoint"]==null&&custom.Capture()["checkpoint_status"].GetValue<string>()=="Replan:UnsupportedExecution","Unsupported custom effects are cancelled and replanned, never replayed as if safe");
            custom.Owner.Free();
            // Navigation intent is restored from physical position, never a serialized path.
            a.Owner.GetComponent<WorldStockComponent3D>().Amount=10;
            var move=new Fixture();
            move.Outer.First.ExecutionGraph=Flow(new NpcMoveToTargetAction{Input=new(){Name="target"}},new NpcWaitAction{Seconds=NpcGraphNumber.Constant(10)});
            var walker=Actor(root,move,"MoveActor");Start(walker);walker.OnUpdate(3);var walked=walker.Capture();
            Check(walked["checkpoint"]!=null&&walked["checkpoint"]["Current"]["Execution"]["Progress"].AsObject().Count==2,"A completed local move and active wait share one semantic checkpoint");
            walker.Restore(walked,walker.SchemaVersion);walker.OnUpdate(0);
            Check(walker.CheckpointStatus=="已恢复"&&Progress(walker.Capture())==3,"A completed move is not repeated while restoring its following wait");
            walked["checkpoint"]["Current"]["Execution"]["Progress"].AsObject().First().Value["Complete"]=false;
            walker.Restore(walked,walker.SchemaVersion);walker.OnUpdate(0);
            Check(walker.LastFailure.Contains("Navigation"),"A missing navigation executor fails recovery explicitly");walker.Owner.Free();
        }
        finally{root.Free();ModuleSystem.Shutdown();}
    }
    async Task NavigationRecovery()
    {
        var root=new GameObject3D{Name="NavigationWorld",ProcessMode=ProcessModeEnum.Disabled,Components=new(){new AiWorldComponent3D()}};AddChild(root);root.AddChild(new GraphBlackboardNode());
        var mesh=new NavigationMesh{Vertices=new[]{new Vector3(-100,0,-100),new Vector3(-100,0,100),new Vector3(100,0,100),new Vector3(100,0,-100)}};mesh.AddPolygon(new[]{0,1,2,3});
        var region=new NavigationRegion3D{NavigationMesh=mesh};root.AddChild(region);Point(root,"A",10);var b=Point(root,"B",3);b.Owner.Position=Vector3.Right*10;b.OnUpdate(0);
        var fixture=new Fixture();fixture.Outer.First.ExecutionGraph=Flow(new NpcMoveToTargetAction{Input=new(){Name="target"}},new NpcWaitAction{Seconds=NpcGraphNumber.Constant(10)});
        var host=new GameObject3D{Name="Walker",PersistentId="Walker",Position=new(0,.36f,-1),Components=new(){new CharacterDataComponent3D{Profile=fixture.Outer.Profile,Persist=false},
            new CharacterMovementComponent3D{Profile=new(),StartDisabled=true},new NpcNavigationComponent3D{EnableAvoidance=false},
            new AiAgentComponent3D{Graph=fixture.Outer.Graph,Persist=false},new CharacterPersistenceComponent3D()}};
        var body=new CharacterBody3D{Name="PhysicsBody",CollisionLayer=0,CollisionMask=0};body.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(.4f,.72f,.4f)}});
        body.AddChild(new NavigationAgent3D{Name="NavigationAgent3D"});host.AddChild(body);root.AddChild(host);host.SetProcess(false);host.SetPhysicsProcess(false);
        var agent=host.GetComponent<AiAgentComponent3D>();
        try
        {
            for(int i=0;i<120&&(!agent.Navigation.MapReady||!agent.Navigation.TryEstimateBetween(host.GlobalPosition,b.UsePosition,out _));i++)
            {await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            Check(agent.Navigation.MapReady&&agent.Navigation.TryEstimateBetween(host.GlobalPosition,b.UsePosition,out _),"The navigation fixture must finish publishing its region before path checks");Start(agent);agent.OnUpdate(2);
            Check(agent.Navigation.Status==NpcNavigationStatus.Moving&&agent.Navigation.TargetId=="B","A detailed move must begin toward the selected object");
            host.GlobalPosition=Vector3.Right*5;host.GetComponent<CharacterMovementComponent3D>().SyncBodyToOwner();
            var position=host.GetComponent<CharacterPersistenceComponent3D>().Capture();var checkpoint=agent.Capture();agent.CancelPlan();host.Position=Vector3.Zero;
            host.GetComponent<CharacterPersistenceComponent3D>().Restore(position,5);agent.Restore(checkpoint,3);agent.OnUpdate(0);
            Check(host.GlobalPosition.X==5&&agent.ActionElapsed==2&&agent.Navigation.Status==NpcNavigationStatus.Moving&&agent.Navigation.TargetId=="B","A detailed move resumes from the restored physical position with a new navigation request");
            var abstractMove=agent.Capture();abstractMove["checkpoint"]["Abstract"]=true;agent.Restore(abstractMove,3);agent.OnUpdate(0);
            var abstractCheckpoint=agent.Capture();
            Check(abstractCheckpoint["checkpoint"]?["Current"]?["Execution"]?["Progress"]?.AsObject().First().Value["Duration"]!=null,"An abstract move must initialize its remaining travel duration: "+agent.Describe());
            double duration=abstractCheckpoint["checkpoint"]["Current"]["Execution"]["Progress"].AsObject().First().Value["Duration"].GetValue<double>();
            agent.OnUpdate(duration/2);var halfway=agent.Capture();
            Check(Progress(halfway)>0&&host.GlobalPosition.X==5,"Abstract local movement saves travel progress without prematurely teleporting");
            agent.Restore(halfway,3);agent.OnUpdate(0);
            Check(Math.Abs(Progress(agent.Capture())-duration/2)<1e-6,"Abstract movement retains elapsed travel time across restoration");
            agent.OnUpdate(duration/2+1e-6);
            Check(host.GlobalPosition.X==10&&agent.CurrentAction.Length>0&&agent.CompletedActions==0,"Only the remaining travel time is simulated before entering the following wait");
        }
        finally{root.Free();ModuleSystem.Shutdown();}
    }
    async Task WorldLifecycle()
    {
        ModuleSystem.RegisterModule<ISaveModule>(new SaveModule(Folder+"saves",Folder+"no-legacy"));
        var world=(WorldModule)ModuleSystem.GetModule<IWorldModule>();
        var blackboard=new GraphBlackboardNode();AddChild(blackboard);
        var place=new WorldLocationDefinition{Id="checkpoint_place"};
        var a=new WorldLevelDefinition{Id="checkpoint_a",ScenePath=Folder+"a.tscn",Location=place};
        var b=new WorldLevelDefinition{Id="checkpoint_b",ScenePath=Folder+"b.tscn",Location=place};
        var definition=new WorldDefinition{Id="checkpoint_world",Locations=new(){place},Levels=new(){a,b},InitialLevel=a,PlayerScene=Folder+"player.tscn",Calendar=new(){DayLengthSeconds=4800}};
        Save(new GameObject3D{Name="Player",PersistentId="player",Components=new(){new CharacterDataComponent3D(),new CharacterPersistenceComponent3D()}},definition.PlayerScene);
        foreach(var level in new[]{a,b})
        {
            var root=new GameObject3D{Name="Level",Components=new(){new WorldLevelContextComponent3D{Level=level}}};
            void Child(Node parent,Node child){parent.AddChild(child);child.Owner=root;}
            Child(root,new GameObject3D{Name="World",Components=new(){new AiWorldComponent3D()}});
            var spawn=new GameObject3D{Name="Spawn",Components=new(){new WorldSpawnPointComponent3D()}};Child(root,spawn);Child(spawn,new Marker3D{Name="Arrival"});Save(root,level.ScenePath);
        }
        WorldAuthoringCompiler.CompileAll(definition);_driving=true;
        Check(await world.OpenAsync(this,definition,false),"Fixture world must open: "+world.LastError);
        var fixture=new Fixture();
        AiAgentComponent3D Populate()
        {var root=world.CurrentContext.Owner;Point(root,"A",10);Point(root,"B",3);return Actor(root,fixture,"SavedActor",true);}
        var actor=Populate();Start(actor);actor.OnUpdate(4);ModuleSystem.GetModule<ISaveModule>().Save("checkpoint");
        var json=ModuleSystem.GetModule<ISaveModule>().ReadSlot("checkpoint",out _);var document=JsonNode.Parse(json);
        Check(document["sections"]["npc_agent"]["SavedActor"]["schema_version"].GetValue<int>()==3
            &&Progress(document["sections"]["npc_agent"]["SavedActor"].AsObject())==4,"The atomic save envelope contains the action checkpoint and schema");
        actor.OnUpdate(1);Check(await world.SwitchAsync(b.Id),"Switch away from an in-progress action");
        Check(await world.SwitchAsync(a.Id),"Return to the NPC's level");actor=Populate();actor.OnUpdate(0);
        Check(actor.ActionElapsed==5&&actor.CheckpointStatus=="已恢复","Unload snapshots the action before cancelling its scene executor: "+actor.LastFailure);
        Check(world.ResolveObject("B").Owner.GetComponent<WorldUsageCapacityComponent3D>().Reservations==1,"Reload reacquires capacity rather than serializing a stale lease");
        world.Close();Check(await world.OpenAsync(this,definition,true,"checkpoint"),"Continue the saved world");actor=Populate();actor.OnUpdate(0);
        Check(actor.ActionElapsed==4&&actor.SelectorBindings.Values.Single().Symbol=="B","Continue restores the saved time and shared identity, not the later runtime checkpoint");
        actor.OnUpdate(1);Check(ModuleSystem.GetModule<ISaveModule>().Load("checkpoint"),"Load the checkpoint into a live world");
        for(int frame=0;frame<180&&(GodotObject.IsInstanceValid(actor.Owner)||world.IsSwitching);frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        Check(!world.IsSwitching&&!GodotObject.IsInstanceValid(actor.Owner),"Live loading replaces the old level before restoring its NPC");actor=Populate();actor.OnUpdate(0);
        Check(actor.ActionElapsed==4&&actor.CheckpointStatus=="已恢复","Live loading preserves the pending action through the level reconstruction");
        actor.OnUpdate(6);actor.OnUpdate(0);actor.OnUpdate(2);
        Check(actor.CompletedActions==2&&actor.Read("private.received").Number==2&&world.ResolveObject("B").Owner.GetComponent<WorldStockComponent3D>().Amount==1,"Continue completes actor and object effects once");
        ModuleSystem.GetModule<ISaveModule>().Save("completed");world.Close();
        Check(await world.OpenAsync(this,definition,true,"completed"),"Continue after an already completed action");actor=Populate();actor.OnUpdate(0);
        Check(actor.CurrentAction.Length==0&&actor.Read("private.received").Number==2&&world.ResolveObject("B").Owner.GetComponent<WorldStockComponent3D>().Amount==1,"Repeated continue cannot duplicate stock consumption or rewards");
        world.Close();_driving=false;ModuleSystem.Shutdown();blackboard.Free();
    }
    static void Save(Node node,string path)
    {var packed=new PackedScene();if(packed.Pack(node)!=Error.Ok||ResourceSaver.Save(packed,path)!=Error.Ok)throw new Exception("Cannot save fixture "+path);node.Free();}
}
