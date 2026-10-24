using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.AI;
using Godot;
namespace GameLogic;

public sealed record WorldRouteLeg(string Id,string From,string To,string Arrival,Vector3 Exit,Vector3 Entry,double Hours);
public sealed record WorldRoute(IReadOnlyList<WorldRouteLeg> Legs,double Hours);
public sealed class WorldRoutes
{
    readonly WorldDefinition _definition;
    readonly WorldCatalog _catalog;
    public long Version {get;private set;}
    public WorldRoutes(WorldCatalog catalog)
    {
        _catalog=catalog;var definition=catalog.Definition;_definition=definition;
        WorldCatalog.Require(definition.Routes.All(r=>r!=null&&!string.IsNullOrWhiteSpace(r.Id)&&r.From!=null&&r.To!=null&&r.From!=r.To
            &&definition.Levels.Any(l=>l.Id==r.From.Id)&&definition.Levels.Any(l=>l.Id==r.To.Id)&&double.IsFinite(r.TravelHours)&&r.TravelHours>=0
            &&r.FromPosition.IsFinite()&&r.ToPosition.IsFinite()&&!string.IsNullOrWhiteSpace(r.FromEntry)&&!string.IsNullOrWhiteSpace(r.ToEntry))
            &&definition.Routes.Select(r=>r.Id).Distinct(StringComparer.Ordinal).Count()==definition.Routes.Count,"世界路线配置无效或身份重复。");
        foreach(var route in definition.Routes)WorldCatalog.Require(route.UseExplicitPositions||catalog.Entrances.ContainsKey((route.From.Id,route.FromEntry))&&catalog.Entrances.ContainsKey((route.To.Id,route.ToEntry)),"路线入口未登记，请保存关卡："+route.Id);
    }
    readonly HashSet<string> _closed=new(StringComparer.Ordinal);
    public void SetOpen(string id,bool open){WorldCatalog.Require(_definition.Routes.Any(r=>r.Id==id),"路线不存在："+id);if(open?_closed.Remove(id):_closed.Add(id))Version++;}
    public IReadOnlyCollection<string> Closed=>_closed;
    public void Restore(IEnumerable<string> closed){_closed.Clear();foreach(var id in closed??Array.Empty<string>())SetOpen(id,false);Version++;}
    public bool Allowed(WorldRouteDefinition route,AiAgentComponent3D actor)=>route.Enabled&&!_closed.Contains(route.Id)
        &&(route.Public||actor?.Character.WorldKnowledge.KnownRoutes.Contains(route.Id)==true)
        &&(route.Permission==null||actor!=null&&route.Permission.Freeze().Evaluate(actor.Read,actor.Program.Parameter).IsTrue);
    public WorldRouteLeg Leg(string id,string from,AiAgentComponent3D actor)
    {
        var route=_definition.Routes.FirstOrDefault(r=>r.Id==id);if(route==null||!Allowed(route,actor))return null;
        var exit=route.UseExplicitPositions?route.FromPosition:_catalog.Entrances[(route.From.Id,route.FromEntry)];
        var entry=route.UseExplicitPositions?route.ToPosition:_catalog.Entrances[(route.To.Id,route.ToEntry)];
        return from==route.From.Id?new(id,from,route.To.Id,route.ToEntry,exit,entry,route.TravelHours)
            :route.Bidirectional&&from==route.To.Id?new(id,from,route.From.Id,route.FromEntry,entry,exit,route.TravelHours):null;
    }
    public WorldRoute Find(string from,string to,AiAgentComponent3D actor)
    {
        if(from==to)return new(Array.Empty<WorldRouteLeg>(),0);
        var edges=new List<WorldRouteLeg>();
        foreach(var route in _definition.Routes.OrderBy(r=>r.Id,StringComparer.Ordinal).Where(r=>Allowed(r,actor)))
        {
            edges.Add(Leg(route.Id,route.From.Id,actor));
            if(route.Bidirectional)edges.Add(Leg(route.Id,route.To.Id,actor));
        }
        var distances=new Dictionary<string,double>(StringComparer.Ordinal){{from,0}};var previous=new Dictionary<string,WorldRouteLeg>(StringComparer.Ordinal);
        var pending=new SortedSet<(double Cost,string Level)>(Comparer<(double,string)>.Create((a,b)=>{int c=a.Item1.CompareTo(b.Item1);return c!=0?c:StringComparer.Ordinal.Compare(a.Item2,b.Item2);})){(0,from)};
        while(pending.Count>0)
        {
            var current=pending.Min;pending.Remove(current);if(current.Cost!=distances[current.Level])continue;
            if(current.Level==to)
            {var path=new List<WorldRouteLeg>();for(string at=to;at!=from;at=previous[at].From)path.Add(previous[at]);path.Reverse();return new(path,current.Cost);}
            foreach(var edge in edges.Where(e=>e.From==current.Level))
            {double cost=current.Cost+edge.Hours;if(distances.TryGetValue(edge.To,out double old)&&old<=cost)continue;distances[edge.To]=cost;previous[edge.To]=edge;pending.Add((cost,edge.To));}
        }
        return null;
    }
}
