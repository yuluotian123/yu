using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameLogic.Goap;
using Godot;
namespace GameLogic.AI;

public sealed record NpcFactSpec(string Key,CharacterValueType Type,double Minimum,double Maximum,double Step,CharacterValue Initial,bool Writable);
public sealed record NpcNeedSpec(string Id,double Min,double Max,double Initial,bool Higher,double Enter,double Exit,double Exponent);
public sealed record NpcWindowSpec(string Id,double Start,double End,int Priority,IReadOnlyList<string> Tags);
public sealed record NpcEffect(string Key,NpcEffectMode Mode,CharacterExpression Value);
public sealed record NpcBindingSpec(string Name,CharacterValueType Type,string Adapter,string SelectorId,CharacterExpression Value,string SelectionNodeId="")
{
    public bool IsTarget=>Adapter.Length>0;
}
public sealed record NpcClaimSpec(string Input,string Capability);
public sealed record NpcActionSpec(string Id,string Name,IReadOnlyList<NpcBindingSpec> Inputs,IReadOnlyList<NpcClaimSpec> Resources,CharacterExpression Precondition,CharacterExpression Success,CharacterExpression Cost,double Duration,double Timeout,FlowGraphAsset Execution,IReadOnlyList<NpcEffect> Predictions,IReadOnlyList<NpcEffect> Results);
public sealed record NpcTargetSelectorSpec(string Id,string Name,NpcTargetSource Source,string ParameterKey,IReadOnlyList<string> AllTags,IReadOnlyList<string> AnyTags,IReadOnlyList<string> ExcludedTags,double MaximumDistance,bool RequireUsable,bool RequireReachable,CharacterExpression Filter,NpcTargetSelection Selection,CharacterExpression Score,int Limit,string Adapter="point",string FixedObjectId="");
public sealed record NpcPlanSpec(string Id,string Name,int Priority,CharacterExpression Eligibility,IReadOnlyList<string> ActionIds);
public sealed record NpcGoalSpec(string Id,string Name,CharacterExpression Activation,CharacterExpression Utility,CharacterExpression Completion,IReadOnlyList<NpcActionSpec> Actions,IReadOnlyList<NpcPlanSpec> Plans);
public sealed record NpcPolicy(int Slice,int Expanded,int Depth,int Targets,double Commitment,double Margin,double Retry,float Detailed,float Abstract);

