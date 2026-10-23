#if TOOLS
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Framework;
using Framework.UI;
using GameLogic;
using GameLogic.AI;
using Godot;

public partial class WorldFrameworkValidation : Node
{
    const string Folder="res://.godot/world-validation/";
    int _checks;
    public override void _Process(double delta){if(_driving&&ModuleSystem.GetModule<IWorldModule>().CurrentContext==null)ModuleSystem.Process(delta,delta);}
    bool _driving=true;
    void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);_checks++;}
    void Reject(Action action,string message){try{action();}catch(ArgumentException){_checks++;return;}throw new InvalidOperationException(message);}
    public override async void _Ready()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Folder));
            ModuleSystem.RegisterModule<ISaveModule>(new SaveModule(Folder+"saves",Folder+"legacy"));
            var world=(WorldModule)ModuleSystem.GetModule<IWorldModule>();
            var place=new WorldLocationDefinition{Id="validation_place",DisplayName="Validation",PublicServices=new[]{"storage"}};
            var type=WorldTestObjects.StockType();
            var a=new WorldLevelDefinition{Id="validation_a",ScenePath=Folder+"a.tscn",Location=place};
            var b=new WorldLevelDefinition{Id="validation_b",ScenePath=Folder+"b.tscn",Location=place};
            var bad=new WorldLevelDefinition{Id="validation_bad",ScenePath=Folder+"bad.tscn",Location=place};
            var wrong=new WorldLevelDefinition{Id="validation_wrong_root",ScenePath=Folder+"wrong_root.tscn",Location=place};
            var item=new WorldCatalogEntry("world/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","Saved crate",type,b,place,Array.Empty<string>(),"",type.Capabilities.ToArray());
            var definition=new WorldDefinition{Id="validation_world",Locations=new(){place},Levels=new(){a,b,bad,wrong},ObjectTypes=new(){type},InitialLevel=a,PlayerScene=Folder+"player.tscn",Calendar=new(){DayLengthSeconds=4800}};
            SavePlayer(definition.PlayerScene);SaveLevel(a,null,true);SaveLevel(b,item,false);SaveLevel(bad,null,false,true);
            Save(new Node3D{Name="WrongRoot"},wrong.ScenePath);WorldAuthoringCompiler.CompileAll(definition);
            var catalog=new WorldCatalog(definition);
            Check(catalog.Query(place:place.Id,allTags:new[]{"storage"}).Single().Id==item.Id,"Indexed world queries must find unloaded objects without loading scenes");
            definition.Locations.Add(place);Reject(()=>new WorldCatalog(definition),"Duplicate identities must fail");definition.Locations.RemoveAt(1);
            place.Parent=place;Reject(()=>new WorldCatalog(definition),"Location cycles must fail");place.Parent=null;
            Check(await ObserveLoading(world,world.OpenAsync(this,definition,false)),"New world must open: "+world.LastError);
            Check(world.CurrentLevelId==a.Id&&world.Player.PersistentId=="player","World owns one persistent player");
            ulong player=world.Player.GetInstanceId();var character=world.Player.GetComponent<CharacterDataComponent3D>();
            character.State.Apply(new[]{new System.Collections.Generic.KeyValuePair<string,CharacterValue>("private.test",CharacterValue.Numeric(7))});
            var resident=world.CurrentContext.Owner.GetNode<GameObject3D>("Resident").GetComponent<CharacterDataComponent3D>();
            resident.State.Apply(new[]{new System.Collections.Generic.KeyValuePair<string,CharacterValue>("private.test",CharacterValue.Numeric(9))});
            Check(world.ResolveStatus(item.Id)=="未加载","Directory identity must remain valid while its level is unloaded");
            Check(await UnloadedQuery(world,item),"Disabling reachability must never permit unloaded point actions");
            Check(await ObserveLoading(world,world.SwitchAsync(b.Id)),"A to B must switch: "+world.LastError);
            Check(world.Player.GetInstanceId()==player&&character.State.Read("private.test").Number==7,"Switching must preserve the same player and character state");
            Check(world.ResolveStatus(item.Id)=="已加载"&&world.ResolveObject(item.Id).Owner.GetComponent<WorldStockComponent3D>().Amount==20,"Object defaults apply on first creation only");
            KnowledgeQueries(world,place,item);
            world.ResolveObject(item.Id).Owner.GetComponent<WorldStockComponent3D>().Amount=6;
            Check(await world.SwitchAsync(a.Id),"B to A must switch: "+world.LastError);
            resident=world.CurrentContext.Owner.GetNode<GameObject3D>("Resident").GetComponent<CharacterDataComponent3D>();
            Check(resident.State.Read("private.test").Number==9,"Unloaded character sections must restore their latest state");
            Check(world.ResolveStatus(item.Id)=="未加载"&&world.ObjectState(item.Id).Capability<WorldStockState>("stock").Amount==6,"Unloading must retain latest object state");
            Check(await world.SwitchAsync(b.Id)&&world.ResolveObject(item.Id).Owner.GetComponent<WorldStockComponent3D>().Amount==6,"Reloading must not reset stock");
            world.Player.GlobalPosition=new(1,2,3);ModuleSystem.GetModule<ISaveModule>().Save("world-test");
            Check(!await world.SwitchAsync(a.Id,"missing-entry")&&world.CurrentLevelId==b.Id,"Missing entry must preserve the old level");
            double orphans=Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
            Check(!await world.SwitchAsync(wrong.Id)&&world.CurrentLevelId==b.Id&&Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount)==orphans,"Invalid scene roots must not leak orphan nodes or replace the current level");
            Check(!await ObserveLoading(world,world.SwitchAsync(bad.Id))&&world.CurrentLevelId==b.Id&&world.ResolveObject(item.Id).Owner.GetComponent<WorldStockComponent3D>().Amount==6,"Activation failure must roll back the original scene and state");
            Check(world.Player.GetInstanceId()==player&&world.Player.GlobalPosition.IsEqualApprox(new(1,2,3)),"Rollback must preserve player identity and transform");
            Check(!await world.SwitchAsync(a.Id,cancellation:new CancellationToken(true))&&world.CurrentLevelId==b.Id,"Cancelled preparation must not replace current level");
            var cancelledLoad=world.SwitchAsync(a.Id);
            while(ModuleSystem.GetModule<IUIModule>().GetWindow<LevelLoadingWindow>()?.IsPrepare!=true)
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            world.CancelTransition();
            Check(!await cancelledLoad&&world.CurrentLevelId==b.Id&&!ModuleSystem.GetModule<IUIModule>().HasWindow<LevelLoadingWindow>()
                &&world.Player.ProcessMode!=ProcessModeEnum.Disabled,"Cancelling visible loading must close the overlay and restore player control");
            world.Close();Check(!world.IsOpen&&world.Player==null,"Closing must release the session and player");
            Check(await world.OpenAsync(this,definition,true,"world-test"),"Saved world must reopen: "+world.LastError);
            Check(world.CurrentLevelId==b.Id&&world.Player.GlobalPosition.IsEqualApprox(new(1,2,3))&&world.ResolveObject(item.Id).Owner.GetComponent<WorldStockComponent3D>().Amount==6,"Continue must restore saved level, position and unloaded state");
            Check(world.Player.GetComponent<CharacterDataComponent3D>().State.Read("private.test").Number==7,"Continue must restore player data");
            Check(await world.SwitchAsync(a.Id),"Saved A remains reachable");
            Check(world.CurrentContext.Owner.GetNode<GameObject3D>("Resident").GetComponent<CharacterDataComponent3D>().State.Read("private.test").Number==9,"Saving from B must retain detached A character sections");
            Check(ModuleSystem.GetModule<ISaveModule>().Load("world-test"),"Live load must be accepted");
            for(int i=0;i<300&&(world.CurrentLevelId!=b.Id||world.IsSwitching);i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(world.CurrentLevelId==b.Id&&world.Player.GlobalPosition.IsEqualApprox(new(1,2,3)),"Live loading another level must apply saved player placement");
            var pending=world.SwitchAsync(b.Id);world.Close();await pending;
            Check(!world.IsOpen&&GetChildren().All(n=>n.Name!="WorldSessionRoot"),"Late requests cannot recreate a closed session");
            var oldOpen=world.OpenAsync(this,definition,false);definition.InitialLevel=b;
            var newOpen=world.OpenAsync(this,definition,false);definition.InitialLevel=a;
            Check(!await oldOpen&&await newOpen&&world.CurrentLevelId==b.Id,"Superseded world opens must not close the newer session");
            Check(await world.OpenAsync(this,definition,false),"New game must start after closing");
            Check(world.Player.GetComponent<CharacterDataComponent3D>().State.Read("private.test").Number==0,"New game must not reuse cached slot state");
            world.Close();CatalogScale(place,type,a);
            _driving=false;ModuleSystem.Shutdown();
            // Drain native collection wrappers before the test engine tears down its allocators.
            GC.Collect();GC.WaitForPendingFinalizers();
            GD.Print($"WORLD_FRAMEWORK_OK: {_checks} checks");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());_driving=false;ModuleSystem.Shutdown();GetTree().Quit(1);}
    }
    async Task<bool> ObserveLoading(WorldModule world,Task<bool> request)
    {
        var ui=ModuleSystem.GetModule<IUIModule>();
        bool visible=false,complete=false,monotonic=true,readyOnly=true,blocked=true;
        float previous=0;
        while(!request.IsCompleted)
        {
            float progress=world.Progress;
            monotonic&=progress>=previous&&progress>=0&&progress<=1;previous=progress;
            readyOnly&=progress<1||world.Status=="加载完成";
            var window=ui.GetWindow<LevelLoadingWindow>();
            if(window?.IsPrepare==true&&window.IsVisible)
            {
                visible=true;complete|=window.Owner.GetNode<ProgressBar>("%Progress").Value==100;
                blocked&=window.Owner.GetParent<CanvasLayer>().Layer>200&&window.Owner.MouseFilter==Control.MouseFilterEnum.Stop
                    &&(world.Player==null||world.Player.ProcessMode==ProcessModeEnum.Disabled);
            }
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        bool result=await request;
        Check(visible&&monotonic&&readyOnly&&blocked,"Loading must show a topmost blocking overlay with monotonic progress that completes only when the level is ready");
        Check(!result||complete,"Successful loading must present a completed progress bar");
        Check(!ui.HasWindow<LevelLoadingWindow>(),"Success and failure must both close the loading overlay");
        return result;
    }
    AiAgentComponent3D QueryActor(WorldModule world)
    {
        var graph=new NpcGraphAsset();graph.Nodes.Add(new NpcRootNodeData());
        var host=new GameObject3D{Name="QueryActor",PersistentId="query_actor",Components=new(){new CharacterDataComponent3D{Profile=Profile()},new AiAgentComponent3D{Graph=graph,Persist=false,Running=false}}};
        world.CurrentContext.Owner.AddChild(host);return host.GetComponent<AiAgentComponent3D>();
    }
    static NpcTargetSelectorSpec Query()=>new("query","query",NpcTargetSource.WorldDirectory,"",Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),0,false,false,null,NpcTargetSelection.Candidates,null,4);
    Task<bool> UnloadedQuery(WorldModule world,WorldCatalogEntry item)
    {
        var actor=QueryActor(world);actor.Character.WorldKnowledge.KnownObjects.Add(item.Id);
        var ids=NpcWorldTargetQuery.Ids(actor,Query() with{Scope=NpcWorldScope.World});
        bool result=ids.Contains(item.Id)&&!NpcBindingAdapters.Find("point").Candidates(actor,Query() with{Scope=NpcWorldScope.World}).Any();
        actor.Owner.Free();return Task.FromResult(result);
    }
    void KnowledgeQueries(WorldModule world,WorldLocationDefinition place,WorldCatalogEntry item)
    {
        var actor=QueryActor(world);var knowledge=actor.Character.WorldKnowledge;var query=Query();
        Check(!NpcWorldTargetQuery.Ids(actor,query).Any(),"Directory existence does not grant object knowledge");
        knowledge.KnownLocations.Add(place.Id);
        Check(!NpcWorldTargetQuery.Ids(actor,query).Any(),"Knowing a place does not reveal all its objects");
        Check(NpcWorldTargetQuery.Ids(actor,query with{Information=NpcWorldInformation.PublicServices}).Contains(item.Id),"Known place may advertise explicitly public service records");
        Check(!actor.PerceivedPoint(item.Id),"Known service must not become current perception");
        var adapter=NpcBindingAdapters.Find("point");var facts=adapter.Capture(actor,new[]{item.Id},true).ToDictionary(p=>p.Key,p=>p.Value);
        Check(facts["point."+item.Id+".stock"].Kind==GameLogic.Goap.GoapValueKind.Unknown,"Unknown stock remains Unknown, never zero or live stock");
        actor.RecordQuery(query with{Information=NpcWorldInformation.Authoritative},new[]{item.Id});
        facts=adapter.Capture(actor,new[]{item.Id},true).ToDictionary(p=>p.Key,p=>p.Value);
        Check(facts["point."+item.Id+".stock"].Kind!=GameLogic.Goap.GoapValueKind.Unknown,"Explicit authoritative queries may read live state");
        world.ObjectState(item.Id).Destroyed=true;
        Check(world.ResolveStatus(item.Id)=="已销毁"&&!adapter.Validate(actor,CharacterValue.Text(item.Id))&&!adapter.Candidates(actor,query with{Source=NpcTargetSource.WorldPoints}).Any(),"Destroyed objects cannot become actionable even when usability and reachability filters are disabled");
        world.ObjectState(item.Id).Destroyed=false;
        knowledge.Objects["preferred"]=item.Id;
        Check(NpcWorldTargetQuery.Ids(actor,query with{Source=NpcTargetSource.CharacterBinding,ParameterKey="preferred"}).Single()==item.Id,"Typed role binding resolves one stable identity");
        knowledge.Observe(item.Id,place.Id,12,17,true);var restored=new WorldKnowledge(null);restored.Restore(knowledge.Capture());
        Check(restored.Objects["preferred"]==item.Id&&restored.Observations[item.Id].Stock==17,"Bindings and observations round-trip independently of live stock");
        Check(!actor.Snapshot(Array.Empty<string>(),true).Facts.Any(p=>p.Key.StartsWith("point.")),"Snapshots exclude unrelated object facts");
#if TOOLS
        var copy=item with {Id="independent",DisplayName="Independent copy"};
        Check(copy.Id!=item.Id&&copy.Type==item.Type&&copy.Capability<WorldStockCapability>().InitialAmount==item.Capability<WorldStockCapability>().InitialAmount,"Independent duplicate shares type but receives new identity");
#endif
        var point=world.ResolvePoint(item.Id);point.Owner.Position=new(80,0,0);point.OnUpdate(0);
        Check(!point.World.Nearby(Vector3.Zero,1).Contains(point)&&point.World.Nearby(new(80,0,0),1).Contains(point),"Spatial index follows moving endpoints");
        actor.Owner.Free();
    }
    void CatalogScale(WorldLocationDefinition place,WorldObjectTypeDefinition type,WorldLevelDefinition level)
    {
        foreach(int count in new[]{1000,10000})
        {
            var definition=new WorldDefinition{Locations=new(){place},ObjectTypes=new(){type},Levels=new(){level}};
            var entries=Enumerable.Range(0,count).Select(i=>new WorldCatalogEntry("scale_"+i,"",type,level,place,Array.Empty<string>(),"",type.Capabilities.ToArray()));
            var watch=System.Diagnostics.Stopwatch.StartNew();var catalog=new WorldCatalog(definition,entries);double build=watch.Elapsed.TotalMilliseconds;
            watch.Restart();Check(catalog.Query(level.Id,allTags:new[]{"storage"}).Count==count,"Indexed scale query count");double cold=watch.Elapsed.TotalMilliseconds;
            watch.Restart();for(int n=0;n<20;n++)_ = catalog.Query(level.Id,allTags:new[]{"storage"});
            GD.Print($"WORLD_CATALOG_SCALE: records={count}, build_ms={build:0.00}, cold_query_ms={cold:0.000}, cached_query_mean_ms={watch.Elapsed.TotalMilliseconds/20:0.000}");
        }
    }
    static CharacterProfile Profile()
    {
        var profile=new CharacterProfile();profile.Facts.Facts.Add(new(){Key="private.test",Type=CharacterValueType.Number,Minimum=0,Maximum=100,Initial=new(){Number=0}});return profile;
    }
    static void SavePlayer(string path)
    {
        var player=new GameObject3D{Name="Player",PersistentId="player",Components=new(){new CharacterDataComponent3D{Profile=Profile()},new CharacterPersistenceComponent3D()}};
        Save(player,path);
    }
    static void SaveLevel(WorldLevelDefinition level,WorldCatalogEntry item,bool resident,bool broken=false)
    {
        var root=new GameObject3D{Name="ValidationLevel",Components=new(){new WorldLevelContextComponent3D{Level=level,RequireNavigation=broken}}};
        void Child(Node parent,Node node){parent.AddChild(node);node.Owner=root;}
        var environment=new GameObject3D{Name="World",Components=new(){new AiWorldComponent3D()}};Child(root,environment);
        var spawn=new GameObject3D{Name="Spawn",Components=new(){new WorldSpawnPointComponent3D()}};Child(root,spawn);Child(spawn,new Marker3D{Name="Arrival",Position=new(0,1,0)});
        if(item!=null)
        {
            var host=WorldTestObjects.WithArea(new GameObject3D{Name="Storage",PersistentId="storage_host",Components=new(WorldTestObjects.Components(item.Type,new(){ObjectType=item.Type,DisplayName=item.DisplayName}))});Child(root,host);
            host.SetMeta(WorldIdentity.PlacementKey,"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            foreach(var child in host.FindChildren("*","",true,false))child.Owner=root;
        }
        if(resident)Child(root,new GameObject3D{Name="Resident",PersistentId="test_resident",Components=new(){new CharacterDataComponent3D{Profile=Profile()}}});
        Save(root,level.ScenePath);
    }
    static void Save(Node node,string path){var scene=new PackedScene();if(scene.Pack(node)!=Error.Ok||ResourceSaver.Save(scene,path)!=Error.Ok)throw new Exception("Cannot save fixture "+path);node.Free();}
}
#endif
