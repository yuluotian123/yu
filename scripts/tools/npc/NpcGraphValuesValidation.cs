using System;
using System.Collections.Generic;
using System.Linq;
using Framework;
using GameLogic;
using GameLogic.AI;
using GameLogic.Goap;
using Godot;

public partial class NpcGraphValuesValidation : Node
{
    int _checks;
    void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);_checks++;}
    void Reject(Action test,string message){try{test();}catch(ArgumentException){_checks++;return;}throw new InvalidOperationException(message);}
    static NpcGraphNumber Min(NpcGraphNumber left,NpcGraphNumber right)=>new(){Kind=CharacterExpressionKind.Minimum,Inputs=new(){left,right}};
    static NpcGraphNumber Product(NpcGraphNumber left,NpcGraphNumber right)=>new(){Kind=CharacterExpressionKind.Multiply,Inputs=new(){left,right}};
    sealed class Fixture
    {
        public readonly NpcSelectorBindingValidation.Fixture Outer=new();
        public readonly FlowGraphAsset Flow=new();
        public readonly FlowEntryNodeData Entry=new();
        public readonly FlowActionNodeData Work=new();
        public readonly FlowReturnNodeData Done=new(){Label="Success"};
        public readonly NpcCalculateNumberAction Calculate=new(){Local=new(){Key="take_count"},Value=Min(NpcGraphNumber.Fact("input.target.stock"),NpcGraphNumber.Constant(5))};
        public readonly NpcWaitAction Wait=new(){Seconds=Product(NpcGraphNumber.Fact("local.take_count"),NpcGraphNumber.Constant(.01))};
        public readonly NpcInteractAction Interaction=new(){Input=new(){Name="target"},OperationId="take_stock",Amount=NpcGraphNumber.Fact("local.take_count")};
        public Fixture()
        {
            Outer.Selector.RequireReachable=false;
            Flow.BlackboardEntries.Add(new(){Key="take_count",Value=new GraphFloatBlackboardValue()});
            Flow.Nodes.Add(Entry);Flow.Nodes.Add(Work);Flow.Nodes.Add(Done);
            Work.Actions.Add(Calculate);Work.Actions.Add(Wait);Work.Actions.Add(Interaction);
            Flow.ConnectNodes(Entry.Id,0,Work.Id,0);Flow.ConnectNodes(Work.Id,0,Done.Id,0);Flow.SaveJsonFields();
            Outer.First.ExecutionGraph=Flow;Outer.First.Cost=new(){Kind=CharacterExpressionKind.Fact,Key="action.duration"};
            Outer.First.Results.Add(new(){Key="private.started",Value=new(){ValueType=CharacterValueType.Boolean,Boolean=true}});
        }
        public NpcProgram Compile()=>NpcProgram.Compile(Outer.Graph);
    }
    static NpcTargetCandidate[] Points()=>new[]{
        new NpcTargetCandidate("A",Array.Empty<string>(),Vector3.Zero,1,true,true,true,true,Operations:new[]{"take_stock"}),
        new NpcTargetCandidate("B",Array.Empty<string>(),Vector3.Right,2,true,true,true,true,Operations:new[]{"take_stock"})};
    static GoapWorldState State(NpcProgram program,double stockA=10,double stockB=3)
    {
        var facts=new List<KeyValuePair<string,GoapValue>>{new("private.started",GoapValue.Bool(false)),new("private.done",GoapValue.Bool(false)),new("self.location",GoapValue.Id(""))};
        foreach(var point in Points())
        {
            string key="point."+point.Id;
            facts.Add(new(key+".open",GoapValue.Bool(true)));facts.Add(new(key+".available",GoapValue.Bool(true)));
            facts.Add(new(key+".stock",program.Encode(key+".stock",CharacterValue.Numeric(point.Id=="A"?stockA:stockB))));
            facts.Add(new(key+".distance",program.Encode(key+".distance",CharacterValue.Numeric(point.Distance))));
            facts.Add(new(NpcProgram.OperationKey(point.Id,"take_stock")+".resource",GoapValue.Id("stock")));
            facts.Add(new(NpcProgram.OperationKey(point.Id,"take_stock")+".capacity",GoapValue.Bool(true)));
        }
        return new(facts);
    }
    static NpcBoundAction Bound(Fixture fixture,NpcProgram program,GoapWorldState state,string id)=>NpcTargetQuery.Ground(program,program.Goals[0],state,Points())
        .Single(b=>b.Action.Id==fixture.Outer.First.DefinitionId&&b.Candidates["target"].Symbol==id);
    public override void _Ready()
    {
        try
        {
            Planning();Validation();RoundTrip();Execution();
            GC.Collect();GC.WaitForPendingFinalizers();
            GD.Print($"NPC_GRAPH_VALUES_OK: {_checks} checks");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());ModuleSystem.Shutdown();GetTree().Quit(1);}
    }
    void Planning()
    {
        var fixture=new Fixture();var program=fixture.Compile();var state=State(program);var bound=Bound(fixture,program,state,"B");
        Check(bound.Action.Inputs.Count==1&&bound.Action.Inputs[0].IsTarget,"Graph quantities must not create Action inputs");
        Check(bound.Search.TryApply(state,out var result)&&program.Read(result,"point.B.stock").Number==0,"Planning must derive consumption from the graph's calculated amount");
        Check(program.Read(state,"point.B.stock").Number==3,"Planning must not mutate its source snapshot");
        Check(Math.Abs(program.Read(program.Contextual(state,bound),"action.duration").Number-.03)<1e-7,"Graph waiting time must feed the action's duration cost");
        var all=NpcTargetQuery.Ground(program,program.Goals[0],state,Points());
        var search=new NpcPlanSearch(program,program.Goals[0],state,all.Select(b=>new KeyValuePair<string,GoapAction>(b.PlanActionKey,b.Search)));
        for(int i=0;i<100&&search.Status==GoapSearchStatus.Pending;i++)search.Advance(32);
        Check(search.Status==GoapSearchStatus.Found&&search.Result.Steps.Count==2,"Graph quantity predictions must participate in a complete GOAP plan");
        fixture.Calculate.Value.Inputs[1].Number=99;
        Check(Bound(fixture,program,state,"A").Search.TryApply(state,out result)&&program.Read(result,"point.A.stock").Number==5,"Compiled graph formulas must remain frozen after editor changes");
        Check(fixture.Outer.Selector.RequiredOperations.Length==0&&program.QuerySelector(bound.Action,bound.Action.Inputs[0]).RequiredOperations.Single()=="take_stock","Operation filtering must be derived per Action without mutating selector resources");
        var noOperation=Points().Select(p=>p with{Operations=Array.Empty<string>()}).ToArray();
        Check(!NpcTargetQuery.Ground(program,program.Goals[0],state,noOperation).Any(b=>b.Action.Id==fixture.Outer.First.DefinitionId),"An unsupported operation must eliminate only that Action's candidates");
        var unknown=state.With(GoapEffect.Set("point.B.stock",default));
        Check(!bound.Search.TryApply(unknown,out _),"Unknown quantity facts must not become zero or use authoritative data");
        var liquid=new Fixture();liquid.Work.Actions.Remove(liquid.Calculate);liquid.Interaction.OperationId="pour_liquid";liquid.Interaction.Amount=NpcGraphNumber.Constant(.25);liquid.Wait.Seconds=NpcGraphNumber.Constant(0);
        var liquidProgram=liquid.Compile();var liquidPoints=Points().Select(p=>p with{Operations=new[]{"pour_liquid"}}).ToArray();
        var liquidState=State(liquidProgram).With(GoapEffect.Set("point.B.liquid",liquidProgram.Encode("point.B.liquid",CharacterValue.Numeric(.5))),
            GoapEffect.Set(NpcProgram.OperationKey("B","pour_liquid")+".resource",GoapValue.Id("liquid")));
        var pour=NpcTargetQuery.Ground(liquidProgram,liquidProgram.Goals[0],liquidState,liquidPoints).Single(b=>b.Action.Id==liquid.Outer.First.DefinitionId&&b.Candidates["target"].Symbol=="B");
        Check(pour.Search.TryApply(liquidState,out result)&&liquidProgram.Read(result,"point.B.liquid").Number==.25,"Liquid quantity must use the same automatic graph prediction");
        var changed=new Fixture();changed.Outer.Profile.Facts.Facts.Add(new(){Key="private.quantity",Type=CharacterValueType.Number,Minimum=0,Maximum=100,Initial=new(){Number=5}});
        changed.Interaction.Amount=NpcGraphNumber.Fact("private.quantity");changed.Outer.First.Predictions.Add(new(){Key="private.quantity",Value=new(){Number=1}});
        var changedProgram=changed.Compile();var changedState=State(changedProgram).With(GoapEffect.Set("private.quantity",changedProgram.Encode("private.quantity",CharacterValue.Numeric(5))));
        Check(Bound(changed,changedProgram,changedState,"A").Search.TryApply(changedState,out result)&&changedProgram.Read(result,"point.A.stock").Number==5
            &&changedProgram.Read(result,"private.quantity").Number==1,"Interaction quantity must use the pre-action snapshot before role prediction effects");
    }
    void Validation()
    {
        var fixture=new Fixture();fixture.Work.Actions.Remove(fixture.Calculate);fixture.Work.Actions.Add(fixture.Calculate);
        Reject(()=>fixture.Compile(),"A graph local must be calculated before its first use");
        fixture=new();fixture.Calculate.Value=NpcGraphNumber.Fact("local.take_count");Reject(()=>fixture.Compile(),"Recursive graph locals must be rejected");
        fixture=new();fixture.Interaction.Amount=NpcGraphNumber.Constant(0);Reject(()=>fixture.Compile(),"Zero consumption must be rejected");
        fixture=new();fixture.Wait.Seconds=NpcGraphNumber.Constant(-1);Reject(()=>fixture.Compile(),"Negative graph duration must be rejected");
        fixture=new();fixture.Interaction.Amount=NpcGraphNumber.Fact("local.missing");Reject(()=>fixture.Compile(),"Unknown graph locals must be rejected");
        fixture=new();var bypass=new FlowReturnNodeData{Label="Success"};fixture.Flow.Nodes.Add(bypass);fixture.Flow.ConnectNodes(fixture.Entry.Id,0,bypass.Id,0);Reject(()=>fixture.Compile(),"A success path cannot bypass the declared interaction");
        fixture=new();var failure=new FlowReturnNodeData{Label="Success"};fixture.Flow.Nodes.Add(failure);fixture.Flow.ConnectNodes(fixture.Work.Id,1,failure.Id,0);Reject(()=>fixture.Compile(),"Interaction failure cannot be turned into a planned success");
        fixture=new();fixture.Outer.First.Predictions.Add(new(){Key="input.target.stock",Value=new(){Number=0}});Reject(()=>fixture.Compile(),"Automatic graph predictions must reject duplicate target effects");
        fixture=new();fixture.Work.Actions.Insert(0,new SetBlackboardAction{Key=new(){Key="take_count"},Value=GraphActionValue.Number(99)});
        Reject(()=>fixture.Compile(),"Planning locals cannot be overwritten by unrelated blackboard writers");
        fixture=new();fixture.Interaction.Amount=NpcGraphNumber.Fact("input.target.id");Reject(()=>fixture.Compile(),"Graph numeric fields must reject symbol facts");
    }
    void RoundTrip()
    {
        var fixture=new Fixture();fixture.Outer.Graph.SaveJsonFields();
        const string path="res://.godot/npc-graph-values-validation/graph.tres";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(path.GetBaseDir()));
        Check(ResourceSaver.Save(fixture.Outer.Graph,path)==Error.Ok,"Graph numeric configuration must save");
        var graph=ResourceLoader.Load<NpcGraphAsset>(path,"",ResourceLoader.CacheMode.Ignore);var program=NpcProgram.Compile(graph,fixture.Outer.Profile);
        var state=State(program);var bound=NpcTargetQuery.Ground(program,program.Goals[0],state,Points()).Single(b=>b.Action.Id==fixture.Outer.First.DefinitionId&&b.Candidates["target"].Symbol=="A");
        Check(bound.Search.TryApply(state,out var result)&&program.Read(result,"point.A.stock").Number==5,"Reloaded graph formulas must preserve quantity and locals");
