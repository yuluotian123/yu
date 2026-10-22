using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Goap;
namespace GameLogic.AI;

public sealed partial class NpcProgram
{
    readonly Dictionary<string,CharacterValueType> _scope=new(StringComparer.Ordinal);
    IReadOnlyList<NpcBindingSpec> CompileBindings(IEnumerable<NpcActionInputDefinition> source,Func<NpcActionInputDefinition,(string SelectorId,string NodeId)> link)
    {
        var definitions=source.ToArray();
        Require(definitions.All(b=>b!=null&&!string.IsNullOrWhiteSpace(b.Name)&&!b.Name.Contains('.'))&&definitions.Select(b=>b.Name).Distinct().Count()==definitions.Length,"参数名称不能为空、重复或包含句点。");
        return ReadOnly(definitions.Select(b=>
        {
            Require(b.ValueType!=CharacterValueType.Unknown&&Enum.IsDefined(b.ValueType),"输入类型无效："+b.Name);
            Require(!b.IsTarget||NpcBindingAdapters.Find(b.Adapter)!=null,"输入对象类型未注册："+b.Name);
            string selectorId="",nodeId="";
            if(b.IsTarget)
            {
                (selectorId,nodeId)=link(b);
                Require(TargetSelectors.TryGetValue(selectorId,out var selector)&&selector.Adapter==b.Adapter,"目标输入必须连接已启用且对象类型相同的选择器："+b.Name);
            }
            else
            {
                Require(b.Value!=null,"请输入参数值："+b.Name);var expression=b.Value.Freeze();
                Require(!expression.FactKeys().Any(k=>k.StartsWith("target.")||k.StartsWith("input.")||k=="action.duration"),"输入来源只能使用人物、世界的已注册参数或常量。");Expect(expression,b.ValueType);
            }
            return new NpcBindingSpec(b.Name,b.ValueType,b.Adapter,selectorId,b.IsTarget?null:b.Value.Freeze(),nodeId);
        }));
    }
    NpcActionSpec CompileAction(NpcActionDefinition action,Func<NpcActionInputDefinition,(string SelectorId,string NodeId)> link)
    {
        Require(action.ExecutionGraph!=null&&double.IsFinite(action.DurationSeconds)&&action.DurationSeconds>=0&&double.IsFinite(action.TimeoutSeconds)&&action.TimeoutSeconds>0,"行动缺少执行图或时间无效："+action.DisplayName);
        Require(action.ExecutionGraph.Validate(out _),"行动执行图无效："+action.DisplayName);
        IReadOnlyList<NpcEffect> Effects(IEnumerable<NpcEffectDefinition> source)=>ReadOnly(source.Select(e=>
        {Require(e?.Value!=null,"效果缺少值。");return new NpcEffect(e.Key,e.Mode,e.Value.Freeze());}));
        var objectInputs=action.Inputs.OfType<NpcActionInputDefinition>().ToArray();
        Require(objectInputs.All(i=>!string.IsNullOrWhiteSpace(i.InputId))&&objectInputs.Select(i=>i.InputId).Distinct(StringComparer.Ordinal).Count()==objectInputs.Length,"行动输入身份为空或重复："+action.DisplayName);
        var inputs=CompileBindings(action.Inputs,link);
        var interactions=action.ExecutionGraph.Nodes.OfType<FlowActionNodeData>().SelectMany(n=>n.Actions).OfType<NpcInteractAction>().ToArray();
        Require(interactions.Length<=1,"一个行动最多提交一个对象交互；多个操作请拆为多个行动。");
        foreach(var interaction in interactions)
        {
            Require(inputs.Any(i=>i.Name==interaction.Input?.Name&&i.Adapter=="point")&&!string.IsNullOrWhiteSpace(interaction.OperationId),"对象交互需要本行动的交互点输入与操作 Id。");
            Require(!action.Results.Any(e=>e.Key.StartsWith("input."+interaction.Input.Name+".",StringComparison.Ordinal)),"对象交互已提交目标变化，请在预测效果中描述它，不要再次写入目标结果。");
        }
        foreach(var ability in action.ExecutionGraph.Nodes.OfType<FlowActionNodeData>().SelectMany(n=>n.Actions))
        {
            if(ability is NpcMoveToTargetAction move)
                Require(inputs.Any(i=>i.Name==move.Input?.Name&&i.Adapter=="point"),"导航能力必须选择本行动的交互点输入："+action.DisplayName);
            if(ability is NpcRoamAction roam&&roam.Input?.Name.Length>0)
                Require(inputs.Any(i=>i.Name==roam.Input.Name&&i.Adapter=="point"),"附近活动能力的地点输入不存在或类型错误："+action.DisplayName);
            if(ability is NpcRememberAction remember&&remember.SubjectInput?.Name.Length>0)
                Require(inputs.Any(i=>i.Name==remember.SubjectInput.Name&&i.Type==CharacterValueType.Symbol),"记忆主题必须选择本行动的文本或对象输入："+action.DisplayName);
        }
        var claims=ReadOnly(action.Inputs.Where(input=>input.IsTarget).SelectMany(input=>input.ResourceRequirements.Select(capability=>
        {
            var adapter=NpcBindingAdapters.Find(input.Adapter);
            Require(input.ValueType==CharacterValueType.Symbol&&adapter!=null&&adapter.Capabilities.Contains(capability),"输入“"+input.Name+"”不支持此资源要求："+capability);
            return new NpcClaimSpec(input.Name,capability);
        })).Distinct());
        return new(action.DefinitionId,action.DisplayName,inputs,claims,action.Preconditions?.Freeze(),action.SuccessConditions?.Freeze(),action.Cost?.Freeze(),action.DurationSeconds,action.TimeoutSeconds,action.ExecutionGraph,Effects(action.Predictions),Effects(action.Results),CompileGraph(action));
    }
    void ValidateAction(NpcActionSpec action)
    {
        _scope.Clear();
        foreach(var input in action.Inputs)
        {
            string prefix="input."+input.Name;_scope.Add(prefix,input.Type);
            if(NpcBindingAdapters.Find(input.Adapter) is {} adapter)foreach(var p in adapter.Properties)_scope.Add(prefix+"."+p.Name,p.Type);
        }
        try
        {
            if(action.Precondition!=null)Expect(action.Precondition,CharacterValueType.Boolean);
            if(action.Success!=null)Expect(action.Success,CharacterValueType.Boolean);
            if(action.Cost!=null)Expect(action.Cost,CharacterValueType.Number);
            foreach(var expression in action.Graph?.PlanningValues??Array.Empty<CharacterExpression>())Expect(expression,CharacterValueType.Number);
            Require(action.Graph?.Interaction==null||!action.Predictions.Any(e=>e.Key.StartsWith("input.",StringComparison.Ordinal)),"交互数量已经从执行图生成预测，该行动不要再手动填写对象预测效果。");
            foreach(var effect in action.Predictions.Concat(action.Results))
            {
                var type=EffectType(action,effect.Key);
                Require(type!=CharacterValueType.Unknown,"效果目标未声明或不可写："+effect.Key);
                Require(Enum.IsDefined(effect.Mode)&&(effect.Mode!=NpcEffectMode.Add||type==CharacterValueType.Number),"增加/减少效果只能修改数值。");
                Expect(effect.Value,type);
            }
            Require(!action.Results.Any(e=>e.Key=="self.location"),"当前位置由实际空间状态观测；请将位置变化配置为预测效果，并配置实际成功条件。");
            Require(!action.Results.Any(e=>e.Key.StartsWith("input.",StringComparison.Ordinal)),"世界对象只能通过公共交互操作修改；目标变化配置为预测效果，执行图使用对象交互能力。");
            // Physical outcomes need an explicit observation check before results may commit.
            Require(!action.Predictions.Any(e=>e.Key=="self.location")||action.Success!=null,"预测位置变化必须配置实际成功条件。");
            foreach(var e in new[]{action.Precondition,action.Success,action.Cost}.Concat(action.Predictions.Concat(action.Results).Select(e=>e.Value)).Where(e=>e!=null))
                Require(!e.FactKeys().Any(k=>k.StartsWith("target.")),"请使用当前行动的输入参数。");
        }
        finally{_scope.Clear();}
    }
    public CharacterValueType EffectType(NpcActionSpec action,string key)
    {
        if(Facts.TryGetValue(key,out var fact))return fact.Type;
        if(key=="self.location")return CharacterValueType.Symbol;
        if(SplitInputMember(action,key,out var input,out var member))return NpcBindingAdapters.Find(input.Adapter)?.Properties.FirstOrDefault(p=>p.Name==member&&p.Writable)?.Type??CharacterValueType.Unknown;
        return CharacterValueType.Unknown;
    }
    public static bool SplitInputMember(NpcActionSpec action,string key,out NpcBindingSpec input,out string member)
    {
        input=null;member="";
        if(action==null||!key.StartsWith("input.",StringComparison.Ordinal))return false;
        int dot=key.IndexOf('.',6);if(dot<0)return false;
        input=action.Inputs.FirstOrDefault(i=>i.Name==key[6..dot]);member=key[(dot+1)..];return input!=null;
    }
    public string EffectKey(GoapWorldState context,NpcActionSpec action,string key)
    {
        if(Facts.ContainsKey(key)||key=="self.location")return key;
        return SplitInputMember(action,key,out var input,out var member)?NpcBindingAdapters.Find(input.Adapter)?.WriteKey(member,Read(context,"input."+input.Name)):null;
    }
    public GoapWorldState Contextual(GoapWorldState state,NpcBoundAction bound,Func<string,CharacterValue,double?> distance=null,bool physical=false)
    {
        var context=state.With(GoapEffect.Set("action.duration",Encode("action.duration",CharacterValue.Numeric(bound.Action.Duration))));
        foreach(var input in bound.Action.Inputs)
        {
            CharacterValue value;
            if(bound.Resolved?.TryGetValue(input.Name,out value)==true){}
            else if(input.IsTarget)value=bound.Candidates.GetValueOrDefault(input.Name);
            else value=input.Value.Evaluate(k=>Read(state,k),Parameter);
            if(value.Type!=input.Type||input.Adapter.Length>0&&string.IsNullOrEmpty(value.Symbol))return null;
            if(input.SelectionNodeId.Length>0)
            {
                string selectionKey=SelectionKey(bound.Plan.Id,input.SelectionNodeId);
                var previous=Read(context,selectionKey);
                if(previous.Type!=CharacterValueType.Unknown&&previous!=value)return null;
                context=context.With(GoapEffect.Set(selectionKey,Encode(selectionKey,value)));
            }
            string prefix="input."+input.Name;context=context.With(GoapEffect.Set(prefix,Encode(prefix,value)));
            if(NpcBindingAdapters.Find(input.Adapter) is {} adapter)context=adapter.Project(this,context,prefix,value,distance?.Invoke(input.Name,value),physical);
        }
        if(bound.Action.Graph?.Durations is {Count:>0} durations)
        {
            var seconds=durations.Select(e=>e.Evaluate(k=>Read(context,k),Parameter)).ToArray();
            if(seconds.Any(v=>v.Type!=CharacterValueType.Number||v.Number<0)||!double.IsFinite(seconds.Sum(v=>v.Number)))return null;
            context=context.With(GoapEffect.Set("action.duration",Encode("action.duration",CharacterValue.Numeric(seconds.Sum(v=>v.Number)))));
        }
        return context;
    }
    public GoapWorldState Predict(GoapWorldState state,NpcBoundAction bound,Func<string,CharacterValue,double?> distance=null)
    {
        var context=Contextual(state,bound,distance);if(context==null)return null;
        foreach(var input in bound.Action.Inputs)
        {
            if(!input.IsTarget)continue;
            var selector=TargetSelectors[input.SelectorId];string id=Read(context,"input."+input.Name).Symbol;
            var target=TargetContext(context,id,Read(context,"input."+input.Name+".distance").Number,selector.Adapter);
            if(selector.RequireUsable&&!Read(target,"target.open").IsTrue||selector.Filter!=null&&!selector.Filter.Evaluate(k=>Read(target,k),Parameter).IsTrue)return null;
        }
        foreach(var claim in bound.Action.Resources)
        {
            var input=bound.Action.Inputs.First(i=>i.Name==claim.Input);
            if(!NpcBindingAdapters.Find(input.Adapter).CanClaim(this,context,"input."+input.Name,claim.Capability))return null;
        }
        if(bound.Action.Precondition!=null&&!bound.Action.Precondition.Evaluate(k=>Read(context,k),Parameter).IsTrue)return null;
        if(!TryEffects(context,bound.Action.Predictions,out var next,bound.Action))return null;
        if(!PredictInteraction(bound.Action,context,next,out next))return null;
        return new GoapWorldState(next.Facts.Where(f=>!f.Key.StartsWith("input.")&&!f.Key.StartsWith("target.")&&f.Key!="action.duration"));
    }
}
