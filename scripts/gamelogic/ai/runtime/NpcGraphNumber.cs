using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace GameLogic.AI;

/// <summary>Plain graph data; the same pure expression feeds planning and execution.</summary>
public sealed class NpcGraphNumber
#if TOOLS
    : IGraphParameterEditor
#endif
{
    public CharacterExpressionKind Kind {get;set;}
    public double Number {get;set;}
    public string Key {get;set;}="";
    public List<NpcGraphNumber> Inputs {get;set;}=new();
    public static NpcGraphNumber Constant(double value)=>new(){Number=value};
    public static NpcGraphNumber Fact(string key)=>new(){Kind=CharacterExpressionKind.Fact,Key=key};
    public CharacterExpression Freeze(int depth=0)
    {
        NpcProgram.Require(depth<=32&&double.IsFinite(Number)&&Inputs!=null&&Inputs.All(i=>i!=null),"执行图数值配置无效或递归过深。");
        NpcProgram.Require(Kind is CharacterExpressionKind.Literal or CharacterExpressionKind.Fact or CharacterExpressionKind.Parameter
            or CharacterExpressionKind.Add or CharacterExpressionKind.Subtract or CharacterExpressionKind.Multiply or CharacterExpressionKind.Divide
            or CharacterExpressionKind.Minimum or CharacterExpressionKind.Maximum or CharacterExpressionKind.Clamp01,"执行图参数需要数值公式。");
        if(Kind is CharacterExpressionKind.Fact or CharacterExpressionKind.Parameter)NpcProgram.Require(!string.IsNullOrWhiteSpace(Key),"执行图数值引用不能为空。");
        if(Kind is CharacterExpressionKind.Subtract or CharacterExpressionKind.Divide)NpcProgram.Require(Inputs.Count==2,"减法／除法需要两个输入。");
        if(Kind==CharacterExpressionKind.Clamp01)NpcProgram.Require(Inputs.Count==1,"Clamp01 需要一个输入。");
        return new(Kind,Key,CharacterValue.Numeric(Number),Inputs.Select(i=>i.Freeze(depth+1)));
    }
    public bool TryRead(GraphExecutionContext context,out double value)
    {
        value=0;var call=context?.GetUserData<NpcActionContext>();
        return call!=null&&call.TryNumber(this,context,out value);
    }
#if TOOLS
    public Control CreateEditUI(GraphEditorContext context)=>NpcGraphValueEditor.Number(this,context);
    public CharacterExpressionDefinition ToDefinition()=>new(){Kind=Kind,Key=Key,Number=Number,Inputs=new(Inputs.Select(i=>i.ToDefinition()))};
    public static NpcGraphNumber FromDefinition(CharacterExpressionDefinition value)=>new(){Kind=value.Kind,Key=value.Key,Number=value.Number,Inputs=value.Inputs.Select(FromDefinition).ToList()};
#endif
}

public sealed class NpcLocalNumberReference
#if TOOLS
    : IGraphParameterEditor
#endif
{
    public string Key {get;set;}="";
#if TOOLS
    public Control CreateEditUI(GraphEditorContext context)=>NpcGraphValueEditor.Local(this,context);
#endif
}

[GraphCallable("Calculate Local Number", "NPC/能力", ChineseName="计算并保存图内数值")]
[GraphProvidedBy(typeof(AiAgentComponent3D))]
public sealed class NpcCalculateNumberAction : GraphInstantAction
{
    public NpcLocalNumberReference Local {get;set;}=new();
    public NpcGraphNumber Value {get;set;}=NpcGraphNumber.Constant(0);
#if TOOLS
    public override Control CreateEditUI(GraphEditorContext context)
    {
        var root=new VBoxContainer();root.AddChild(new Label{Text="计算结果保存到"});root.AddChild(Local.CreateEditUI(context));
        root.AddChild(new Label{Text="数值公式"});root.AddChild(Value.CreateEditUI(context));return root;
    }
#endif
    protected override bool Run(GraphActionInvocation invocation)=>Value.TryRead(invocation.Execution,out double value)
        &&invocation.Execution.GetUserData<NpcActionContext>()?.SetLocalNumber(Local.Key,value,invocation.Execution)==true;
}
