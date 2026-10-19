using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameLogic.Goap;
namespace GameLogic.AI;

public partial class AiAgentComponent3D
{
    void ReleaseResources()
    {
        foreach(var lease in _leases)try{lease.Dispose();}catch(Exception e){Record("ResourceReleaseError",e.Message);}
        _leases.Clear();
    }
    void StopExecution()
    {
        var execution=_execution;_execution=null;
        try{execution?.Stop();}catch(Exception e){Record("ExecutionCleanupError",e.Message);}
    }
    GoapWorldState LiveContext(NpcBoundAction bound)=>Program.Contextual(Snapshot(),bound,physical:true);
    bool ValidateInputs(NpcBoundAction bound,GoapWorldState context)
    {
        if(context==null)return false;
        foreach(var input in bound.Action.Inputs)
        {
            var value=Program.Read(context,"input."+input.Name);
            if(NpcBindingAdapters.Find(input.Adapter) is {} adapter&&!adapter.Validate(this,value))return false;
            var source=Program.Source(bound,input);
            if(source.Source!=NpcBindingSource.Selector)continue;
            if(!NpcBindingAdapters.Find(input.Adapter).Matches(this,value,Program.TargetSelectors[source.SelectorId]))return false;
        }
        return true;
    }
    bool Meets(NpcExpression expression,GoapWorldState context)=>expression==null||expression.Evaluate(k=>Program.Read(context,k),Program.Parameter).IsTrue;
    internal void BeginAction(NpcBoundAction bound)
    {
        if(!Meets(_selectedPlan?.Eligibility,Snapshot())){Fail("PlanEligibilityChanged");return;}
        var context=LiveContext(bound);
        if(!ValidateInputs(bound,context)){Fail("InputUnavailable",false);return;}
        if(!Meets(bound.Action.Precondition,context)){Fail("PreconditionChanged");return;}
        var inputs=new ReadOnlyDictionary<string,NpcValue>(bound.Action.Inputs.ToDictionary(i=>i.Name,i=>Program.Read(context,"input."+i.Name),StringComparer.Ordinal));
        bound=bound with{Resolved=inputs};
        foreach(var input in bound.Action.Inputs.Where(i=>i.Source==NpcBindingSource.PlanVariable))
        {
            var value=inputs[input.Name];
            if(_bindings.TryGetValue(input.Variable,out var old)&&old!=value){Fail("PlanVariableChanged");return;}
            _bindings[input.Variable]=value;
        }
        var acquired=new HashSet<(string,NpcValue,string)>();
        foreach(var resource in bound.Action.Resources)
        {
            var input=bound.Action.Inputs.First(i=>i.Name==resource.Input);var value=inputs[input.Name];
            if(!acquired.Add((input.Adapter,value,resource.Capability)))continue;
            INpcResourceLease lease;
            try{lease=NpcBindingAdapters.Find(input.Adapter).Acquire(this,value,resource.Capability);}
            catch(Exception e){Fail("ResourceException:"+e.Message);return;}
            if(lease==null){Fail("ResourceBusy",false);return;}_leases.Add(lease);
        }
        _current=bound;_actionElapsed=0;_committed=false;LastFailure="";
        var call=new GraphExecutionContext(bound.Action.Execution,new GraphBlackboardRuntime());call.UserData.Add(Owner);call.UserData.Add(new NpcActionContext(this,bound.Action,inputs));
        _execution=new(bound.Action.Execution,call);
        try{if(!_execution.Start()){Fail("ExecutionGraphFailed");return;}}
        catch(Exception e){Fail("ExecutionException:"+e.Message);return;}
        Record("Action",bound.Action.Name+" · "+string.Join(", ",inputs.Select(v=>v.Key+"="+v.Value)));
    }
    internal void TickExecution(double delta)
    {
        if(!Meets(_selectedPlan?.Eligibility,Snapshot())){Fail("PlanEligibilityChanged");return;}
        _actionElapsed+=delta;if(_actionElapsed>_current.Action.Timeout){Fail("Timeout");return;}
        var context=LiveContext(_current);
        if(!ValidateInputs(_current,context)){Fail("InputChanged");return;}
        if(_leases.Any(l=>!l.Renew())){Fail("ResourceLost");return;}
        try{_execution.Update(delta);}catch(Exception e){Fail("ExecutionException:"+e.Message);return;}
        if(!_execution.IsCompleted)return;
        if(!_execution.ReturnLabels.Contains("Success")){Fail("ExecutionFailed:"+Navigation?.Failure);return;}
        if(_committed)return;
        context=LiveContext(_current);
        if(!ValidateInputs(_current,context)||!Meets(_current.Action.Precondition,context)||!Meets(_current.Action.Success,context)||
            !Program.TryEffects(context,_current.Action.Results,out var result,_current.Action)){Fail("ResultValidationFailed");return;}
        // Validate the complete transaction and all leases before changing any participant.
        if(_leases.Any(l=>!l.Renew())){Fail("ResourceLost");return;}
        State.Apply(result,_current.Action.Results.Select(e=>e.Key));
        foreach(var effect in _current.Action.Results)
        {
            if(!NpcProgram.SplitInputMember(_current.Action,effect.Key,out var input,out var member))continue;
            var value=Program.Read(context,"input."+input.Name);string key=Program.EffectKey(context,_current.Action,effect.Key);
            NpcBindingAdapters.Find(input.Adapter).Commit(this,value,member,Program.Read(result,key));
        }
        _committed=true;CompletedActions++;Record("Completed",_current.Action.Name);
        StopExecution();_current=null;ReleaseResources();Navigation?.Stop();Sense();_selectAt=0;
        if(_plan.Count==0){_bindings.Clear();_selectedPlan=null;}
    }
}
