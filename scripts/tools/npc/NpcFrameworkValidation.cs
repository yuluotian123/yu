using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Framework;
using GameLogic;
using GameLogic.AI;
using GameLogic.Goap;
using Godot;

public partial class NpcFrameworkValidation : Node
{
    int _checks;
    void Check(bool condition,string reason){if(!condition)throw new Exception(reason);_checks++;}
    public override async void _Ready()
    {
        int exit=0;Node3D level=null;
        try
        {
            PureChecks();
            ModuleSystem.RegisterModule<ISaveModule>(new SaveModule("res://.godot/npc/framework_saves","res://.godot/npc/no_legacy"));
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            level=GD.Load<PackedScene>("res://assets/scenes/spacelevel.tscn").Instantiate<Node3D>();
            PrepareRuntimeTargetFixture(level.GetNode<GameObject3D>("Resident"));
            GetTree().Root.AddChild(level);GetTree().CurrentScene=level;
            var world=level.GetNode<GameObject3D>("RPGTown/TownContent/NpcLife").GetComponent<AiWorldComponent3D>();
            world.RainOverride=0;world.Clock.Paused=true;
            var resident=level.GetNode<GameObject3D>("Resident").GetComponent<AiAgentComponent3D>();var courier=level.GetNode<GameObject3D>("Courier").GetComponent<AiAgentComponent3D>();
            Check(resident!=null&&courier!=null&&resident.Owner.GetParent()==level&&courier.Owner.GetParent()==level,"Two directly authored NPCs required");
            for(int frame=0;frame<60&&!world.Points.ContainsKey("resident_bed");frame++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            Check(world.Points.ContainsKey("resident_bed"),"Interaction points did not register");
            var home=world.Points["resident_bed"];var start=resident.Owner.GlobalPosition;
            var movement=resident.Owner.GetComponent<CharacterMovementComponent3D>();
            resident.Owner.GlobalPosition=home.UsePosition+Vector3.Up*movement.HalfHeight;movement.SyncBodyToOwner();
            for(int frame=0;frame<30;frame++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            Check(home.ContainsPosition(resident.Owner.GlobalPosition)&&resident.State.Location==home.PointId,"Standing at home was not reflected in planning location");
            resident.Owner.GlobalPosition=start;movement.SyncBodyToOwner();
            for(int frame=0;frame<30;frame++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            Check(!home.ContainsPosition(resident.Owner.GlobalPosition)&&resident.State.Location!=home.PointId,"Leaving home left a stale planning location");
            // Exercise actions while the world clock is paused; production preset starts at low urgency.
            resident.State.Needs.Values["hunger"]=.8;resident.State.Needs.Values["fun"]=.75;resident.State.Needs.Refresh();
            courier.State.Needs.Values["hunger"]=.8;courier.State.Needs.Values["fun"]=.75;courier.State.Needs.Refresh();
            bool walked=false,sawSharedMovement=false,sawSharedActivity=false,consistentBinding=true;string sharedTarget="";
            for(int frame=0;frame<4200&&(!sawSharedActivity||resident.State.Needs.Values["hunger"]>=.25||courier.CompletedActions<2);frame++)
            {
                await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                walked|=courier.Owner.GetComponent<CharacterAnimationComponent3D>().ActiveAnimation=="walk";
                if(resident.CurrentAction=="Validation movement"&&resident.PlanBindings.Count==1){sawSharedMovement=true;sharedTarget=resident.PlanBindings.Values.Single().Symbol;}
                if(resident.CurrentAction=="Validation activity"&&resident.PlanBindings.Count==1){sawSharedActivity=true;consistentBinding&=resident.PlanBindings.Values.Single().Symbol==sharedTarget;}
            }
            Check(world.Actors.Count==2,"Unexpected spawned actors");
            Check(consistentBinding,"Runtime changed the shared target between movement and activity");
            Check(resident.CompletedActions>=2&&sawSharedMovement&&sawSharedActivity&&resident.State.Needs.Values["hunger"]<.25,resident.Describe()+"\n"+resident.LastFailure+"\n"+resident.Navigation.Diagnostics);
            Check(courier.CompletedActions>=2,courier.Describe()+"\n"+courier.LastFailure+"\n"+courier.Navigation.Diagnostics);
            Check(walked,"Walking animation did not play");
            Check(courier.State.PrivateFacts["private.completed_visits"].Number>=2,"Configured real effects were not committed");
            double visits=courier.State.PrivateFacts["private.completed_visits"].Number;
            courier.CancelPlan("ValidationCancel");for(int i=0;i<60;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            Check(courier.State.PrivateFacts["private.completed_visits"].Number==visits,"Cancelled action committed again");
            var saved=courier.Capture();courier.State.Needs.Values["hunger"]=.9;courier.Restore(saved,1);
            Check(Math.Abs(courier.State.Needs.Values["hunger"]-saved["needs"]["hunger"].GetValue<double>())<1e-8,"Module save restore failed");
            await ValidateResidentStrollRuntime(resident,courier,world);
            resident.Running=false;courier.Running=false;resident.CancelPlan("ValidationStop");courier.CancelPlan("ValidationStop");
            ValidateBindingResources(resident,world);
            Check(resident.PlanBindings.Count==0&&courier.PlanBindings.Count==0&&!resident.Snapshot().Facts.Any(f=>f.Key.StartsWith("binding.")),"Cancelled/completed target binding leaked into a later planning run");
            Check(world.Points.Values.All(p=>p.Reservations==0),"Reservation leaked");
            foreach(var a in new[]{resident,courier})ValidateFrames(a);
            GD.Print($"NPC_FRAMEWORK_VALIDATION_PASS {_checks} checks; two NPCs, scoped GOAP plans, movement, result transactions, cancellation, saves and sprites");
        }
        catch(Exception e){exit=1;GD.PushError(e.ToString());if(level!=null)foreach(var name in new[]{"Resident","Courier"}){var a=level.GetNode<GameObject3D>(name).GetComponent<AiAgentComponent3D>();GD.Print(a.Describe()+"\n"+string.Join("\n",a.Trace)+"\n"+a.LastFailure+"\n"+a.Navigation.Diagnostics);}}
        level?.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);ModuleSystem.Shutdown();GetTree().Quit(exit);
    }
    void ValidateFrames(AiAgentComponent3D agent)
    {
        var frames=agent.Program.Frames;
        foreach(string clip in new[]{"idle","walk"})
        {
            int count=frames.GetFrameCount(clip);Check(count>1,"Missing real animation "+clip);
            for(int i=0;i<count;i++)
            {
                using var image=frames.GetFrameTexture(clip,i).GetImage();
                if(image.IsCompressed())Check(image.Decompress()==Error.Ok,"Could not decompress imported frame");
                Check(image.GetSize()==new Vector2I(256,256),"Wrong frame dimensions");
                string folder=agent.ActorId=="town_courier_01"?"ochre_courier":"gray_green_resident";
                using var source=Image.LoadFromFile(ProjectSettings.GlobalizePath($"res://assets/textures/sprites/npc/{folder}/{clip}/{clip}_{i:00}.png"));
                int foreground=0;bool binary=true;
                bool matches=true;
                for(int y=0;y<256;y++)for(int x=0;x<256;x++)
                {
                    var actual=image.GetPixel(x,y);var expected=source.GetPixel(x,y);float alpha=actual.A;binary&=alpha is 0 or 1;if(alpha==1)foreground++;
                    matches&=actual.A==expected.A&&(expected.A==0||actual==expected);
                }
                Check(binary,"Nonbinary imported alpha");
                Check(matches,"Imported frame changed opaque RGB or alpha");
                Check(foreground>1000,"Empty frame");
            }
        }
    }
    void PureChecks()
    {
        NpcExpression E(NpcExpressionKind kind,params NpcExpression[] args)=>new(kind,"",default,args);
        var unknown=new NpcExpression(NpcExpressionKind.Fact,"absent",default,Array.Empty<NpcExpression>());
        Check(E(NpcExpressionKind.Not,unknown).Evaluate(_=>default).Type==NpcValueType.Unknown,"Not unknown must stay unknown");
        var truth=new NpcExpression(NpcExpressionKind.Literal,"",NpcValue.Bool(true),Array.Empty<NpcExpression>());
        Check(E(NpcExpressionKind.Any,unknown,truth).Evaluate(_=>default).IsTrue,"Any true failed");
        foreach(string folder in new[]{"gray_green_resident","ochre_courier"})
        {
            var graph=GD.Load<NpcGraphAsset>($"res://assets/resources/npc/{folder}/npc_graph.tres");
            Check(graph.Validate(out var result),result.ToDisplayText());var p=NpcProgram.Compile(graph);
            if(folder=="gray_green_resident")ValidateResidentStrollPlans(p);
            var a=new NpcCharacterState(p);var b=new NpcCharacterState(p);a.Needs.Values["hunger"]=0;
            Check(b.Needs.Values["hunger"]==p.Needs.Single(n=>n.Id=="hunger").Initial&&b.Needs.Values["hunger"]>0,"Shared runtime state");
            Check(a.Relations.Adjust("friend","trust",.5),"Configurable relationship dimension failed");
            Check(a.Memory.Remember(new("visited","shop",1,"test")),"Configurable memory category failed");
            var save=a.Capture();var copy=new NpcCharacterState(p);copy.Restore(save);
            Check(copy.Relations.Contacts["friend"]["trust"]==.5&&copy.Memory.Entries.Count==1,"Social module persistence failed");
            var bad=(JsonObject)save.DeepClone();bad["needs"]["hunger"]=-100;bool rejected=false;
            string beforeInvalid=copy.Capture().ToJsonString();
            try{copy.Restore(bad);}catch(ArgumentException){rejected=true;}
            Check(rejected&&copy.Capture().ToJsonString()==beforeInvalid,"Invalid restore changed live state");
            var goal=p.Goals[0];var action=goal.Actions[0];
            var snapshot=new GoapWorldState(p.Facts.Values.Select(f=>new KeyValuePair<string,GoapValue>(f.Key,p.Encode(f.Key,f.Initial))))
                .With(GoapEffect.Set("needs.hunger.value",p.Encode("needs.hunger.value",NpcValue.Numeric(.8))),GoapEffect.Set("point.test.available",GoapValue.Bool(true)),GoapEffect.Set("point.test.open",GoapValue.Bool(true)),GoapEffect.Set("point.test.stock",p.Encode("target.stock",NpcValue.Numeric(3))));
            var search=new GoapPlanner(snapshot,new GoapGoal(goal.Id,s=>goal.Completion.Evaluate(k=>p.Read(s,k),p.Parameter).IsTrue),new[]{new GoapAction("test","test",1,s=>p.Predict(s,BindForTest(p,action,"test")))});
            while(search.Status==GoapSearchStatus.Pending)search.Advance(1);
            Check(search.Status==GoapSearchStatus.Found,"Configured expressions did not plan");
            Check(p.Read(snapshot,"needs.hunger.value").Number>.5,"Search mutated live snapshot");
            ValidatePlanSearch(p);
            ValidateTargetSelectors(graph);
            ValidateActionBindings(graph);
            // A content author can rename a need and its entire behavior without editing any runtime type.
            var custom=(NpcGraphAsset)graph.Duplicate(true);
            var customNeed=custom.Definitions.OfType<NpcNeedsDefinition>().Single().Needs.Single(n=>n.NeedId=="hunger");customNeed.NeedId="invented_focus";
            custom.Definitions.OfType<NpcFactSchema>().Single().Facts.Add(new(){Key="private.unspecified"});
            void Rename(NpcExpressionDefinition expression){if(expression==null)return;expression.Key=expression.Key.Replace("needs.hunger.","needs.invented_focus.");foreach(var child in expression.Inputs)Rename(child);}
            foreach(var g in custom.Definitions.OfType<NpcGoalDefinition>()){Rename(g.Activation);Rename(g.Utility);Rename(g.Completion);}
            foreach(var act in custom.Definitions.OfType<NpcActionDefinition>())foreach(var effect in act.Predictions.Concat(act.Results)){effect.Key=effect.Key.Replace("needs.hunger.","needs.invented_focus.");Rename(effect.Value);}
            var renamed=NpcProgram.Compile(custom);var renamedState=new NpcCharacterState(renamed);
            renamedState.Restore(renamedState.Capture());
            Check(renamedState.Read("private.unspecified").Type==NpcValueType.Unknown,"Unknown private fact changed during save restore");
            Check(renamedState.Read("needs.invented_focus.value").Type==NpcValueType.Number&&renamedState.Read("needs.hunger.value").Type==NpcValueType.Unknown,"Custom need was not isolated from built-in names");
            var invalid=(NpcGraphAsset)graph.Duplicate(true);invalid.Definitions.OfType<NpcGoalDefinition>().First().Completion=new(){Number=1};rejected=false;
            try{NpcProgram.Compile(invalid);}catch(ArgumentException){rejected=true;}
            Check(rejected,"A numeric completion expression was accepted as a boolean");
#if TOOLS
            var paletteContext=new GraphEditorContext{CurrentGraph=graph,RootGraph=graph};
            Check(GraphProviderCatalog.Nodes(paletteContext).Contains(typeof(NpcGoalNodeData)),"AI component did not provide NPC nodes");
            Check(!GraphCallableCatalog.Actions(GraphCallableUsage.Flow).Any(GraphProviderCatalog.IsProvided),"AI actions leaked into global palette");
            var childContext=new GraphEditorContext{CurrentGraph=action.Execution,RootGraph=graph};
            Check(GraphCallableCatalog.Actions(GraphCallableUsage.Flow,childContext).Contains(typeof(NpcWaitAction)),"NPC execution palette missing AI capability");
            var goalNode=graph.Nodes.OfType<NpcGoalNodeData>().First();
            var view=GraphNodeViewBuilder.CreateNodeUI(goalNode,paletteContext);Check(view.Title==goal.Name,"Node title ignored configured display name");view.Free();
            ValidateEditorTopology(graph);
            ValidateExpressionEditor(graph);
            ValidateNeedPresets(graph);
            ValidateEffectEditor(graph);
            ValidatePlanEditor(graph);
            ValidateClipboard(graph);
            var ui=GraphCommonActionEditor.Build(new NpcWaitAction{DurationOverride=1.25},childContext);Check(ui.GetChildren().OfType<SpinBox>().Any(s=>Math.Abs(s.Value-1.25)<1e-8),"Double action parameter not displayed");ui.Free();
            var roundtrip=(NpcGraphAsset)graph.Duplicate(true);
            string temp="res://.godot/npc/graph_roundtrip.tres";
            roundtrip.SaveJsonFields();Check(ResourceSaver.Save(roundtrip,temp)==Error.Ok,"Could not save NPC graph");
            roundtrip.TakeOverPath(temp);
            var execution=roundtrip.Definitions.OfType<NpcActionDefinition>().First().ExecutionGraph;
            execution.Nodes[0].Position=new(19,37);execution.MarkDirty();GraphSaveService.BindOwner(execution,roundtrip);
            Check(GraphSaveService.SaveGraphResource(this,execution,false),"Nested graph save failed");
            var reloaded=ResourceLoader.Load<NpcGraphAsset>(temp,cacheMode:ResourceLoader.CacheMode.Ignore);
            Check(reloaded.Definitions.OfType<NpcActionDefinition>().First().ExecutionGraph.Nodes[0].Position==new Vector2(19,37),"Nested execution graph edits were lost");
#endif
        }
        var initial=new GoapWorldState(new[]{new KeyValuePair<string,GoapValue>("custom",GoapValue.Number(0))});
        var chain=new GoapPlanner(initial,new GoapGoal("invented goal",s=>s["custom"].Integer==2),new[]{new GoapAction("invented action 1","",1,s=>s["custom"].Integer==0?s.With(GoapEffect.Set("custom",GoapValue.Number(1))):null),new GoapAction("invented action 2","",1,s=>s["custom"].Integer==1?s.With(GoapEffect.Set("custom",GoapValue.Number(2))):null)});
        while(chain.Status==GoapSearchStatus.Pending)chain.Advance(1);
        Check(chain.Plan?.Steps.Count==2,"Custom multistep plan failed");
    }
    void ValidatePlanSearch(NpcProgram program)
    {
        var completion=new NpcExpressionDefinition{Kind=NpcExpressionKind.AtMost,Inputs=new(){new(){Kind=NpcExpressionKind.Fact,Key="needs.fun.value"},new(){Number=.25}}}.Freeze();
        NpcPlanSpec Pool(string id,int priority,params string[] ids)=>new(id,id,priority,null,ids);
        var pool=Pool("leisure",0,"play","go_town","go_home");
        var goal=program.Goals.First() with{Completion=completion,Plans=new[]{pool}};
        GoapWorldState Snapshot(string location,double fun=.8)=>new GoapWorldState(new[]{
            new KeyValuePair<string,GoapValue>("self.location",GoapValue.Id(location)),
            new KeyValuePair<string,GoapValue>("needs.fun.value",program.Encode("needs.fun.value",NpcValue.Numeric(fun)))});
        var actions=new[]{
            new GoapAction("go_town","town",1,s=>s.With(GoapEffect.Set("self.location",GoapValue.Id("town")))),
            new GoapAction("go_home","home",3,s=>s.With(GoapEffect.Set("self.location",GoapValue.Id("home")))),
            new GoapAction("play","",1,s=>program.Read(s,"self.location").Symbol is "home" or "town"
                ?s.With(GoapEffect.Set("needs.fun.value",program.Encode("needs.fun.value",NpcValue.Numeric(.15)))):null)};
        NpcPlanSearch Search(string location,IReadOnlyList<NpcPlanSpec> plans=null,IEnumerable<GoapAction> choices=null,NpcProgram policy=null,double fun=.8)=>
            new(policy??program,goal with{Plans=plans??new[]{pool}},Snapshot(location,fun),(choices??actions).Select(a=>new KeyValuePair<string,GoapAction>(a.Operation,a)));
        string[] Finish(NpcPlanSearch search)
        {
            for(int i=0;i<4096&&search.Status==GoapSearchStatus.Pending;i++)search.Advance(1);
            Check(search.Status!=GoapSearchStatus.Pending,"Scoped plan search did not finish within slices");
            return search.Result?.Steps.Select(a=>a.Id).ToArray()??Array.Empty<string>();
        }
        Check(Finish(Search("home")).SequenceEqual(new[]{"play"}),"Already at home still planned movement");
        Check(Finish(Search("town")).SequenceEqual(new[]{"play"}),"Already in town still planned movement");
        Check(Finish(Search("outside")).SequenceEqual(new[]{"go_town","play"}),"Action pool order replaced dependency/cost planning");
        Check(Finish(Search("outside",new[]{Pool("home_only",0,"play","go_home")})).SequenceEqual(new[]{"go_home","play"}),"Scoped plan used a cheaper action outside its pool");
        var fallback=Search("outside",new[]{Pool("cannot_finish",10,"go_town"),Pool("home",0,"play","go_home")});
        Check(Finish(fallback).SequenceEqual(new[]{"go_home","play"})&&fallback.ChosenPlan.Id=="home","Infeasible preferred plan did not fall back to another eligible plan");
        var mixed=Search("outside",new[]{Pool("travel_only",10,"go_home"),Pool("play_only",0,"play")});Finish(mixed);
        Check(mixed.Status==GoapSearchStatus.NoPlan,"Planner combined actions from different plan pools");
        var blocked=Pool("not_eligible",100,"go_town","play") with{Eligibility=new NpcExpressionDefinition{ValueType=NpcValueType.Boolean,Boolean=false}.Freeze()};
        var ineligible=Search("outside",new[]{blocked});Finish(ineligible);
        Check(ineligible.FailureReason=="NoEligiblePlan","Ineligible plan leaked its actions into search");
        var repeated=new GoapAction("repeat_play","",1,s=>s.With(GoapEffect.Set("needs.fun.value",program.Encode("needs.fun.value",NpcValue.Numeric(Math.Max(0,program.Read(s,"needs.fun.value").Number-.2))))));
        Check(Finish(Search("home",new[]{Pool("repeat",0,"repeat_play")},new[]{repeated},fun:1)).SequenceEqual(Enumerable.Repeat("repeat_play",4)),"Candidate action could not be used repeatedly by GOAP");
        var already=Search("home",fun:.15);Check(already.Status==GoapSearchStatus.AlreadySatisfied&&already.Result.Steps.Count==0,"Already satisfied goal generated actions");
        var pending=Search("outside");Check(pending.Advance(1)==GoapSearchStatus.Pending,"Scoped plan search ignored slice budget");pending.Invalidate();
        Check(pending.Status==GoapSearchStatus.Invalidated&&pending.Result==null,"Cancelled scoped search retained its result");
        var limitedGraph=GD.Load<NpcGraphAsset>("res://assets/resources/npc/gray_green_resident/npc_graph.tres").Duplicate(true) as NpcGraphAsset;
        limitedGraph.Definitions.OfType<NpcPolicyDefinition>().Single().MaximumExpanded=1;var limitedProgram=NpcProgram.Compile(limitedGraph);
        var limited=Search("outside",new[]{Pool("limited",0,"go_home","play")},policy:limitedProgram);Finish(limited);
        Check(limited.Status==GoapSearchStatus.LimitReached,"Budget exhaustion was reported as an impossible plan");
        var finish=new GoapAction("finish","",1,s=>s.With(GoapEffect.Set("needs.fun.value",program.Encode("needs.fun.value",NpcValue.Numeric(.15)))));
        var recover=Search("outside",new[]{Pool("limited",10,"go_home","play"),Pool("finish",0,"finish")},actions.Append(finish),limitedProgram);
        Check(Finish(recover).SequenceEqual(new[]{"finish"})&&recover.ChosenPlan.Id=="finish","A budget-limited pool prevented searching the next plan");
        Check(typeof(NpcGoalDefinition).GetProperty("PlanMode")==null,"Goal still exports a planning mode");
    }
#if TOOLS
    Action _clipboardRedo, _clipboardUndo;
    void RedoClipboardValidation()=>_clipboardRedo();
    void UndoClipboardValidation()=>_clipboardUndo();
    void ValidateClipboard(NpcGraphAsset source)
    {
        var graph=(NpcGraphAsset)source.Duplicate(true);
        var goal=graph.Nodes.OfType<NpcGoalNodeData>().First();
        var actionNode=(NpcActionNodeData)graph.FindNodeById(graph.GetOutgoingConnections(goal.Id,0).First().ToNode);
        var planNode=(NpcTemplateNodeData)graph.FindNodeById(graph.GetOutgoingConnections(goal.Id,1).First().ToNode);
        var action=(NpcActionDefinition)graph.Resolve(actionNode.DefinitionId);
        var plan=(NpcPlanTemplateDefinition)graph.Resolve(planNode.DefinitionId);
        action.Cost=new(){Number=7};
        // External editable resources must also become independent, not just inline subresources.
        string external="res://.godot/npc/clipboard_external_cost.tres";
        Check(ResourceSaver.Save(action.Cost,external)==Error.Ok,"Could not prepare external copy fixture");
        action.Cost.TakeOverPath(external);
        var copiedPosition=new Vector2(17,29);
        action.ExecutionGraph.Nodes[0].Position=copiedPosition;action.ExecutionGraph.MarkDirty();
        string copiedName=action.DisplayName;
        var canvas=new GraphEdit();AddChild(canvas);
        foreach(var node in graph.Nodes)canvas.AddChild(new GraphNode{Name=node.Id,Selected=node==actionNode});
        var clipboard=new GraphClipboardService();clipboard.Copy(graph,canvas);
        action.DisplayName="Original edited after copy";action.Cost.Number=9;
        action.ExecutionGraph.Nodes[0].Position=new(31,41);
        var paste=clipboard.CreatePasteData(graph,new(50,50));
        var pastedNode=paste.Nodes.OfType<NpcActionNodeData>().Single();
        var pasted=paste.Resources.OfType<NpcActionDefinition>().Single();
        Check(pastedNode.Id!=actionNode.Id&&pastedNode.DefinitionId!=actionNode.DefinitionId&&pastedNode.DefinitionId==pasted.DefinitionId,"Paste reused an action definition ID");
        Check(pasted.DisplayName==copiedName&&pasted.Cost.Number==7,"Clipboard did not freeze resources at copy time");
        Check(pasted.ExecutionGraph!=action.ExecutionGraph&&pasted.ExecutionGraph.Nodes[0].Position==copiedPosition,"Paste lost unsaved execution edits or shared its execution graph");
        pasted.DisplayName="Independent pasted action";pasted.Cost.Number=13;
        Check(action.DisplayName=="Original edited after copy"&&action.Cost.Number==9,"Renaming or editing a pasted action changed the original");
        var effect=pasted.Predictions.First();string originalKey=action.Predictions.First().Key;
        Check(effect!=action.Predictions.First()&&effect.Value!=action.Predictions.First().Value&&pasted.Preconditions!=action.Preconditions,"Paste shared nested expressions or effects");
        effect.Key="private.clipboard_test";
        Check(action.Predictions.First().Key==originalKey,"Editing a pasted effect changed the original");effect.Key=originalKey;
        pasted.ExecutionGraph.Nodes[0].Position=new(73,89);pasted.ExecutionGraph.SaveJsonFields();
        Check(action.ExecutionGraph.Nodes[0].Position==new Vector2(31,41),"Editing pasted execution changed the original");
        var second=clipboard.CreatePasteData(graph,Vector2.Zero).Resources.OfType<NpcActionDefinition>().Single();
        Check(second.DefinitionId!=pasted.DefinitionId&&second.Cost.Number==7&&second.ExecutionGraph.Nodes[0].Position==copiedPosition,"Repeated paste reused or modified clipboard resources");
        string nodesBefore=GraphSnapshotService.CaptureNodes(graph),connectionsBefore=GraphSnapshotService.CaptureConnections(graph);
        string nodesJson=GraphJsonHelper.SerializeList(paste.Nodes),connectionsJson=GraphJsonHelper.SerializeList(paste.Connections);
        int definitionsBefore=graph.Definitions.Count;
        var controller=new GraphEditorController(canvas);
        void AddView(GraphNodeData node)
        {
            Check(graph.FindNodeById(node.Id)==node,"Pasted view saw a stale graph index");
            canvas.AddChild(new GraphNode{Name=node.Id});
        }
        using(var history=new UndoRedo())
        {
            history.CreateAction("Paste independent NPC action");
            _clipboardRedo=()=>{graph.AddNodeResources(paste.Resources);GraphSnapshotService.AddSerialized(graph,canvas,AddView,nodesJson,connectionsJson);};
            _clipboardUndo=()=>{graph.RemoveNodeResources(paste.Resources);GraphSnapshotService.Restore(graph,canvas,controller,null,AddView,nodesBefore,connectionsBefore);};
            history.AddDoMethod(new Callable(this,MethodName.RedoClipboardValidation));
            history.AddUndoMethod(new Callable(this,MethodName.UndoClipboardValidation));
            history.CommitAction();
            Check(graph.Resolve(pastedNode.DefinitionId)==pasted&&graph.Definitions.Count==definitionsBefore+1,"Paste did not attach its independent definition");
            history.Undo();
            Check(graph.FindNodeById(pastedNode.Id)==null&&graph.Resolve(pastedNode.DefinitionId)==null&&graph.Definitions.Count==definitionsBefore,"Undo paste left orphan definitions");
            history.Redo();
            Check(graph.Resolve(pastedNode.DefinitionId)==pasted&&graph.Nodes.OfType<NpcActionNodeData>().Single(n=>n.Id==pastedNode.Id).DefinitionId==pasted.DefinitionId,"Redo lost the pasted configuration or reference");
        }
        _clipboardRedo=null;_clipboardUndo=null;
        action.ExecutionGraph.SaveJsonFields();
        graph.SaveJsonFields();string path="res://.godot/npc/clipboard_roundtrip.tres";
        Check(ResourceSaver.Save(graph,path)==Error.Ok,"Pasted definition could not save");
        var loaded=ResourceLoader.Load<NpcGraphAsset>(path,cacheMode:ResourceLoader.CacheMode.Ignore);
        var loadedCopy=(NpcActionDefinition)loaded.Resolve(pastedNode.DefinitionId);
        var loadedOriginal=(NpcActionDefinition)loaded.Resolve(actionNode.DefinitionId);
        loadedCopy.DisplayName="Reloaded copy renamed";loadedCopy.Predictions.First().Value.Number=.42;
        Check(loadedOriginal.DisplayName==action.DisplayName&&loadedCopy.Predictions.First().Value!=loadedOriginal.Predictions.First().Value&&loadedCopy.ExecutionGraph.Nodes[0].Position==new Vector2(73,89)&&loadedOriginal.ExecutionGraph.Nodes[0].Position==new Vector2(31,41),"Saved pasted action lost independence or execution data");
        // Copying a goal/plan/action group remaps both edges and the plan's selected actions.
        plan.ActionIds=new[]{action.DefinitionId};
        foreach(var view in canvas.GetChildren().OfType<GraphNode>())view.Selected=view.Name==goal.Id||view.Name==actionNode.Id||view.Name==planNode.Id;
        clipboard.Copy(graph,canvas);
        var destination=new NpcGraphAsset();var group=clipboard.CreatePasteData(destination,Vector2.Zero);
        destination.AddNodeResources(group.Resources);
        GraphSnapshotService.Add(destination,null,null,group.Nodes,group.Connections);
        var groupAction=group.Resources.OfType<NpcActionDefinition>().Single();
        var groupPlan=group.Resources.OfType<NpcPlanTemplateDefinition>().Single();
        Check(group.Nodes.Count==3&&group.Connections.Count==2&&group.Connections.All(e=>group.Nodes.Any(n=>n.Id==e.FromNode)&&group.Nodes.Any(n=>n.Id==e.ToNode)),"Group paste did not remap internal connections");
        Check(groupPlan.ActionIds.SequenceEqual(new[]{groupAction.DefinitionId})&&NpcPlanActionCatalog.Available(destination,groupPlan).Single().DefinitionId==groupAction.DefinitionId,"Copied plan still refers to the original action");
        foreach(var view in canvas.GetChildren().OfType<GraphNode>())view.Selected=view.Name==planNode.Id;
        clipboard.Copy(graph,canvas);
        var planOnly=clipboard.CreatePasteData(graph,Vector2.Zero).Resources.OfType<NpcPlanTemplateDefinition>().Single();
        Check(planOnly.ActionIds.SequenceEqual(plan.ActionIds)&&planOnly.DefinitionId!=plan.DefinitionId,"Copying only a plan changed its external action references");
        controller.ClearGraphEdit();canvas.QueueFree();
    }
    void ValidatePlanEditor(NpcGraphAsset source)
    {
        var graph=(NpcGraphAsset)source.Duplicate(true);
        var goal=graph.Nodes.OfType<NpcGoalNodeData>().First();
        var planNode=(NpcTemplateNodeData)graph.FindNodeById(graph.GetOutgoingConnections(goal.Id,1).Single().ToNode);
        var plan=(NpcPlanTemplateDefinition)graph.Resolve(planNode.DefinitionId);
        var actionNode=(NpcActionNodeData)graph.FindNodeById(graph.GetOutgoingConnections(goal.Id,0).First().ToNode);
        var action=(NpcActionDefinition)graph.Resolve(actionNode.DefinitionId);
        // This fixture starts with one candidate even when the authored graph has been extended.
        graph.Connections=graph.Connections.Where(e=>e.FromNode!=goal.Id||e.FromPort!=0||e.ToNode==actionNode.Id).ToList();
        plan.ActionIds=new[]{action.DefinitionId};
        var otherGoal=graph.Nodes.OfType<NpcGoalNodeData>().Skip(1).First();
        var otherNode=(NpcActionNodeData)graph.FindNodeById(graph.GetOutgoingConnections(otherGoal.Id,0).Single().ToNode);
        var other=(NpcActionDefinition)graph.Resolve(otherNode.DefinitionId);
        Check(NpcPlanActionCatalog.Available(graph,plan).Select(a=>a.DefinitionId).SequenceEqual(new[]{action.DefinitionId}),"Plan picker contains actions outside its goal");
        graph.ConnectNodes(goal.Id,0,otherNode.Id,0);
        Check(NpcPlanActionCatalog.Available(graph,plan).Count==2,"New candidate connection did not update plan picker");
        other.Enabled=false;
        Check(NpcPlanActionCatalog.Available(graph,plan).Count==1,"Disabled action remains selectable");other.Enabled=true;
        graph.ConnectNodes(otherGoal.Id,1,planNode.Id,0);
        Check(NpcPlanActionCatalog.Available(graph,plan).Select(a=>a.DefinitionId).SequenceEqual(new[]{other.DefinitionId}),"Shared plan offers an action unavailable to one owner");
        graph.Connections.Remove(graph.Connections.Single(e=>e.FromNode==otherGoal.Id&&e.ToNode==planNode.Id));graph.MarkDirty();
        plan.ActionIds=new[]{action.DefinitionId,other.DefinitionId};
        var editor=graph.CreateCustomPropertyEditor(plan,nameof(plan.ActionIds));
        Check(editor is NpcPlanActionsProperty,"Plan still exposes raw action IDs");AddChild(editor);
        using var history=new UndoRedo();
        editor.PropertyChanged+=(field,value,_,_)=>
        {
            history.CreateAction("Edit available actions");history.AddDoProperty(plan,field,value);
            history.AddUndoProperty(plan,field,Variant.From(plan.ActionIds));history.CommitAction();
        };
        editor._UpdateProperty();
        Button[] Buttons()=>editor.FindChildren("*",nameof(Button),true,false).OfType<Button>().ToArray();
        Check(!editor.FindChildren("*",nameof(LineEdit),true,false).Any(),"Plan editor still asks for a typed ID");
        Check(Buttons().Any(b=>b.Text==action.DisplayName)&&Buttons().Any(b=>b.Text==other.DisplayName),"Existing plan did not resolve action names");
        Check(!Buttons().Any(b=>b.Text is "↑" or "↓")&&Buttons().Single(b=>b.Text=="+ 添加可用行动").Disabled,"Candidate pool still offers ordering or duplicate additions");
        var original=plan.ActionIds;
        Buttons().First(b=>b.Text=="移除").EmitSignal(Button.SignalName.Pressed);
        Check(plan.ActionIds.SequenceEqual(new[]{other.DefinitionId})&&original.Length==2,"Removing a candidate mutated undo's old value");
        history.Undo();editor._UpdateProperty();
        Check(plan.ActionIds.SequenceEqual(original),"Plan step undo failed");history.Redo();editor._UpdateProperty();
        Check(plan.ActionIds.SequenceEqual(new[]{other.DefinitionId}),"Candidate pool redo failed");
        Check(!Buttons().Single(b=>b.Text=="+ 添加可用行动").Disabled,"Removed candidate cannot be selected again");
        history.Undo();editor._UpdateProperty();
        action.DisplayName="改名后的行动";editor._UpdateProperty();
        Check(Buttons().Any(b=>b.Text=="改名后的行动")&&plan.ActionIds[0]==action.DefinitionId,"Renaming broke the stable action reference");
        action.Enabled=false;editor._UpdateProperty();
        Check(Buttons().Any(b=>b.Text.Contains("不可用"))&&plan.ActionIds.Length==2,"Unavailable action silently removed from plan");action.Enabled=true;
        plan.ActionIds=new[]{"missing_action"};editor._UpdateProperty();
        Check(Buttons().Any(b=>b.Text.Contains("引用已失效"))&&plan.ActionIds[0]=="missing_action","Missing action was silently replaced");
        plan.ActionIds=new[]{action.DefinitionId,other.DefinitionId,action.DefinitionId};editor._UpdateProperty();
        graph.SaveJsonFields();string path="res://.godot/npc/plan_editor_roundtrip.tres";
        Check(ResourceSaver.Save(graph,path)==Error.Ok,"Plan steps failed to save");
        var loaded=ResourceLoader.Load<NpcGraphAsset>(path,cacheMode:ResourceLoader.CacheMode.Ignore);
        Check(((NpcPlanTemplateDefinition)loaded.Resolve(plan.DefinitionId)).ActionIds.SequenceEqual(plan.ActionIds),"Available actions lost on reload");
        Check(NpcProgram.Compile(loaded).Goals.First().Plans.First().ActionIds.Count==2,"Repeated legacy references changed candidate set semantics");
        graph.Connections.Remove(graph.Connections.Single(e=>e.FromNode==goal.Id&&e.ToNode==planNode.Id));graph.MarkDirty();editor._UpdateProperty();
        Check(NpcPlanActionCatalog.Available(graph,plan).Count==0&&Buttons().Single(b=>b.Text=="+ 添加可用行动").Disabled,"Unconnected plan offers global actions");
        editor.QueueFree();
    }
    void ValidateEffectEditor(NpcGraphAsset source)
    {
        var graph=(NpcGraphAsset)source.Duplicate(true);
        var program=NpcProgram.Compile(graph);
        var targets=NpcExpressionEditorCatalog.WritableTargets(graph);
        Check(targets.Select(s=>s.Key).ToHashSet().SetEquals(program.Facts.Keys.Concat(new[]{"self.location","input.地点.stock"})),"Effect target picker differs from runtime writable facts");
        Check(!targets.Any(s=>s.Parameter||s.Key.EndsWith(".urgency")||s.Key.EndsWith(".urgent")||s.Key.StartsWith("world.")||s.Key.StartsWith("relations.")),"Read-only parameter offered as effect target");
        var schema=graph.Definitions.OfType<NpcFactSchema>().Single();
        schema.Facts.Add(new(){Key="private.test_flag",Type=NpcValueType.Boolean,Initial=new(){ValueType=NpcValueType.Boolean}});
        schema.Facts.Add(new(){Key="private.test_text",Type=NpcValueType.Symbol,Initial=new(){ValueType=NpcValueType.Symbol,Symbol="before"}});
        Check(NpcExpressionEditorCatalog.WritableTargets(graph).Any(s=>s.Key=="private.test_flag"&&s.Type==NpcValueType.Boolean),"Newly registered boolean missing from effect target picker");
        var action=graph.Definitions.OfType<NpcActionDefinition>().First();
        var effect=new NpcEffectDefinition{Key="private.test_flag",Value=new(){ValueType=NpcValueType.Boolean,Boolean=true}};
        action.Results.Add(effect);
        action.Results.Add(new(){Key="private.test_text",Value=new(){ValueType=NpcValueType.Symbol,Symbol="after"}});
        foreach(string field in new[]{nameof(effect.Key),nameof(effect.Mode),nameof(effect.Value)})
        {
            var editor=graph.CreateCustomPropertyEditor(effect,field);
            Check(editor!=null&&(field==nameof(effect.Value)?editor is NpcExpressionProperty:editor is NpcEffectProperty),"Effect field did not receive parameter editor: "+field);
            editor.Free();
        }
        Check(NpcExpressionEditorCatalog.ExpectedType(graph,effect,nameof(effect.Value))==NpcValueType.Boolean,"Boolean effect uses numeric editor");
        program=NpcProgram.Compile(graph);
        var snapshot=new GoapWorldState(program.Facts.Values.Select(f=>new KeyValuePair<string,GoapValue>(f.Key,program.Encode(f.Key,f.Initial))))
            .With(GoapEffect.Set("point.test.available",GoapValue.Bool(true)),GoapEffect.Set("point.test.open",GoapValue.Bool(true)),GoapEffect.Set("point.test.stock",program.Encode("target.stock",NpcValue.Numeric(3))));
        var spec=program.Goals.First().Actions.First();
        Check(program.TryEffects(program.Contextual(snapshot,BindForTest(program,spec,"test")),spec.Results,out var result,spec)&&program.Read(result,"private.test_flag").IsTrue&&program.Read(result,"private.test_text").Symbol=="after","Typed effect targets did not commit");
        effect.Mode=NpcEffectMode.Add;effect.Value=new(){Number=1};bool rejected=false;
        try{NpcProgram.Compile(graph);}catch(ArgumentException error){rejected=error.Message.Contains("增加/减少");}
        Check(rejected,"Numeric Add accepted a boolean target");
        effect.Mode=NpcEffectMode.Set;effect.Value=new(){ValueType=NpcValueType.Boolean,Boolean=true};
        schema.Enabled=false;
        Check(!NpcExpressionEditorCatalog.WritableTargets(graph).Any(s=>s.Key=="private.test_flag"),"Disabled module still provides writable targets");
        schema.Enabled=true;graph.SaveJsonFields();string path="res://.godot/npc/effect_editor_roundtrip.tres";
        Check(ResourceSaver.Save(graph,path)==Error.Ok,"Typed effects failed to save");
        var loaded=ResourceLoader.Load<NpcGraphAsset>(path,cacheMode:ResourceLoader.CacheMode.Ignore);
        var restored=loaded.Definitions.OfType<NpcActionDefinition>().First().Results.Single(e=>e.Key=="private.test_flag");
        Check(restored.Mode==NpcEffectMode.Set&&restored.Value.ValueType==NpcValueType.Boolean&&restored.Value.Boolean,"Typed effect lost on reload");
        NpcProgram.Compile(loaded);
    }
    void ValidateNeedPresets(NpcGraphAsset source)
    {
        var preset=GD.Load<NpcNeedsDefinition>(NpcNeedsDefinition.CommonPresetPath);
        Check(preset.Needs.Select(n=>n.NeedId).SequenceEqual(new[]{"thirst","hunger","fun","fatigue"}),"Common needs preset must contain exactly the four requested needs");
        var graph=(NpcGraphAsset)source.Duplicate(true);
        var module=graph.Definitions.OfType<NpcNeedsDefinition>().Single();
        Check(module.Needs.Select(n=>n.NeedId).SequenceEqual(preset.Needs.Select(n=>n.NeedId))&&module.Needs.Zip(preset.Needs).All(pair=>pair.First.Initial==pair.Second.Initial&&pair.First.ChangePerHour==pair.Second.ChangePerHour),"Scene NPC needs do not match common preset");
        module.Needs.Add(new(){NeedId="custom_business",DisplayName="业务测试需求"});
        var hunger=module.Needs.Single(n=>n.NeedId=="hunger");double oldHunger=hunger.Initial;
        var original=module.Needs;
        var merged=module.MergePreset(preset);
        Check(original.Count==5&&merged.Count==5&&merged.Any(n=>n.NeedId=="custom_business"),"Preset merge mutated the source list or lost custom business needs");
        Check(merged.Single(n=>n.NeedId=="hunger")==hunger&&hunger.Initial==oldHunger,"Preset overwrote existing configuration");
        module.Needs=merged;
        Check(module.MergePreset(preset).Count==5,"Applying the same preset duplicated need IDs");
        var thirst=module.Needs.Single(n=>n.NeedId=="thirst");thirst.Initial=.9;
        Check(preset.Needs.Single(n=>n.NeedId=="thirst").Initial==.2,"Preset and character share mutable need resources");
        var independent=new NpcNeedsDefinition();independent.Needs=independent.MergePreset(preset);
        Check(independent.Needs.Single(n=>n.NeedId=="thirst").Initial==.2,"One character modified another character's preset copy");
        module.Needs=module.AddCustomNeed();var added=module.Needs.Last();added.NeedId="focus";added.DisplayName="专注需求";
        Check(NpcExpressionEditorCatalog.Collect(graph).Any(s=>s.Key=="needs.focus.value"),"Custom need missing from registered parameter picker");
        module.Needs=module.AddCustomNeed();module.Needs=module.AddCustomNeed();
        Check(module.Needs.Select(n=>n.NeedId).Distinct().Count()==module.Needs.Count,"Custom need creation generated duplicate IDs");
        var compiled=NpcProgram.Compile(graph);var state=new NpcCharacterState(compiled);state.Advance(12);state.Advance(13);
        Check(Math.Abs(state.Read("needs.focus.value").Number-.24)<1e-8,"Custom need does not participate in runtime updates");
        var editor=graph.CreateCustomPropertyEditor(module,nameof(module.Needs));
        Check(editor is NpcNeedsProperty,"Needs editor does not provide preset/custom authoring");editor.Free();
        var freshGraph=new NpcGraphAsset();var fresh=(NpcNeedsDefinition)freshGraph.Ensure(new NpcNeedsNodeData());
        Check(fresh.Needs.Count==4&&fresh.Needs[0]!=preset.Needs[0],"New needs node did not receive independent common preset");
        graph.SaveJsonFields();string path="res://.godot/npc/needs_preset_roundtrip.tres";
        Check(ResourceSaver.Save(graph,path)==Error.Ok,"Needs preset/custom data failed to save");
        var reloaded=ResourceLoader.Load<NpcGraphAsset>(path,cacheMode:ResourceLoader.CacheMode.Ignore);
        Check(reloaded.Definitions.OfType<NpcNeedsDefinition>().Single().Needs.Any(n=>n.NeedId=="focus"),"Custom need lost on reload");
        NpcProgram.Compile(reloaded);
    }
    void ValidateExpressionEditor(NpcGraphAsset source)
    {
        var graph=(NpcGraphAsset)source.Duplicate(true);
        var symbols=NpcExpressionEditorCatalog.Collect(graph,true);
        var program=NpcProgram.Compile(graph);
        Check(symbols.Where(s=>!s.Parameter&&!s.Key.StartsWith("input.")).Select(s=>s.Key).ToHashSet().SetEquals(program.KnownKeys.Where(k=>!k.StartsWith("target."))),"Editor symbol registry differs from runtime facts");
        Check(symbols.Where(s=>s.Parameter).Select(s=>s.Key).ToHashSet().SetEquals(program.Parameters.Keys),"Editor parameter registry differs from runtime");
        Check(!NpcExpressionEditorCatalog.Collect(graph).Any(s=>s.Key.StartsWith("target.")||s.Key=="action.duration"),"Action-only symbols leaked into goal conditions");
        graph.Definitions.OfType<NpcNeedsDefinition>().Single().Needs.Add(new(){NeedId="custom_test",DisplayName="自定义需求"});
        Check(NpcExpressionEditorCatalog.Collect(graph).Any(s=>s.Key=="needs.custom_test.value"),"Newly registered need missing in picker");
        var goal=graph.Definitions.OfType<NpcGoalDefinition>().First();
        var propertyEditor=graph.CreateCustomPropertyEditor(goal,nameof(goal.Activation));
        Check(graph.HasCustomPropertyEditors(goal)&&propertyEditor is NpcExpressionProperty,"Graph did not provide expression editor");propertyEditor.Free();
        NpcExpressionDefinition Number(double n)=>new(){Number=n};
        NpcExpressionDefinition Fact(string key)=>new(){Kind=NpcExpressionKind.Fact,Key=key};
        var a=NpcExpressionEditor.Operation(NpcExpressionKind.AtLeast,Fact("needs.hunger.value"),Number(.65));
        var b=NpcExpressionEditor.Operation(NpcExpressionKind.AtLeast,Fact("world.hour"),Number(7));
        var original=NpcExpressionEditor.Operation(NpcExpressionKind.All,a,b);
        NpcExpressionDefinition edited=null;
        var ui=NpcExpressionEditor.Build(graph,goal,NpcValueType.Boolean,original,(e,_)=>edited=e);
        AddChild(ui);
        static System.Collections.Generic.IEnumerable<T> Descendants<T>(Node node) where T:Node
        {foreach(Node child in node.GetChildren()){if(child is T item)yield return item;foreach(var descendant in Descendants<T>(child))yield return descendant;}}
        var numbers=Descendants<SpinBox>(ui).ToArray();
        Check(numbers.Length==2,"Conditions are not editable as two parameter/operator/value rows");
        numbers[0].Value=.9;numbers[1].Value=9;
        Check(Math.Abs(edited.Inputs[0].Inputs[1].Number-.9)<1e-8&&edited.Inputs[1].Inputs[1].Number==9,$"Editing a second row reverted the first row: {edited.Inputs[0].Inputs[1].Number:R}, {edited.Inputs[1].Inputs[1].Number:R}");
        Check(original.Inputs[0].Inputs[1].Number==.65&&original.Inputs[1].Inputs[1].Number==7,"Expression editor mutated undo's previous value");
        NpcValue Read(string key)=>key=="needs.hunger.value"?NpcValue.Numeric(.95):NpcValue.Numeric(10);
        Check(edited.Freeze().Evaluate(Read).IsTrue,"Multiple condition rows did not evaluate together");
        var modes=Descendants<OptionButton>(ui).First();modes.Select(1);modes.EmitSignal(OptionButton.SignalName.ItemSelected,1L);
        Check(edited.Kind==NpcExpressionKind.Any&&Math.Abs(edited.Inputs[0].Inputs[1].Number-.9)<1e-8,"Changing AND/OR lost row edits");
        ui.Free();
        goal.Activation=edited;graph.SaveJsonFields();
        string path="res://.godot/npc/expression_editor_roundtrip.tres";
        Check(ResourceSaver.Save(graph,path)==Error.Ok,"Edited conditions could not be saved");
        var loaded=ResourceLoader.Load<NpcGraphAsset>(path,cacheMode:ResourceLoader.CacheMode.Ignore);
        var restored=loaded.Definitions.OfType<NpcGoalDefinition>().First().Activation;
        Check(restored.Kind==NpcExpressionKind.Any&&Math.Abs(restored.Inputs[0].Inputs[1].Number-.9)<1e-8&&restored.Freeze().Evaluate(Read).IsTrue,"Edited conditions lost on reload");
        NpcProgram.Compile(loaded);
        var math=NpcExpressionEditor.Operation(NpcExpressionKind.Multiply,Number(2),Number(3));
        var mathUi=NpcExpressionEditor.Build(graph,goal,NpcValueType.Number,math,(e,_)=>edited=e);
        AddChild(mathUi);
        var values=Descendants<SpinBox>(mathUi).ToArray();values[0].Value=4;values[1].Value=5;
        Check(edited.Freeze().Evaluate(_=>default).Number==20&&math.Freeze().Evaluate(_=>default).Number==6,"Numeric operands or immutable arithmetic edits failed");
        mathUi.Free();
    }
    void ValidateEditorTopology(NpcGraphAsset source)
    {
        var graph=(NpcGraphAsset)source.Duplicate(true);
        var canvas=new GraphEdit();AddChild(canvas);
        var context=new GraphEditorContext{CurrentGraph=graph,RootGraph=graph,GraphEdit=canvas};
        var controller=new GraphEditorController(canvas);
        void AddView(GraphNodeData data)=>canvas.AddChild(GraphNodeViewBuilder.CreateNodeUI(data,context));
        controller.LoadGraph(graph,AddView,e=>canvas.ConnectNode(e.FromNode,e.FromPort,e.ToNode,e.ToPort));
        foreach(var data in graph.Nodes)
        {
            var view=canvas.GetNodeOrNull<GraphNode>(new NodePath(data.Id));
            Check(view!=null&&view.Name.ToString()==data.Id,"Canvas changed a node ID: "+data.Id);
            Check(graph.FindNodeById(view.Name)==data,"Selected canvas node cannot resolve its data");
            Check(data.GetInspectorObject(graph)!=null,"NPC node has no inspector definition");
            if(data is NpcGoalNodeData or NpcActionNodeData or NpcTemplateNodeData)
                Check(view.FindChildren("*",nameof(OptionButton),true,false).Count==0,"Behavior node still has a definition dropdown: "+data.Id);
        }
        Check(canvas.GetConnectionList().Count==graph.Connections.Count,"Canvas connections differ from serialized graph");
        var connectionEditor=new GraphConnectionEditorService(canvas,canvas,()=>graph,_=>{});
        connectionEditor.UpdateConnectionLabels();
        Check(!canvas.GetChildren().OfType<Label>().Any(),"Plain structural connections obscure NPC nodes with redundant labels");
        var removed=graph.Nodes.OfType<NpcFactsNodeData>().Single();
        GraphCommandService.RemoveNode(graph,canvas,removed.Id);
        Check(canvas.GetNodeOrNull<GraphNode>(new NodePath(removed.Id))==null,"Deleted node still attached before next frame");
        Check(canvas.GetConnectionList().Count==graph.Connections.Count,"Deleted node left canvas connections");
        GraphCommandService.AddNode(graph,removed.NodeType,removed.Id,removed.Position,AddView);
        Check(canvas.GetNodeOrNull<GraphNode>(new NodePath(removed.Id))?.Name.ToString()==removed.Id,"Immediate undo/re-add changed node ID");
        connectionEditor.Reset();controller.ClearGraphEdit();
        Check(!canvas.GetChildren().OfType<GraphNode>().Any()&&canvas.GetConnectionList().Count==0,"Graph clear left nodes or connections");
        canvas.QueueFree();
        var invalid=new FlowGraphAsset{Nodes=new List<GraphNodeData>{new FlowEntryNodeData{Id="module.with_dot"}}};
        Check(!invalid.Validate(out var errors)&&errors.Errors.Any(e=>e.NodeId=="module.with_dot"),"Invalid native node name passed validation");
    }
#endif
    public override void _Process(double delta)=>ModuleSystem.Process(delta,delta);
}
