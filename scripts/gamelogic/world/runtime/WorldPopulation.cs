using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Framework;
using GameLogic.AI;
using Godot;
namespace GameLogic;

public sealed class WorldTravelState
{
    public string Target {get;set;}="";
    public List<string> Routes {get;set;}=new();
    public int Leg {get;set;}
    public double ElapsedHours {get;set;}
    public long Version {get;set;}
    public double ApproachSeconds {get;set;}
    public double ArrivalSeconds {get;set;}
}
public sealed class WorldActorRuntime
{
    public WorldActorEntry Definition {get;}
    public string Level {get;internal set;}
    public Vector3 Position {get;internal set;}
    public double Hours {get;internal set;}
    public double NextHours {get;internal set;}
    public WorldTravelState Travel {get;internal set;}
    public WorldActorComponent3D Host {get;internal set;}
    public AiAgentComponent3D Agent=>GodotObject.IsInstanceValid(Host)?Host.Owner?.GetComponent<AiAgentComponent3D>():null;
    public bool IsProxy=>Host?.Proxy==true;
    public string Mode=>Travel!=null?"Transit":IsProxy?"Distant":"Detailed";
    internal JsonObject Character,Brain;
    internal bool Running;
    internal long Generation;
    internal double ChangedAt;
    internal bool Loading;
    internal SceneHandle Handle;
    public WorldActorRuntime(WorldActorEntry definition,double hours){Definition=definition;Level=definition.LevelId;Position=definition.Position;Hours=NextHours=hours;Running=definition.Running;}
}

