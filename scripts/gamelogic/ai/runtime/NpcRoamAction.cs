using System;
using Godot;
namespace GameLogic.AI;

/// <summary>Local movement/animation capability. Weather and business effects belong to graph configuration.</summary>
[GraphCallable("Roam Around Origin", "NPC/能力", ChineseName="附近活动或原地踏步")]
[GraphProvidedBy(typeof(AiAgentComponent3D))]
public sealed class NpcRoamAction : GraphSharedAction
{
    public NpcInputReference Input {get;set;}=new(){Name=""};
    public double Radius {get;set;}=.6;
    public double DurationOverride {get;set;}=-1;
    public string Animation {get;set;}="walk";
    public int AnimationPriority {get;set;}=50;
    public override void Validate(GraphAsset graph,string nodeId,GraphValidationResult result)
    {
        if(!double.IsFinite(Radius)||Radius<0||!double.IsFinite(DurationOverride)||DurationOverride< -1||string.IsNullOrWhiteSpace(Animation))
            result.AddError("附近活动的范围、时长或动画配置无效。",nodeId);
    }
    public override GraphActionTask CreateTask(GraphActionInvocation invocation)
    {
        var call=invocation.Execution.GetUserData<NpcActionContext>();
        var animation=call?.Agent.Owner.GetComponent<CharacterAnimationComponent3D>();
        var navigation=call?.Agent.Navigation;
        string request="NpcActivity:"+Guid.NewGuid().ToString("N");
        bool started=false;double elapsed=0;int direction=0;
        Vector3 origin=default;float radius=(float)Radius;
        void Stop(){navigation?.Stop();animation?.ClearAnimationRequest(request);}
        return new GraphDelegateTask(delta=>
        {
            if(call==null||animation?.PlaybackBackend?.HasAnimation(Animation)!=true)return BehaviorTreeStatus.Failure;
            if(!started)
            {
                navigation?.Stop();
                float half=call.Agent.Owner.GetComponent<CharacterMovementComponent3D>()?.HalfHeight??.36f;
                origin=call.Point(Input.Name)?.UsePosition??call.Agent.Owner.GlobalPosition-Vector3.Up*half;
                animation.RequestAnimation(request,Animation,AnimationPriority,restartIfPlaying:false);started=true;
            }
            elapsed+=delta;
            if(elapsed>=(DurationOverride<0?call.Action.Duration:DurationOverride)){Stop();return BehaviorTreeStatus.Success;}
            if(radius<=.01f||call.Agent.IsAbstract)return BehaviorTreeStatus.Running;
            if(navigation==null||!navigation.MapReady)return BehaviorTreeStatus.Failure;
            if(navigation.Status==NpcNavigationStatus.Moving)return BehaviorTreeStatus.Running;
            // Try alternate nearby positions when one side is blocked; never leave the configured activity area.
            for(int attempt=0;attempt<8;attempt++)
            {
                float angle=(direction++%8)*Mathf.Pi/4;
                var destination=origin+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                if(call.Point(Input.Name) is {} area&&!area.ContainsPosition(destination+Vector3.Up*(call.Agent.Owner.GetComponent<CharacterMovementComponent3D>()?.HalfHeight??.36f)))continue;
                var offset=destination-call.Agent.Owner.GlobalPosition;
                if(new Vector2(offset.X,offset.Z).Length()<.2f)continue;
                if(navigation.TryEstimateBetween(call.Agent.Owner.GlobalPosition,destination,out _)&&navigation.BeginPosition(destination,"local_activity",.12f))return BehaviorTreeStatus.Running;
            }
            Stop();return BehaviorTreeStatus.Failure;
        },Stop);
    }
}
