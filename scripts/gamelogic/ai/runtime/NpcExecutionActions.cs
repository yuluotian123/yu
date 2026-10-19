using System;
using System.Linq;
using Godot;
namespace GameLogic.AI;

[GraphCallable("Move To Target", "NPC/能力", ChineseName="前往输入地点")]
[GraphProvidedBy(typeof(AiAgentComponent3D))]
public sealed class NpcMoveToTargetAction : GraphSharedAction
{
    public NpcInputReference Input {get;set;}=new();
    public override GraphActionTask CreateTask(GraphActionInvocation invocation)
    {
        var call=invocation.Execution.GetUserData<NpcActionContext>();bool started=false;double elapsed=0,travel=0;
        return new GraphDelegateTask(delta=>
        {
            if(call?.Point(Input.Name)==null||!call.Point(Input.Name).IsActive||!GodotObject.IsInstanceValid(call.Point(Input.Name).Owner))return BehaviorTreeStatus.Failure;
            var agent=call.Agent;var target=call.Point(Input.Name);var nav=agent.Navigation;
            var a=agent.Owner.GlobalPosition;var b=target.UsePosition;
            if(target.ContainsPosition(a)){nav?.Stop();return BehaviorTreeStatus.Success;}
            if(nav==null)return BehaviorTreeStatus.Failure;
            if(agent.IsAbstract)
            {
                if(!started)
                {
                    if(!nav.TryEstimateBetween(a,b,out double distance))return BehaviorTreeStatus.Failure;
                    travel=distance/Math.Max(.1,agent.Owner.GetComponent<CharacterMovementComponent3D>()?.Profile?.MoveSpeed??2.2);started=true;
                }
                elapsed+=delta;if(elapsed<travel)return BehaviorTreeStatus.Running;
                var movement=agent.Owner.GetComponent<CharacterMovementComponent3D>();
                agent.Owner.GlobalPosition=b+Vector3.Up*(movement?.HalfHeight??.36f);movement?.SyncBodyToOwner();
                return BehaviorTreeStatus.Success;
            }
            if(!started){if(!nav.BeginPosition(b,target.PointId,target.UseRadius))return BehaviorTreeStatus.Failure;started=true;}
            if(nav.Status is NpcNavigationStatus.Failed or NpcNavigationStatus.Arrived)return BehaviorTreeStatus.Failure;
            return BehaviorTreeStatus.Running;
        },()=>call?.Agent.Navigation?.Stop());
    }
}

[GraphCallable("Activate Ability", "NPC/能力", ChineseName="执行通用技能")]
[GraphProvidedBy(typeof(AiAgentComponent3D))]
public sealed class NpcAbilityAction : GraphSharedAction
{
    public string AbilityId {get;set;}="";
    public override GraphActionTask CreateTask(GraphActionInvocation invocation)
    {
        var system=invocation.Execution.GameObject?.GetComponent<AbilitySystemComponent3D>();bool started=false;ulong version=0;
        return new GraphDelegateTask(delta=>
        {
            if(system==null)return BehaviorTreeStatus.Failure;
            if(!started){if(system.TryActivateAbility(AbilityId,"NpcExecution")!=AbilityActivationResult.Activated)return BehaviorTreeStatus.Failure;started=true;version=system.GetRuntime(AbilityId).ActivationVersion;}
            var runtime=system.GetRuntime(AbilityId);
            if(runtime==null||runtime.ActivationVersion!=version)return BehaviorTreeStatus.Failure;
            return runtime.IsRunning?BehaviorTreeStatus.Running:runtime.IsCompleted?BehaviorTreeStatus.Success:BehaviorTreeStatus.Failure;
        },()=>{if(started&&system?.GetRuntime(AbilityId)?.ActivationVersion==version)system.CancelAbility(AbilityId,"NpcExecutionCancelled");});
    }
}

[GraphCallable("Wait Action Duration", "NPC/能力", ChineseName="等待行动时长")]
[GraphProvidedBy(typeof(AiAgentComponent3D))]
public sealed class NpcWaitAction : GraphSharedAction
{
    public double DurationOverride {get;set;}=-1;
    public override GraphActionTask CreateTask(GraphActionInvocation invocation)
    {
        var call=invocation.Execution.GetUserData<NpcActionContext>();double elapsed=0;
        return new GraphDelegateTask(delta=>
        {if(call==null)return BehaviorTreeStatus.Failure;elapsed+=delta;return elapsed>=(DurationOverride<0?call.Action.Duration:DurationOverride)?BehaviorTreeStatus.Success:BehaviorTreeStatus.Running;});
    }
}

[GraphCallable("Remember", "NPC/能力", ChineseName="记录记忆")]
[GraphProvidedBy(typeof(AiAgentComponent3D))]
public sealed class NpcRememberAction : GraphInstantAction
{
    public string Category {get;set;}="";
    public string Detail {get;set;}="";
    public NpcInputReference SubjectInput {get;set;}=new();
    protected override bool Run(GraphActionInvocation invocation)
    {
        var c=invocation.Execution.GetUserData<NpcActionContext>();
        return c!=null&&c.Agent.State.Memory.Remember(new(Category,c.Input(SubjectInput.Name).Symbol??"",c.Agent.World.Clock.TotalHours,Detail));
    }
}

[GraphCallable("Adjust Relationship", "NPC/能力", ChineseName="改变关系维度")]
[GraphProvidedBy(typeof(AiAgentComponent3D))]
public sealed class NpcAdjustRelationAction : GraphInstantAction
{
    public string ContactParameter {get;set;}="";
    public string Dimension {get;set;}="";
    public double Amount {get;set;}
    protected override bool Run(GraphActionInvocation invocation)
    {
        var c=invocation.Execution.GetUserData<NpcActionContext>();
        return c!=null&&c.Agent.State.Relations.Adjust(c.Agent.Program.Parameter(ContactParameter).Symbol,Dimension,Amount);
    }
}

[GraphCallable("Adjust Affect", "NPC/能力", ChineseName="改变情绪维度")]
[GraphProvidedBy(typeof(AiAgentComponent3D))]
public sealed class NpcAdjustAffectAction : GraphInstantAction
{
    public string Dimension {get;set;}="";
    public double Amount {get;set;}
    protected override bool Run(GraphActionInvocation invocation)
    {
        var c=invocation.Execution.GetUserData<NpcActionContext>();
        if(c==null||!double.IsFinite(Amount)||!c.Agent.State.Affect.Values.TryGetValue(Dimension,out double previous)||!double.IsFinite(previous+Amount))return false;
        c.Agent.State.Affect.Values[Dimension]=previous+Amount;return true;
    }
}
