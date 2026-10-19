using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
namespace GameLogic.AI;

/// <summary>A tagged world capability endpoint. No food/work/profession enum.</summary>
[Tool, GlobalClass]
public partial class AiInteractionPointComponent3D : Component3D
{
    public override int Priority=>ComponentPriority.Interaction;
    [Export] public string PointId {get;set;}="";
    [Export] public string DisplayName {get;set;}="";
    [Export] public string[] Tags {get;set;}=Array.Empty<string>();
    [Export] public string OwnerId {get;set;}="";
    [Export] public double StartingStock {get;set;}=100;
    [Export] public int Capacity {get;set;}=1;
    [Export] public bool UseOpeningHours {get;set;}
    [Export] public double OpenHour {get;set;}=8;
    [Export] public double CloseHour {get;set;}=20;
    [Export] public float UseRadius {get;set;}=.55f;
    [Export] public NodePath UsePositionPath {get;set;}=new("UsePosition");
    public AiWorldComponent3D World {get;private set;}
    public double Stock {get;internal set;}
    public long Revision {get;private set;}
    public bool? ManualOpen {get;private set;}
    public Vector3 UsePosition=>Owner.GetNodeOrNull<Node3D>(UsePositionPath)?.GlobalPosition??Owner.GlobalPosition;
    public bool ContainsPosition(Vector3 position)
    {
        var offset=position-UsePosition;
        return new Vector2(offset.X,offset.Z).LengthSquared()<=UseRadius*UseRadius&&Math.Abs(offset.Y)<1;
    }
    public bool IsOpen=>IsActive&&(ManualOpen??(!UseOpeningHours||OpenHour==CloseHour||(OpenHour<CloseHour?Hour>=OpenHour&&Hour<CloseHour:Hour>=OpenHour||Hour<CloseHour)));
    private double Hour=>(World?.Clock.TotalHours??12)%24;
    private readonly Dictionary<string,double> _leases=new(StringComparer.Ordinal);
    public int Reservations {get {Expire();return _leases.Count;}}
    public override void OnInit(){if(PointId.Length==0)PointId=Owner.PersistentId;Stock=StartingStock;NpcProgram.Require(StartingStock>=0&&Capacity>0&&UseRadius>0,"Invalid interaction point.");Register();}
    public override void OnUpdate(double delta){if(World==null)Register();Expire();}
    void Register(){World=AiWorldComponent3D.Find(Owner);World?.RegisterPoint(this);}
    public bool CanUse(string actor)=>GodotObject.IsInstanceValid(Owner)&&IsOpen&&(OwnerId.Length==0||OwnerId==actor);
    public bool CanReserve(string actor){Expire();return CanUse(actor)&&(_leases.ContainsKey(actor)||_leases.Count<Capacity);}
    public bool TryReserve(string actor){Expire();if(!CanUse(actor)||!_leases.ContainsKey(actor)&&_leases.Count>=Capacity)return false;_leases[actor]=World.RuntimeSeconds+5;return true;}
    public bool Reserved(string actor){Expire();return _leases.ContainsKey(actor);}
    public void Release(string actor)=>_leases.Remove(actor);
    public void SetOpen(bool open){ManualOpen=open;Revision++;}
    public void ClearOpenOverride(){ManualOpen=null;Revision++;}
    public void MarkChanged()=>Revision++;
    public JsonObject Capture()=>new(){["stock"]=Stock,["manual_open"]=ManualOpen};
    public void Restore(JsonObject state){Stock=state["stock"]?.GetValue<double>()??StartingStock;ManualOpen=state["manual_open"]?.GetValue<bool>();_leases.Clear();Revision++;}
    void Expire(){if(World==null)return;foreach(var key in _leases.Where(x=>x.Value<=World.RuntimeSeconds).Select(x=>x.Key).ToArray())_leases.Remove(key);}
    public override void OnDestroy(){if(World?.Points.GetValueOrDefault(PointId)==this)World.Points.Remove(PointId);_leases.Clear();World=null;}
}
