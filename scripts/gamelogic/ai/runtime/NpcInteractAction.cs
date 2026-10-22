namespace GameLogic.AI;

[GraphCallable("Interact With Object", "NPC/能力", ChineseName="执行对象交互")]
[GraphProvidedBy(typeof(AiAgentComponent3D))]
public sealed class NpcInteractAction : GraphInstantAction
{
    public NpcInputReference Input {get;set;}=new();
    public string OperationId {get;set;}="";
    protected override bool Run(GraphActionInvocation invocation)=>invocation.Execution.GetUserData<NpcActionContext>()?.QueueInteraction(Input.Name,OperationId)==true;
}
