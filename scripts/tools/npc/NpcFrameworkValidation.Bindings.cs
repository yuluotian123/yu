using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.AI;
using GameLogic.Goap;
using Godot;

public partial class NpcFrameworkValidation
{
    sealed class ProbeBindingAdapter : INpcBindingAdapter
    {
        public string Id=>"validation_object";
        public string Label=>"验证对象";
        public IReadOnlyList<NpcObjectProperty> Properties=>new[]{new NpcObjectProperty("flag","标记",NpcValueType.Boolean,true)};
        public IReadOnlyList<string> Capabilities=>Array.Empty<string>();
        public IEnumerable<KeyValuePair<string,GoapValue>> Capture(AiAgentComponent3D agent)=>Array.Empty<KeyValuePair<string,GoapValue>>();
        public IEnumerable<NpcTargetCandidate> Candidates(AiAgentComponent3D agent)=>Array.Empty<NpcTargetCandidate>();
        public bool Matches(AiAgentComponent3D agent,NpcValue value,NpcTargetSelectorSpec selector)=>true;
        public GoapWorldState Project(NpcProgram program,GoapWorldState state,string prefix,NpcValue value,double? distance,bool physical)=>state.With(GoapEffect.Set(prefix+".flag",state[WriteKey("flag",value)]));
        public string WriteKey(string member,NpcValue value)=>member=="flag"?"probe."+value.Symbol+".flag":null;
        public bool Validate(AiAgentComponent3D agent,NpcValue value)=>value.Type==NpcValueType.Symbol;
        public bool CanClaim(NpcProgram program,GoapWorldState state,string prefix,string capability)=>false;
        public INpcResourceLease Acquire(AiAgentComponent3D agent,NpcValue value,string capability)=>null;
        public void Commit(AiAgentComponent3D agent,NpcValue value,string member,NpcValue result){}
    }
    void ValidateBindingResources(AiAgentComponent3D agent,AiWorldComponent3D world)
    {
        var first=world.Points["resident_bed"];var second=world.Points["food_shop"];
        NpcExpression Value(NpcValue value)=>new(NpcExpressionKind.Literal,"",value,Array.Empty<NpcExpression>());
        var inputs=new[]{new NpcBindingSpec("甲",NpcValueType.Symbol,"point",NpcBindingSource.Expression,"","",Value(NpcValue.Text(first.PointId))),
            new NpcBindingSpec("乙",NpcValueType.Symbol,"point",NpcBindingSource.Expression,"","",Value(NpcValue.Text(second.PointId)))};
        FlowGraphAsset Flow(double wait)
        {
            var flow=new FlowGraphAsset{Nodes=new List<GraphNodeData>{new FlowEntryNodeData{Id="entry"},new FlowActionNodeData{Id="wait",Actions=new(){new NpcWaitAction{DurationOverride=wait}}},new FlowReturnNodeData{Id="success",Label="Success"}}};
            flow.ConnectNodes("entry",0,"wait",0);flow.ConnectNodes("wait",0,"success",0);return flow;
        }
        var template=agent.Program.Goals[0].Actions[0];var plan=new NpcPlanSpec("resource-test","resource-test",0,null,new[]{template.Id});
        var action=template with{Name="Resource validation",Inputs=inputs,Resources=new[]{new NpcClaimSpec("甲","capacity"),new NpcClaimSpec("乙","capacity")},Precondition=null,Success=null,Execution=Flow(5),Timeout=10,
            Predictions=Array.Empty<NpcEffect>(),Results=new[]{new NpcEffect("needs.fun.value",NpcEffectMode.Set,Value(NpcValue.Numeric(.01)))}};
        NpcBoundAction Bound(NpcActionSpec value)=>new(value,plan,new Dictionary<string,NpcValue>(),null);
        double before=agent.State.Needs.Values["fun"];int completed=agent.CompletedActions;
        first.SetOpen(true);second.SetOpen(true);Check(second.TryReserve("resource_test_owner"),"Could not arrange resource contention");
        agent.BeginAction(Bound(action));
        Check(agent.LastFailure=="ResourceBusy"&&first.Reservations==0&&second.Reserved("resource_test_owner")&&agent.CurrentAction.Length==0,"Failure to acquire a later resource leaked an earlier lease");
        second.Release("resource_test_owner");
        agent.BeginAction(Bound(action));Check(first.Reserved(agent.ActorId)&&second.Reserved(agent.ActorId),"Action did not acquire multiple resources");
        agent.CancelPlan("TestMultiResourceCancel");Check(first.Reservations==0&&second.Reservations==0,"Cancellation did not release every resource");
        agent.BeginAction(Bound(action with{Timeout=.001}));agent.TickExecution(.1);
        Check(agent.LastFailure=="Timeout"&&first.Reservations==0&&second.Reservations==0,"Timeout leaked a lease");
        var far=world.Points.Values.First(p=>!p.ContainsPosition(agent.Owner.GlobalPosition));
        var physical=action with{Inputs=new[]{inputs[0] with{Value=Value(NpcValue.Text(far.PointId))}},Resources=new[]{new NpcClaimSpec("甲","capacity")},Execution=Flow(0),
            Success=new NpcExpressionDefinition{Kind=NpcExpressionKind.Fact,Key="input.甲.at"}.Freeze()};
        string location=agent.State.Location;agent.BeginAction(Bound(physical));agent.TickExecution(.1);
        Check(agent.LastFailure=="ResultValidationFailed"&&agent.CompletedActions==completed&&agent.State.Needs.Values["fun"]==before&&agent.State.Location==location&&far.Reservations==0,"A successful Flow bypassed the real spatial success condition or committed partial results");
    }
    void ValidateActionBindings(NpcGraphAsset source)
    {
        var graph=(NpcGraphAsset)source.Duplicate(true);var definition=graph.Definitions.OfType<NpcActionDefinition>().First();
        var planDefinition=graph.Definitions.OfType<NpcPlanTemplateDefinition>().First(p=>p.ActionIds.Contains(definition.DefinitionId));
        string selectorId=definition.Inputs[0].Source==NpcBindingSource.Selector?definition.Inputs[0].SelectorId:planDefinition.Variables.First(v=>v.Name==definition.Inputs[0].Variable).SelectorId;
        var selector=(NpcTargetSelectorDefinition)graph.Resolve(selectorId);selector.Source=NpcTargetSource.WorldPoints;selector.AllTags=Array.Empty<string>();selector.Filter=null;selector.ParameterKey="";selector.RequireReachable=false;
        planDefinition.Variables=new(){new(){Name="来源",Adapter="point",Source=NpcBindingSource.Selector,SelectorId=selectorId},new(){Name="去处",Adapter="point",Source=NpcBindingSource.Selector,SelectorId=selectorId}};
        definition.Inputs=new(){new(){Name="甲",Adapter="point",Source=NpcBindingSource.PlanVariable,Variable="来源"},new(){Name="乙",Adapter="point",Source=NpcBindingSource.PlanVariable,Variable="去处"},
            new(){Name="数量",ValueType=NpcValueType.Number,Value=new(){Number=2}},new(){Name="允许",ValueType=NpcValueType.Boolean,Value=new(){ValueType=NpcValueType.Boolean,Boolean=true}}};
        foreach(var move in definition.ExecutionGraph.Nodes.OfType<FlowActionNodeData>().SelectMany(n=>n.Actions).OfType<NpcMoveToTargetAction>())move.Input.Name="甲";
        definition.ExecutionGraph.SaveJsonFields();
        NpcExpressionDefinition Fact(string name)=>new(){Kind=NpcExpressionKind.Fact,Key=name};
        definition.Resources.Clear();definition.Preconditions=new(){Kind=NpcExpressionKind.All,Inputs=new(){new(){Kind=NpcExpressionKind.NotEqual,Inputs=new(){Fact("input.甲.id"),Fact("input.乙.id")}},Fact("input.允许")}};
        definition.SuccessConditions=null;definition.Cost=Fact("input.数量");definition.Predictions=new(){new(){Key="needs.fun.value",Value=new(){Number=.1}}};definition.Results=new(definition.Predictions);
        var program=NpcProgram.Compile(graph);var action=program.Goals.SelectMany(g=>g.Actions).First(a=>a.Id==definition.DefinitionId);
        var plan=program.Goals.SelectMany(g=>g.Plans).First(p=>p.Id==planDefinition.DefinitionId) with{ActionIds=new[]{action.Id}};
        var goal=program.Goals[0] with{Actions=new[]{action},Plans=new[]{plan},Completion=new NpcExpressionDefinition{Kind=NpcExpressionKind.AtMost,Inputs=new(){Fact("needs.fun.value"),new(){Number=.25}}}.Freeze()};
        var state=new GoapWorldState(program.Facts.Values.Select(f=>new KeyValuePair<string,GoapValue>(f.Key,program.Encode(f.Key,f.Initial))))
            .With(GoapEffect.Set("needs.fun.value",program.Encode("needs.fun.value",NpcValue.Numeric(.8))));
        foreach(string id in new[]{"a","b"})state=state.With(GoapEffect.Set("point."+id+".open",GoapValue.Bool(true)),GoapEffect.Set("point."+id+".stock",program.Encode("point."+id+".stock",NpcValue.Numeric(10))));
        var points=new[]{new NpcTargetCandidate("a",Array.Empty<string>(),default,1,true,true,true,true),new NpcTargetCandidate("b",Array.Empty<string>(),new(2,0,0),2,true,true,true,true)};
        var bound=NpcTargetQuery.Ground(program,goal,state,points);
        Check(bound.Count==4,"Two independent plan variables using one selector were collapsed");
        var valid=bound.Where(b=>program.Predict(state,b)!=null).ToArray();Check(valid.Length==2,"Multi-input preconditions were not evaluated for each combination");
        var chosen=valid[0];var context=program.Contextual(state,chosen);var next=program.Predict(state,chosen);
        Check(program.Read(context,"input.甲").Symbol!=program.Read(context,"input.乙").Symbol&&program.Read(context,"input.数量").Number==2&&program.Read(context,"input.允许").IsTrue,"Typed inputs did not resolve");
        Check(program.Read(next,NpcProgram.BindingKey(plan.Id,"来源")).Type==NpcValueType.Symbol&&program.Read(next,NpcProgram.BindingKey(plan.Id,"去处")).Type==NpcValueType.Symbol,"Distinct variables were not saved in the search branch");
        Check(program.Predict(next,valid[1])==null&&program.Predict(state,valid[1])!=null,"Plan variables switched after binding or leaked across searches");
        Check(!next.Facts.Any(f=>f.Key.StartsWith("input.")||f.Key=="action.duration"),"Transient action inputs polluted GOAP state identity");
        var sameSlot=action with{Inputs=action.Inputs.Select(i=>i.Name=="乙"?i with{Variable="来源"}:i).ToArray(),Precondition=null};
        var shared=NpcTargetQuery.Ground(program,goal with{Actions=new[]{sameSlot}},state,points);
        Check(shared.Count==2&&shared.All(b=>b.Candidates["甲"]==b.Candidates["乙"]),"Two inputs referencing one variable selected different objects");
        var otherPlan=plan with{Id="second-plan"};var scoped=NpcTargetQuery.Ground(program,goal with{Plans=new[]{plan,otherPlan}},state,points);
        var scopedSearch=new NpcPlanSearch(program,goal with{Plans=new[]{plan,otherPlan}},state,scoped.Select(b=>new KeyValuePair<string,GoapAction>(b.PlanActionKey,b.Search)));
        while(scopedSearch.Status==GoapSearchStatus.Pending)scopedSearch.Advance(1);
        Check(scopedSearch.Status==GoapSearchStatus.Found&&scopedSearch.Result.Steps.All(s=>s.Id.StartsWith(plan.Id+"::",StringComparison.Ordinal)),"Shared action inputs crossed plan boundaries");
        var frozenAmount=program.Read(program.Contextual(state,chosen),"input.数量").Number;definition.Inputs[2].Value.Number=99;
        Check(frozenAmount==2&&program.Read(program.Contextual(state,chosen),"input.数量").Number==2,"Planning read mutable input resources");definition.Inputs[2].Value.Number=2;
        var stockEffects=new[]{new NpcEffect("input.甲.stock",NpcEffectMode.Add,new NpcExpressionDefinition{Number=-2}.Freeze()),new NpcEffect("input.乙.stock",NpcEffectMode.Add,new NpcExpressionDefinition{Number=-3}.Freeze())};
        Check(program.TryEffects(context,stockEffects,out var stock,action)&&program.Read(stock,program.EffectKey(context,action,"input.甲.stock")).Number==8&&program.Read(stock,program.EffectKey(context,action,"input.乙.stock")).Number==7,"Multi-object result effects did not address their own input");
        var invalid=stockEffects.Append(new NpcEffect("input.甲.stock",NpcEffectMode.Add,new NpcExpressionDefinition{Number=-100}.Freeze())).ToArray();
        Check(!program.TryEffects(context,invalid,out _,action)&&program.Read(context,"input.甲.stock").Number==10,"Invalid multi-object transaction changed its source");
        bool Rejects(Action edit,Action undo)
        {
            edit();bool rejected=false;try{NpcProgram.Compile(graph);}catch(ArgumentException){rejected=true;}undo();return rejected;
        }
        Check(Rejects(()=>definition.Inputs[0].Variable="missing",()=>definition.Inputs[0].Variable="来源"),"Missing plan variable was accepted");
        Check(Rejects(()=>definition.Inputs[2].ValueType=NpcValueType.Boolean,()=>definition.Inputs[2].ValueType=NpcValueType.Number),"An input with a mismatched expression type was accepted");
        Check(Rejects(()=>definition.Preconditions=Fact("input.别人的参数.at"),()=>definition.Preconditions=null),"Another action's input was visible in this action");
        Check(Rejects(()=>definition.Resources.Add(new(){Input="数量"}),()=>definition.Resources.Clear()),"A scalar input could be claimed as an object resource");
        if(NpcBindingAdapters.Find("validation_object")==null)NpcBindingAdapters.Register(new ProbeBindingAdapter());
        var extraInput=new NpcBindingDefinition{Name="物品",Adapter="validation_object",Value=new(){ValueType=NpcValueType.Symbol,Symbol="token"}};
        var extraEffect=new NpcEffectDefinition{Key="input.物品.flag",Value=new(){ValueType=NpcValueType.Boolean,Boolean=true}};
        definition.Inputs.Add(extraInput);definition.Results.Add(extraEffect);
        var extended=NpcProgram.Compile(graph);var extraAction=extended.Goals.SelectMany(g=>g.Actions).First(a=>a.Id==action.Id);
        var extraContext=extended.Contextual(state.With(GoapEffect.Set("probe.token.flag",GoapValue.Bool(false))),chosen with{Action=extraAction});
        Check(extended.TryEffects(extraContext,extraAction.Results,out var extraResult,extraAction)&&extended.Read(extraResult,"probe.token.flag").IsTrue,"A registered object adapter could not provide typed, writable input facts");
        definition.Inputs.Remove(extraInput);definition.Results.Remove(extraEffect);
#if TOOLS
        Check(NpcExpressionEditorCatalog.For(graph,definition).Any(s=>s.Key=="input.甲.at")&&NpcExpressionEditorCatalog.For(graph,definition).Any(s=>s.Key=="input.数量"&&s.Type==NpcValueType.Number),"Input facts were not registered in the action editor");
        Check(!NpcExpressionEditorCatalog.For(graph,planDefinition).Any(s=>s.Key.StartsWith("input.")),"Action input symbols leaked into plan eligibility");
        foreach(string property in new[]{nameof(NpcActionDefinition.TargetSelectorId),nameof(NpcActionDefinition.ShareTargetWithinPlan),nameof(NpcActionDefinition.RequireAtTarget),nameof(NpcActionDefinition.ArrivesAtTarget),nameof(NpcActionDefinition.ReserveTarget),nameof(NpcActionDefinition.DistanceCost)})
            Check(!definition.GetPropertyList().Any(p=>p["name"].AsString()==property&&((PropertyUsageFlags)p["usage"].AsInt64()).HasFlag(PropertyUsageFlags.Editor)),"Special target switch is still editable: "+property);
        var editor=graph.CreateCustomPropertyEditor(definition,nameof(definition.Inputs));AddChild(editor);editor._UpdateProperty();
        using(var history=new UndoRedo())
        {
            editor.PropertyChanged+=(property,value,_,_)=>{history.CreateAction("Add action input");history.AddDoProperty(definition,property,value);history.AddUndoProperty(definition,property,definition.Get(property));history.CommitAction();};
            editor.FindChildren("*",nameof(Button),true,false).OfType<Button>().Single(b=>b.Text=="+ 添加行动输入").EmitSignal(Button.SignalName.Pressed);
            Check(definition.Inputs.Count==5,"Input list did not apply its native edit");history.Undo();Check(definition.Inputs.Count==4,"Input list cannot undo an addition");history.Redo();Check(definition.Inputs.Count==5,"Input list cannot redo an addition");history.Undo();
        }
        editor.QueueFree();
        var inputReference=new NpcInputReference{Name="甲"};var flowUi=inputReference.CreateEditUI(new(){RootGraph=graph,CurrentGraph=definition.ExecutionGraph});
        Check(!flowUi.FindChildren("*",nameof(LineEdit),true,false).Any()&&flowUi.FindChildren("*",nameof(Button),true,false).OfType<Button>().Any(b=>b.Text=="甲"),"Flow input reference requires a hand-entered name");flowUi.Free();
#endif
        graph.SaveJsonFields();string path="res://.godot/npc/action_inputs/bindings_roundtrip.tres";
        Check(ResourceSaver.Save(graph,path)==Error.Ok,"Could not save generic inputs");var loaded=ResourceLoader.Load<NpcGraphAsset>(path,cacheMode:ResourceLoader.CacheMode.Ignore);
        Check(NpcProgram.Compile(loaded).Goals.SelectMany(g=>g.Actions).First(a=>a.Id==action.Id).Inputs.Count==4,"Multiple inputs were lost on reload");
    }
}
