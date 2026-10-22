using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
namespace GameLogic;

/// <summary>Only declared capability states exist. Defaults apply once per object identity.</summary>
public sealed class WorldObjectState
{
    readonly Dictionary<string,WorldCapabilityState> _capabilities=new(StringComparer.Ordinal);
    JsonObject _unresolved;
    internal bool NeedsDefinitions=>_unresolved!=null;
    bool _destroyed;
    public bool Destroyed {get=>_destroyed;set{if(_destroyed!=value){_destroyed=value;Touch();}}}
    public long Revision {get;private set;}
    public WorldObjectState(IEnumerable<WorldCapabilityDefinition> definitions)
    {foreach(var definition in definitions){var state=definition.CreateState(Touch);WorldCatalog.Require(state!=null,"能力状态工厂返回空值："+definition.Key);_capabilities.Add(definition.Key,state);}}
    public T Capability<T>(string key) where T:WorldCapabilityState=>_capabilities.GetValueOrDefault(key) as T;
    public void Touch()=>Revision++;
    public JsonObject Capture()
    {
        if(_unresolved!=null){var raw=(JsonObject)_unresolved.DeepClone();raw["destroyed"]=Destroyed;return raw;}
        var states=new JsonObject();foreach(var pair in _capabilities)states[pair.Key]=new JsonObject{["version"]=pair.Value.Version,["data"]=pair.Value.Capture()};
        return new(){["destroyed"]=Destroyed,["capabilities"]=states};
    }
    public static WorldObjectState Read(JsonObject json,IEnumerable<WorldCapabilityDefinition> definitions)
    {
        var state=new WorldObjectState(definitions??Array.Empty<WorldCapabilityDefinition>()){Destroyed=json["destroyed"].GetValue<bool>()};
        var saved=json["capabilities"]?.AsObject()??throw new ArgumentException("世界对象缺少能力状态。");
        // Missing records retain raw state for repair without inventing executable capabilities.
        if(definitions==null){state._unresolved=(JsonObject)json.DeepClone();return state;}
        WorldCatalog.Require(saved.Count==state._capabilities.Count,"对象的能力集合与存档不匹配。");
        foreach(var entry in saved)
        {
            WorldCatalog.Require(state._capabilities.TryGetValue(entry.Key,out var capability)&&entry.Value["version"].GetValue<int>()==capability.Version,"对象能力或存档版本已失效："+entry.Key);
            capability.Restore(entry.Value["data"].AsObject());
        }
        return state;
    }
}
