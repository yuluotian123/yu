using System;
using System.Linq;
using System.Threading.Tasks;
using Framework;
using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldTransitionComponent3D : Component3D
{
    public override int Priority=>ComponentPriority.Interaction;
    WorldLevelDefinition _targetLevel;
    [Export] public WorldLevelDefinition TargetLevel
    {
        get=>_targetLevel;
        set{if(_targetLevel==value)return;_targetLevel=value;NotifyPropertyListChanged();}
    }
    [Export] public string TargetEntry {get;set;}="default";
    [Export] public NodePath AreaPath {get;set;}=new("Area3D");
    [Export] public bool Automatic {get;set;}=true;
    [Export(PropertyHint.Range,"0,24,0.01,or_greater")] public double NpcTravelHours {get;set;}
    Area3D _area;
    public override void OnInit()
    {
        _area=Owner.GetNodeOrNull<Area3D>(AreaPath);
        WorldCatalog.Require(TargetLevel!=null&&!string.IsNullOrWhiteSpace(TargetEntry),"关卡入口缺少目标关卡或目标入口。");
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
#if TOOLS
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if(!Engine.IsEditorHint()||property["name"].AsString()!=nameof(TargetEntry)||TargetLevel==null
            ||string.IsNullOrWhiteSpace(TargetLevel.ScenePath)||!ResourceLoader.Exists(TargetLevel.CatalogPath))return;
        // Use the exported entrance index, without instantiating the destination scene in the inspector.
        var catalog=ResourceLoader.Load<WorldLevelCatalog>(TargetLevel.CatalogPath);
        if(catalog==null||catalog.LevelId!=TargetLevel.Id||catalog.Entrances.Count==0)return;
        property["hint"]=(long)PropertyHint.EnumSuggestion;
        property["hint_string"]=string.Join(",",catalog.Entrances.Keys.OrderBy(key=>key,StringComparer.Ordinal));
    }
#endif
}
