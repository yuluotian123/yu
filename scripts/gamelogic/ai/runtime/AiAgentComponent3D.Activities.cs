using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace GameLogic.AI;

public sealed record NpcActivityRequest(string Id,string Goal,string Place,int Priority,double ExpiresHours,bool Emergency=false,string[] Operations=null,double CommitmentSeconds=0,bool Interruptible=true,string Fallback="");
public partial class AiAgentComponent3D
{
    readonly Dictionary<string,NpcActivityRequest> _activityRequests=new(StringComparer.Ordinal);
    NpcWindowSpec _activity;
    double _activityUntil;
    public string CurrentActivity=>_activity?.Id??"";
    public string ActivityPlace=>_activity==null?"":_activity.LocationBinding.Length>0?Character.WorldKnowledge.Locations.GetValueOrDefault(_activity.LocationBinding,""):_activity.Location;
    public bool RequestActivity(NpcActivityRequest request)
    {
        if(request==null||string.IsNullOrWhiteSpace(request.Id)||request.Goal==null||request.Place==null||request.Fallback==null||request.Place.Length>0&&World.Session.Catalog.Locations.ContainsKey(request.Place)!=true||!double.IsFinite(request.ExpiresHours)||request.ExpiresHours<=SimulationHours
            ||!double.IsFinite(request.CommitmentSeconds)||request.CommitmentSeconds<0||request.Fallback.Length>0&&!Program.Goals.Any(g=>g.Id==request.Fallback)
            ||!Program.Goals.Any(g=>g.Id==request.Goal)||!_activityRequests.ContainsKey(request.Id)&&_activityRequests.Count>=16)return false;
        _activityRequests[request.Id]=request;_selectAt=0;World?.Session.Population?.Wake(ActorId);return true;
    }
    public void CancelActivity(string id){_activityRequests.Remove(id);_selectAt=0;World?.Session.Population?.Wake(ActorId);}
    public bool ActivityAllows(string operation)=>_activity?.Operations is not {Count:>0} allowed||allowed.Contains(operation);
    void UpdateActivity()
    {
        foreach(var id in _activityRequests.Values.Where(r=>r.ExpiresHours<=SimulationHours).Select(r=>r.Id).ToArray())_activityRequests.Remove(id);
        double hour=SimulationHours%24;
        var windows=Program.Windows.Where(w=>(w.Start==w.End||(w.Start<w.End?hour>=w.Start&&hour<w.End:hour>=w.Start||hour<w.End))
            &&(w.Activation==null||w.Activation.Evaluate(Read,Program.Parameter).IsTrue));
        var requests=_activityRequests.Values.Select(r=>new NpcWindowSpec(r.Id,0,0,r.Priority,Array.Empty<string>(),r.Place,"",r.Goal,r.Fallback,r.Operations,r.CommitmentSeconds,r.Interruptible,r.Emergency));
        var next=windows.Concat(requests).OrderByDescending(w=>w.Emergency).ThenByDescending(w=>w.Priority).ThenBy(w=>w.Id,StringComparer.Ordinal).FirstOrDefault();
        if(next?.Id==_activity?.Id&&next?.Goal==_activity?.Goal){_activity=next;return;}
        if(_activity!=null&&next?.Emergency!=true&&Now<_activityUntil)return;
        if(_activity!=null&&!_activity.Interruptible&&_current!=null&&next?.Emergency!=true)return;
        CancelPlan("ActivityChanged");_activity=next;_activityUntil=Now+(next?.Commitment??0);_goal=null;_selectAt=0;
        Record("Activity",next?.Id??"none");
    }
    bool ActivityGoalAllowed(NpcGoalSpec goal)
    {
        if(_activity==null||_activity.Goal.Length==0)return true;
        bool blocked=_goalBlockedUntil.GetValueOrDefault(_activity.Goal)>Now;
        return goal.Id==(blocked&&_activity.Fallback.Length>0?_activity.Fallback:_activity.Goal);
    }
    internal double NextSimulationSeconds()
    {
        if(_pendingCheckpoint!=null||_search!=null||_current==null&&_plan.Count>0)return .001;
        double secondsPerHour=World.Session.Population?.SecondsPerHour??World.Clock.DayLengthSeconds/24;
        double seconds=24*secondsPerHour;
        if(Program.Goals.Any(g=>!g.Completion.Evaluate(Read,Program.Parameter).IsTrue))seconds=secondsPerHour/60;
        if(_retryAt>Now)seconds=Math.Min(seconds,_retryAt-Now);
        foreach(double blocked in _goalBlockedUntil.Values.Where(t=>t>Now))seconds=Math.Min(seconds,blocked-Now);
        var progress=_execution?.Context.GetUserData<NpcActionContext>()?.CaptureExecution()?.Progress.Values;
        if(progress!=null)foreach(var step in progress.Where(p=>!p.Complete&&p.Duration.HasValue))seconds=Math.Min(seconds,Math.Max(.001,step.Duration.Value-step.Elapsed));
        if(ManagedActor?.Travel is {} travel&&travel.Leg<travel.Routes.Count)
        {
            var leg=World.Session.Routes.Leg(travel.Routes[travel.Leg],ManagedActor.Level,this);
            if(travel.ApproachSeconds>0)seconds=Math.Min(seconds,travel.ApproachSeconds);
            else if(leg!=null)seconds=Math.Min(seconds,Math.Max(.001,(leg.Hours-travel.ElapsedHours)*World.Session.Population.SecondsPerHour));
        }
        else if(ManagedActor?.Travel?.ArrivalSeconds>0)seconds=Math.Min(seconds,ManagedActor.Travel.ArrivalSeconds);
        foreach(var window in Program.Windows)foreach(double boundary in new[]{window.Start,window.End})
        {double hours=(boundary-SimulationHours%24+24)%24;if(hours>1e-9)seconds=Math.Min(seconds,hours*secondsPerHour);}
        foreach(var need in State.Profile.Needs)
        {
            var scale=need.Multiplier?.Evaluate(Read,Program.Parameter)??CharacterValue.Numeric(1);if(scale.Type!=CharacterValueType.Number)continue;
            double rate=need.Rate*scale.Number;if(Math.Abs(rate)<1e-10)continue;
            foreach(double threshold in new[]{need.Enter,need.Exit,need.Min,need.Max})
            {double hours=(threshold-State.Needs.Values[need.Id])/rate;if(hours>1e-9)seconds=Math.Min(seconds,hours*secondsPerHour);}
        }
        foreach(var request in _activityRequests.Values)seconds=Math.Min(seconds,Math.Max(.001,(request.ExpiresHours-SimulationHours)*secondsPerHour));
        return Math.Max(.001,Math.Min(seconds,_activityUntil>Now?_activityUntil-Now:seconds));
    }
    JsonObject CaptureActivity()=>new(){["id"]=_activity?.Id,["remaining"]=Math.Max(0,_activityUntil-Now),["requests"]=JsonSerializer.SerializeToNode(_activityRequests.Values)};
    void RestoreActivity(JsonObject data)
    {
        _activityRequests.Clear();_activity=null;
        if(data==null)return;
        foreach(var request in data["requests"]?.Deserialize<List<NpcActivityRequest>>()??new())RequestActivity(request);
        string id=data["id"]?.GetValue<string>();_activity=Program.Windows.FirstOrDefault(w=>w.Id==id);
        if(_activity==null&&id!=null&&_activityRequests.TryGetValue(id,out var r))_activity=new(r.Id,0,0,r.Priority,Array.Empty<string>(),r.Place,"",r.Goal,r.Fallback,r.Operations,r.CommitmentSeconds,r.Interruptible,r.Emergency);
        double remaining=data["remaining"]?.GetValue<double>()??0;NpcProgram.Require(double.IsFinite(remaining)&&remaining>=0,"活动承诺存档无效。");_activityUntil=Now+remaining;
    }
}
