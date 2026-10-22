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
        var call=invocation.Execution.GetUserData<NpcActionContext>();bool started=false;
        var progress=call?.Progress(this)??new();
        return new GraphDelegateTask(delta=>
        {
            if(call?.Point(Input.Name)==null||!call.Point(Input.Name).IsActive||!GodotObject.IsInstanceValid(call.Point(Input.Name).Owner))return BehaviorTreeStatus.Failure;
            var agent=call.Agent;var target=call.Point(Input.Name);var nav=agent.Navigation;
            var a=agent.Owner.GlobalPosition;var b=target.UsePosition;
            if(progress.Complete)return BehaviorTreeStatus.Success;
            if(target.ContainsPosition(a)){progress.Complete=true;nav?.Stop();return BehaviorTreeStatus.Success;}
            if(nav==null)return BehaviorTreeStatus.Failure;
            if(agent.IsAbstract)
            {
                if(!started)
                {
                    if(!progress.Duration.HasValue)
                    {
                        if(!nav.TryEstimateBetween(a,b,out double distance))return BehaviorTreeStatus.Failure;
                        progress.Duration=distance/Math.Max(.1,agent.Owner.GetComponent<CharacterMovementComponent3D>()?.Profile?.MoveSpeed??2.2);
                    }
                    started=true;
                }
                progress.Elapsed+=delta;if(progress.Elapsed<progress.Duration.Value)return BehaviorTreeStatus.Running;
                var movement=agent.Owner.GetComponent<CharacterMovementComponent3D>();
                agent.Owner.GlobalPosition=b+Vector3.Up*(movement?.HalfHeight??.36f);movement?.SyncBodyToOwner();
                progress.Complete=true;return BehaviorTreeStatus.Success;
            }
            if(!started){if(!nav.BeginPosition(b,target.PointId,.12f))return BehaviorTreeStatus.Failure;started=true;}
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

[GraphCallable("Wait Action Duration", "NPC/能力", ChineseName="等待图内时长")]
[GraphProvidedBy(typeof(AiAgentComponent3D))]
public sealed class NpcWaitAction : GraphSharedAction
{
    public NpcGraphNumber Seconds {get;set;}=NpcGraphNumber.Constant(2);
#if TOOLS
    public override Control CreateEditUI(GraphEditorContext context)
    {
        var root=new VBoxContainer();root.AddChild(new Label{Text="等待秒数"});root.AddChild(Seconds.CreateEditUI(context));return root;
    }
#endif
    public override GraphActionTask CreateTask(GraphActionInvocation invocation)
    {
        var progress=invocation.Execution.GetUserData<NpcActionContext>()?.Progress(this)??new();
        bool valid=Seconds.TryRead(invocation.Execution,out double seconds)&&seconds>=0;
        progress.Duration??=seconds;
        return new GraphDelegateTask(delta=>
        {
            if(!valid)return BehaviorTreeStatus.Failure;if(progress.Complete)return BehaviorTreeStatus.Success;
            progress.Elapsed+=delta;progress.Complete=progress.Elapsed>=progress.Duration.Value;
            return progress.Complete?BehaviorTreeStatus.Success:BehaviorTreeStatus.Running;
        });
    }
}

[GraphCallable("Remember", "NPC/能力", ChineseName="记录记忆")]
[GraphProvidedBy(typeof(AiAgentComponent3D))]
public sealed class NpcRememberAction : GraphInstantAction
{
    public string Category {get;set;}="";
    public string Detail {get;set;}="";
    public NpcInputReference SubjectInput {get;set;}=new(){Name=""};
    protected override bool Run(GraphActionInvocation invocation)
    {
        var c=invocation.Execution.GetUserData<NpcActionContext>();
        return c!=null&&c.Agent.Decision.Memory.Remember(new(Category,c.Input(SubjectInput.Name).Symbol??"",c.Agent.World.Clock.TotalHours,Detail));
    }
}

[GraphCallable("Adjust Relationship", "NPC/能力", ChineseName="改变关系维度")]
[GraphProvidedBy(typeof(AiAgentComponent3D))]
public sealed class NpcAdjustRelationAction : GraphInstantAction
{
    public string ContactParameter {get;set;}="";
    public string Dimension {get;set;}="";
    public double Amount {get;set;}
#if TOOLS
    public override Control CreateEditUI(GraphEditorContext context)=>NpcCharacterFeatureActionEditor.Build(this,context);
#endif
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
#if TOOLS
    public override Control CreateEditUI(GraphEditorContext context)=>NpcCharacterFeatureActionEditor.Build(this,context);
#endif
    protected override bool Run(GraphActionInvocation invocation)
    {
        var c=invocation.Execution.GetUserData<NpcActionContext>();
        return c!=null&&c.Agent.State.Affect.Adjust(Dimension,Amount);
    }
}
