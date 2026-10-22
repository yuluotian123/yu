using System;
using System.Linq;
using System.Threading.Tasks;
using Framework;
using GameLogic;
using Godot;

/// <summary>Exercise shipped scenes with the real player and GM; outputs stay in the import cache.</summary>
public partial class WorldProductionValidation : Node
{
    bool _driving=true;
    double _watchdog;
    public override void _Process(double delta)
    {
        if(!_driving)return;
        var world=ModuleSystem.GetModule<IWorldModule>();if(world.CurrentContext==null)ModuleSystem.Process(delta,delta);
        _watchdog+=delta;
        if(_watchdog>10){_watchdog=0;GD.Print($"WORLD_WAIT: {world.Status}, context={world.CurrentContext?.Owner?.Name}, in_tree={world.CurrentContext?.Owner?.IsInsideTree()}");}
    }
    public override async void _Ready()
    {
        try
        {
            ModuleSystem.RegisterModule<ISaveModule>(new SaveModule("res://.godot/world-validation/production-saves","res://.godot/world-validation/no-legacy"));
            var world=(WorldModule)ModuleSystem.GetModule<IWorldModule>();
            var definition=ResourceLoader.Load<WorldDefinition>(WorldDefinition.CommonPath);
            Require(await world.OpenAsync(this,definition,false),world.LastError);
            Require(world.CurrentLevelId=="town_level"&&world.Player?.PersistentId=="player","Default world must open the town with one player");
            await Frames(3);ulong player=world.Player.GetInstanceId();
            // Arrival protection uses wall time, independently of simulated game speed.
            while(world.ArrivalBlocked)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            world.Player.GlobalPosition=world.CurrentContext.Owner.GetNode<Node3D>("WorkshopPortal").GlobalPosition;
            world.Player.GetComponent<CharacterMovementComponent3D>().SyncBodyToOwner();
            for(int i=0;i<300&&(world.CurrentLevelId!="workshop_level"||world.IsSwitching);i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            Require(world.CurrentLevelId=="workshop_level"&&!world.IsSwitching,"Automatic town portal must switch the real physics player: "+world.LastError+
                $" level={world.CurrentLevelId}, switching={world.IsSwitching}, blocked={world.ArrivalBlocked}, position={world.Player.GlobalPosition}, portal={world.CurrentContext.Owner.GetNodeOrNull<GameObject3D>("WorkshopPortal")?.InitializationError}");
            Require(world.Player.GetInstanceId()==player,"Actual player identity must survive the transition");
            var point=world.ResolvePoint(WorldTestObjects.ShippedId(world,"仓储间储物箱"));Require(point!=null,"Workshop object must register");
            Require(point.Owner.GetComponent<WorldStockComponent3D>().Amount==50,"Shipped stock override must take precedence over the type default");
            var bottle=world.ResolveObject(WorldTestObjects.ShippedId(world,"水瓶"));Require(bottle!=null&&world.ResolvePoint(WorldTestObjects.ShippedId(world,"水瓶"))!=null,"Interactive bottle must register its configured operations");
            Require(bottle.Owner.GetComponent<WorldLiquidComponent3D>().TryConsume(.25),"Shipped bottle should provide liquid contents");
            point.Owner.GetComponent<WorldOpeningHoursComponent3D>().SetOpen(false);await Frames(3);
            ModuleSystem.GetModule<ISaveModule>().Save("production");
            Require(await world.CurrentContext.Owner.GetNode<GameObject3D>("TownPortal").GetComponent<WorldTransitionComponent3D>().Activate(),world.LastError);
            Require(await world.SwitchAsync("workshop_level"),world.LastError);
            Require(world.ResolveObject(WorldTestObjects.ShippedId(world,"仓储间储物箱")).Owner.GetComponent<WorldOpeningHoursComponent3D>().ManualOpen==false,"Shipped record state must survive return travel");
            Require(world.ResolveObject(WorldTestObjects.ShippedId(world,"水瓶")).Owner.GetComponent<WorldLiquidComponent3D>().Volume==.5,"Liquid state must survive unload and reload");
            world.Close();Require(await world.OpenAsync(this,definition,true,"production"),world.LastError);
            Require(world.CurrentLevelId=="workshop_level"&&world.ResolveObject(WorldTestObjects.ShippedId(world,"仓储间储物箱")).Owner.GetComponent<WorldOpeningHoursComponent3D>().ManualOpen==false,"Shipped save must resume the workshop");
            Require(world.ResolveObject(WorldTestObjects.ShippedId(world,"水瓶")).Owner.GetComponent<WorldLiquidComponent3D>().Volume==.5,"Continue must restore the bottle's own capability state");
            Require(WorldModule.Hosts(world.CurrentContext.Owner).All(h=>h.InitializationError.Length==0),"No shipped host initialization errors");
            GD.Print($"WORLD_PRODUCTION_OK: town -> workshop -> town -> workshop -> saved workshop; last_transition_ms={world.LastTransitionMilliseconds:0.0}");
            world.Close();await MenuFlow();_driving=false;ModuleSystem.Shutdown();GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());_driving=false;ModuleSystem.Shutdown();GetTree().Quit(1);}
    }
    async Task Until(Func<bool> condition,string message)
    {for(int n=0;n<600;n++){if(condition())return;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}throw new InvalidOperationException(message);}
    async Task MenuFlow()
    {
        var main=ResourceLoader.Load<PackedScene>("res://assets/scenes/main.tscn").Instantiate<RootModule>();_driving=false;GetTree().Root.AddChild(main);
        Button Find(string name)=>GetTree().Root.FindChildren(name,"Button",true,false).OfType<Button>().FirstOrDefault();
        await Until(()=>Find("BtnNew")!=null,"Main menu did not open");
        Find("BtnNew").EmitSignal(Button.SignalName.Pressed);
        await Until(()=>ModuleSystem.GetModule<IProcedureModule>().CurrentProcedure is LevelProcedure,"New game did not enter the level procedure");
        var world=ModuleSystem.GetModule<IWorldModule>();Require(world.CurrentLevelId=="town_level","New menu action must start at initial level");
        Require(await world.SwitchAsync("workshop_level"),world.LastError);ModuleSystem.GetModule<ISaveModule>().Save();
        Input.ParseInputEvent(new InputEventAction{Action="ui_cancel",Pressed=true});await Frames(2);Input.ParseInputEvent(new InputEventAction{Action="ui_cancel",Pressed=false});
        await Until(()=>ModuleSystem.GetModule<IProcedureModule>().CurrentProcedure is MainMenuProcedure&&Find("BtnStart")!=null,"Escape did not return to the menu");
        Require(!world.IsOpen&&!Find("BtnStart").Disabled,"Returning to menu must close world and enable continue");
        Find("BtnStart").EmitSignal(Button.SignalName.Pressed);
        await Until(()=>ModuleSystem.GetModule<IProcedureModule>().CurrentProcedure is LevelProcedure,"Continue did not enter the level procedure");
        Require(world.CurrentLevelId=="workshop_level","Continue menu action must restore saved workshop");
        GD.Print("WORLD_GAME_FLOW_OK: new game -> level -> save -> Escape -> menu -> continue saved level");world.Close();main.Free();
    }
    async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
