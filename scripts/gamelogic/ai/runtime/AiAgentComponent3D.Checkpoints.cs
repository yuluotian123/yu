using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using GameLogic.Goap;

namespace GameLogic.AI;

public partial class AiAgentComponent3D
{
    string _checkpointSignature="",_planInstanceId="",_actionInstanceId="",_lastCommittedInstanceId="";
    NpcPlanCheckpoint _pendingCheckpoint;
    double _checkpointNavigationDeadline;
    public string CheckpointStatus {get;private set;}="无检查点";

    static NpcSavedAction SaveAction(NpcBoundAction bound)=>new()
    {
        ActionId=bound.Action.Id,Targets=new(bound.Candidates,StringComparer.Ordinal),
        Inputs=bound.Resolved==null?null:new(bound.Resolved,StringComparer.Ordinal)
    };
    public JsonObject Capture()
    {
        var data=Decision?.Capture()??new();
        data["completed_actions"]=CompletedActions;data["last_committed_action"]=_lastCommittedInstanceId;
        data["activity"]=CaptureActivity();
        NpcPlanCheckpoint checkpoint=_pendingCheckpoint;
        if(checkpoint==null&&_selectedPlan!=null&&_goal!=null&&(_current!=null||_plan.Count>0))
        {
            var current=_current==null?null:SaveAction(_current);
            if(current!=null)
            {
                current.InstanceId=_actionInstanceId;current.Elapsed=_actionElapsed;current.Committed=_committed;
                current.Execution=_execution?.Context.GetUserData<NpcActionContext>()?.CaptureExecution();
                if(current.Execution==null&&!current.Committed)
                {data["checkpoint_status"]="Replan:UnsupportedExecution";return data;}
            }
            checkpoint=new()
            {
                Signature=_checkpointSignature,Actor=ActorId,Level=Read("self.level").Symbol??"",Goal=_goal.Id,Plan=_selectedPlan.Id,
                InstanceId=_planInstanceId,Abstract=IsAbstract,Bindings=new(_selectorBindings,StringComparer.Ordinal),
                Current=current,Remaining=_plan.Select(SaveAction).ToList()
            };
        }
        if(checkpoint!=null)data["checkpoint"]=JsonSerializer.SerializeToNode(checkpoint);
        data["checkpoint_status"]=checkpoint==null?"Idle":"Recoverable";
        return data;
    }
    public void Restore(JsonObject data,int version)
    {
        CancelPlan("Restored");_goal=null;_selectAt=_retryAt=0;_goalBlockedUntil.Clear();
        _checkpointNavigationDeadline=0;
        if(State==null||version!=SchemaVersion){CheckpointStatus="Replan:UnsupportedSchema";return;}
        Decision.Restore(data);CompletedActions=Math.Max(0,data?["completed_actions"]?.GetValue<int>()??0);
        RestoreActivity(data?["activity"] as JsonObject);
        _lastCommittedInstanceId=data?["last_committed_action"]?.GetValue<string>()??"";
        try
        {
            _pendingCheckpoint=data?["checkpoint"]?.Deserialize<NpcPlanCheckpoint>();
            CheckpointStatus=_pendingCheckpoint==null?(data?["checkpoint_status"]?.GetValue<string>()??"Idle"):"等待现场就绪";
            if(_pendingCheckpoint==null&&CheckpointStatus.StartsWith("Replan:",StringComparison.Ordinal))Record("CheckpointCancelled",CheckpointStatus);
        }
        catch(Exception e) when(e is JsonException or ArgumentException or InvalidOperationException)
        {CheckpointRejected("InvalidData:"+e.Message);}
    }
    void CheckpointRejected(string reason)
    {
        CancelPlan("Checkpoint:"+reason);_goal=null;_selectAt=_retryAt=0;
        CheckpointStatus="Replan:"+reason;LastFailure="Checkpoint:"+reason;Record("CheckpointCancelled",reason);
    }
    static bool ValidValue(CharacterValue value)=>Enum.IsDefined(value.Type)&&value.Type!=CharacterValueType.Unknown
        &&double.IsFinite(value.Number)&&(value.Type!=CharacterValueType.Boolean||value.Number is 0 or 1)
        &&(value.Type!=CharacterValueType.Symbol||value.Symbol!=null);
    void TryResumeCheckpoint()
    {
        var saved=_pendingCheckpoint;
        try
        {
            NpcProgram.Require(saved.Version==1&&saved.Signature==_checkpointSignature&&saved.Actor==ActorId
                &&saved.Level==(Read("self.level").Symbol??"")&&Guid.TryParseExact(saved.InstanceId,"N",out _),"DefinitionOrWorldChanged");
            var goal=Program.Goals.SingleOrDefault(g=>g.Id==saved.Goal);
            var plan=goal?.Plans.SingleOrDefault(p=>p.Id==saved.Plan);
            NpcProgram.Require(plan!=null&&saved.Remaining!=null&&saved.Bindings!=null&&saved.Remaining.Count+(saved.Current==null?0:1)<=Program.Policy.Depth,"PlanMissingOrInvalid");
            NpcProgram.Require(Meets(goal.Activation,Snapshot())&&!Meets(goal.Completion,Snapshot()),"GoalNoLongerActive");
            var all=saved.Current==null?saved.Remaining:saved.Remaining.Prepend(saved.Current).ToList();
            var expectedBindings=new Dictionary<string,CharacterValue>(StringComparer.Ordinal);
            NpcBoundAction Bind(NpcSavedAction step)
            {
                var action=goal.Actions.SingleOrDefault(a=>a.Id==step.ActionId&&plan.ActionIds.Contains(a.Id));
                NpcProgram.Require(action!=null&&step.Targets!=null,"ActionMissing");
                var targets=action.Inputs.Where(i=>i.IsTarget).ToArray();
                NpcProgram.Require(step.Targets.Count==targets.Length&&targets.All(i=>step.Targets.TryGetValue(i.Name,out var v)&&ValidValue(v)&&v.Type==i.Type&&!string.IsNullOrWhiteSpace(v.Symbol)),"InvalidTargets");
                foreach(var input in targets)
                {
                    var value=step.Targets[input.Name];
                    if(input.SelectionNodeId.Length==0)continue;
                    NpcProgram.Require(!expectedBindings.TryGetValue(input.SelectionNodeId,out var previous)||previous==value,"SharedTargetChanged");
                    expectedBindings[input.SelectionNodeId]=value;
                }
                if(step.Inputs!=null)NpcProgram.Require(step.Inputs.Count==action.Inputs.Count&&action.Inputs.All(i=>step.Inputs.TryGetValue(i.Name,out var v)
                    &&ValidValue(v)&&v.Type==i.Type&&(!i.IsTarget||v==step.Targets[i.Name])),"InvalidFrozenInputs");
                return new(action,plan,new Dictionary<string,CharacterValue>(step.Targets,StringComparer.Ordinal),null,
                    step.Inputs==null?null:new Dictionary<string,CharacterValue>(step.Inputs,StringComparer.Ordinal));
            }
            var bound=all.Select(Bind).ToArray();
            // Bindings may also have been selected by an already completed step of this plan.
            var selectorNodes=goal.Actions.Where(a=>plan.ActionIds.Contains(a.Id)).SelectMany(a=>a.Inputs).Where(i=>i.SelectionNodeId.Length>0).Select(i=>i.SelectionNodeId).ToHashSet(StringComparer.Ordinal);
            NpcProgram.Require(saved.Bindings.All(b=>selectorNodes.Contains(b.Key)&&ValidValue(b.Value)&&b.Value.Type==CharacterValueType.Symbol&&!string.IsNullOrWhiteSpace(b.Value.Symbol))
                &&expectedBindings.All(b=>saved.Bindings.GetValueOrDefault(b.Key)==b.Value),"InvalidSelectorBindings");
            bool committed=saved.Current!=null&&(saved.Current.Committed||saved.Current.InstanceId==_lastCommittedInstanceId);
            if(saved.Current!=null)
            {
                NpcProgram.Require(Guid.TryParseExact(saved.Current.InstanceId,"N",out _)&&double.IsFinite(saved.Current.Elapsed)&&saved.Current.Elapsed>=0
                    &&saved.Current.Elapsed<=bound[0].Action.Timeout&&saved.Current.Inputs!=null,"InvalidActionProgress");
                NpcProgram.Require(!saved.Current.Committed||saved.Current.InstanceId==_lastCommittedInstanceId,"InvalidCommitMarker");
                if(!committed)
                {
                    NpcProgram.Require(saved.Current.Execution!=null&&NpcCheckpointLayout.Create(bound[0].Action.Execution)!=null,"UnsupportedExecution");
                }
            }
            var first=bound.Skip(committed?1:0).FirstOrDefault();
            var firstLayout=first==null?null:NpcCheckpointLayout.Create(first.Action.Execution);
            bool needsNavigation=firstLayout?.Steps.Any(step=>step.Key is NpcMoveToTargetAction
                &&(saved.Current==null||committed||saved.Current.Execution.Progress?.GetValueOrDefault(step.Value)?.Complete!=true))==true;
            if(needsNavigation&&!IsWorldProxy)
            {
                NpcProgram.Require(Navigation!=null,"NavigationMissing");
                if(!Navigation.MapReady)
                {
                    if(_checkpointNavigationDeadline==0)_checkpointNavigationDeadline=Now+5;
                    NpcProgram.Require(Now<_checkpointNavigationDeadline,"NavigationUnavailable");
                    CheckpointStatus="等待导航就绪";return;
                }
            }
            _pendingCheckpoint=null;_goal=goal;_selectedPlan=plan;_planInstanceId=saved.InstanceId;
            // Execution progress survives handoff; simulation precision belongs to the current host.
            IsAbstract=ManagedActor!=null?IsWorldProxy:saved.Abstract;
            foreach(var binding in saved.Bindings)_selectorBindings.Add(binding.Key,binding.Value);
            foreach(var step in bound.Skip(saved.Current==null?0:1))_plan.Enqueue(step);
            foreach(var step in bound.Skip(committed?1:0))foreach(var input in step.Action.Inputs.Where(i=>i.IsTarget))
            {
                var selector=Program.TargetSelectors[input.SelectorId];string id=step.Candidates[input.Name].Symbol;
                // A source is queried when selecting, not on every tick. Perception/current-place
                // may legitimately change after selection; execution revalidates the bound object.
                RecordQuery(selector,new[]{id});
            }
            _committedAt=Now;_selectAt=Now+.5;CheckpointStatus="已恢复";
            if(saved.Current!=null&&!committed)
            {
                BeginAction(bound[0],saved.Current);
                if(_current==null){CheckpointStatus="Replan:"+LastFailure;Record("CheckpointCancelled",LastFailure);return;}
            }
            else if(_plan.Count==0){_selectedPlan=null;_selectorBindings.Clear();}
            Record("CheckpointResumed",saved.InstanceId+(committed?" · committed action skipped":""));
        }
        catch(Exception e) when(e is JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or OverflowException)
        {CheckpointRejected(e.Message);}
    }
}
