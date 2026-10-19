using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Goap;
namespace GameLogic.AI;

public sealed partial class NpcProgram
{
    readonly Dictionary<string,CharacterValueType> _scope=new(StringComparer.Ordinal);
    IReadOnlyList<NpcBindingSpec> CompileBindings(IEnumerable<NpcBindingDefinition> source,bool variable)
    {
        var definitions=source.ToArray();
        Require(definitions.All(b=>b!=null&&!string.IsNullOrWhiteSpace(b.Name)&&!b.Name.Contains('.'))&&definitions.Select(b=>b.Name).Distinct().Count()==definitions.Length,"参数名称不能为空、重复或包含句点。");
        return ReadOnly(definitions.Select(b=>
        {
            Require(Enum.IsDefined(b.Source)&&b.ValueType!=CharacterValueType.Unknown&&Enum.IsDefined(b.ValueType),"输入类型或来源无效："+b.Name);
            Require(b.Adapter.Length==0||b.ValueType==CharacterValueType.Symbol&&NpcBindingAdapters.Find(b.Adapter)!=null,"输入对象类型未注册："+b.Name);
            Require(!variable||b.Source!=NpcBindingSource.PlanVariable,"计划变量不能引用另一个计划变量。");
            Require(!variable||b is not NpcActionInputDefinition,"计划变量只保存值，请使用普通绑定定义；资源要求应配置在行动输入中。");
            Require(b.Source!=NpcBindingSource.Selector||TargetSelectors.TryGetValue(b.SelectorId,out var selector)&&selector.Adapter==b.Adapter,"选择器输入必须引用已连接启用的交互点选择器："+b.Name);
            Require(b.Source!=NpcBindingSource.PlanVariable||!string.IsNullOrWhiteSpace(b.Variable),"请选择计划变量："+b.Name);
            if(b.Source==NpcBindingSource.Expression)
            {
                Require(b.Value!=null,"请输入参数值："+b.Name);var expression=b.Value.Freeze();
                Require(!expression.FactKeys().Any(k=>k.StartsWith("target.")||k.StartsWith("input.")||k=="action.duration"),"输入来源只能使用人物、世界的已注册参数或常量。");Expect(expression,b.ValueType);
            }
            return new NpcBindingSpec(b.Name,b.ValueType,b.Adapter,b.Source,b.SelectorId,b.Variable,b.Source==NpcBindingSource.Expression?b.Value.Freeze():null);
        }));
    }
    NpcActionSpec CompileAction(NpcActionDefinition action)
    {
        Require(action.ExecutionGraph!=null&&double.IsFinite(action.DurationSeconds)&&action.DurationSeconds>=0&&double.IsFinite(action.TimeoutSeconds)&&action.TimeoutSeconds>0,"行动缺少执行图或时间无效："+action.DisplayName);
        Require(action.ExecutionGraph.Validate(out _),"行动执行图无效："+action.DisplayName);
        IReadOnlyList<NpcEffect> Effects(IEnumerable<NpcEffectDefinition> source)=>ReadOnly(source.Select(e=>
        {Require(e?.Value!=null,"效果缺少值。");return new NpcEffect(e.Key,e.Mode,e.Value.Freeze());}));
        var inputs=CompileBindings(action.Inputs,false);
        foreach(var ability in action.ExecutionGraph.Nodes.OfType<FlowActionNodeData>().SelectMany(n=>n.Actions))
        {
            if(ability is NpcMoveToTargetAction move)
                Require(inputs.Any(i=>i.Name==move.Input?.Name&&i.Adapter=="point"),"导航能力必须选择本行动的交互点输入："+action.DisplayName);
            if(ability is NpcRoamAction roam&&roam.Input?.Name.Length>0)
                Require(inputs.Any(i=>i.Name==roam.Input.Name&&i.Adapter=="point"),"附近活动能力的地点输入不存在或类型错误："+action.DisplayName);
            if(ability is NpcRememberAction remember&&remember.SubjectInput?.Name.Length>0)
                Require(inputs.Any(i=>i.Name==remember.SubjectInput.Name&&i.Type==CharacterValueType.Symbol),"记忆主题必须选择本行动的文本或对象输入："+action.DisplayName);
        }
        var claims=ReadOnly(action.Inputs.OfType<NpcActionInputDefinition>().SelectMany(input=>input.ResourceRequirements.Select(capability=>
        {
            var adapter=NpcBindingAdapters.Find(input.Adapter);
            Require(input.ValueType==CharacterValueType.Symbol&&adapter!=null&&adapter.Capabilities.Contains(capability),"输入“"+input.Name+"”不支持此资源要求："+capability);
            return new NpcClaimSpec(input.Name,capability);
        })).Distinct());
        return new(action.DefinitionId,action.DisplayName,inputs,claims,action.Preconditions?.Freeze(),action.SuccessConditions?.Freeze(),action.Cost?.Freeze(),action.DurationSeconds,action.TimeoutSeconds,action.ExecutionGraph,Effects(action.Predictions),Effects(action.Results));
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
            foreach(var effect in action.Predictions.Concat(action.Results))
            {
                var type=EffectType(action,effect.Key);
                Require(type!=CharacterValueType.Unknown,"效果目标未声明或不可写："+effect.Key);
                Require(Enum.IsDefined(effect.Mode)&&(effect.Mode!=NpcEffectMode.Add||type==CharacterValueType.Number),"增加/减少效果只能修改数值。");
                Expect(effect.Value,type);
            }
            Require(!action.Results.Any(e=>e.Key=="self.location"),"当前位置由实际空间状态观测；请将位置变化配置为预测效果，并配置实际成功条件。");
            // Physical outcomes need an explicit observation check before results may commit.
            Require(!action.Predictions.Any(e=>e.Key=="self.location")||action.Success!=null,"预测位置变化必须配置实际成功条件。");
            foreach(var e in new[]{action.Precondition,action.Success,action.Cost}.Concat(action.Predictions.Concat(action.Results).Select(e=>e.Value)).Where(e=>e!=null))
                Require(!e.FactKeys().Any(k=>k.StartsWith("target.")),"请使用当前行动的输入参数。");
        }
        finally{_scope.Clear();}
    }
    void ValidatePlan(NpcPlanSpec plan,IReadOnlyList<NpcActionSpec> actions)
    {
        foreach(var action in actions.Where(a=>plan.ActionIds.Contains(a.Id)))foreach(var input in action.Inputs.Where(i=>i.Source==NpcBindingSource.PlanVariable))
        {
            var variable=plan.Bindings.FirstOrDefault(v=>v.Name==input.Variable);
            Require(variable!=null&&variable.Type==input.Type&&variable.Adapter==input.Adapter,$"计划“{plan.Name}”缺少行动“{action.Name}”所需的同类型变量：{input.Variable}");
        }
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
    public NpcBindingSpec Source(NpcBoundAction bound,NpcBindingSpec input)=>input.Source==NpcBindingSource.PlanVariable?bound.Plan.Bindings.First(v=>v.Name==input.Variable):input;
    public GoapWorldState Contextual(GoapWorldState state,NpcBoundAction bound,Func<string,CharacterValue,double?> distance=null,bool physical=false)
    {
        var context=state.With(GoapEffect.Set("action.duration",Encode("action.duration",CharacterValue.Numeric(bound.Action.Duration))));
        foreach(var input in bound.Action.Inputs)
        {
            var source=Source(bound,input);string key=BindingKey(bound.Plan.Id,source.Name);
            CharacterValue value;
            if(bound.Resolved?.TryGetValue(input.Name,out value)==true){}
            else if(source.Source==NpcBindingSource.Selector)
            {
                value=bound.Candidates.GetValueOrDefault(input.Name);
                var previous=Read(context,key);
                if(input.Source==NpcBindingSource.PlanVariable&&previous.Type!=CharacterValueType.Unknown&&previous!=value)return null;
            }
            else value=input.Source==NpcBindingSource.PlanVariable&&Read(context,key).Type!=CharacterValueType.Unknown?Read(context,key):source.Value.Evaluate(k=>Read(state,k),Parameter);
            if(value.Type!=input.Type||input.Adapter.Length>0&&string.IsNullOrEmpty(value.Symbol))return null;
            if(input.Source==NpcBindingSource.PlanVariable)context=context.With(GoapEffect.Set(key,Encode(key,value)));
            string prefix="input."+input.Name;context=context.With(GoapEffect.Set(prefix,Encode(prefix,value)));
            if(NpcBindingAdapters.Find(input.Adapter) is {} adapter)context=adapter.Project(this,context,prefix,value,distance?.Invoke(input.Name,value),physical);
        }
        return context;
    }
    public GoapWorldState Predict(GoapWorldState state,NpcBoundAction bound,Func<string,CharacterValue,double?> distance=null)
    {
        var context=Contextual(state,bound,distance);if(context==null)return null;
        foreach(var input in bound.Action.Inputs)
        {
            var source=Source(bound,input);if(source.Source!=NpcBindingSource.Selector)continue;
            var selector=TargetSelectors[source.SelectorId];string id=Read(context,"input."+input.Name).Symbol;
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
        return new GoapWorldState(next.Facts.Where(f=>!f.Key.StartsWith("input.")&&!f.Key.StartsWith("target.")&&f.Key!="action.duration"));
    }
}
