using System;
using System.Linq;
using System.Threading.Tasks;
using Framework;
using GameLogic;
using GameLogic.AI;
using Godot;

public partial class WorldInteractionValidation : Node
{
    [Export] public bool CaptureFrames {get;set;}
    int _checks;
    bool _driving=true;
    void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);_checks++;}
    void Reject(Action action,string message){try{action();}catch(ArgumentException){_checks++;return;}throw new InvalidOperationException(message);}
    async Task Frames(int count=3){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    public override void _Process(double delta){if(_driving&&ModuleSystem.GetModule<IWorldModule>().CurrentContext==null)ModuleSystem.Process(delta,delta);}
    async Task PressE(int hold=3)
    {
        Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.E,Pressed=true});await Frames(hold);
        Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.E,Pressed=false});await Frames();
    }
    void Place(GameObject3D actor,Vector3 position)
    {actor.GlobalPosition=position;actor.GetComponent<CharacterMovementComponent3D>()?.SyncBodyToOwner();}
    public override async void _Ready()
    {
        try
        {
            ModuleSystem.RegisterModule<ISaveModule>(new SaveModule("res://.godot/world-validation/interaction-saves","res://.godot/world-validation/no-legacy"));
            var world=(WorldModule)ModuleSystem.GetModule<IWorldModule>();
            var definition=(WorldDefinition)ResourceLoader.Load<WorldDefinition>(WorldDefinition.CommonPath).Duplicate();
            var town=WorldTestObjects.ShippedLevel(definition,WorldTestObjects.TownScene);
            var workshop=WorldTestObjects.ShippedLevel(definition,WorldTestObjects.WorkshopScene);
            definition.InitialLevel=workshop;
            WorldTestObjects.KeepShippedActorsIdle(definition);
            Check(await world.OpenAsync(this,definition,false),"Interactive workshop must open: "+world.LastError);await Frames(8);
            var player=world.Player;var input=player.GetComponent<PlayerCharacterInputComponent3D>();var interactor=player.GetComponent<PlayerWorldInteractionComponent3D>();
            var point=world.ResolvePoint(WorldTestObjects.ShippedId(world,"仓储间储物箱"));var stock=point.Owner.GetComponent<WorldStockComponent3D>();var capacity=point.Owner.GetComponent<WorldUsageCapacityComponent3D>();
            Place(player,point.UsePosition+Vector3.Up*.468f);await Frames(8);interactor.RefreshTarget();
            Check(interactor.CurrentPointId==point.PointId&&interactor.PromptText.Contains("E"),"Player must see the nearest usable point prompt");
            await Capture("interaction-prompt");
            Place(player,point.Owner.GlobalPosition+new Vector3(1.4f,.468f,-1.4f));await Frames(8);
            Check(WorldInteraction.Check(player,point,"take_stock").Success,"The entire area permits interaction far from the navigation marker");
            await PressE(12);Check(stock.Amount==49&&capacity.Reservations==0,$"A physical E press executes once while held and releases its transient lease (stock={stock.Amount}, slots={capacity.Reservations}, last={interactor.LastResult})");
            await Capture("interaction-result");
            await PressE();Check(stock.Amount==48,"A second E press executes exactly once");
            string otherId=world.Catalog.Objects.Values.Single(e=>e.Type.Id==point.Object.ObjectType.Id&&e.Id!=point.PointId).Id;
            Check(world.ResolveObject(otherId).Owner.GetComponent<WorldStockComponent3D>().Amount==50,"Reused storage Scene instances have independent stock");
            input.GameplayBlocked=true;await PressE();Check(stock.Amount==48,"Inventory or other gameplay blocking prevents E interaction");input.GameplayBlocked=false;
            ModuleSystem.GetModule<IInputModule>().DisableLayer("Combat");await PressE();Check(stock.Amount==48,"GM input layer blocking prevents E interaction");ModuleSystem.GetModule<IInputModule>().EnableLayer("Combat");
            Check(!WorldInteraction.Execute(player,point,"unknown").Success&&stock.Amount==48,"Unknown operation cannot mutate stock");
            point.Object.State.Destroyed=true;Check(!WorldInteraction.Execute(player,point,"take_stock").Success,"Destroyed objects reject interaction");point.Object.State.Destroyed=false;
            Place(player,point.UsePosition+Vector3.Right*5);Check(!WorldInteraction.Execute(player,point,"take_stock").Success,"Out of range requests fail");Place(player,point.UsePosition+Vector3.Up*.468f);
            var collision=point.Owner.GetNode<CollisionShape3D>("InteractionArea/CollisionShape3D");
            var box=(BoxShape3D)collision.Shape.Duplicate();collision.Shape=box;var boxSize=box.Size;box.Size=new(40,2,40);
            Place(player,point.Owner.GlobalPosition+new Vector3(-17,.468f,0));await Frames(8);interactor.RefreshTarget();
            Check(interactor.CurrentPointId==point.PointId,"Spatial queries find wide areas even when their navigation marker is several grid cells away");box.Size=boxSize;
            Place(player,point.UsePosition+Vector3.Up*.468f);await Frames();
            var hours=point.Owner.GetComponent<WorldOpeningHoursComponent3D>();hours.SetOpen(false);
            Check(!WorldInteraction.Execute(player,point,"take_stock").Success&&stock.Amount==48,"Closed objects reject operations");hours.ClearOverride();
            point.Object.OwnerId="someone_else";Check(!WorldInteraction.Execute(player,point,"take_stock").Success,"Ownership applies to player operations");point.Object.OwnerId="";
            Check(capacity.Reserve("other")&&!WorldInteraction.Execute(player,point,"take_stock").Success&&stock.Amount==48,"Player operations respect NPC capacity claims");capacity.Release("other");
            capacity.Reserve(player.PersistentId);Check(WorldInteraction.Execute(player,point,"take_stock").Success&&capacity.Reserved(player.PersistentId),"An actor's preexisting lease is retained");capacity.Release(player.PersistentId);
            stock.IsActive=false;Check(!WorldInteraction.Execute(player,point,"take_stock").Success,"Inactive business components reject operations");stock.IsActive=true;
            stock.Amount=0;Check(!WorldInteraction.Execute(player,point,"take_stock").Success&&capacity.Reservations==0,"Insufficient stock does not acquire or leak a slot");stock.Amount=10;
            Place(player,point.UsePosition+new Vector3(1,.468f,0));
            var blocker=new StaticBody3D{Position=point.UsePosition+new Vector3(.5f,.65f,0)};
            blocker.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(.1f,2,1)}});world.CurrentContext.Owner.AddChild(blocker);await Frames();
            Check(!WorldInteraction.Execute(player,point,"take_stock").Success&&stock.Amount==10,"A wall blocks interaction without modifying the object");blocker.QueueFree();await Frames();
            Place(player,new Vector3(-5,.468f,-4));
            var npc=await NpcOperation(world,point);Check(stock.Amount==0,"NPC action commits the same operation exactly once, including the final stock unit");npc.Free();
            var bottle=world.ResolvePoint(WorldTestObjects.ShippedId(world,"水瓶"));var liquid=bottle.Owner.GetComponent<WorldLiquidComponent3D>();Place(player,bottle.UsePosition+Vector3.Up*.468f);await Frames(8);
            bottle.Operations.Add(new WorldConsumeLiquidInteraction{Id="pour_small",DisplayName="少量倒出",Amount=.1});await PressE();
            Check(liquid.Volume==.75&&player.GetNode<Control>("WorldInteractionHUD/Root/Panel/Content/Choices").Visible,"Multiple operations open a choice list without changing the object");await Capture("interaction-menu");await PressE();bottle.Operations.RemoveAt(1);
            Check(liquid.Volume==.5,"E executes the bottle's configured .25 liquid operation");
            ModuleSystem.GetModule<ISaveModule>().Save("interaction");
            Check(await world.SwitchAsync(town.Id)&&await world.SwitchAsync(workshop.Id),"Interacted objects survive level travel");
            Check(world.ResolveObject(WorldTestObjects.ShippedId(world,"仓储间储物箱")).Owner.GetComponent<WorldStockComponent3D>().Amount==0&&world.ResolveObject(WorldTestObjects.ShippedId(world,"水瓶")).Owner.GetComponent<WorldLiquidComponent3D>().Volume==.5,"Travel retains player and NPC mutations");
            Check(world.ResolveObject(otherId).Owner.GetComponent<WorldStockComponent3D>().Amount==50,"Independent reused storage state survives level travel");
            Check(!WorldInteraction.Execute(player,point,"take_stock").Success,"A stale component from an unloaded level cannot be used after travel");
            world.Close();Check(await world.OpenAsync(this,definition,true,"interaction"),"Interaction save must resume");await Frames(8);
            Check(world.ResolveObject(WorldTestObjects.ShippedId(world,"水瓶")).Owner.GetComponent<WorldLiquidComponent3D>().Volume==.5,"Continue restores interaction state");
            ConfigurationChecks();world.Close();_driving=false;ModuleSystem.Shutdown();GD.Print($"WORLD_INTERACTION_OK: {_checks} checks");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());_driving=false;ModuleSystem.Shutdown();GetTree().Quit(1);}
    }
    async Task Capture(string name)
    {
        if(!CaptureFrames)return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        Check(image.SavePng("res://.godot/world-validation/"+name+".png")==Error.Ok,"Visual evidence must save");
    }
    async Task<GameObject3D> NpcOperation(WorldModule world,WorldInteractionComponent3D point)
    {
        var fixture=new NpcSelectorBindingValidation.Fixture();fixture.Selector.RequireReachable=false;fixture.Selector.Source=NpcTargetSource.FixedObject;fixture.Selector.FixedObjectId=point.PointId;
        var flow=new FlowGraphAsset();var entry=new FlowEntryNodeData();var use=new FlowActionNodeData();use.Actions.Add(new NpcInteractAction{Input=new(){Name="target"},OperationId="take_stock"});var done=new FlowReturnNodeData{Label="Success"};
        flow.Nodes.Add(entry);flow.Nodes.Add(use);flow.Nodes.Add(done);flow.ConnectNodes(entry.Id,0,use.Id,0);flow.ConnectNodes(use.Id,0,done.Id,0);flow.SaveJsonFields();fixture.First.ExecutionGraph=flow;
        fixture.First.Results.Add(new(){Key="private.started",Value=new(){ValueType=CharacterValueType.Boolean,Boolean=true}});fixture.Second.Results.Add(new(){Key="private.done",Value=new(){ValueType=CharacterValueType.Boolean,Boolean=true}});
        var stock=point.Owner.GetComponent<WorldStockComponent3D>();stock.Amount=1;
        var host=new GameObject3D{Name="InteractionNpc",PersistentId="interaction_npc",Position=point.UsePosition+Vector3.Up*.468f,Components=new(){new CharacterDataComponent3D{Profile=fixture.Profile,Persist=false},new AiAgentComponent3D{Graph=fixture.Graph,Persist=false,Running=false}}};world.CurrentContext.Owner.AddChild(host);
        var agent=host.GetComponent<AiAgentComponent3D>();
        var context=new NpcActionContext(agent,agent.Program.Goals[0].Actions[0],new System.Collections.Generic.Dictionary<string,CharacterValue>{{"target",CharacterValue.Text(point.PointId)}});
        Check(context.QueueInteraction("target","take_stock")&&stock.Amount==1,"NPC graph requests do not mutate objects before the action commits");
        host.Position=point.UsePosition+Vector3.Right*5;Check(!WorldInteraction.Check(host,point,"take_stock").Success,"NPCs use the same region restriction as players");host.Position=point.UsePosition+Vector3.Up*.468f;
        agent.SetRunning(true);
        for(int i=0;i<150&&agent.CompletedActions<2;i++){agent.AdvancePlanning(100);await Frames(1);}
        Check(agent.CompletedActions==2&&agent.Read("private.done").IsTrue,"NPC execution graph must commit interaction and complete its plan: "+agent.LastFailure);
        return host;
    }
    void ConfigurationChecks()
    {
        var interaction=new WorldInteractionCapability{Operations=new(){new WorldTakeStockInteraction()}};
        Reject(()=>WorldCatalog.ValidateCapabilities(new WorldCapabilityDefinition[]{interaction}),"Operations require their declared business capabilities");
        interaction.Operations.Add(new WorldTakeStockInteraction());Reject(()=>interaction.Validate(),"Operation IDs must be unique");interaction.Operations.RemoveAt(1);
        interaction.Operations[0]=new WorldTakeStockInteraction{Amount=double.NaN};Reject(()=>interaction.Validate(),"Operation amounts must be finite and positive");
        var placement=new GameObject3D();Reject(()=>new WorldInteractionComponent3D{UsePositionPath=new("")}.ValidatePlacement(placement),"Interaction components require an explicit Area3D region");placement.Free();
        var badFixture=new NpcSelectorBindingValidation.Fixture();badFixture.First.Results.Add(new(){Key="input.target.stock",Value=new(){Number=0}});
        Reject(()=>NpcProgram.Compile(badFixture.Graph),"NPC results cannot bypass shared operations to write object stock");
        var type=WorldTestObjects.StockType();var local=new WorldInteractionCapability{Operations=new(){new WorldTakeStockInteraction()}};var record=new WorldObjectComponent3D{ObjectType=type,Overrides=new(){local}};
        WorldCatalog.ValidateOverrides(record);
#if TOOLS
        var copy=(WorldObjectComponent3D)record.Duplicate(true);((WorldTakeStockInteraction)copy.Definition<WorldInteractionCapability>().Operations[0]).Amount=7;
        Check(((WorldTakeStockInteraction)local.Operations[0]).Amount==1,"Copied records deep-copy nested operation configuration");
#endif
    }
}