/// <summary>Compiled composition. Graph edges select definitions; searches never read mutable Resources.</summary>
public sealed partial class NpcProgram
{
    public CharacterProfileSpec Character {get;private set;}
    public readonly Dictionary<string,NpcFactSpec> Facts = new(StringComparer.Ordinal);
    public readonly Dictionary<string,CharacterValue> Parameters = new(StringComparer.Ordinal);
    public readonly List<NpcNeedSpec> Needs = new();
    public readonly List<NpcWindowSpec> Windows = new();
    public readonly List<NpcGoalSpec> Goals = new();
    public readonly Dictionary<string,NpcTargetSelectorSpec> TargetSelectors = new(StringComparer.Ordinal);
    public readonly List<string> MemoryCategories = new();
    public readonly HashSet<string> KnownKeys = new(StringComparer.Ordinal);
    public NpcPolicy Policy { get; private set; } = new(16,2048,16,4,.8,.2,2,30,45);
    public double ViewRange { get; private set; } = 12;
    public double SenseInterval { get; private set; } = .3;
    public bool LineOfSight { get; private set; } = true;
    public string[] PointTags { get; private set; } = Array.Empty<string>();
    public string[] KnownPoints { get; private set; } = Array.Empty<string>();
    public int MemoryCapacity { get; private set; } = 32;
    public double MemoryLifetime { get; private set; } = 24;
    private NpcProgram() { }
    static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> items) => Array.AsReadOnly(items.ToArray());
    public static NpcProgram Compile(NpcGraphAsset graph,CharacterProfile profile=null)
    {
        if (graph == null) throw new ArgumentException("缺少 NPC 图。");
        foreach(var actionNode in graph.Nodes.OfType<NpcActionNodeData>())actionNode.Bind(graph);
        var p = new NpcProgram();
        var character=profile??graph.CharacterContext;
        p.Character=CharacterProfileSpec.Compile(character);
        foreach(var x in p.Character.Parameters)p.Parameters.Add(x.Key,x.Value);
        p.KnownKeys.UnionWith(p.Character.KnownKeys);
        foreach(var f in p.Character.Facts.Values)p.AddFact(new(f.Key,f.Type,f.Minimum,f.Maximum,f.Step,f.Initial,f.Writable));
        foreach(var n in p.Character.Needs)
        {
            p.Needs.Add(new(n.Id,n.Min,n.Max,n.Initial,n.Higher,n.Enter,n.Exit,n.Exponent));
            p.KnownKeys.Add("needs."+n.Id+".urgent");p.KnownKeys.Add("needs."+n.Id+".urgency");
        }
        var roots = graph.Nodes.OfType<NpcRootNodeData>().ToArray();
        Require(roots.Length == 1,"NPC 图必须有且仅有一个人物根节点。");
        Require(graph.Definitions.All(d=>d!=null) && graph.Definitions.Select(d=>d.DefinitionId).Distinct().Count()==graph.Definitions.Count,"定义 ID 为空或重复。");
        var root = roots[0];
        var active = graph.Connections.Where(c=>c.IsAvailable).ToArray();
        foreach(var edge in active)
        {
            var from=graph.FindNodeById(edge.FromNode);var to=graph.FindNodeById(edge.ToNode);
            Require(from!=null&&to!=null,"连线端点丢失。");
            Require(edge.ToPort==0 && (from is NpcRootNodeData && (edge.FromPort==0 && to is NpcDefinitionNodeData and not (NpcGoalNodeData or NpcActionNodeData or NpcTemplateNodeData or NpcTargetSelectorNodeData)
                || edge.FromPort==1&&to is NpcGoalNodeData || edge.FromPort==2&&to is NpcTargetSelectorNodeData) || from is NpcGoalNodeData&&(edge.FromPort==0&&to is NpcActionNodeData||edge.FromPort==1&&to is NpcTemplateNodeData))
                || from is NpcTargetSelectorNodeData&&edge.FromPort==0&&to is NpcActionNodeData action&&action.InputAt(edge.ToPort)!=null,"NPC 连线类型不匹配，或目标输入已被移除/更改来源。");
        }
        Require(active.Where(e=>e.ToPort>0).GroupBy(e=>(e.ToNode,e.ToPort)).All(g=>g.Count()==1),"每个行动输入只能连接一个选择器节点。");
        NpcDefinitionResource Resolve(NpcDefinitionNodeData node)
        {
            var d=graph.Resolve(node.DefinitionId);Require(d!=null&&node.DefinitionType.IsInstanceOfType(d),$"节点 {node.Id} 的定义未绑定或类型错误。");return d;
        }
        var modules=active.Where(c=>c.FromNode==root.Id&&c.FromPort==0).Select(c=>Resolve((NpcDefinitionNodeData)graph.FindNodeById(c.ToNode))).Where(d=>d.Enabled).ToArray();
        Require(modules.GroupBy(d=>d.GetType()).All(g=>g.Count()==1),"同类 AI 模块只能装配一次。");
        foreach(var d in modules)
        {
            switch(d)
            {
                case NpcScheduleDefinition schedule:
                    foreach(var w in schedule.Windows){Require(w!=null&&w.StartHour>=0&&w.StartHour<=24&&w.EndHour>=0&&w.EndHour<=24&&!p.Windows.Any(x=>x.Id==w.WindowId),"日程窗口无效。");p.Windows.Add(new(w.WindowId,w.StartHour,w.EndHour,w.Priority,ReadOnly(w.Tags)));foreach(var tag in w.Tags)p.KnownKeys.Add("schedule."+tag);}
                    break;
                case NpcPerceptionDefinition perception:
                    Require(perception.ViewRange>0&&perception.SenseInterval>0,"感知范围/间隔必须大于零。");
                    p.ViewRange=perception.ViewRange;p.SenseInterval=perception.SenseInterval;p.LineOfSight=perception.RequireLineOfSight;p.PointTags=perception.PointTags.ToArray();p.KnownPoints=perception.KnownPointIds.ToArray();break;
                case NpcMemoryDefinition memory:
                    p.MemoryCategories.AddRange(memory.Categories);p.MemoryCapacity=memory.Capacity;p.MemoryLifetime=memory.LifetimeHours;Require(p.MemoryCapacity>0&&p.MemoryLifetime>0,"记忆配置无效。");break;
                case NpcPolicyDefinition policy:
                    p.Policy=new(policy.NodesPerSlice,policy.MaximumExpanded,policy.MaximumPlanDepth,policy.TargetLimit,policy.CommitmentSeconds,policy.SwitchMargin,policy.RetrySeconds,policy.DetailedDistance,policy.AbstractDistance);
                    Require(p.Policy.Slice>0&&p.Policy.Expanded>0&&p.Policy.Depth>0&&p.Policy.Targets>0&&p.Policy.Retry>0&&p.Policy.Detailed<=p.Policy.Abstract,"决策预算无效。");break;
            }
        }
        foreach(var key in p.MemoryCategories)p.KnownKeys.Add("memory."+key+".count");
        foreach(var key in new[]{"world.hour","world.rain","self.location","action.duration"})p.KnownKeys.Add(key);
        foreach(var selector in active.Where(c=>c.FromNode==root.Id&&c.FromPort==2)
            .Select(c=>(NpcTargetSelectorDefinition)Resolve((NpcTargetSelectorNodeData)graph.FindNodeById(c.ToNode))).Where(s=>s.Enabled).Distinct())
        {
            Require(Enum.IsDefined(selector.Source)&&Enum.IsDefined(selector.Selection)&&double.IsFinite(selector.MaximumDistance)&&selector.MaximumDistance>=0&&selector.CandidateLimit>=0&&NpcBindingAdapters.Find(selector.Adapter)!=null,"目标选择器的来源、策略、距离或数量无效。");
            Require(selector.Source!=NpcTargetSource.IdentityParameter||p.Parameters.TryGetValue(selector.ParameterKey,out var value)&&value.Type==CharacterValueType.Symbol,"目标选择器必须选择已注册的文本角色参数。");
            Require(selector.Source!=NpcTargetSource.FixedObject||!string.IsNullOrWhiteSpace(selector.FixedObjectId),"固定对象选择器必须选择一个对象。");
            var selection=NpcTargetSelectorDefinition.IsSingleSource(selector.Source)?NpcTargetSelection.Nearest:selector.Selection;
            Require(selection is not (NpcTargetSelection.LowestScore or NpcTargetSelection.HighestScore)||selector.Score!=null,"评分选择策略必须配置评分表达式。");
            var score=selection is NpcTargetSelection.LowestScore or NpcTargetSelection.HighestScore?selector.Score?.Freeze():null;
            p.TargetSelectors.Add(selector.DefinitionId,new(selector.DefinitionId,selector.DisplayName,selector.Source,selector.ParameterKey,ReadOnly(selector.AllTags),ReadOnly(selector.AnyTags),ReadOnly(selector.ExcludedTags),selector.MaximumDistance,selector.RequireUsable,selector.RequireReachable,selector.Filter?.Freeze(),selection,score,selector.CandidateLimit,selector.Adapter,selector.FixedObjectId));
        }
        foreach(var c in active.Where(c=>c.FromNode==root.Id&&c.FromPort==1))
        {
            var node=(NpcGoalNodeData)graph.FindNodeById(c.ToNode);var g=(NpcGoalDefinition)Resolve(node);if(!g.Enabled)continue;
            Require(g.Completion!=null&&g.Utility!=null,"目标必须配置完成条件与评分。");
            var actionNodes=active.Where(e=>e.FromNode==node.Id&&e.FromPort==0).Select(e=>(NpcActionNodeData)graph.FindNodeById(e.ToNode)).Distinct().Where(n=>Resolve(n).Enabled).ToArray();
            Require(actionNodes.GroupBy(n=>n.DefinitionId).All(group=>group.Count()==1||!((NpcActionDefinition)Resolve(group.First())).Inputs.Any(i=>i is {IsTarget:true})),"使用目标输入的行动请复用同一个节点，不要在同一目标下放置重复定义的行动节点。");
            var actions=actionNodes.DistinctBy(n=>n.DefinitionId).Select(n=>
            {
                return p.CompileAction((NpcActionDefinition)Resolve(n),input=>
                {
                    int port=n.InputPorts.IndexOf(((NpcActionInputDefinition)input).InputId)+1;
                    var links=active.Where(e=>e.ToNode==n.Id&&e.ToPort==port&&e.FromPort==0&&graph.FindNodeById(e.FromNode) is NpcTargetSelectorNodeData).ToArray();
                    Require(port>0&&links.Length==1,"行动“"+Resolve(n).DisplayName+"”的输入“"+input.Name+"”必须连接一个选择器节点。");
                    var selectorNode=(NpcTargetSelectorNodeData)graph.FindNodeById(links[0].FromNode);
                    Require(active.Any(e=>e.FromNode==root.Id&&e.FromPort==2&&e.ToNode==selectorNode.Id),"行动输入所连选择器必须连接人物根节点："+input.Name);
                    return(selectorNode.DefinitionId,selectorNode.Id);
                });
            }).ToArray();
            var plans=active.Where(e=>e.FromNode==node.Id&&e.FromPort==1).Select(e=>(NpcPlanTemplateDefinition)Resolve((NpcTemplateNodeData)graph.FindNodeById(e.ToNode))).Where(t=>t.Enabled)
                .Select(t=>new NpcPlanSpec(t.DefinitionId,t.DisplayName,t.Priority,t.Eligibility?.Freeze(),ReadOnly(t.ActionIds.Distinct(StringComparer.Ordinal)))).ToArray();
            Require(actions.Length>0&&plans.Length>0,"目标必须连接候选行动和至少一个启用计划。");
            Require(plans.All(t=>t.ActionIds.Count>0&&t.ActionIds.All(id=>actions.Any(a=>a.Id==id))),"计划必须选择可用行动，且只能引用所属目标中已启用的候选行动。");
            p.Goals.Add(new(g.DefinitionId,g.DisplayName,g.Activation?.Freeze(),g.Utility.Freeze(),g.Completion.Freeze(),ReadOnly(actions),ReadOnly(plans)));
        }
        // An authoring scaffold may contain only modules; no goals means normal idle.
        foreach(var g in p.Goals)
        {
            if(g.Activation!=null)p.Expect(g.Activation,CharacterValueType.Boolean);p.Expect(g.Utility,CharacterValueType.Number);p.Expect(g.Completion,CharacterValueType.Boolean);
            foreach(var a in g.Actions)p.ValidateAction(a);
            foreach(var t in g.Plans)
            {
                if(t.Eligibility!=null)p.Expect(t.Eligibility,CharacterValueType.Boolean);
            }
        }
        foreach(var selector in p.TargetSelectors.Values)
        {
            foreach(var field in NpcBindingAdapters.Find(selector.Adapter).Properties)p._scope["target."+field.Name]=field.Type;
            if(selector.Filter!=null)p.Expect(selector.Filter,CharacterValueType.Boolean);
            if(selector.Score!=null)p.Expect(selector.Score,CharacterValueType.Number);
            p._scope.Clear();
        }
        var globalExpressions=p.Goals.SelectMany(g=>new[]{g.Activation,g.Utility,g.Completion}.Concat(g.Plans.Select(t=>t.Eligibility)));
        foreach(var e in globalExpressions.Where(e=>e!=null))Require(!e.FactKeys().Any(k=>k.StartsWith("target.")||k=="action.duration"),"人物、目标和计划条件尚未绑定行动目标，不能引用 target 或 action.duration。");
        foreach(var s in p.TargetSelectors.Values)foreach(var e in new[]{s.Filter,s.Score}.Where(e=>e!=null))Require(!e.FactKeys().Contains("action.duration"),"目标选择器尚未绑定行动时长。");
        return p;
    }
    void Expect(CharacterExpression expression,CharacterValueType expected)=>Require(Infer(expression)==expected,$"表达式应返回 {expected}。");
    CharacterValueType Infer(CharacterExpression e)
    {
        if(e.Kind==CharacterExpressionKind.Literal)return e.Literal.Type;
        if(e.Kind==CharacterExpressionKind.Parameter){Require(Parameters.TryGetValue(e.Key,out var value),"未知参数："+e.Key);return value.Type;}
        if(e.Kind==CharacterExpressionKind.Fact)
        {
            if(_scope.TryGetValue(e.Key,out var local))return local;
            Require(KnownKeys.Contains(e.Key),"表达式引用未知或不属于当前上下文的事实："+e.Key);
            if(Facts.TryGetValue(e.Key,out var f))return f.Type;
            if(e.Key.StartsWith("personality."))return Character.Personality[e.Key[12..]].Type;
            if(e.Key.StartsWith("preference."))return Character.Preferences[e.Key[11..]].Type;
            if(e.Key is "target.id" or "self.location")return CharacterValueType.Symbol;
            if(e.Key=="target.open"||e.Key.StartsWith("schedule.")||e.Key.StartsWith("trait.")||e.Key.EndsWith(".urgent"))return CharacterValueType.Boolean;
            return CharacterValueType.Number;
        }
        var types=e.Inputs.Select(Infer).ToArray();
        if(e.Kind==CharacterExpressionKind.Known)return CharacterValueType.Boolean;
        if(e.Kind is CharacterExpressionKind.Equal or CharacterExpressionKind.NotEqual){Require(types.Length==2&&types[0]==types[1]&&types[0]!=CharacterValueType.Unknown,"比较两侧类型不同。");return CharacterValueType.Boolean;}
        bool logical=e.Kind is CharacterExpressionKind.All or CharacterExpressionKind.Any or CharacterExpressionKind.Not;
        Require(types.All(t=>t==(logical?CharacterValueType.Boolean:CharacterValueType.Number))&&(logical||types.Length>0),"运算符与输入类型不匹配。");
        return logical||e.Kind is CharacterExpressionKind.Greater or CharacterExpressionKind.AtLeast or CharacterExpressionKind.Less or CharacterExpressionKind.AtMost?CharacterValueType.Boolean:CharacterValueType.Number;
    }
    void AddFact(NpcFactSpec spec)
    {
        Require(!Facts.ContainsKey(spec.Key),"事实重复："+spec.Key);
        Require(double.IsFinite(spec.Minimum)&&double.IsFinite(spec.Maximum)&&double.IsFinite(spec.Step)&&spec.Step>0&&spec.Minimum<=spec.Maximum&&Math.Abs(spec.Minimum/spec.Step)<long.MaxValue/2d&&Math.Abs(spec.Maximum/spec.Step)<long.MaxValue/2d,"事实数值范围无效："+spec.Key);
        Require(spec.Initial.Type==CharacterValueType.Unknown||spec.Initial.Type==spec.Type&&(spec.Type!=CharacterValueType.Number||spec.Initial.Number>=spec.Minimum&&spec.Initial.Number<=spec.Maximum),"事实初值类型/范围无效："+spec.Key);
        Facts.Add(spec.Key,spec);KnownKeys.Add(spec.Key);
    }
    public static void Require(bool condition,string message){if(!condition)throw new ArgumentException(message);}
    public CharacterValue Parameter(string key)=>Parameters.GetValueOrDefault(key);
    public CharacterValue Read(GoapWorldState state,string key)
    {
        foreach(var n in Needs)
        {
            var prefix="needs."+n.Id+".";
            if(key==prefix+"urgent")
            {
                var v=Read(state,prefix+"value");if(v.Type!=CharacterValueType.Number)return default;
                bool previous=state[key].Kind==GoapValueKind.Boolean&&state[key].Integer!=0;
                return CharacterValue.Bool(n.Higher?(previous?v.Number>n.Exit:v.Number>=n.Enter):(previous?v.Number<n.Exit:v.Number<=n.Enter));
            }
            if(key==prefix+"urgency") {var v=Read(state,prefix+"value");return v.Type==CharacterValueType.Number?CharacterValue.Numeric(Math.Pow(Math.Clamp(n.Higher?(v.Number-n.Min)/(n.Max-n.Min):(n.Max-v.Number)/(n.Max-n.Min),0,1),n.Exponent)):default;}
        }
        var value=state[key];return value.Kind switch {GoapValueKind.Boolean=>CharacterValue.Bool(value.Integer!=0),GoapValueKind.Symbol=>CharacterValue.Text(value.Symbol),GoapValueKind.Integer=>CharacterValue.Numeric(value.Integer*(Facts.TryGetValue(key,out var f)?f.Step:.001)),_=>default};
    }
    public GoapValue Encode(string key,CharacterValue value)=>value.Type switch {CharacterValueType.Boolean=>GoapValue.Bool(value.IsTrue),CharacterValueType.Symbol=>GoapValue.Id(value.Symbol),CharacterValueType.Number=>GoapValue.Number((long)Math.Round(value.Number/(Facts.TryGetValue(key,out var f)?f.Step:.001))),_=>default};
    public static string SelectionKey(string planId,string nodeId)=>"selection."+planId+"."+nodeId;
    public GoapWorldState TargetContext(GoapWorldState state,string target,double? distance=null,string adapter="point")
        =>NpcBindingAdapters.Find(adapter).Project(this,state,"target",CharacterValue.Text(target),distance,false);
    public bool TryEffects(GoapWorldState state,IReadOnlyList<NpcEffect> effects,out GoapWorldState result,NpcActionSpec action=null)
    {
        var output=new List<GoapEffect>();var values=new Dictionary<string,CharacterValue>();
        foreach(var e in effects)
        {
            var value=e.Value.Evaluate(k=>Read(state,k),Parameter);string key=EffectKey(state,action,e.Key);if(key==null){result=null;return false;}var current=values.GetValueOrDefault(key,Read(state,key));
            if(e.Mode==NpcEffectMode.Add){if(value.Type!=CharacterValueType.Number||current.Type!=CharacterValueType.Number){result=null;return false;}value=CharacterValue.Numeric(current.Number+value.Number);}
            NpcObjectProperty property=null;
            if(SplitInputMember(action,e.Key,out var input,out var member))property=NpcBindingAdapters.Find(input.Adapter)?.Properties.FirstOrDefault(p=>p.Name==member&&p.Writable);
            var spec=Facts.GetValueOrDefault(key)??new NpcFactSpec(key,key=="self.location"?CharacterValueType.Symbol:property?.Type??CharacterValueType.Unknown,property?.Minimum??0,property?.Maximum??1000000,.001,default,true);
            if(value.Type!=spec.Type||value.Type==CharacterValueType.Number&&(value.Number<spec.Minimum-1e-8||value.Number>spec.Maximum+1e-8)){result=null;return false;}
            values[key]=value;
        }
        foreach(var e in values)output.Add(GoapEffect.Set(e.Key,Encode(e.Key,e.Value)));
        result=state.With(output.ToArray());
        return true;
    }
}
