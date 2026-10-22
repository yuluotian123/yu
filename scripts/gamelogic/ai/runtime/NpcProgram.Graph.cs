using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameLogic.Goap;
namespace GameLogic.AI;

public sealed record NpcInteractionSpec(string Input,string Operation,CharacterExpression Amount);
public sealed record NpcGraphContract(IReadOnlyDictionary<NpcGraphNumber,CharacterExpression> Values,
    IReadOnlyList<CharacterExpression> PlanningValues,IReadOnlyList<CharacterExpression> Durations,NpcInteractionSpec Interaction);

public sealed partial class NpcProgram
{
    NpcGraphContract CompileGraph(NpcActionDefinition action)
    {
        var graph=action.ExecutionGraph;
        var actions=graph.Nodes.OfType<FlowActionNodeData>().SelectMany(n=>n.Actions.Select((a,index)=>(Node:n,Action:a,Index:index))).ToArray();
        var values=actions.SelectMany(a=>(a.Action switch
        {
            NpcInteractAction interaction=>new[]{interaction.Amount},NpcCalculateNumberAction calculate=>new[]{calculate.Value},
            NpcWaitAction wait=>new[]{wait.Seconds},NpcRoamAction roam=>new[]{roam.Seconds},_=>Array.Empty<NpcGraphNumber>()
        }).Select(value=>(a.Node,a.Index,a.Action,Value:value))).ToArray();
        if(values.Length==0)return null;
        var edges=graph.Connections.Where(e=>e.IsAvailable).ToArray();
        Require(!edges.OfType<FlowConnection>().Any(e=>e.Mode==FlowConnectionMode.Parallel),"包含 NPC 数值参数的执行图暂不支持并行连线；请拆分行动。");
        Require(edges.GroupBy(e=>(e.FromNode,e.FromPort)).All(g=>g.Count()==1),"NPC 数值执行图同一输出端口只能有一个后继；分支请使用条件节点。");
        Require(!graph.Nodes.OfType<SubGraphNodeData>().Any(),"NPC 数值参数请直接配置在所属 Action 执行图中，暂不跨子图引用。");
        Require(!graph.Nodes.OfType<FlowEventNodeData>().Any(),"NPC 数值行动需要单一 Entry 入口，事件入口请拆到独立行动。");
        var entries=graph.Nodes.OfType<FlowEntryNodeData>().ToArray();Require(entries.Length==1,"执行图数值需要唯一入口。");
        bool Reach(string start,string target,string excluded=null)
        {
            var queue=new Queue<string>();var visited=new HashSet<string>();queue.Enqueue(start);
            while(queue.TryDequeue(out var current))
            {
                if(current==excluded||!visited.Add(current))continue;if(current==target)return true;
                if(graph.FindNodeById(current) is FlowReturnNodeData)continue;
                foreach(var edge in edges.Where(e=>e.FromNode==current))queue.Enqueue(edge.ToNode);
            }
            return false;
        }
        var locals=graph.BlackboardEntries.Where(e=>e.Value is GraphFloatBlackboardValue or GraphIntBlackboardValue).ToDictionary(e=>e.Key,StringComparer.Ordinal);
        var calculations=actions.Where(a=>a.Action is NpcCalculateNumberAction).ToArray();
        Require(calculations.Select(a=>((NpcCalculateNumberAction)a.Action).Local.Key).Distinct().Count()==calculations.Length,"每个图内计算变量只能有一个计算节点。");
        foreach(var calculation in calculations)
            Require(locals.TryGetValue(((NpcCalculateNumberAction)calculation.Action).Local.Key,out var local)&&local.Value is GraphFloatBlackboardValue&&local.Binding==null,"计算结果需要选择本图声明且无外部绑定的 Float 局部变量。");
        var initial=entries[0].Id;
        bool Dominates(string node,string target)=>Reach(initial,target)&&!Reach(initial,target,node);
        var success=graph.Nodes.OfType<FlowReturnNodeData>().Where(n=>n.Label=="Success"&&Reach(initial,n.Id)).ToArray();
        bool FailureCanSucceed(string node)=>edges.Where(e=>e.FromNode==node&&e.FromPort==1).Any(e=>success.Any(s=>Reach(e.ToNode,s.Id)));
        var raw=new Dictionary<NpcGraphNumber,CharacterExpression>();var expanded=new List<CharacterExpression>();var durations=new List<CharacterExpression>();NpcInteractionSpec interactionSpec=null;
        foreach(var use in values)
        {
            Require(use.Value!=null,"执行图数值不能为空。");var expression=use.Value.Freeze();var visiting=new HashSet<string>();int size=0;
            CharacterExpression Expand(CharacterExpression value,string consumer,int index)
            {
                Require(++size<=256,"执行图数值公式展开超过 256 项，请简化局部计算。");
                if(value.Kind==CharacterExpressionKind.Fact&&value.Key.StartsWith("local.",StringComparison.Ordinal))
                {
                    string key=value.Key[6..];Require(locals.TryGetValue(key,out var local)&&local.Binding==null&&visiting.Add(key),"图内数值引用不存在、有外部绑定或有循环："+key);
                    Require(!actions.Any(a=>a.Action is SetBlackboardAction set&&set.Key.Key==key||a.Action is AddBlackboardNumberAction add&&add.Key.Key==key),"规划使用的局部数值请由计算节点赋值，不要混用通用黑板写入："+key);
                    var source=calculations.FirstOrDefault(a=>((NpcCalculateNumberAction)a.Action).Local.Key==key);
                    CharacterExpression resolved;
                    if(source.Action is NpcCalculateNumberAction calculate)
                    {
                        Require(source.Node.Id==consumer?source.Index<index:Dominates(source.Node.Id,consumer),"局部数值必须先计算再读取，且不能从分支绕过计算："+key);
                        Require(!FailureCanSucceed(source.Node.Id),"计算失败不能继续返回 Success："+key);
                        resolved=Expand(calculate.Value.Freeze(),source.Node.Id,source.Index);
                    }
                    else resolved=new(CharacterExpressionKind.Literal,"",CharacterValue.Numeric(Convert.ToDouble(local.Value.GetObjectValue())),Array.Empty<CharacterExpression>());
                    visiting.Remove(key);return resolved;
                }
                return new(value.Kind,value.Key,value.Literal,value.Inputs.Select(i=>Expand(i,consumer,index)).ToArray());
            }
            var planning=Expand(expression,use.Node.Id,use.Index);raw.TryAdd(use.Value,expression);expanded.Add(planning);
            var constant=planning.Evaluate(_=>default,Parameter);
            if(constant.Type==CharacterValueType.Number)
                Require(use.Action is NpcInteractAction?constant.Number>0:use.Action is not (NpcWaitAction or NpcRoamAction)||constant.Number>=0,"交互数量必须大于零，图内时长不能为负数。");
            if(use.Action is NpcInteractAction interaction)
            {
                Require(success.Length>0&&success.All(s=>Dominates(use.Node.Id,s.Id))&&!FailureCanSucceed(use.Node.Id),"交互节点必须位于所有成功路径上，交互失败不能返回 Success；分支操作请拆为多个行动。");
                interactionSpec=new(interaction.Input.Name,interaction.OperationId,planning);
            }
            bool cycle=edges.Where(e=>e.FromNode==use.Node.Id).Any(e=>Reach(e.ToNode,use.Node.Id));
            if(use.Action is NpcWaitAction or NpcRoamAction&&success.Length>0&&success.All(s=>Dominates(use.Node.Id,s.Id))&&!FailureCanSucceed(use.Node.Id)&&!cycle)durations.Add(planning);
        }
        return new(new ReadOnlyDictionary<NpcGraphNumber,CharacterExpression>(raw),Array.AsReadOnly(expanded.ToArray()),Array.AsReadOnly(durations.ToArray()),interactionSpec);
    }

