using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Framework;
using Godot;
using GameLogic.AI;
namespace GameLogic;

/// <summary>Session lifetime and authoritative object state, independent of loaded scenes.</summary>
public sealed partial class WorldModule : Module,IWorldModule,ISaveSection,IProcessModule
{
    public WorldCatalog Catalog {get;private set;}
    public WorldRoutes Routes {get;private set;}
    public WorldPopulation Population {get;private set;}
    public bool IsOpen=>_sessionRoot!=null&&GodotObject.IsInstanceValid(_sessionRoot);
    public bool IsSwitching=>_transition!=null;
    public string CurrentLevelId {get;private set;}="";
    public string Status {get;private set;}="未进入世界";
    public string LastError {get;private set;}="";
    public float Progress=>_loading?.Progress??(IsSwitching?0:1);
    public GameObject3D Player {get;private set;}
    public WorldLevelContextComponent3D CurrentContext {get;private set;}
    public double RuntimeSeconds {get;private set;}
    public int PathQueriesLastFrame {get;private set;}
    public int PathQueriesPerFrame {get;set;}=8;
    public int SightQueriesLastFrame {get;private set;}
    public int SightQueriesPerFrame {get;set;}=32;
    public string SectionKey=>"world";
    public string EntryKey=>"session";
    public int SchemaVersion=>3;
    public override int Priority=>30;
    readonly Dictionary<string,WorldObjectState> _objects=new(StringComparer.Ordinal);
    readonly Dictionary<ulong,(WorldLevelDefinition Definition,WorldLevelContextComponent3D Context)> _levels=new();
    readonly Dictionary<string,WorldInteractionComponent3D> _points=new(StringComparer.Ordinal);
    readonly Dictionary<string,WorldObjectComponent3D> _instances=new(StringComparer.Ordinal);
    readonly Dictionary<string,WorldCapabilityDefinition[]> _localDefinitions=new(StringComparer.Ordinal);
    readonly Dictionary<ulong,AiWorldComponent3D> _environments=new();
    ISaveModule _save;
    WorldDefinition _definition;
    string _resumeLevel="";
    Transform3D? _resumeTransform;
    bool _restoreRequested;
    public override void OnInit()
    {
        _save=ModuleSystem.GetModule<ISaveModule>();
        // Session-owned scene cleanup must run before these dependencies shut down.
        ModuleSystem.GetModule<ITimeOfDayModule>();ModuleSystem.GetModule<IResourceModule>();ModuleSystem.GetModule<ICameraModule>();ModuleSystem.GetModule<IInputModule>();
        if(ResourceLoader.Exists(WorldDefinition.CommonPath))Configure(ResourceLoader.Load<WorldDefinition>(WorldDefinition.CommonPath));
        _save.RegisterSection(this);
    }
    public void Configure(WorldDefinition definition){Catalog=new(definition);_definition=definition;Routes=new(Catalog);Population=new(this);Population.Configure();}
    public void Process(double elapsed,double realElapsed)
    {
        RuntimeSeconds+=elapsed;PathQueriesLastFrame=0;SightQueriesLastFrame=0;
        if(IsOpen&&!IsSwitching)Population?.Process();
        if(_restoreRequested&&!IsSwitching&&IsOpen)
        { _restoreRequested=false;_ = SwitchInternalAsync(_resumeLevel,"",true,default); }
    }
    public bool TrySpendPathQuery(){if(PathQueriesLastFrame>=PathQueriesPerFrame)return false;PathQueriesLastFrame++;return true;}
    public bool TrySpendSightQuery(){if(SightQueriesLastFrame>=SightQueriesPerFrame)return false;SightQueriesLastFrame++;return true;}
    public WorldObjectState ObjectState(string id,IEnumerable<WorldCapabilityDefinition> definitions=null)
    {
        if(_objects.TryGetValue(id,out var existing)&&existing.NeedsDefinitions)
        {
            var available=DefinitionsFor(id)??definitions?.ToArray();
            if(available!=null)
            {
                _objects[id]=WorldObjectState.Read(existing.Capture(),available);
                if(Catalog?.Objects.ContainsKey(id)!=true)_localDefinitions[id]=available;
            }
        }
        if(!_objects.TryGetValue(id,out var state))
        {
            var configs=DefinitionsFor(id)??definitions?.ToArray();
            WorldCatalog.Require(configs!=null,"对象没有有效记录或本地类型："+id);
            _objects[id]=state=new(configs);
            if(Catalog?.Objects.ContainsKey(id)!=true)_localDefinitions[id]=configs;
        }
        return state;
    }
    internal WorldCapabilityDefinition[] DefinitionsFor(string id)=>Catalog?.Objects.TryGetValue(id,out var record)==true?record.EffectiveCapabilities.ToArray():_localDefinitions.GetValueOrDefault(id);
    public WorldObjectState ExistingState(string id)=>_objects.GetValueOrDefault(id);
    public string ResolveStatus(string id)=>Catalog?.Objects.ContainsKey(id)!=true&&!_instances.ContainsKey(id)?"记录缺失"
        :IsDestroyed(id)?"已销毁":_instances.ContainsKey(id)?"已加载":"未加载";
    public WorldObjectComponent3D ResolveObject(string id)=>_instances.GetValueOrDefault(id??"");
    public WorldInteractionComponent3D ResolvePoint(string id)=>_points.GetValueOrDefault(id??"");
    public bool IsDestroyed(string id)=>_objects.TryGetValue(id,out var state)&&state.Destroyed;
    internal void RegisterPoint(WorldInteractionComponent3D point)
    {
        if(_points.TryGetValue(point.PointId,out var old)&&old!=point)throw new InvalidOperationException("重复对象身份："+point.PointId);
        WorldCatalog.Require(_instances.GetValueOrDefault(point.PointId)==point.Object,"交互入口没有绑定到有效世界对象。");
        _points[point.PointId]=point;
    }
    internal void UnregisterPoint(WorldInteractionComponent3D point){if(_points.GetValueOrDefault(point.PointId)==point)_points.Remove(point.PointId);}
    internal void RegisterObject(WorldObjectComponent3D instance)
    {
        WorldCatalog.Require(!_instances.TryGetValue(instance.ObjectId,out var old)||old==instance,"重复世界对象身份："+instance.ObjectId);
        if(Catalog.Objects.TryGetValue(instance.ObjectId,out var entry))WorldCatalog.Require(LevelFor(instance.Owner)?.Id==entry.Level.Id,"世界对象摆放在错误关卡："+instance.DisplayName);
        _instances[instance.ObjectId]=instance;
    }
    internal void UnregisterObject(WorldObjectComponent3D instance){if(_instances.GetValueOrDefault(instance.ObjectId)==instance)_instances.Remove(instance.ObjectId);}
    internal void RegisterEnvironment(AiWorldComponent3D world)=>_environments.Add(world.Owner.GetInstanceId(),world);
    internal void UnregisterEnvironment(AiWorldComponent3D world)=>_environments.Remove(world.Owner.GetInstanceId());
    public AiWorldComponent3D EnvironmentFor(Node node)
    {
        if(Population?.OwnsEnvironment(node)==true)return Population.Environment;
        var level=LevelFor(node);
        return _environments.Values.FirstOrDefault(e=>level!=null?LevelFor(e.Owner)?.Id==level.Id:e.Owner.GetTree()==node.GetTree());
    }
    public WorldLevelContextComponent3D ContextFor(Node node)
    {
        string level=LevelFor(node)?.Id;
        return level==null?null:_levels.Values.FirstOrDefault(c=>c.Definition.Id==level).Context;
    }
    public WorldLevelDefinition LevelFor(Node node)
    {
        if(node==null)return null;
        if(Population?.For(node) is {} actor)return Catalog.Levels.GetValueOrDefault(actor.Level);
        if(Player!=null&&(node==Player||Player.IsAncestorOf(node)))return CurrentContext?.Level;
        for(var n=node;n!=null;n=n.GetParent())
            if(_levels.TryGetValue(n.GetInstanceId(),out var entry))return entry.Definition;
            else if(n is GameObject3D host&&host.Components.OfType<WorldLevelContextComponent3D>().FirstOrDefault() is {} context)
                return Catalog?.Levels.Values.FirstOrDefault(l=>l.ScenePath==host.SceneFilePath)??context.Level;
        return null;
    }
    internal void RegisterContext(WorldLevelContextComponent3D context)
    {
        var definition=_levels.GetValueOrDefault(context.Owner.GetInstanceId()).Definition
            ??Catalog?.Levels.Values.FirstOrDefault(l=>l.ScenePath==context.Owner.SceneFilePath)??context.Level;
        WorldCatalog.Require(definition!=null&&Catalog?.Levels.ContainsKey(definition.Id)==true,"关卡上下文未配置或未登记。");
        context.Bind(definition);_levels[context.Owner.GetInstanceId()]=(definition,context);
        if(_activeLevel==context.Owner||_activeLevel==null){CurrentContext=context;}
        if(!IsOpen)Callable.From(()=>AdoptStandalone(context)).CallDeferred();
    }
    internal void UnregisterContext(WorldLevelContextComponent3D context)
    {if(_levels.GetValueOrDefault(context.Owner.GetInstanceId()).Context==context)_levels.Remove(context.Owner.GetInstanceId());}
    public JsonObject Capture()
    {
        var objects=new JsonObject();foreach(var entry in _objects)objects[entry.Key]=entry.Value.Capture();
        return new(){["world_id"]=_definition?.Id??"",["level"]=CurrentLevelId,["player_transform"]=Player!=null?WriteTransform(Player.GlobalTransform):null,["objects"]=objects,
            ["population"]=Population?.Capture(),["closed_routes"]=new JsonArray((Routes?.Closed??Array.Empty<string>()).Select(id=>(JsonNode)JsonValue.Create(id)).ToArray())};
    }
    public void Restore(JsonObject state,int version)
    {
        WorldCatalog.Require(version==SchemaVersion,"世界存档版本不受支持。");
        WorldCatalog.Require(state["world_id"]?.GetValue<string>()==_definition?.Id,"存档属于另一世界。");
        var restored=new Dictionary<string,WorldObjectState>(StringComparer.Ordinal);
        if(state["objects"] is JsonObject objects)foreach(var entry in objects)restored.Add(entry.Key,WorldObjectState.Read(entry.Value.AsObject(),DefinitionsFor(entry.Key)));
        var level=state["level"]?.GetValue<string>()??"";
        WorldCatalog.Require(level.Length==0||Catalog.Levels.ContainsKey(level),"存档关卡已缺失："+level);
        Transform3D? playerTransform=state["player_transform"] is JsonArray transform?ReadTransform(transform):null;
        foreach(var world in _environments.Values)foreach(var actor in world.Actors.Values)actor.CancelPlan("WorldRestored");
        _objects.Clear();foreach(var entry in restored)_objects.Add(entry.Key,entry.Value);
        foreach(var instance in _instances.Values)instance.BindState(ObjectState(instance.ObjectId,instance.Definitions));
        Routes?.Restore((state["closed_routes"] as JsonArray)?.Select(n=>n.GetValue<string>()));Population?.Restore(state["population"] as JsonObject);
        _resumeLevel=level;_resumeTransform=playerTransform;
        _restoreRequested=IsOpen&&level.Length>0;
    }
    static JsonArray WriteTransform(Transform3D t)=>new(t.Basis.X.X,t.Basis.X.Y,t.Basis.X.Z,t.Basis.Y.X,t.Basis.Y.Y,t.Basis.Y.Z,t.Basis.Z.X,t.Basis.Z.Y,t.Basis.Z.Z,t.Origin.X,t.Origin.Y,t.Origin.Z);
    static Transform3D ReadTransform(JsonArray values)
    {
        WorldCatalog.Require(values.Count==12,"玩家变换存档无效。");var v=values.Select(n=>n.GetValue<float>()).ToArray();
        WorldCatalog.Require(v.All(float.IsFinite),"玩家变换包含非有限数值。");
        return new(new Basis(new(v[0],v[1],v[2]),new(v[3],v[4],v[5]),new(v[6],v[7],v[8])),new(v[9],v[10],v[11]));
    }
    public override void Shutdown(){Close();_save?.UnregisterSection(this);_levels.Clear();_points.Clear();_instances.Clear();_localDefinitions.Clear();_environments.Clear();}
}
