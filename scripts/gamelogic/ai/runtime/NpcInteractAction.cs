namespace GameLogic.AI;

[GraphCallable("Interact With Object", "NPC/能力", ChineseName="执行对象交互")]
[GraphProvidedBy(typeof(AiAgentComponent3D))]
public sealed class NpcInteractAction : GraphInstantAction
{
    public NpcInputReference Input {get;set;}=new();
    public string OperationId {get;set;}="";
    public NpcGraphNumber Amount {get;set;}=NpcGraphNumber.Constant(1);
#if TOOLS
    public override Godot.Control CreateEditUI(GraphEditorContext context)=>NpcGraphValueEditor.Interaction(this,context);
#endif
    protected override bool Run(GraphActionInvocation invocation)=>Amount.TryRead(invocation.Execution,out double amount)
        &&invocation.Execution.GetUserData<NpcActionContext>()?.QueueInteraction(Input.Name,OperationId,amount)==true;
}
