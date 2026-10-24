using Framework;
using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldLevelContextComponent3D : Component3D
{
    public override int Priority=>1000;
    [Export] public WorldLevelDefinition Level {get;set;}
    [Export] public NodePath EnvironmentPath {get;set;}=new("EnvironmentRig");
    [Export] public bool RequireNavigation {get;set;}
    [Export] public NodePath NavigationRegionPath {get;set;}=new("World/NavigationRegion3D");
    public WeatherComponent3D Weather=>(Owner.GetNodeOrNull(EnvironmentPath) as GameObject3D)?.GetComponent<WeatherComponent3D>();
    internal void Bind(WorldLevelDefinition level)=>Level=level;
    public override void OnInit()=>((WorldModule)ModuleSystem.GetModule<IWorldModule>()).RegisterContext(this);
    public override void OnUpdate(double delta)
    {
        if(RootModule.Instance==null&&ModuleSystem.GetModule<IWorldModule>().CurrentContext==this)ModuleSystem.Process(delta,delta);
    }
    public override void OnDestroy()=>((WorldModule)ModuleSystem.GetModule<IWorldModule>()).UnregisterContext(this);
#if TOOLS
    [ExportToolButton("导出关卡配置")]
    public Callable ExportConfiguration=>Callable.From(()=>WorldLevelExporter.ExportCurrent(this));
#endif
}
