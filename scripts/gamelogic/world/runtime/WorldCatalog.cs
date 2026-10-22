using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace GameLogic;

/// <summary>One shared directory. Scene locators never load PackedScenes while indexing.</summary>
public sealed class WorldCatalog
{
    public WorldDefinition Definition {get;}
    public IReadOnlyDictionary<string,WorldLocationDefinition> Locations {get;}
    public IReadOnlyDictionary<string,WorldLevelDefinition> Levels {get;}
    public IReadOnlyDictionary<string,WorldCatalogEntry> Objects {get;}
    readonly Dictionary<string,WorldCatalogEntry[]> _byLevel=new(),_byPlace=new(),_byTag=new();
    readonly Dictionary<string,HashSet<string>> _tags=new();
    readonly Dictionary<string,HashSet<string>> _adapters=new();
    readonly Dictionary<(string,string,string,string),IReadOnlyList<WorldCatalogEntry>> _queries=new();
    public WorldCatalog(WorldDefinition definition,IEnumerable<WorldCatalogEntry> entries=null)
    {
        Definition=definition??throw new ArgumentNullException(nameof(definition));
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var record in definition.Locations.Cast<WorldRecord>().Concat(definition.Levels).Concat(definition.ObjectTypes))
            Require(record!=null&&!string.IsNullOrWhiteSpace(record.Id)&&ids.Add(record.Id),"世界记录身份为空或重复。");
        Locations=definition.Locations.ToDictionary(r=>r.Id,StringComparer.Ordinal);
        Levels=definition.Levels.ToDictionary(r=>r.Id,StringComparer.Ordinal);
        Objects=(entries??LoadEntries(definition)).ToDictionary(r=>r.Id,StringComparer.Ordinal);
        foreach(var location in Locations.Values)
        {
            var visited=new HashSet<string>();
            for(var next=location;next!=null;next=next.Parent)
                Require(Locations.ContainsKey(next.Id)&&visited.Add(next.Id),"地点父级丢失或存在循环："+location.DisplayName);
        }
        foreach(var level in Levels.Values)Require(!string.IsNullOrWhiteSpace(level.ScenePath)&&!string.IsNullOrWhiteSpace(level.DefaultEntry)
            &&(level.Location==null||Locations.ContainsKey(level.Location.Id)),"关卡缺少场景、入口或地点："+level.DisplayName);
        Require(Levels.Values.Select(l=>l.ScenePath).Distinct(StringComparer.Ordinal).Count()==Levels.Count,"一个场景只能对应一个制作关卡。");
        foreach(var type in definition.ObjectTypes)ValidateCapabilities(type.Capabilities);
        foreach(var item in Objects.Values)
        {
            Require(!string.IsNullOrWhiteSpace(item.Id)&&ids.Add(item.Id),"世界对象身份为空或重复："+item.DisplayName);
            Require(item.Type!=null&&definition.ObjectTypes.Any(t=>t.Id==item.Type.Id)&&item.Level!=null&&Levels.ContainsKey(item.Level.Id)
                &&(item.Location==null||Locations.ContainsKey(item.Location.Id)),"世界对象配置无效："+item.DisplayName);
            ValidateCapabilities(item.EffectiveCapabilities);
        }
        Index(_byLevel,Objects.Values.GroupBy(o=>o.Level.Id));
        Index(_byPlace,Objects.Values.GroupBy(o=>o.Location?.Id??o.Level.Location?.Id??""));
        foreach(var record in Objects.Values)
        {
            _tags[record.Id]=record.AllTags.ToHashSet(StringComparer.Ordinal);
            _adapters[record.Id]=record.EffectiveCapabilities.Select(c=>c.Adapter).Where(a=>a.Length>0).ToHashSet(StringComparer.Ordinal);
        }
        foreach(var group in Objects.Values.SelectMany(o=>_tags[o.Id].Select(t=>(Tag:t,Object:o))).GroupBy(x=>x.Tag))_byTag[group.Key]=group.Select(x=>x.Object).ToArray();
        Require(definition.InitialLevel==null||Levels.ContainsKey(definition.InitialLevel.Id),"初始关卡未登记。");
    }
    static void Index(Dictionary<string,WorldCatalogEntry[]> target,IEnumerable<IGrouping<string,WorldCatalogEntry>> groups)
    {foreach(var group in groups)target[group.Key]=group.ToArray();}
    public bool InPlace(string place,string ancestor)
    {
        while(!string.IsNullOrEmpty(place)&&Locations.TryGetValue(place,out var location))
        {if(place==ancestor)return true;place=location.Parent?.Id;}
        return false;
    }
    public IReadOnlyList<WorldCatalogEntry> Query(string level="",string place="",IReadOnlyList<string> allTags=null,string adapter="point")
    {
        var key=(level,place,adapter,allTags==null?"":string.Concat(allTags.Distinct().OrderBy(t=>t,StringComparer.Ordinal).Select(t=>t.Length+":"+t)));
        if(_queries.TryGetValue(key,out var cached))return cached;
        IEnumerable<WorldCatalogEntry> source=Objects.Values;
        if(level.Length>0)source=_byLevel.GetValueOrDefault(level,Array.Empty<WorldCatalogEntry>());
        else if(place.Length>0)source=_byPlace.Where(p=>InPlace(p.Key,place)).SelectMany(p=>p.Value);
        if(allTags?.Count>0)
        {
            var tag=allTags.OrderBy(t=>_byTag.GetValueOrDefault(t,Array.Empty<WorldCatalogEntry>()).Length).First();
            var tagged=_byTag.GetValueOrDefault(tag,Array.Empty<WorldCatalogEntry>());
            if(level.Length==0&&place.Length==0)source=tagged;
        }
        var result=Array.AsReadOnly(source.Where(o=>Matches(o,level,place,allTags,adapter)).ToArray());
        if(_queries.Count>=256)_queries.Clear();_queries[key]=result;return result;
    }
    public bool Matches(WorldCatalogEntry record,string level,string place,IReadOnlyList<string> allTags,string adapter)=>_adapters[record.Id].Contains(adapter)
        &&(level.Length==0||record.Level.Id==level)&&(place.Length==0||InPlace(record.Location?.Id??record.Level.Location?.Id,place))
        &&(allTags==null||allTags.All(_tags[record.Id].Contains));
    public static void Require(bool condition,string message){if(!condition)throw new ArgumentException(message);}
    public static void ValidateCapabilities(IEnumerable<WorldCapabilityDefinition> capabilities,bool checkDependencies=true)
    {
        var definitions=capabilities.ToArray();var keys=new HashSet<string>();foreach(var capability in definitions)
        {
            Require(capability!=null&&!string.IsNullOrWhiteSpace(capability.Key)&&keys.Add(capability.Key),"对象能力为空或重复。");
            Require(typeof(Component3D).IsAssignableFrom(capability.ComponentType)&&typeof(IWorldCapabilityProvider).IsAssignableFrom(capability.ComponentType),"能力组件类型无效。");capability.Validate();
        }
        if(checkDependencies)foreach(var interaction in definitions.OfType<WorldInteractionCapability>())foreach(var operation in interaction.Operations)
        {
            foreach(var key in operation.RequiredCapabilities)Require(keys.Contains(key),"交互“"+operation.DisplayName+"”缺少能力："+key);
            Require(!operation.RequireCapacity||keys.Contains("usage_capacity"),"交互“"+operation.DisplayName+"”需要使用容量能力。");
        }
    }
    static IEnumerable<WorldCatalogEntry> LoadEntries(WorldDefinition definition)
    {
        foreach(var level in definition.Levels)
        {
            Require(ResourceLoader.Exists(level.CatalogPath),"关卡目录未生成，请在编辑器保存关卡："+level.DisplayName);
            var catalog=ResourceLoader.Load<WorldLevelCatalog>(level.CatalogPath);
            Require(catalog!=null&&catalog.Version==1&&catalog.LevelId==level.Id,"关卡目录无效："+level.CatalogPath);
            foreach(var data in catalog.Entries)yield return WorldCatalogEntry.Decode(data,level);
        }
    }
    public static void ValidateOverrides(WorldObjectComponent3D binding)
    {
        Require(binding?.ObjectType!=null,"世界对象缺少类型。");ValidateCapabilities(binding.ObjectType.Capabilities);ValidateCapabilities(binding.Overrides,false);
        foreach(var value in binding.Overrides)Require(binding.ObjectType.Capabilities.Any(c=>c.Key==value.Key&&c.GetType()==value.GetType()),"不能覆盖类型未声明的能力："+value.Key);
        ValidateCapabilities(binding.Definitions);
    }
}
