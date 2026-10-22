using System;
using System.Collections.Generic;
using System.Linq;
using Framework;
using Godot;
namespace GameLogic;

/// <summary>Scene-authored configuration; persistent state is keyed by the automatic placement identity.</summary>
[Tool, GlobalClass]
public partial class WorldObjectComponent3D : Component3D
{
    public override int Priority=>ComponentPriority.WorldObject;
    WorldObjectTypeDefinition _type;
    [Export] public WorldObjectTypeDefinition ObjectType {get=>_type;set{_type=value;NotifyPropertyListChanged();}}
    [Export] public string DisplayName {get;set;}="";
    [Export] public string[] ExtraTags {get;set;}=Array.Empty<string>();
    [Export] public string OwnerId {get;set;}="";
    [Export] public WorldLocationDefinition Location {get;set;}
    [ExportGroup("能力覆盖")]
    [Export] public Godot.Collections.Array<WorldCapabilityDefinition> Overrides {get;set;}=new();
    public WorldLocationDefinition EffectiveLocation=>Location??Session.LevelFor(Owner)?.Location;
    public string ObjectId=>Owner?.PersistentId??"";
    public string[] Tags=>_tags??(ObjectType?.Tags??Array.Empty<string>()).Concat(ExtraTags).Distinct().ToArray();
    WorldCapabilityDefinition[] _definitions;
    string[] _tags;
    readonly Dictionary<Type,WorldCapabilityDefinition> _byType=new();
    public IEnumerable<WorldCapabilityDefinition> Definitions=>_definitions??(ObjectType?.Capabilities.Select(c=>Overrides.FirstOrDefault(o=>o.Key==c.Key)??c)??Enumerable.Empty<WorldCapabilityDefinition>());
    public T Definition<T>() where T:WorldCapabilityDefinition=>_definitions==null?Definitions.OfType<T>().SingleOrDefault():_byType.GetValueOrDefault(typeof(T)) as T;
    public WorldObjectState State {get;private set;}
    public bool IsAvailable=>IsActive&&State?.Destroyed==false&&(Owner.GetComponent<WorldOpeningHoursComponent3D>()?.AllowsUse??true);
    public bool CanUse(string actor)=>IsAvailable&&(string.IsNullOrEmpty(OwnerId)||OwnerId==actor);
    public long Revision=>State?.Revision??0;
    WorldModule Session=>(WorldModule)ModuleSystem.GetModule<IWorldModule>();
    public override void OnInit()
    {
        WorldCatalog.Require(ObjectType!=null,"世界对象需要 ObjectType。");WorldCatalog.ValidateOverrides(this);
        _definitions=Definitions.ToArray();_tags=Tags;_byType.Clear();foreach(var definition in _definitions)_byType.Add(definition.GetType(),definition);
        ValidateComponents(Owner.GetAllComponents());State=Session.ObjectState(ObjectId,Definitions);Session.RegisterObject(this);
    }
    public WorldCatalogEntry Snapshot(string id,WorldLevelDefinition level,string fallbackName)=>new(id,
        string.IsNullOrWhiteSpace(DisplayName)?fallbackName:DisplayName,ObjectType,level,Location??level.Location,
        ExtraTags.ToArray(),OwnerId,Array.AsReadOnly(Definitions.ToArray()));
    public void ValidateComponents(IEnumerable<Component3D> components,GameObject3D host=null)
    {
        var providers=components.OfType<IWorldCapabilityProvider>().ToArray();var definitions=Definitions.ToArray();
        WorldCatalog.Require(providers.Select(p=>p.CapabilityKey).Distinct().Count()==providers.Length,"对象有重复能力组件。");
        foreach(var definition in definitions)WorldCatalog.Require(providers.Count(p=>p.CapabilityKey==definition.Key&&definition.ComponentType.IsInstanceOfType(p))==1,"对象缺少能力组件："+definition.Key+" · "+DisplayName);
        foreach(var provider in providers)WorldCatalog.Require(definitions.Any(d=>d.Key==provider.CapabilityKey&&d.ComponentType.IsInstanceOfType(provider)),"组件能力没有在对象类型中声明："+provider.CapabilityKey);
        host??=Owner;if(host!=null)foreach(var point in components.OfType<WorldInteractionComponent3D>())point.ValidatePlacement(host);
    }
    internal void BindState(WorldObjectState state)=>State=state;
    public override void OnDestroy(){Session.UnregisterObject(this);State=null;}
#if TOOLS
    GameObject3D _editorHost;
    WorldObjectTypeDefinition _editorType;
    public override void OnEditorUpdate(GameObject3D host)
    {
        _editorHost=host;
        if(_editorType==ObjectType)return;_editorType=ObjectType;
        if(ObjectType!=null)Callable.From(()=>
        {
            if(!GodotObject.IsInstanceValid(host))return;
            try{var before=host.Components;WorldObjectAuthoring.SynchronizeHost(this,host);if(!ReferenceEquals(before,host.Components))EditorInterface.Singleton.MarkSceneAsUnsaved();}
            catch(Exception error){GD.PushError(error.Message);}
        }).CallDeferred();
    }
    public override void OnEditorExit(GameObject3D host){if(_editorHost==host)_editorHost=null;}
    [Export(PropertyHint.Enum)] public string OverrideCapability {get;set;}="";
    [ExportToolButton("添加所选能力覆盖")] public Callable AddOverride=>Callable.From(()=>
    {
        var source=ObjectType?.Capabilities.FirstOrDefault(c=>c.Key==OverrideCapability);
        if(source==null||Overrides.Any(c=>c.Key==source.Key))return;
        Overrides=new(Overrides){(WorldCapabilityDefinition)source.Duplicate(true)};EmitChanged();NotifyPropertyListChanged();
        if(GodotObject.IsInstanceValid(_editorHost))EditorInterface.Singleton.MarkSceneAsUnsaved();
    });
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if(property["name"].AsString()==nameof(OverrideCapability))
            property["hint_string"]=string.Join(",",ObjectType?.Capabilities.Where(c=>c!=null&&!Overrides.Any(o=>o.Key==c.Key)).Select(c=>c.Key)??Enumerable.Empty<string>());
    }
#endif
}
