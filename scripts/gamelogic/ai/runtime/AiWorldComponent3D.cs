using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using Framework;
using Godot;
namespace GameLogic.AI;

[GlobalClass]
public partial class AiWorldComponent3D : Component3D, ISaveSection
{
    public const string Group="npc_ai_world";
    public override int Priority=>ComponentPriority.AI+50;
    [Export] public double PlanningMillisecondsPerFrame {get;set;}=1.5;
    [Export] public bool Persist {get;set;}=true;
    public string SectionKey=>"npc_ai_world";
    public string EntryKey=>Owner.PersistentId;
    public int SchemaVersion=>1;
    public WorldClock Clock {get;private set;}
    public double RuntimeSeconds {get;private set;}
    public double LastPlanningMilliseconds {get;private set;}
    public double RainAmount {get;private set;}
    public float? RainOverride {get;set;}
    public readonly Dictionary<string,AiAgentComponent3D> Actors=new(StringComparer.Ordinal);
    public readonly Dictionary<string,AiInteractionPointComponent3D> Points=new(StringComparer.Ordinal);
    private int _cursor;
    private ISaveModule _save;
    private JsonObject _restoredPoints=new();
    public override void OnInit()
    {
        if(Owner.GetTree().GetFirstNodeInGroup(Group)!=null)throw new InvalidOperationException("Only one NPC AI world is allowed per scene.");
        Clock=ModuleSystem.GetModule<ITimeOfDayModule>().Clock;
        Owner.AddToGroup(Group);Owner.SetMeta(Group,this);
        _save=ModuleSystem.GetModule<ISaveModule>();if(Persist)_save.RegisterSection(this);
    }
    public override void OnUpdate(double delta)
    {
        RuntimeSeconds+=delta;
        var environment=Owner.GetTree().CurrentScene?.GetNodeOrNull<GameObject3D>("EnvironmentRig");
        RainAmount=RainOverride??environment?.GetComponent<WeatherComponent3D>()?.RainAmount??0;
        var actors=Actors.Values.Where(a=>a.IsActive).OrderBy(a=>a.ActorId,StringComparer.Ordinal).ToArray();
        var timer=Stopwatch.StartNew();
        for(int i=0;i<actors.Length&&timer.Elapsed.TotalMilliseconds<PlanningMillisecondsPerFrame;i++)
        { _cursor%=actors.Length;actors[_cursor++].AdvancePlanning(Math.Max(.01,PlanningMillisecondsPerFrame-timer.Elapsed.TotalMilliseconds)); }
        LastPlanningMilliseconds=timer.Elapsed.TotalMilliseconds;
    }
    public static AiWorldComponent3D Find(Node node)=>(node.GetTree().GetFirstNodeInGroup(Group) as GameObject3D)?.GetComponent<AiWorldComponent3D>();
    public void RegisterPoint(AiInteractionPointComponent3D point)
    {
        if(Points.TryGetValue(point.PointId,out var old)&&old!=point)throw new InvalidOperationException("Duplicate point "+point.PointId);
        Points[point.PointId]=point;
        if(_restoredPoints[point.PointId] is JsonObject state)point.Restore(state);
    }
    public JsonObject Capture()
    {
        var points=(JsonObject)_restoredPoints.DeepClone();foreach(var p in Points.Values)points[p.PointId]=p.Capture();
        return new(){["points"]=points};
    }
    public void Restore(JsonObject data,int version)
    {
        if(version!=SchemaVersion||data?["points"] is not JsonObject points)return;
        foreach(var entry in points){double stock=entry.Value?["stock"]?.GetValue<double>()??0;NpcProgram.Require(double.IsFinite(stock)&&stock>=0,"Invalid saved point stock.");}
        foreach(var a in Actors.Values)a.CancelPlan("WorldRestored");
        _restoredPoints=(JsonObject)points.DeepClone();foreach(var p in Points.Values)if(points[p.PointId] is JsonObject state)p.Restore(state);
    }
    public override void OnDestroy(){foreach(var a in Actors.Values.ToArray())a.CancelPlan("WorldDestroyed");Actors.Clear();Points.Clear();if(Persist)_save?.UnregisterSection(this);Owner.RemoveFromGroup(Group);}
}
