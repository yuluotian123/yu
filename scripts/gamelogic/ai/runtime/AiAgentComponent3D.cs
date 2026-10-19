using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Framework;
using GameLogic.Goap;
using Godot;
namespace GameLogic.AI;

public sealed record NpcBoundAction(NpcActionSpec Action,NpcPlanSpec Plan,IReadOnlyDictionary<string,CharacterValue> Candidates,GoapAction Search,IReadOnlyDictionary<string,CharacterValue> Resolved=null)
{
    public string PlanActionKey=>Plan.Id+"::"+Action.Id;
}
public sealed record NpcActionContext(AiAgentComponent3D Agent,NpcActionSpec Action,IReadOnlyDictionary<string,CharacterValue> Inputs)
{
    public CharacterValue Input(string name)=>Inputs.GetValueOrDefault(name??"");
    public AiInteractionPointComponent3D Point(string name)=>Input(name) is {Type:CharacterValueType.Symbol,Symbol:{Length:>0} id}?Agent.World?.Points.GetValueOrDefault(id):null;
}

[GlobalClass]
public partial class AiAgentComponent3D : Component3D, ISaveSection, IGraphNodeProvider
{
    public override int Priority=>ComponentPriority.AI;
    [Export] public NpcGraphAsset Graph {get;set;}
    [Export] public bool Running {get;set;}=true;
    [Export] public bool Persist {get;set;}=true;
    public string ActorId=>Owner.PersistentId;
    public AiWorldComponent3D World {get;private set;}
    public NpcProgram Program {get;private set;}
    public CharacterDataComponent3D Character {get;private set;}
    public CharacterState State=>Character?.State;
    public NpcDecisionState Decision {get;private set;}
    public NpcNavigationComponent3D Navigation {get;private set;}
    public string CurrentGoal=>_goal?.Name??"";
    public string CurrentAction=>_current?.Action.Name??"";
    public string LastFailure {get;private set;}="";
    public bool IsAbstract {get;private set;}
    public int CompletedActions {get;private set;}
    public IReadOnlyList<string> Trace=>_trace;
    public IReadOnlyList<string> GoalScores=>_scores;
    public IReadOnlyDictionary<string,CharacterValue> PlanBindings=>new System.Collections.ObjectModel.ReadOnlyDictionary<string,CharacterValue>(_bindings);
    public string SectionKey=>"npc_agent";
    public string EntryKey=>ActorId;
    public int SchemaVersion=>2;
    public IEnumerable<Type> GetGraphNodeTypes(GraphAsset graph, GraphAsset root) => graph is NpcGraphAsset
        ? new[]{typeof(NpcRootNodeData),typeof(NpcScheduleNodeData),typeof(NpcPerceptionNodeData),typeof(NpcMemoryNodeData),typeof(NpcNeedPolicyNodeData),typeof(NpcPolicyNodeData),typeof(NpcGoalNodeData),typeof(NpcActionNodeData),typeof(NpcTemplateNodeData),typeof(NpcTargetSelectorNodeData)} : Array.Empty<Type>();
    public IEnumerable<Type> GetGraphActionTypes(GraphAsset graph, GraphAsset root) => graph is FlowGraphAsset && (root is NpcGraphAsset || Graph?.Definitions.OfType<NpcActionDefinition>().Any(a=>a.ExecutionGraph==graph)==true)
        ? new[]{typeof(NpcMoveToTargetAction),typeof(NpcWaitAction),typeof(NpcRoamAction),typeof(NpcAbilityAction),typeof(NpcRememberAction),typeof(NpcAdjustRelationAction),typeof(NpcAdjustAffectAction)} : Array.Empty<Type>();
    readonly List<string> _trace=new(),_scores=new();
    readonly Dictionary<string,NpcBoundAction> _grounded=new(StringComparer.Ordinal);
    readonly Queue<NpcBoundAction> _plan=new();
    readonly HashSet<string> _seenPoints=new(StringComparer.Ordinal);
    readonly Dictionary<string,CharacterValue> _bindings=new(StringComparer.Ordinal);
    readonly List<INpcResourceLease> _leases=new();
    readonly Dictionary<string,double> _goalBlockedUntil=new(StringComparer.Ordinal);
    NpcPlanSearch _search;
    NpcPlanSpec _selectedPlan;
    NpcGoalSpec _goal;
    NpcBoundAction _current;
    FlowGraphRuntime _execution;
    double _senseAt,_selectAt,_retryAt,_committedAt,_actionElapsed;
    ISaveModule _save;
    bool _registered,_committed;
    public override void OnInit()
    {
        try
        {
            Character=Owner.GetComponent<CharacterDataComponent3D>();
            NpcProgram.Require(Character?.State!=null,"AI 需要已初始化的通用角色数据组件。");
            Program=NpcProgram.Compile(Graph,Character.Profile);
            foreach(var parameter in Character.State.Profile.Parameters)Program.Parameters[parameter.Key]=parameter.Value;
            Decision=new(Program,State);Character.Restored+=CharacterRestored;
            Navigation=Owner.GetComponent<NpcNavigationComponent3D>();
            _save=ModuleSystem.GetModule<ISaveModule>();if(Persist)_save.RegisterSection(this);
            Register();Record("Initialized");
        }
        catch(Exception error){Running=false;LastFailure=error.Message;GD.PushError($"NPC {ActorId}: {error.Message}");}
    }
    void Register()
    {
        World=AiWorldComponent3D.Find(Owner);if(World==null)return;
        if(World.Actors.TryGetValue(ActorId,out var old)&&old!=this)throw new InvalidOperationException("Duplicate NPC identity: "+ActorId);
        World.Actors[ActorId]=this;_registered=true;
    }
    public override void OnUpdate(double delta)
    {
        if(Program==null||State==null)return;
        if(!_registered)Register();if(World==null)return;
        Decision.Refresh();
        if(!Running){if(_current!=null||_search!=null||_plan.Count>0||_bindings.Count>0)CancelPlan("Stopped");return;}
        var player=Owner.GetTree().CurrentScene?.GetNodeOrNull<Node3D>("Player");
        if(player!=null){float distance=player.GlobalPosition.DistanceTo(Owner.GlobalPosition);bool next=IsAbstract?distance>Program.Policy.Detailed:distance>Program.Policy.Abstract;if(next!=IsAbstract){CancelPlan("LodChanged");IsAbstract=next;}}
        if(World.RuntimeSeconds>=_senseAt){_senseAt=World.RuntimeSeconds+Program.SenseInterval;Sense();}
        if(World.RuntimeSeconds>=_selectAt){_selectAt=World.RuntimeSeconds+.5;SelectGoal();}
        if(_current!=null)TickExecution(delta);
        else if(_plan.Count>0)BeginAction(_plan.Dequeue());
        else if(_search==null&&World.RuntimeSeconds>=_retryAt)BeginPlanning();
    }
    void Sense()
    {
        _seenPoints.Clear();
        foreach(var p in World.Points.Values)
        {
            if(Program.PointTags.Length>0&&!Program.PointTags.Any(p.Tags.Contains))continue;
            if(Program.KnownPoints.Contains(p.PointId)||Owner.GlobalPosition.DistanceTo(p.UsePosition)<=Program.ViewRange&&(!Program.LineOfSight||CanSee(p.UsePosition)))_seenPoints.Add(p.PointId);
        }
    }
    public bool PerceivedPoint(string id)=>_seenPoints.Contains(id);
    public bool CanSee(Vector3 position)
    {
        var query=PhysicsRayQueryParameters3D.Create(Owner.GlobalPosition+Vector3.Up*.25f,position+Vector3.Up*.3f);
        if(Owner.GetNodeOrNull<CollisionObject3D>("PhysicsBody") is {} body)query.Exclude=new(){body.GetRid()};
        return Owner.GetWorld3D().DirectSpaceState.IntersectRay(query).Count==0;
    }
    public CharacterValue Read(string key)=>key switch
    {
        "world.hour"=>CharacterValue.Numeric((World?.Clock.TotalHours??12)%24),
        "world.rain"=>CharacterValue.Numeric(World?.RainAmount??0),
        _=>Decision?.Read(key)??default
    };
    void SelectGoal()
    {
        _scores.Clear();NpcGoalSpec best=null;double bestScore=0,currentScore=0;
        foreach(var goal in Program.Goals)
        {
            bool active=goal.Activation==null||goal.Activation.Evaluate(Read,Program.Parameter).IsTrue;
            bool done=goal.Completion.Evaluate(Read,Program.Parameter).IsTrue;
            var score=goal.Utility.Evaluate(Read,Program.Parameter);
            bool blocked=_goalBlockedUntil.GetValueOrDefault(goal.Id)>World.RuntimeSeconds;
            double value=active&&!done&&!blocked&&score.Type==CharacterValueType.Number?Math.Max(0,score.Number):0;
            _scores.Add($"{goal.Name}: {value:0.###} · {(done?"已完成":blocked?"失败冷却":!active?"条件不满足":score.Type!=CharacterValueType.Number?"评分未知":"可选")}");
            if(goal==_goal)currentScore=value;
            if(value>bestScore){best=goal;bestScore=value;}
        }
        if(best==_goal)return;
        if(_goal!=null&&currentScore>0&&(World.RuntimeSeconds-_committedAt<Program.Policy.Commitment||bestScore<currentScore+Program.Policy.Margin))return;
        CancelPlan("GoalChanged");_goal=best;_committedAt=World.RuntimeSeconds;
        if(best!=null)Record("Goal",best.Name);
    }
    public GoapWorldState Snapshot()
    {
        var facts=Program.KnownKeys.Select(k=>new KeyValuePair<string,GoapValue>(k,Program.Encode(k,Read(k)))).ToList();
        foreach(var adapter in NpcBindingAdapters.All)facts.AddRange(adapter.Capture(this));
        if(_selectedPlan!=null)foreach(var binding in _bindings)facts.Add(new(NpcProgram.BindingKey(_selectedPlan.Id,binding.Key),Program.Encode(NpcProgram.BindingKey(_selectedPlan.Id,binding.Key),binding.Value)));
        return new(facts);
    }
    void BeginPlanning()
    {
        if(_goal==null)return;
        if(_goal.Actions.SelectMany(a=>a.Inputs).Concat(_goal.Plans.SelectMany(p=>p.Bindings)).Any(i=>i.Source==NpcBindingSource.Selector&&Program.TargetSelectors[i.SelectorId].RequireReachable)&&Navigation?.MapReady!=true){_retryAt=World.RuntimeSeconds+.1;return;}
        _grounded.Clear();var snapshot=Snapshot();
        var points=NpcBindingAdapters.All.SelectMany(a=>a.Candidates(this)).ToArray();
        try{foreach(var bound in NpcTargetQuery.Ground(Program,_goal,snapshot,points))_grounded[bound.Search.Id]=bound;}
        catch(InvalidOperationException e){Fail(e.Message);return;}
        _search=new(Program,_goal,snapshot,_grounded.Values.Select(a=>new KeyValuePair<string,GoapAction>(a.PlanActionKey,a.Search)),State.Revision);
    }
    public void AdvancePlanning(double milliseconds)
    {
        if(_search==null||!Running)return;
        if(_search.SourceVersion!=State.Revision){CancelPlan("StateChanged");return;}
        var status=_search.Advance(Program.Policy.Slice,milliseconds);
        if(status==GoapSearchStatus.Pending)return;
        if(status is GoapSearchStatus.Found or GoapSearchStatus.AlreadySatisfied)
        {
            _selectedPlan=_search.ChosenPlan;
            foreach(var step in _search.Result.Steps)_plan.Enqueue(_grounded[step.Id]);
            Record("Plan",$"{_selectedPlan?.Name}: {_plan.Count} steps, {_search.Expanded} expanded");_search=null;
        }
        else {string reason=_search.FailureReason;_search=null;Fail(reason);}
    }
    void Fail(string reason,bool blockGoal=true)
    {
        LastFailure=reason;
        if(blockGoal&&_goal!=null)_goalBlockedUntil[_goal.Id]=(World?.RuntimeSeconds??0)+(Program?.Policy.Retry??2);
        CancelPlan(reason);_retryAt=(World?.RuntimeSeconds??0)+(blockGoal?0:Program?.Policy.Retry??2);_selectAt=0;
    }
    public void CancelPlan(string reason="Replan")
    {
        StopExecution();_search?.Invalidate();_search=null;_plan.Clear();_selectedPlan=null;_bindings.Clear();
        ReleaseResources();_current=null;Navigation?.Stop();Record("Cancelled",reason);
    }
    public void Record(string kind,string detail=""){_trace.Add($"{World?.RuntimeSeconds??0:0.0} {kind} {detail}");while(_trace.Count>128)_trace.RemoveAt(0);}
    public string Describe()=>$"{ActorId}  {CurrentGoal} → {CurrentAction}  完成 {CompletedActions}  LOD {(IsAbstract?"抽象":"详细")}\n"+string.Join("\n",_bindings.Select(b=>b.Key+" = "+b.Value))+"\n"+string.Join("\n",_scores)+"\n"+string.Join("\n",_trace.TakeLast(8));
    void CharacterRestored(){CancelPlan("CharacterRestored");Decision?.Refresh();_goal=null;_selectAt=_retryAt=0;}
    public JsonObject Capture()=>Decision?.Capture()??new();
    public void Restore(JsonObject data,int version){if(version!=SchemaVersion||State==null)return;Decision.Restore(data);CancelPlan("Restored");_goal=null;_selectAt=_retryAt=0;}
    public override void OnDestroy(){if(Character!=null)Character.Restored-=CharacterRestored;CancelPlan("Destroyed");if(_registered&&World?.Actors.GetValueOrDefault(ActorId)==this)World.Actors.Remove(ActorId);if(Persist)_save?.UnregisterSection(this);World=null;_registered=false;}
}