/// <summary>One actor roster; lightweight EC proxies reuse the real planner and semantic graph tasks.</summary>
public sealed class WorldPopulation
{
    readonly WorldModule _world;
    readonly Dictionary<string,WorldActorRuntime> _actors=new(StringComparer.Ordinal);
    readonly Dictionary<ulong,WorldActorRuntime> _hosts=new();
    readonly PriorityQueue<(string Id,long Generation),(double Hours,string Id)> _events=new(Comparer<(double Hours,string Id)>.Create((a,b)=>{int c=a.Hours.CompareTo(b.Hours);return c!=0?c:StringComparer.Ordinal.Compare(a.Id,b.Id);}));
    readonly Dictionary<string,double> _interest=new(StringComparer.Ordinal);
    readonly HashSet<WorldActorRuntime> _detailed=new();
    readonly Queue<WorldActorRuntime> _unbound=new();
    readonly Dictionary<(string,int,int),HashSet<WorldActorRuntime>> _cells=new();
    readonly Dictionary<WorldActorRuntime,(string,int,int)> _actorCells=new();
    float _promotionRadius;
    GameObject3D _lockedPlayer;
    Node.ProcessModeEnum _playerProcess;
    public bool IsCatchingUp {get;private set;}
    GameObject3D _root;
    bool _closing,_restoring;
    double _lastHours;
    public IReadOnlyDictionary<string,WorldActorRuntime> Actors=>_actors;
    public int EventsPerFrame {get;set;}=128;
    public double MillisecondsPerFrame {get;set;}=2;
    public int LastEvents {get;private set;}
    public double LastMilliseconds {get;private set;}
    public int Backlog=>_actors.Values.Count(a=>a.IsProxy&&a.NextHours<=Clock.TotalHours);
    public string LastError {get;private set;}="";
    public void Wake(string id)
    {if(_actors.TryGetValue(id,out var actor)&&actor.IsProxy)Schedule(actor,Math.Max(actor.Hours,Clock.TotalHours));}
    public WorldClock Clock=>ModuleSystem.GetModule<ITimeOfDayModule>().Clock;
    public double SecondsPerHour=>Clock.DayLengthSeconds/24;
    internal WorldPopulation(WorldModule world){_world=world;}
    public WorldActorRuntime For(Node node)
    {for(var at=node;at!=null;at=at.GetParent())if(_hosts.TryGetValue(at.GetInstanceId(),out var actor))return actor;return null;}
    public void Configure()
    {
        _actors.Clear();_cells.Clear();_actorCells.Clear();_promotionRadius=0;
        foreach(var definition in _world.Catalog.Actors.Values){var actor=new WorldActorRuntime(definition,Clock.TotalHours);_actors.Add(definition.Id,actor);Index(actor);_promotionRadius=Math.Max(_promotionRadius,definition.DetailedDistance);}
    }
    void Index(WorldActorRuntime actor)
    {
        var cell=(actor.Level,(int)Math.Floor(actor.Position.X/16),(int)Math.Floor(actor.Position.Z/16));
        if(_actorCells.TryGetValue(actor,out var old)){if(old==cell)return;if(_cells.TryGetValue(old,out var previous)){previous.Remove(actor);if(previous.Count==0)_cells.Remove(old);}}
        if(!_cells.TryGetValue(cell,out var entries))_cells[cell]=entries=new();entries.Add(actor);_actorCells[actor]=cell;
    }
    IEnumerable<WorldActorRuntime> PromotionCandidates()
    {
        var seen=new HashSet<WorldActorRuntime>();
        foreach(string id in _interest.Keys)if(_actors.TryGetValue(id,out var interested)&&seen.Add(interested))yield return interested;
        if(_world.Player==null)yield break;
        var p=_world.Player.GlobalPosition;int minX=(int)Math.Floor((p.X-_promotionRadius)/16),maxX=(int)Math.Floor((p.X+_promotionRadius)/16),minZ=(int)Math.Floor((p.Z-_promotionRadius)/16),maxZ=(int)Math.Floor((p.Z+_promotionRadius)/16);
        for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)if(_cells.TryGetValue((_world.CurrentLevelId,x,z),out var entries))foreach(var actor in entries)if(seen.Add(actor))yield return actor;
    }
    internal void Start(Node parent,string enteringLevel)
    {
        _closing=false;_lastHours=Clock.TotalHours;
        _root=new(){Name="WorldLogicalActors",Components=new(){new AiWorldComponent3D()}};parent.AddChild(_root);_root.SetProcess(false);
        foreach(var actor in _actors.Values.OrderBy(a=>a.Definition.Id,StringComparer.Ordinal))
        {
            if(actor.Character==null)actor.Hours=actor.NextHours=Clock.TotalHours;
            // The entering Scene gets the first chance to bind its residents. Running their
            // proxies during the loading screen could send them away before they ever appear.
            if(actor.Level==enteringLevel&&actor.Travel==null)_unbound.Enqueue(actor);
            else EnsureProxy(actor);
        }
    }
    internal bool OwnsEnvironment(Node node)=>_root!=null&&(node==_root||_root.IsAncestorOf(node));
    internal AiWorldComponent3D Environment=>_root?.GetComponent<AiWorldComponent3D>();
    internal void Bind(WorldActorComponent3D host)
    {
        WorldCatalog.Require(_actors.TryGetValue(host.Owner.PersistentId,out var actor),"世界角色未生成目录，请保存登记关卡："+host.Owner.PersistentId);
        WorldCatalog.Require(actor.Host==null||actor.Host==host,"世界角色重复实例："+host.Owner.PersistentId);
        actor.Host=host;host.Runtime=actor;_hosts[host.Owner.GetInstanceId()]=actor;actor.ChangedAt=_world.RuntimeSeconds;
        if(!host.Proxy)_detailed.Add(actor);
    }
    internal void Ready(AiAgentComponent3D agent)
    {
        var actor=For(agent.Owner);if(actor==null)return;
        agent.Running=actor.Running;
        agent.Owner.GlobalPosition=actor.Position;agent.Owner.GetComponent<CharacterMovementComponent3D>()?.SyncBodyToOwner();
        if(actor.Character!=null)agent.Character.Restore((JsonObject)actor.Character.DeepClone(),agent.Character.SchemaVersion);
        if(actor.Brain!=null)agent.Restore((JsonObject)actor.Brain.DeepClone(),agent.SchemaVersion);
        else agent.State.Advance(actor.Hours,agent.Read,agent.Program.Parameter);
        if(actor.IsProxy)agent.OnUpdate(0);
        Schedule(actor,Math.Max(actor.Hours,actor.NextHours));
    }
    void Snapshot(WorldActorRuntime actor)
    {
        if(actor.Agent is not {} agent||agent.State==null)return;
        actor.NextHours=Math.Max(actor.Hours,actor.NextHours);
        actor.Position=agent.Owner.GlobalPosition;Index(actor);actor.Running=agent.Running;actor.Character=agent.Character.Capture();actor.Brain=agent.Capture();
    }
    internal void Unbind(WorldActorComponent3D host)
    {
        var actor=host.Runtime;
        if(actor?.Host!=host){_hosts.Remove(host.Owner.GetInstanceId());return;}
        if(!_restoring)Snapshot(actor);_hosts.Remove(host.Owner.GetInstanceId());_detailed.Remove(actor);actor.Host=null;actor.Handle?.Dispose();actor.Handle=null;if(!_closing)_unbound.Enqueue(actor);
    }
    void RemoveHost(WorldActorRuntime actor)
    {
        var host=actor.Host;if(host==null)return;
        Snapshot(actor);host.Owner.Free();actor.Host=null;actor.Generation++;
    }
    void EnsureProxy(WorldActorRuntime actor)
    {
        if(_closing||_root==null||actor.Host!=null||actor.Loading)return;
        var host=new GameObject3D{Name="Logical_"+actor.Definition.Id.ValidateNodeName(),PersistentId=actor.Definition.Id,Position=actor.Position,
            Components=new(){new WorldActorComponent3D{Proxy=true},new CharacterDataComponent3D{Profile=actor.Definition.Profile,ParameterOverrides=new(actor.Definition.ParameterOverrides),Persist=false},
                new AiAgentComponent3D{Graph=actor.Definition.Graph,Persist=false}}};
        host.SetMeta("_world_proxy",true);_root.AddChild(host);host.SetProcess(false);host.SetPhysicsProcess(false);
    }
    void Schedule(WorldActorRuntime actor,double hours)
    {actor.NextHours=hours;actor.Generation++;_events.Enqueue((actor.Definition.Id,actor.Generation),(hours,actor.Definition.Id));}
    public bool RequestInterest(string id,double seconds=10)
    {
        if(!_actors.ContainsKey(id)||!double.IsFinite(seconds)||seconds<=0||seconds>120||!_interest.ContainsKey(id)&&_interest.Count>=16)return false;
        _interest[id]=_world.RuntimeSeconds+seconds;return true;
    }
    internal void PrepareScene(GameObject3D root,WorldLevelDefinition level)
    {
        foreach(var host in WorldModule.Hosts(root).Where(h=>h.Components.OfType<WorldActorComponent3D>().Any()).ToArray())
        {
            WorldCatalog.Require(_actors.TryGetValue(host.PersistentId,out var actor),"关卡角色未登记："+host.PersistentId);
            if(actor.Level!=level.Id||actor.Travel!=null){host.Free();continue;}
            RemoveHost(actor);
        }
    }
    public void Process(bool transition=false)
    {
        if(_root==null||_closing||_world.IsSwitching&&!transition)return;
        double target=Clock.TotalHours;
        if(target<_lastHours-1e-8){LastError="活动会话不能倒退世界时间；请读取存档。";return;}LastError="";
        bool fastForward=target-_lastHours>.05;_lastHours=target;
        if(fastForward)IsCatchingUp=true;
        if(IsCatchingUp&&!transition&&_lockedPlayer==null&&_world.Player!=null)
        {_lockedPlayer=_world.Player;_playerProcess=_lockedPlayer.ProcessMode;_lockedPlayer.ProcessMode=Node.ProcessModeEnum.Disabled;}
        foreach(var id in _interest.Where(p=>p.Value<=_world.RuntimeSeconds).Select(p=>p.Key).ToArray())_interest.Remove(id);
        int changes=0;
        if(!transition)for(int pending=0;pending<16&&_unbound.TryDequeue(out var unbound);pending++)EnsureProxy(unbound);
        foreach(var actor in _detailed.Concat(PromotionCandidates()).Distinct().OrderBy(a=>a.Definition.Id,StringComparer.Ordinal).ToArray())
        {
            if(actor.Host==null){EnsureProxy(actor);continue;}
            if(actor.Agent==null||actor.Loading)continue;
            bool inLevel=actor.Level==_world.CurrentLevelId&&actor.Travel==null;
            float distance=_world.Player==null?float.PositiveInfinity:_world.Player.GlobalPosition.DistanceTo(actor.Position);
            bool requested=_interest.ContainsKey(actor.Definition.Id);
            bool wantsDetailed=inLevel&&(requested||distance<actor.Definition.DetailedDistance);
            if(!actor.IsProxy)
            {
                actor.Position=actor.Host.Owner.GlobalPosition;
                if(changes<4&&(IsCatchingUp||actor.Travel!=null||!inLevel||distance>actor.Definition.AbstractDistance&&!requested&&_world.RuntimeSeconds-actor.ChangedAt>=actor.Definition.ResidencySeconds))
                {RemoveHost(actor);EnsureProxy(actor);changes++;}
            }
            else if(!transition&&!IsCatchingUp&&wantsDetailed&&actor.NextHours>target&&changes<4&&_world.RuntimeSeconds-actor.ChangedAt>=actor.Definition.ResidencySeconds)
            {_=Promote(actor);changes++;}
        }
        var watch=Stopwatch.StartNew();LastEvents=0;
        while(LastEvents<EventsPerFrame&&watch.Elapsed.TotalMilliseconds<MillisecondsPerFrame&&_events.TryPeek(out var item,out var due)&&due.Hours<=target+1e-10)
        {
            _events.Dequeue();if(!_actors.TryGetValue(item.Id,out var actor)||item.Generation!=actor.Generation||!actor.IsProxy||actor.Agent==null)continue;
            var agent=actor.Agent;double hours=Math.Max(actor.Hours,due.Hours),delta=(hours-actor.Hours)*SecondsPerHour;
            actor.Hours=hours;agent.State.Advance(hours,agent.Read,agent.Program.Parameter);agent.OnUpdate(delta);agent.AdvancePlanning(.25);agent.OnUpdate(0);
            double delay=agent.NextSimulationSeconds();Schedule(actor,hours+Math.Max(1e-7,delay/SecondsPerHour));LastEvents++;
        }
        LastMilliseconds=watch.Elapsed.TotalMilliseconds;
        if(IsCatchingUp&&_detailed.Count==0&&(!_events.TryPeek(out _,out var next)||next.Hours>target))
        {IsCatchingUp=false;UnlockPlayer();}
        if(_events.Count>_actors.Count*8+128)
        {_events.Clear();foreach(var actor in _actors.Values.Where(a=>a.IsProxy).OrderBy(a=>a.Definition.Id,StringComparer.Ordinal))Schedule(actor,actor.NextHours);}
    }
    async System.Threading.Tasks.Task Promote(WorldActorRuntime actor)
    {
        actor.Loading=true;long generation=actor.Generation;SceneHandle handle=null;
        try
        {
            handle=ModuleSystem.GetModule<IResourceModule>().LoadSceneAsync(actor.Definition.ScenePath);await handle.Task;
            if(_closing||_world.IsSwitching||actor.Generation!=generation||actor.Level!=_world.CurrentLevelId||actor.Travel!=null||!handle.IsValid)return;
            var instance=handle.Instantiate();
            if(instance is not GameObject3D host){instance.Free();throw new ArgumentException("世界角色 Scene 根必须是 GameObject3D。");}
            if(!host.Components.OfType<WorldActorComponent3D>().Any()){host.Free();throw new ArgumentException("世界角色 Scene 缺少 WorldActorComponent3D。");}
            host.Components=new(host.Components.Select(component=>
            {
                if(component is AiAgentComponent3D ai){var copy=(AiAgentComponent3D)ai.Duplicate();copy.Graph=actor.Definition.Graph;return copy;}
                if(component is not CharacterDataComponent3D data)return component;
                var character=(CharacterDataComponent3D)data.Duplicate();character.Profile=actor.Definition.Profile;character.ParameterOverrides=new(actor.Definition.ParameterOverrides);return (Component3D)character;
            }));
            RemoveHost(actor);host.PersistentId=actor.Definition.Id;
            // Physics children enter the world before OnInit restores the actor. Spawn at the saved
            // location immediately so a body at the scene origin cannot push or carry the player.
            var parent=_world.CurrentContext.Owner;host.Position=parent.ToLocal(actor.Position);parent.AddChild(host);actor.Handle=handle;handle=null;
        }
        catch(Exception e){actor.Agent?.Record("MaterializationFailed",e.Message);}
        finally{handle?.Dispose();actor.Loading=false;EnsureProxy(actor);}
    }
    internal BehaviorTreeStatus TravelTo(AiAgentComponent3D agent,string targetId,double seconds,NpcTaskProgress progress)
    {
        var actor=For(agent.Owner);if(actor==null||!_world.Catalog.Objects.TryGetValue(targetId,out var target))return BehaviorTreeStatus.Failure;
        if(progress.Complete)return BehaviorTreeStatus.Success;
        if(actor.Travel==null)
        {
            var route=_world.Routes.Find(actor.Level,target.Level.Id,agent);if(route==null)return BehaviorTreeStatus.Failure;
            if(!actor.IsProxy&&route.Legs.Count>0)
            {
                var exit=route.Legs[0].Exit;
                var offset=agent.Owner.GlobalPosition-exit;
                // Portals are often authored above the floor. Match navigation's horizontal
                // arrival radius so an arrived agent cannot wait forever for a height difference.
                if(new Vector2(offset.X,offset.Z).Length()>.6f)
                {if(agent.Navigation==null)return BehaviorTreeStatus.Failure;if(agent.Navigation.TargetId!="route:"+route.Legs[0].Id&&!agent.Navigation.BeginPosition(exit,"route:"+route.Legs[0].Id,.6f))return BehaviorTreeStatus.Failure;return agent.Navigation.Status==NpcNavigationStatus.Failed?BehaviorTreeStatus.Failure:BehaviorTreeStatus.Running;}
            }
            actor.Travel=new(){Target=targetId,Routes=route.Legs.Select(l=>l.Id).ToList(),Version=_world.Routes.Version};
            agent.State.UpdateLocation("");
            progress.Duration=route.Hours*SecondsPerHour;
            if(route.Legs.Count>0)
            {
                actor.Travel.ApproachSeconds=actor.Position.DistanceTo(route.Legs[0].Exit)/Math.Max(.1,actor.Definition.Speed);
                actor.Travel.ArrivalSeconds=target.Position.HasValue?route.Legs[^1].Entry.DistanceTo(target.Position.Value)/Math.Max(.1,actor.Definition.Speed):0;
                progress.Duration+=actor.Travel.ApproachSeconds+actor.Travel.ArrivalSeconds;
                for(int index=1;index<route.Legs.Count;index++)progress.Duration+=route.Legs[index-1].Entry.DistanceTo(route.Legs[index].Exit)/Math.Max(.1,actor.Definition.Speed);
            }
            if(route.Legs.Count==0)
            {
                if(!target.Position.HasValue)return BehaviorTreeStatus.Failure;
                progress.Duration=actor.Position.DistanceTo(target.Position.Value)/Math.Max(.1,actor.Definition.Speed);
            }
        }
        if(!actor.IsProxy&&actor.Level!=target.Level.Id)return BehaviorTreeStatus.Running;
        var travel=actor.Travel;
        if(travel.Target!=targetId)return BehaviorTreeStatus.Failure;
        double remaining=seconds/SecondsPerHour;
        while(travel.Leg<travel.Routes.Count)
        {
            if(travel.ApproachSeconds>0){double approach=Math.Min(remaining*SecondsPerHour,travel.ApproachSeconds);remaining-=approach/SecondsPerHour;travel.ApproachSeconds-=approach;progress.Elapsed+=approach;if(travel.ApproachSeconds>1e-8)return BehaviorTreeStatus.Running;}
            var leg=_world.Routes.Leg(travel.Routes[travel.Leg],actor.Level,agent);
            if(leg==null){actor.Travel=null;return BehaviorTreeStatus.Failure;}
            double used=Math.Min(remaining,leg.Hours-travel.ElapsedHours);travel.ElapsedHours+=used;remaining-=used;progress.Elapsed+=used*SecondsPerHour;
            if(travel.ElapsedHours+1e-9<leg.Hours)return BehaviorTreeStatus.Running;
            actor.Level=leg.To;actor.Position=leg.Entry;agent.Owner.GlobalPosition=leg.Entry;travel.Leg++;travel.ElapsedHours=0;
            if(travel.Leg<travel.Routes.Count)
            {
                var next=_world.Routes.Leg(travel.Routes[travel.Leg],actor.Level,agent);
                if(next==null){actor.Travel=null;return BehaviorTreeStatus.Failure;}
                travel.ApproachSeconds=leg.Entry.DistanceTo(next.Exit)/Math.Max(.1,actor.Definition.Speed);
            }
            if(remaining<=0&&travel.Leg<travel.Routes.Count)return BehaviorTreeStatus.Running;
        }
        if(travel.Routes.Count>0&&travel.ArrivalSeconds>0){double used=Math.Min(remaining*SecondsPerHour,travel.ArrivalSeconds);travel.ArrivalSeconds-=used;progress.Elapsed+=used;if(travel.ArrivalSeconds>1e-8)return BehaviorTreeStatus.Running;}
        if(travel.Routes.Count==0){progress.Elapsed+=seconds;if(progress.Elapsed+1e-8<(progress.Duration??0))return BehaviorTreeStatus.Running;}
        actor.Position=(_world.ResolvePoint(targetId)?.UsePosition??target.Position??actor.Position)+Vector3.Up*actor.Definition.HalfHeight;Index(actor);agent.Owner.GlobalPosition=actor.Position;agent.State.UpdateLocation(targetId);actor.Travel=null;progress.Complete=true;
        return BehaviorTreeStatus.Success;
    }
    internal JsonObject Capture()
    {
        var result=new JsonObject();foreach(var actor in _actors.Values)
        {Snapshot(actor);result[actor.Definition.Id]=new JsonObject{["level"]=actor.Level,["position"]=new JsonArray(actor.Position.X,actor.Position.Y,actor.Position.Z),["hours"]=actor.Hours,["next"]=actor.NextHours,
            ["character"]=actor.Character?.DeepClone(),["brain"]=actor.Brain?.DeepClone(),["running"]=actor.Running,["travel"]=JsonSerializer.SerializeToNode(actor.Travel)};}
        return result;
    }
    internal void Restore(JsonObject data)
    {
        var restored=new List<(WorldActorRuntime Actor,string Level,Vector3 Position,double Hours,double Next,JsonObject Character,JsonObject Brain,WorldTravelState Travel,bool Running)>();
        foreach(var actor in _actors.Values)
        {
            if(data?[actor.Definition.Id] is not JsonObject saved)
            {restored.Add((actor,actor.Definition.LevelId,actor.Definition.Position,Clock.TotalHours,Clock.TotalHours,null,null,null,actor.Definition.Running));continue;}
            string level=saved["level"].GetValue<string>();var position=saved["position"].AsArray();double hours=saved["hours"].GetValue<double>(),next=saved["next"].GetValue<double>();
            WorldCatalog.Require(_world.Catalog.Levels.ContainsKey(level)&&position.Count==3&&double.IsFinite(hours)&&hours>=0&&double.IsFinite(next)&&next>=hours,"世界角色位置或事件存档无效。");
            var pose=new Vector3(position[0].GetValue<float>(),position[1].GetValue<float>(),position[2].GetValue<float>());WorldCatalog.Require(pose.IsFinite(),"世界角色坐标无效。");
            var travel=saved["travel"]?.Deserialize<WorldTravelState>();
            if(travel!=null)
            {
                WorldCatalog.Require(_world.Catalog.Objects.ContainsKey(travel.Target)&&travel.Routes!=null&&travel.Leg>=0&&travel.Leg<=travel.Routes.Count
                    &&travel.Routes.Count<=_world.Catalog.Levels.Count&&travel.Routes.All(id=>_world.Catalog.Definition.Routes.Any(r=>r.Id==id))
                    &&double.IsFinite(travel.ElapsedHours)&&travel.ElapsedHours>=0&&double.IsFinite(travel.ApproachSeconds)&&travel.ApproachSeconds>=0&&double.IsFinite(travel.ArrivalSeconds)&&travel.ArrivalSeconds>=0,"旅行检查点无效。");
                string at=level;
                for(int index=travel.Leg;index<travel.Routes.Count;index++)
                {
                    var route=_world.Catalog.Definition.Routes.Single(r=>r.Id==travel.Routes[index]);
                    WorldCatalog.Require(at==route.From.Id||route.Bidirectional&&at==route.To.Id,"旅行路段不连续。");
                    if(index==travel.Leg)WorldCatalog.Require(travel.ElapsedHours<=route.TravelHours,"旅行进度超过路段时长。");
                    at=at==route.From.Id?route.To.Id:route.From.Id;
                }
                WorldCatalog.Require(at==_world.Catalog.Objects[travel.Target].Level.Id,"旅行终点与目标不匹配。");
            }
            restored.Add((actor,level,pose,hours,next,saved["character"] as JsonObject,saved["brain"] as JsonObject,travel,saved["running"]?.GetValue<bool>()??actor.Definition.Running));
        }
        IsCatchingUp=false;UnlockPlayer();_lastHours=Clock.TotalHours;_unbound.Clear();
        _restoring=true;
        try
        {
            foreach(var actor in _actors.Values){if(actor.Host!=null)actor.Host.Owner.Free();actor.Host=null;actor.Generation++;}
            _hosts.Clear();_events.Clear();
            foreach(var record in restored)
            {
                var actor=record.Actor;actor.Level=record.Level;actor.Position=record.Position;Index(actor);
                actor.Hours=record.Hours;actor.NextHours=record.Next;actor.Character=record.Character;actor.Brain=record.Brain;actor.Travel=record.Travel;
                actor.Running=record.Running;
            }
        }
        finally{_restoring=false;}
        if(_root!=null)foreach(var actor in _actors.Values.OrderBy(a=>a.Definition.Id,StringComparer.Ordinal))EnsureProxy(actor);
    }
    void UnlockPlayer(){if(GodotObject.IsInstanceValid(_lockedPlayer))_lockedPlayer.ProcessMode=_playerProcess;_lockedPlayer=null;}
    internal void Close(){_closing=true;IsCatchingUp=false;UnlockPlayer();foreach(var actor in _actors.Values)Snapshot(actor);_root?.Free();_root=null;_hosts.Clear();_events.Clear();_interest.Clear();_unbound.Clear();_detailed.Clear();}
}
