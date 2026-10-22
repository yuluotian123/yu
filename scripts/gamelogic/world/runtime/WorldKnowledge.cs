using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
namespace GameLogic;

public sealed record WorldObservation(double Hours,double? Stock,bool Open);
public sealed class WorldKnowledge
{
    public readonly Dictionary<string,string> Locations=new(StringComparer.Ordinal),Objects=new(StringComparer.Ordinal);
    public readonly HashSet<string> KnownLocations=new(StringComparer.Ordinal),KnownObjects=new(StringComparer.Ordinal);
    public readonly Dictionary<string,WorldObservation> Observations=new(StringComparer.Ordinal);
    public WorldKnowledge(CharacterWorldDefinition definition)
    {
        if(definition==null)return;
        foreach(var pair in definition.Locations){WorldCatalog.Require(pair.Value!=null,"角色地点绑定为空。");Locations.Add(pair.Key,pair.Value.Id);KnownLocations.Add(pair.Value.Id);}
        foreach(var pair in definition.Objects){WorldCatalog.Require(!string.IsNullOrWhiteSpace(pair.Value),"角色对象绑定为空。");Objects.Add(pair.Key,pair.Value);KnownObjects.Add(pair.Value);}
        KnownLocations.UnionWith(definition.KnownLocations.Where(p=>p!=null).Select(p=>p.Id));
        KnownObjects.UnionWith(definition.KnownObjects.Where(p=>!string.IsNullOrWhiteSpace(p)));
    }
    public void Observe(string id,string place,double hours,double? stock,bool open)
    {KnownObjects.Add(id);if(!string.IsNullOrEmpty(place))KnownLocations.Add(place);Observations[id]=new(hours,stock,open);}
    public JsonObject Capture()
    {
        var places=new JsonObject();foreach(var p in Locations)places[p.Key]=p.Value;
        var objects=new JsonObject();foreach(var p in Objects)objects[p.Key]=p.Value;
        var observations=new JsonObject();foreach(var p in Observations)observations[p.Key]=new JsonObject{["hours"]=p.Value.Hours,["stock"]=p.Value.Stock,["open"]=p.Value.Open};
        return new(){["locations"]=places,["objects"]=objects,["known_locations"]=new JsonArray(KnownLocations.Select(p=>(JsonNode)JsonValue.Create(p)).ToArray()),
            ["known_objects"]=new JsonArray(KnownObjects.Select(p=>(JsonNode)JsonValue.Create(p)).ToArray()),["observations"]=observations};
    }
    public void Restore(JsonObject json)
    {
        if(json==null)return;
        Locations.Clear();Objects.Clear();KnownLocations.Clear();KnownObjects.Clear();Observations.Clear();
        if(json["locations"] is JsonObject places)foreach(var p in places)Locations[p.Key]=p.Value.GetValue<string>();
        if(json["objects"] is JsonObject objects)foreach(var p in objects)Objects[p.Key]=p.Value.GetValue<string>();
        if(json["known_locations"] is JsonArray kp)KnownLocations.UnionWith(kp.Select(p=>p.GetValue<string>()));
        if(json["known_objects"] is JsonArray ko)KnownObjects.UnionWith(ko.Select(p=>p.GetValue<string>()));
        if(json["observations"] is JsonObject observations)foreach(var p in observations)
        {
            double time=p.Value["hours"].GetValue<double>();double? stock=p.Value["stock"]?.GetValue<double>();
            WorldCatalog.Require(double.IsFinite(time)&&(!stock.HasValue||double.IsFinite(stock.Value)&&stock>=0),"角色观测存档无效。");
            Observations[p.Key]=new(time,stock,p.Value["open"].GetValue<bool>());
        }
    }
}
