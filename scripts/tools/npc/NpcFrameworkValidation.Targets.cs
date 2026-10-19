using System;
using System.Linq;
using System.Collections.Generic;
using GameLogic.AI;
using GameLogic.Goap;
using Godot;

public partial class NpcFrameworkValidation
{
    void PrepareRuntimeTargetFixture(GameLogic.GameObject3D host)
    {
        // Isolated test configuration: exercise two separate real Flow executions without editing the scene asset.
        var original=host.Components.OfType<AiAgentComponent3D>().Single();
        var component=(AiAgentComponent3D)original.Duplicate(true);host.Components[host.Components.IndexOf(original)]=component;
        component.Graph.MigrateActionBindings();component.Graph.SaveJsonFields();
        var graph=component.Graph=(NpcGraphAsset)component.Graph.Duplicate(true);
        var goal=graph.Nodes.OfType<NpcGoalNodeData>().First();
        var activityNode=(NpcActionNodeData)graph.FindNodeById(graph.GetOutgoingConnections(goal.Id,0).First().ToNode);
        var activity=(NpcActionDefinition)graph.Resolve(activityNode.DefinitionId);
        FlowGraphAsset Execution(GraphActionBase ability)
        {
            var flow=new FlowGraphAsset{Nodes=new List<GraphNodeData>{new FlowEntryNodeData{Id="entry"},new FlowActionNodeData{Id="step",Actions=new(){ability}},new FlowReturnNodeData{Id="success",Label="Success"},new FlowReturnNodeData{Id="failure",Label="Failure"}}};
            flow.ConnectNodes("entry",0,"step",0);flow.ConnectNodes("step",0,"success",0);flow.ConnectNodes("step",1,"failure",0);flow.SaveJsonFields();return flow;
        }
        activity.DisplayName="Validation activity";activity.Preconditions=new NpcExpressionDefinition{Kind=NpcExpressionKind.All,Inputs=new(){activity.Preconditions,new(){Kind=NpcExpressionKind.Fact,Key="input.地点.at"}}};
        activity.SuccessConditions=null;activity.Predictions=new(activity.Predictions.Where(e=>e.Key!="self.location"));activity.ExecutionGraph=Execution(new NpcWaitAction());
        var originalInput=activity.Inputs[0];string selectorId=originalInput.SelectorId;
        var variable=originalInput.Source==NpcBindingSource.PlanVariable?originalInput.Variable:"验证地点";
        originalInput.Source=NpcBindingSource.PlanVariable;originalInput.Variable=variable;originalInput.SelectorId="";
        var movement=new NpcActionDefinition{DisplayName="Validation movement",BindingVersion=2,TimeoutSeconds=60,ExecutionGraph=Execution(new NpcMoveToTargetAction()),
            Inputs=new(){new(){Name="地点",Adapter="point",Source=NpcBindingSource.PlanVariable,Variable=variable}},
            SuccessConditions=new(){Kind=NpcExpressionKind.Fact,Key="input.地点.at"},
            Predictions=new(){new(){Key="self.location",Value=new(){Kind=NpcExpressionKind.Fact,Key="input.地点.id"}}},Resources=new(){new(){Input="地点"}}};
        var movementNode=new NpcActionNodeData{Id="validation_move",DefinitionId=movement.DefinitionId};graph.Definitions.Add(movement);graph.Nodes.Add(movementNode);graph.MarkDirty();graph.ConnectNodes(goal.Id,0,movementNode.Id,0);
        foreach(var edge in graph.GetOutgoingConnections(goal.Id,1))
        {
            var plan=(NpcPlanTemplateDefinition)graph.Resolve(((NpcTemplateNodeData)graph.FindNodeById(edge.ToNode)).DefinitionId);
            if(plan.ActionIds.Contains(activity.DefinitionId))
            {
                if(!plan.Variables.Any(v=>v.Name==variable))plan.Variables.Add(new(){Name=variable,Adapter="point",Source=NpcBindingSource.Selector,SelectorId=selectorId});
                plan.ActionIds=plan.ActionIds.Append(movement.DefinitionId).ToArray();
            }
        }
        graph.SaveJsonFields();NpcProgram.Compile(graph);
    }
    void ValidateTargetSelectors(NpcGraphAsset source)
    {
        var graph=(NpcGraphAsset)source.Duplicate(true);
        var root=graph.Nodes.OfType<NpcRootNodeData>().Single();
        var definition=new NpcTargetSelectorDefinition{DisplayName="娱乐场所",Source=NpcTargetSource.WorldPoints,AllTags=new[]{"test_leisure"}};
        var selectorNode=new NpcTargetSelectorNodeData{Id="test_targets",DefinitionId=definition.DefinitionId};
        graph.Definitions.Add(definition);graph.Nodes.Add(selectorNode);graph.MarkDirty();
        Check(graph.ConnectNodes(root.Id,2,selectorNode.Id,0),"Could not connect a reusable target selector");
        var p=NpcProgram.Compile(graph);var selector=p.TargetSelectors[definition.DefinitionId];
        var template=p.Goals[0].Actions[0];
        NpcExpression Fact(string key)=>new NpcExpressionDefinition{Kind=NpcExpressionKind.Fact,Key=key}.Freeze();
        var variable=new NpcBindingSpec("场所",NpcValueType.Symbol,"point",NpcBindingSource.Selector,selector.Id,"",null);
        var input=variable with{Name="地点",Source=NpcBindingSource.PlanVariable,SelectorId="",Variable="场所"};
        var move=template with{Id="move",Name="前往",Inputs=new[]{input},Resources=Array.Empty<NpcClaimSpec>(),Precondition=null,Success=Fact("input.地点.at"),
            Cost=new NpcExpressionDefinition{Kind=NpcExpressionKind.Add,Inputs=new(){new(){Number=1},new(){Kind=NpcExpressionKind.Fact,Key="input.地点.distance"}}}.Freeze(),
            Predictions=new[]{new NpcEffect("self.location",NpcEffectMode.Set,Fact("input.地点.id"))},Results=Array.Empty<NpcEffect>()};
        var play=move with{Id="play",Name="玩",Precondition=Fact("input.地点.at"),Success=null,Predictions=new[]{new NpcEffect("needs.fun.value",NpcEffectMode.Set,new NpcExpressionDefinition{Number=.15}.Freeze())}};
        var completion=new NpcExpressionDefinition{Kind=NpcExpressionKind.AtMost,Inputs=new(){new(){Kind=NpcExpressionKind.Fact,Key="needs.fun.value"},new(){Number=.25}}}.Freeze();
        var pool=new NpcPlanSpec("pool","娱乐",0,null,new[]{move.Id,play.Id},new[]{variable});
        var goal=p.Goals[0] with{Id="test",Completion=completion,Actions=new[]{move,play},Plans=new[]{pool}};
        NpcBoundAction Bound(NpcActionSpec action,string id)=>new(action,pool,new Dictionary<string,NpcValue>{{"地点",NpcValue.Text(id)}},null);
        GoapWorldState Snapshot(string location="outside")
        {
            var state=new GoapWorldState().With(GoapEffect.Set("self.location",GoapValue.Id(location)),GoapEffect.Set("needs.fun.value",p.Encode("needs.fun.value",NpcValue.Numeric(.8))));
            foreach(var (id,stock,open) in new[]{("home",2d,true),("town",9d,true),("closed",5d,false)})
                state=state.With(GoapEffect.Set("point."+id+".stock",p.Encode("target.stock",NpcValue.Numeric(stock))),GoapEffect.Set("point."+id+".open",GoapValue.Bool(open)));
            return state;
        }
        NpcTargetCandidate[] Points(string location="outside")=>new[]{
            new NpcTargetCandidate("home",new[]{"test_leisure","indoor"},new(4,0,0),location=="home"?0:4,true,true,false,true),
            new NpcTargetCandidate("town",new[]{"test_leisure","outdoor"},new(1,0,0),location=="town"?0:1,true,true,true,false),
            new NpcTargetCandidate("closed",new[]{"test_leisure"},new(2,0,0),2,false,true,true,true),
            new NpcTargetCandidate("unreachable",new[]{"test_leisure"},new(3,0,0),3,true,false,true,true)};
        string[] Select(NpcTargetSelectorSpec spec,string location="outside")=>NpcTargetQuery.Select(p,spec,Snapshot(location),Points(location)).Select(c=>c.Id).ToArray();
        Check(Select(selector).SequenceEqual(new[]{"town","home"}),"Candidate query did not filter unusable/unreachable points or sort deterministically");
        Check(Select(selector with{Source=NpcTargetSource.PerceivedPoints}).SequenceEqual(new[]{"town"}),"Perceived source leaked unseen targets");
        Check(Select(selector with{Source=NpcTargetSource.KnownPoints}).SequenceEqual(new[]{"home"}),"Known source leaked unknown targets");
        Check(Select(selector with{Source=NpcTargetSource.CurrentLocation},"home").SequenceEqual(new[]{"home"}),"Current-location selector did not resolve its point");
        Check(Select(selector with{Source=NpcTargetSource.CurrentLocation}).Length==0,"Missing current location fell back to arbitrary targets");
        p.Parameters["test_place"]=NpcValue.Text("home");
        Check(Select(selector with{Source=NpcTargetSource.IdentityParameter,ParameterKey="test_place"}).SequenceEqual(new[]{"home"}),"Identity parameter target was not resolved");
        p.Parameters["test_place"]=NpcValue.Text("");
        Check(Select(selector with{Source=NpcTargetSource.IdentityParameter,ParameterKey="test_place"}).Length==0,"Empty parameter selected an unrelated target");
        Check(Select(selector with{AnyTags=new[]{"indoor"}}).SequenceEqual(new[]{"home"}),"Any-tag filter failed");
        Check(Select(selector with{ExcludedTags=new[]{"indoor"},MaximumDistance=2}).SequenceEqual(new[]{"town"}),"Excluded tags/distance filter failed");
        Check(Select(selector with{Limit=1}).SequenceEqual(new[]{"town"}),"Candidate limit was ignored");
        Check(Select(selector with{RequireReachable=false}).Contains("unreachable")&&Select(selector with{RequireUsable=false}).Contains("closed"),"Optional feasibility filters could not be configured");
        var stock=new NpcExpressionDefinition{Kind=NpcExpressionKind.Fact,Key="target.stock"}.Freeze();
        Check(Select(selector with{Selection=NpcTargetSelection.HighestScore,Score=stock}).SequenceEqual(new[]{"town"}),"Highest-score strategy did not evaluate each candidate");
        Check(Select(selector with{Selection=NpcTargetSelection.LowestScore,Score=stock}).SequenceEqual(new[]{"home"}),"Lowest-score strategy failed");
        Check(Select(selector with{Selection=NpcTargetSelection.Nearest}).SequenceEqual(new[]{"town"}),"Nearest strategy did not reduce the candidates");
        Check(Select(selector with{Filter=new NpcExpressionDefinition{Kind=NpcExpressionKind.Fact,Key="missing"}.Freeze()}).Length==0,"Unknown filter was treated as true");
        NpcPlanSearch Search(string location)
        {
            var state=Snapshot(location);var grounded=NpcTargetQuery.Ground(p,goal,state,Points(location));
            var search=new NpcPlanSearch(p,goal,state,grounded.Select(a=>new KeyValuePair<string,GoapAction>(a.PlanActionKey,a.Search)));
            for(int i=0;i<4096&&search.Status==GoapSearchStatus.Pending;i++)search.Advance(1);
            Check(search.Status==GoapSearchStatus.Found,"Target-bound plan could not complete");return search;
        }
        var outside=Search("outside");
        Check(outside.Result.Steps.Select(s=>s.Id.Split("::").Last()).SequenceEqual(new[]{"move@town","play@town"}),"GOAP did not bind movement and activity to the same target");
        Check(Math.Abs(outside.Result.Cost-3)<1e-8,"Activity was charged the original travel distance again after arrival");
        Check(Search("home").Result.Steps.Single().Id.Split("::").Last()=="play@home"&&Search("town").Result.Steps.Single().Id.Split("::").Last()=="play@town","Already at a valid target still required movement");
        var initial=Snapshot();
        Check(p.Predict(initial,Bound(play,"town"))==null,"A non-movement activity teleported to its target");
        var bound=initial.With(GoapEffect.Set(NpcProgram.BindingKey(pool.Id,variable.Name),GoapValue.Id("home")));
        Check(p.Predict(bound,Bound(move,"town"))==null&&p.Predict(bound,Bound(move,"home"))!=null,"Shared binding switched to a different target in the same branch");
        Check(p.Predict(bound,Bound(move with{Inputs=new[]{input with{Source=NpcBindingSource.Selector,SelectorId=selector.Id}}},"town"))!=null,"Independent selection could not use a different target");
        Check(initial[NpcProgram.BindingKey(pool.Id,variable.Name)].Kind==GoapValueKind.Unknown&&Snapshot()[NpcProgram.BindingKey(pool.Id,variable.Name)].Kind==GoapValueKind.Unknown,"A simulated binding leaked to a different planning run");
        var atHome=Snapshot("home");var playHome=p.Predict(atHome,Bound(play,"home"));
        Check(p.Read(playHome,"self.location").Symbol=="home"&&p.Read(playHome,"needs.fun.value").Number<.25,"Activity prediction did not apply its configured effect");
        var hasStock=new NpcExpressionDefinition{Kind=NpcExpressionKind.AtLeast,Inputs=new(){new(){Kind=NpcExpressionKind.Fact,Key="target.stock"},new(){Number=1}}}.Freeze();
        p.TargetSelectors[selector.Id]=selector with{Filter=hasStock};
        Check(p.Predict(atHome.With(GoapEffect.Set("point.home.stock",p.Encode("target.stock",NpcValue.Numeric(0)))),Bound(play,"home"))==null,"Target filter ignored predicted resource changes");p.TargetSelectors[selector.Id]=selector;
        definition.AllTags=new[]{"changed_after_compile"};
        Check(Select(selector).Length==2,"Planning read mutable selector resources");definition.AllTags=new[]{"test_leisure"};
        var actionNode=graph.Nodes.OfType<NpcActionNodeData>().First();
        var action=(NpcActionDefinition)graph.Resolve(actionNode.DefinitionId);var binding=action.Inputs[0];binding.Source=NpcBindingSource.Selector;binding.SelectorId=definition.DefinitionId;
        graph.SaveJsonFields();string path="res://.godot/npc/target_selectors_roundtrip.tres";
        Check(ResourceSaver.Save(graph,path)==Error.Ok,"Could not save target selectors");
        var loaded=ResourceLoader.Load<NpcGraphAsset>(path,cacheMode:ResourceLoader.CacheMode.Ignore);
        Check(NpcProgram.Compile(loaded).TargetSelectors[definition.DefinitionId].Name=="娱乐场所"&&((NpcActionDefinition)loaded.Resolve(action.DefinitionId)).Inputs[0].SelectorId==definition.DefinitionId,"Binding references were lost on reload");
        definition.Enabled=false;bool rejected=false;try{NpcProgram.Compile(graph);}catch(ArgumentException){rejected=true;}
        Check(rejected,"Disabled selector was accepted by an enabled action");definition.Enabled=true;
        definition.Source=NpcTargetSource.IdentityParameter;definition.ParameterKey="not_registered";rejected=false;try{NpcProgram.Compile(graph);}catch(ArgumentException){rejected=true;}
        Check(rejected,"Unknown target parameter was accepted");definition.Source=NpcTargetSource.WorldPoints;
        definition.Score=new(){Kind=NpcExpressionKind.Fact,Key="removed_inactive_score"};
        Check(NpcProgram.Compile(graph).TargetSelectors[definition.DefinitionId].Score==null,"An inactive hidden score prevented using a non-scoring strategy");definition.Score=null;
#if TOOLS
        var editor=graph.CreateCustomPropertyEditor(binding,nameof(binding.SelectorId));AddChild(editor);editor._UpdateProperty();
        Check(editor is NpcBindingProperty&&!editor.FindChildren("*",nameof(LineEdit),true,false).Any()&&editor.FindChildren("*",nameof(Button),true,false).OfType<Button>().Any(b=>b.Text=="娱乐场所"),"Input editor exposes an ID instead of registered names");
        editor.QueueFree();
        Check(NpcTargetCatalog.Available(graph).Any(s=>s==definition),"Selector node missing from the picker");
        var canvas=new GraphEdit();AddChild(canvas);
        canvas.AddChild(new GraphNode{Name=actionNode.Id,Selected=true});canvas.AddChild(new GraphNode{Name=selectorNode.Id,Selected=true});
        var clipboard=new GraphClipboardService();clipboard.Copy(graph,canvas);var paste=clipboard.CreatePasteData(graph,Vector2.Zero);
        var pastedSelector=paste.Resources.OfType<NpcTargetSelectorDefinition>().Single();var pastedAction=paste.Resources.OfType<NpcActionDefinition>().Single();
        Check(pastedSelector.DefinitionId!=definition.DefinitionId&&pastedAction.Inputs[0].SelectorId==pastedSelector.DefinitionId,"Copying selector/action retained the source selector reference");
        pastedSelector.AllTags=new[]{"copy_only"};Check(definition.AllTags[0]=="test_leisure","Copied selector shared mutable query configuration");canvas.QueueFree();
        Check(action.GetPropertyList().Where(v=>v["name"].AsString() is nameof(action.TargetTags) or nameof(action.TargetIdParameter)).All(v=>!((PropertyUsageFlags)v["usage"].AsInt64()).HasFlag(PropertyUsageFlags.Editor)),"Legacy target fields are still editable");
#endif
        var legacy=(NpcGraphAsset)source.Duplicate(true);var legacyAction=legacy.Definitions.OfType<NpcActionDefinition>().First();
        string originalName=legacyAction.DisplayName;var execution=legacyAction.ExecutionGraph;
        legacyAction.Inputs.Clear();legacyAction.Resources.Clear();legacyAction.TargetSelectorId="";legacyAction.TargetTags=new[]{"test_migration"};legacyAction.TargetIdParameter="home";
        Check(legacy.MigrateActionBindings()&&legacyAction.TargetTags.Length==0&&legacyAction.TargetIdParameter.Length==0&&legacyAction.Inputs.Count==1,"Legacy fields did not migrate");
        string migratedId=legacyAction.Inputs[0].SelectorId;
        Check(!legacy.MigrateActionBindings()&&legacyAction.DisplayName==originalName&&legacyAction.ExecutionGraph==execution&&legacy.Definitions.OfType<NpcTargetSelectorDefinition>().Single(s=>s.DefinitionId==migratedId).ParameterKey=="home","Migration was not idempotent or changed authored content");
        Check(NpcProgram.Compile((NpcGraphAsset)legacy.Duplicate(true)).TargetSelectors.ContainsKey(migratedId),"Duplicating a migrated graph lost selector nodes");
    }
    static NpcBoundAction BindForTest(NpcProgram program,NpcActionSpec action,string target)
    {
        var plan=program.Goals.SelectMany(g=>g.Plans).First(p=>p.ActionIds.Contains(action.Id));
        return new(action,plan,action.Inputs.Where(i=>(i.Source==NpcBindingSource.PlanVariable?plan.Bindings.First(v=>v.Name==i.Variable):i).Source==NpcBindingSource.Selector).ToDictionary(i=>i.Name,_=>NpcValue.Text(target)),null);
    }
}
