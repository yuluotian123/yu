using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Framework;
using Godot;
namespace GameLogic.AI;

/// <summary>Loaded level context; persistent state belongs to the world session.</summary>
[Tool, GlobalClass]
public partial class AiWorldComponent3D : Component3D,ICharacterEnvironment
{
    public const string Group="npc_ai_world";
    public override int Priority=>ComponentPriority.AI+50;
    [Export] public double PlanningMillisecondsPerFrame {get;set;}=1.5;
    public WorldClock Clock {get;private set;}
    public double RuntimeSeconds=>Session.RuntimeSeconds;
    public double LastPlanningMilliseconds {get;private set;}
    public double RainAmount=>RainOverride??Session.ContextFor(Owner)?.Weather?.RainAmount??0;
    public float? RainOverride {get;set;}
    public readonly Dictionary<string,AiAgentComponent3D> Actors=new(StringComparer.Ordinal);
    public readonly Dictionary<string,WorldInteractionComponent3D> Points=new(StringComparer.Ordinal);
    readonly List<AiAgentComponent3D> _actors=new();
    internal WorldModule Session {get;private set;}
    public double TotalHours=>Clock.TotalHours;
    public double Rain=>RainAmount;
    public string LevelId=>Session.LevelFor(Owner)?.Id??"";
    public string LocationAt(Vector3 position,string previous)
    {
        var old=Points.GetValueOrDefault(previous);
        return (old?.ContainsPosition(position)==true?old:Nearby(position,0).Where(p=>p.IsActive&&p.ContainsPosition(position))
            .OrderBy(p=>position.DistanceSquaredTo(p.UsePosition)).ThenBy(p=>p.PointId,StringComparer.Ordinal).FirstOrDefault())?.PointId??"";
    }
    int _cursor;
    readonly Dictionary<(int,int),HashSet<WorldInteractionComponent3D>> _cells=new();
    readonly Dictionary<WorldInteractionComponent3D,(int X1,int Z1,int X2,int Z2)> _pointCells=new();
    static (int,int) Cell(Vector3 position)=>((int)Math.Floor(position.X/8),(int)Math.Floor(position.Z/8));
    public IEnumerable<WorldInteractionComponent3D> Nearby(Vector3 position,float radius)
    {
        var min=Cell(position-new Vector3(radius,0,radius));var max=Cell(position+new Vector3(radius,0,radius));
        var seen=new HashSet<WorldInteractionComponent3D>();
        for(int x=min.Item1;x<=max.Item1;x++)for(int z=min.Item2;z<=max.Item2;z++)
            if(_cells.TryGetValue((x,z),out var points))foreach(var point in points)if(seen.Add(point))yield return point;
    }
    public override void OnInit()
    {
        Session=(WorldModule)ModuleSystem.GetModule<IWorldModule>();Clock=ModuleSystem.GetModule<ITimeOfDayModule>().Clock;
        Session.RegisterEnvironment(this);Owner.AddToGroup(Group);
    }
    public override void OnUpdate(double delta)
    {
        if(Session.IsSwitching)return;
        if(_actors.Count!=Actors.Count||_actors.Any(a=>Actors.GetValueOrDefault(a.ActorId)!=a))
        {_actors.Clear();_actors.AddRange(Actors.Values.OrderBy(a=>a.ActorId,StringComparer.Ordinal));}
        var timer=Stopwatch.StartNew();
        for(int i=0;i<_actors.Count&&timer.Elapsed.TotalMilliseconds<PlanningMillisecondsPerFrame;i++)
        {_cursor%=_actors.Count;var actor=_actors[_cursor++];if(actor.IsActive)actor.AdvancePlanning(Math.Max(.01,PlanningMillisecondsPerFrame-timer.Elapsed.TotalMilliseconds));if(Session.PathQueriesLastFrame>=Session.PathQueriesPerFrame)break;}
        LastPlanningMilliseconds=timer.Elapsed.TotalMilliseconds;
    }
    public static AiWorldComponent3D Find(Node node)=>((WorldModule)ModuleSystem.GetModule<IWorldModule>()).EnvironmentFor(node);
    public void RegisterPoint(WorldInteractionComponent3D point)
    {
        if(Points.TryGetValue(point.PointId,out var old)&&old!=point)throw new InvalidOperationException("重复交互点："+point.PointId);
        Session.RegisterPoint(point);Points.Add(point.PointId,point);
        UpdatePoint(point);
    }
    internal void UpdatePoint(WorldInteractionComponent3D point)
    {
        var bounds=point.InteractionBounds;var min=Cell(bounds.Position);var max=Cell(bounds.End);var cell=(min.Item1,min.Item2,max.Item1,max.Item2);
        if(_pointCells.TryGetValue(point,out var previous))
        {if(previous==cell)return;RemoveCells(point,previous);}
        for(int x=min.Item1;x<=max.Item1;x++)for(int z=min.Item2;z<=max.Item2;z++)
        {if(!_cells.TryGetValue((x,z),out var points))_cells[(x,z)]=points=new();points.Add(point);}
        _pointCells[point]=cell;
    }
    void RemoveCells(WorldInteractionComponent3D point,(int X1,int Z1,int X2,int Z2) cell)
    {for(int x=cell.X1;x<=cell.X2;x++)for(int z=cell.Z1;z<=cell.Z2;z++)if(_cells.TryGetValue((x,z),out var points)){points.Remove(point);if(points.Count==0)_cells.Remove((x,z));}}
    public void RemovePoint(WorldInteractionComponent3D point)
    {
        if(Points.GetValueOrDefault(point.PointId)!=point)return;
        Points.Remove(point.PointId);Session.UnregisterPoint(point);
        if(_pointCells.Remove(point,out var cell))RemoveCells(point,cell);
    }
    public override void OnDestroy()
    {
        foreach(var actor in Actors.Values.ToArray())actor.CancelPlan("LevelUnloaded");Actors.Clear();_actors.Clear();
        foreach(var point in Points.Values.ToArray())Session.UnregisterPoint(point);Points.Clear();_cells.Clear();_pointCells.Clear();
        Session.UnregisterEnvironment(this);Owner.RemoveFromGroup(Group);
    }
}