    public static string QueryKey(NpcActionSpec action,NpcBindingSpec input)=>action.Graph?.Interaction is {} interaction&&interaction.Input==input.Name
        ?input.SelectorId+"::action:"+action.Id:input.SelectorId;
    public NpcTargetSelectorSpec QuerySelector(NpcActionSpec action,NpcBindingSpec input)
    {
        var selector=TargetSelectors[input.SelectorId];
        return action.Graph?.Interaction is {} interaction&&interaction.Input==input.Name
            ?selector with{RequiredOperations=(selector.RequiredOperations??Array.Empty<string>()).Append(interaction.Operation).Distinct(StringComparer.Ordinal).ToArray(),ActionConstraint=action}:selector;
    }
    public bool CandidateInteraction(AiAgentComponent3D agent,NpcTargetSelectorSpec selector,WorldInteractionComponent3D point,double distance)
    {
        var action=selector.ActionConstraint;if(action?.Graph?.Interaction is not {} interaction)return true;
        string prefix="input."+interaction.Input;
        var keys=interaction.Amount.FactKeys().ToArray();
        // Other targets and route-dependent values are resolved by grounding; do not guess them here.
        var ordinary=action.Inputs.Where(i=>!i.IsTarget).Select(i=>"input."+i.Name).ToHashSet(StringComparer.Ordinal);
        if(keys.Any(k=>k=="action.duration"||k==prefix+".distance"||k.StartsWith("input.",StringComparison.Ordinal)
            &&k!=prefix&&!k.StartsWith(prefix+".",StringComparison.Ordinal)&&!ordinary.Contains(k)))return true;
        var context=agent.Snapshot(new[]{point.PointId},true).With(GoapEffect.Set(prefix,GoapValue.Id(point.PointId)));
        context=NpcBindingAdapters.Find(selector.Adapter).Project(this,context,prefix,CharacterValue.Text(point.PointId),distance,false);
        foreach(var input in action.Inputs.Where(i=>!i.IsTarget))
        {
            var value=input.Value.Evaluate(k=>Read(context,k),Parameter);if(value.Type!=input.Type)return false;
            context=context.With(GoapEffect.Set("input."+input.Name,Encode("input."+input.Name,value)));
        }
        return PredictInteraction(action,context,context,out _);
    }
    public static string OperationKey(string id,string operation)=>"point."+id+".operation."+operation;
    bool PredictInteraction(NpcActionSpec action,GoapWorldState context,GoapWorldState predicted,out GoapWorldState result)
    {
        result=predicted;if(action.Graph?.Interaction is not {} interaction)return true;
        var amount=interaction.Amount.Evaluate(k=>Read(context,k),Parameter);if(amount.Type!=CharacterValueType.Number||amount.Number<=0)return false;
        string id=Read(context,"input."+interaction.Input).Symbol,key=OperationKey(id,interaction.Operation);
        var member=Read(context,key+".resource");if(member.Type!=CharacterValueType.Symbol||member.Symbol is not ("stock" or "liquid"))return false;
        if(Read(context,key+".capacity").IsTrue&&!Read(context,"input."+interaction.Input+".available").IsTrue)return false;
        string resource="point."+id+"."+member.Symbol;var current=Read(context,resource);
        if(current.Type!=CharacterValueType.Number||current.Number+1e-8<amount.Number)return false;
        result=predicted.With(GoapEffect.Set(resource,Encode(resource,CharacterValue.Numeric(Math.Max(0,current.Number-amount.Number)))));return true;
    }
}