#if TOOLS
        var context=new GraphEditorContext{RootGraph=fixture.Outer.Graph,CurrentGraph=fixture.Flow};
        var ui=fixture.Interaction.CreateEditUI(context);
        try{Check(ui.FindChildren("*",nameof(Button),true,false).OfType<Button>().Any(b=>b.Text=="编辑数值／公式"),"Interaction node must expose its own number editor");}
        finally{ui.Free();}
        using var owner=new NpcGraphExpressionOwner{Action=fixture.Outer.First,Flow=fixture.Flow};
        var symbols=NpcExpressionEditorCatalog.For(fixture.Outer.Graph,owner);
        Check(symbols.Any(s=>s.Key=="local.take_count")&&symbols.Any(s=>s.Key=="input.target.stock"),"Graph expression picker must include local numbers and enclosing target facts");
#endif
    }
    void Execution()
    {
        var fixture=new Fixture();var hosts=new List<GameObject3D>();var blackboard=new GraphBlackboardNode();AddChild(blackboard);
        GameObject3D Host(string name,params Component3D[] components)
        {
            var host=WorldTestObjects.WithArea(new GameObject3D{Name=name,PersistentId=name,Components=new(components)});AddChild(host);hosts.Add(host);return host;
        }
        try
        {
            Host("GraphNumberWorld",new AiWorldComponent3D());
            WorldInteractionComponent3D Point(string name,double amount)
            {
                var type=WorldTestObjects.StockType(amount);type.Capability<WorldInteractionCapability>().Operations.Add(new WorldTakeStockInteraction{RequireCapacity=true});
                var point=Host(name,WorldTestObjects.Components(type)).GetComponent<WorldInteractionComponent3D>();point.RequireLineOfSight=false;return point;
            }
            var a=Point("A",10);var b=Point("B",3);
            AiAgentComponent3D Actor(string name)=>Host(name,new CharacterDataComponent3D{Profile=fixture.Outer.Profile,Persist=false},new AiAgentComponent3D{Graph=fixture.Outer.Graph,Persist=false,Running=false}).GetComponent<AiAgentComponent3D>();
            var first=Actor("GraphNumberActor1");var second=Actor("GraphNumberActor2");
            var action=first.Program.Goals[0].Actions[0];var request=first.Program.QuerySelector(action,action.Inputs[0]) with{Limit=1};
            a.Owner.GetComponent<WorldStockComponent3D>().Amount=0;b.Owner.Position=Vector3.Right;
            Check(NpcBindingAdapters.Find("point").Candidates(first,request).Single().Id=="B","Empty nearby objects must not fill the candidate limit before a valid farther object");
            a.Owner.GetComponent<WorldStockComponent3D>().Amount=10;b.Owner.Position=Vector3.Zero;
            (FlowGraphRuntime Run,NpcActionContext Call,GraphExecutionContext Context) Run(AiAgentComponent3D actor,string id)
            {
                var bound=Bound(fixture,actor.Program,State(actor.Program),id);var start=actor.Program.Contextual(actor.Snapshot(new[]{id}),bound,physical:true);
                var call=new NpcActionContext(actor,bound.Action,new Dictionary<string,CharacterValue>{{"target",CharacterValue.Text(id)}},start);
                var context=new GraphExecutionContext(fixture.Flow,new GraphBlackboardRuntime(blackboard));context.UserData.Add(actor.Owner);context.UserData.Add(call);
                var run=new FlowGraphRuntime(fixture.Flow,context);Check(run.Start(),"Execution graph must start");return(run,call,context);
            }
            var one=Run(first,"B");var two=Run(second,"A");
            Check(one.Context.Blackboard.GetValue<float>("take_count")==3&&two.Context.Blackboard.GetValue<float>("take_count")==5,"Shared graph must have independent local values per NPC execution");
            Check(b.Owner.GetComponent<WorldStockComponent3D>().Amount==3&&a.Owner.GetComponent<WorldStockComponent3D>().Amount==10,"Calculating and waiting must not consume stock");
            one.Run.Update(.04);two.Run.Update(.06);
            Check(one.Run.ReturnLabels.Contains("Success")&&two.Run.ReturnLabels.Contains("Success"),"Both graph-local wait durations must complete independently");
            Check(b.Owner.GetComponent<WorldStockComponent3D>().Amount==3,"Reaching the interaction node must only queue its request");
            Check(one.Call.CommitInteraction().Success&&b.Owner.GetComponent<WorldStockComponent3D>().Amount==0,"First NPC must commit the graph quantity, including the final units");
            Check(two.Call.CommitInteraction().Success&&a.Owner.GetComponent<WorldStockComponent3D>().Amount==5,"Second NPC must commit its own graph quantity");
            Check(two.Call.CommitInteraction().Success&&a.Owner.GetComponent<WorldStockComponent3D>().Amount==5,"Repeated submission must not consume twice");
            Check(((WorldTakeStockInteraction)a.Operations[0]).Amount==1&&a.Owner.GetComponent<WorldUsageCapacityComponent3D>().Reservations==0,"Node parameters must not mutate shared operation defaults or leak capacity");
            one.Run.Stop();two.Run.Stop();
            b.Owner.GetComponent<WorldStockComponent3D>().Amount=2.1;var fractional=Run(first,"B");fractional.Run.Update(.03);
            Check(fractional.Call.CommitInteraction().Success&&b.Owner.GetComponent<WorldStockComponent3D>().Amount==0,"Fractional local calculations must not lose the last stock units to Float rounding");fractional.Run.Stop();
            b.Owner.GetComponent<WorldStockComponent3D>().Amount=3;var cancelled=Run(first,"B");cancelled.Run.Stop();
            Check(!cancelled.Call.CommitInteraction().Success&&b.Owner.GetComponent<WorldStockComponent3D>().Amount==3,"Cancelled execution before interaction cannot commit a predicted effect");
            var again=Run(first,"B");Check(again.Context.Blackboard.GetValue<float>("take_count")==3,"A new execution must recreate its local scope");
            again.Run.Update(.04);b.Owner.GetComponent<WorldStockComponent3D>().Amount=1;
            Check(!again.Call.CommitInteraction().Success&&b.Owner.GetComponent<WorldStockComponent3D>().Amount==1,"Final live validation must reject depleted stock without partial changes");again.Run.Stop();
            Check(!WorldInteraction.Execute(first.Owner,a,"take_stock",double.NaN).Success&&!WorldInteraction.Execute(first.Owner,a,"take_stock",-1).Success,"Invalid runtime quantity requests must be rejected");
            var type=new WorldObjectTypeDefinition{Capabilities=new(){new WorldLiquidCapability{InitialVolume=1},new WorldInteractionCapability{Operations=new(){new WorldConsumeLiquidInteraction()}}}};
            var bottle=Host("Bottle",WorldTestObjects.Components(type)).GetComponent<WorldInteractionComponent3D>();bottle.RequireLineOfSight=false;
            Check(WorldInteraction.Execute(first.Owner,bottle,"pour_liquid",.5).Success&&bottle.Owner.GetComponent<WorldLiquidComponent3D>().Volume==.5,"Shared interaction entry must support an explicit liquid quantity");
            Check(((WorldConsumeLiquidInteraction)bottle.Operations[0]).Amount==.25,"Explicit liquid quantity must leave player defaults unchanged");
            var boundIntegrated=Bound(fixture,first.Program,State(first.Program),"A");first.BeginAction(boundIntegrated);first.TickExecution(.06);
            Check(first.CompletedActions==1&&a.Owner.GetComponent<WorldStockComponent3D>().Amount==0,"AI lifecycle must use the same graph parameter and commit once");
        }
        finally
        {
            foreach(var host in hosts.AsEnumerable().Reverse())host.Free();blackboard.Free();ModuleSystem.Shutdown();
        }
    }
}
