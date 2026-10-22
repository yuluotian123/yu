#if TOOLS
using System;
using System.Linq;
using Godot;
namespace GameLogic.AI;

public partial class NpcGraphExpressionOwner : RefCounted
{
    public NpcActionDefinition Action;
    public FlowGraphAsset Flow;
}

public static class NpcGraphValueEditor
{
    public static NpcActionDefinition Action(GraphEditorContext context)=>(context.RootGraph as NpcGraphAsset)?.Definitions.OfType<NpcActionDefinition>()
        .FirstOrDefault(a=>a.ExecutionGraph==context.CurrentGraph||context.ParentGraphs.Contains(a.ExecutionGraph));
    public static Control Number(NpcGraphNumber value,GraphEditorContext context)
    {
        if(context.RootGraph is not NpcGraphAsset)return new Label{Text="请从所属 NPC 的 Action 打开执行图，以编辑角色／对象公式。"};
        var root=new VBoxContainer();
        var owner=new NpcGraphExpressionOwner{Action=Action(context),Flow=context.CurrentGraph as FlowGraphAsset};
        root.TreeExiting+=()=>owner.Dispose();
        var preview=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};
        var literal=new SpinBox{MinValue=-1000000,MaxValue=1000000,AllowGreater=true,AllowLesser=true,Step=.001};root.AddChild(literal);
        void Refresh()
        {
            preview.Text=NpcExpressionPresentation.Format(value.ToDefinition(),NpcExpressionEditorCatalog.For(context.RootGraph as NpcGraphAsset,owner));
            literal.Visible=value.Kind==CharacterExpressionKind.Literal;preview.Visible=!literal.Visible;literal.SetValueNoSignal(value.Number);
        }
        literal.ValueChanged+=number=>{value.Number=number;context.CurrentGraph.MarkDirty();};Refresh();root.AddChild(preview);
        var edit=new Button{Text="编辑数值／公式"};root.AddChild(edit);
        edit.Pressed+=()=>
        {
            var dialog=new NpcExpressionDialog(context.RootGraph as NpcGraphAsset,owner,CharacterValueType.Number,value.ToDefinition(),"执行图数值",next=>
            {
                var replacement=NpcGraphNumber.FromDefinition(next);
                value.Kind=replacement.Kind;value.Key=replacement.Key;value.Number=replacement.Number;value.Inputs=replacement.Inputs;
                context.CurrentGraph.MarkDirty();Refresh();
            },required:true);
            root.AddChild(dialog);dialog.PopupCenteredClamped(new(1040,700),.9f);
        };
        return root;
    }
    public static Control Local(NpcLocalNumberReference value,GraphEditorContext context)
    {
        var choices=context.CurrentGraph.BlackboardEntries.Where(e=>e.Value is GraphFloatBlackboardValue&&e.Binding==null).Select(e=>e.Key).ToArray();
        var button=new Button{Text=choices.Contains(value.Key)?value.Key:"选择本图局部数值",Disabled=choices.Length==0};
        button.Pressed+=()=>
        {
            var popup=new SearchablePopup<string>(choices,k=>k,_=>"本次执行的图内变量",k=>k);
            popup.OnItemSelected+=key=>{value.Key=key;button.Text=key;context.CurrentGraph.MarkDirty();};popup.ShowBelow(button);
        };
        button.TooltipText="在本执行图的本地黑板中声明无外部绑定的 Float 变量；不同角色及每次行动各自保存。";return button;
    }
    public static Control Interaction(NpcInteractAction action,GraphEditorContext context)
    {
        var root=new VBoxContainer();root.AddChild(new Label{Text="目标输入"});root.AddChild(action.Input.CreateEditUI(context));
        WorldInteractionDefinition[] operations;
        try
        {
            operations=WorldAuthoringCompiler.EditorCatalog.Objects.Values.SelectMany(o=>o.Capability<WorldInteractionCapability>()?.Operations??new())
                .Concat(WorldAuthoringCompiler.EditorCatalog.Definition.ObjectTypes.SelectMany(t=>t.Capability<WorldInteractionCapability>()?.Operations??new()))
                .GroupBy(o=>o.Id,StringComparer.Ordinal).Select(g=>g.First()).ToArray();
        }
        catch(ArgumentException){operations=Array.Empty<WorldInteractionDefinition>();}
        var operation=new Button{Text=operations.FirstOrDefault(o=>o.Id==action.OperationId)?.DisplayName??(action.OperationId.Length==0?"选择交互操作":"操作引用未找到："+action.OperationId),Disabled=operations.Length==0};root.AddChild(operation);
        operation.Pressed+=()=>
        {
            var popup=new SearchablePopup<WorldInteractionDefinition>(operations,o=>o.DisplayName,_=>"对象交互操作",o=>o.DisplayName+" · "+o.Id);
            popup.OnItemSelected+=o=>{action.OperationId=o.Id;operation.Text=o.DisplayName;context.CurrentGraph.MarkDirty();};popup.ShowBelow(operation);
        };
        root.AddChild(new Label{Text="本次操作数量"});root.AddChild(action.Amount.CreateEditUI(context));
        return root;
    }
}
#endif
