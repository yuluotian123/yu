using System.Threading.Tasks;
using Framework;
using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldTransitionComponent3D : Component3D
{
    public override int Priority=>ComponentPriority.Interaction;
    [Export] public WorldLevelDefinition TargetLevel {get;set;}
    [Export] public string TargetEntry {get;set;}="default";
    [Export] public NodePath AreaPath {get;set;}=new("Area3D");
    [Export] public bool Automatic {get;set;}=true;
    Area3D _area;
    public override void OnInit()
    {
        _area=Owner.GetNodeOrNull<Area3D>(AreaPath);
        WorldCatalog.Require(TargetLevel!=null&&TargetEntry.Length>0,"关卡入口缺少目标。");
        WorldCatalog.Require(!Automatic||_area!=null,"自动切换入口缺少 Area3D："+AreaPath);
        if(_area!=null&&Automatic)_area.BodyEntered+=Entered;
    }
    async void Entered(Node3D body)
    {
        var world=ModuleSystem.GetModule<IWorldModule>();
        if(world.IsSwitching||world.Player==null||!world.Player.IsAncestorOf(body)||((WorldModule)world).ArrivalBlocked)return;
        if(!await Activate())GD.PushWarning("关卡入口："+world.LastError);
    }
    public Task<bool> Activate()=>ModuleSystem.GetModule<IWorldModule>().SwitchAsync(TargetLevel.Id,TargetEntry);
    public override void OnDestroy(){if(GodotObject.IsInstanceValid(_area)&&Automatic)_area.BodyEntered-=Entered;_area=null;}
}
