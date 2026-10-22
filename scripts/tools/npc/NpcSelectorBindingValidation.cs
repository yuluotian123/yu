using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic;
using GameLogic.AI;
using GameLogic.Goap;
using Godot;

/// <summary>Headless regression checks for authored input wires and branch-local target bindings.</summary>
public partial class NpcSelectorBindingValidation : Node
{
    int _checks;
    public override async void _Ready()
    {
        try
        {
            SharedSearch();
            IndependentSearch();
            InputSourcesAndActionSets();
            SelectorSources();
            WorldFilters();
            CatalogFilters();
            ConfigurationSurface();
            PortsAndValidation();
#if TOOLS
            await CanvasPorts();
#endif
            RoundTripAndClipboard();
            ExecutionLifecycle();
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            GD.Print($"NPC_SELECTOR_BINDINGS_OK: {_checks} checks");
            GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
    void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);_checks++;}
    void Reject(Action action,string message)
    {
        try{action();}catch(ArgumentException){_checks++;return;}
        throw new InvalidOperationException(message);
    }
    static CharacterExpressionDefinition Fact(string key)=>new(){Kind=CharacterExpressionKind.Fact,Key=key};
    static CharacterExpressionDefinition Number(double value)=>new(){Number=value};
    static CharacterExpressionDefinition Text(string value)=>new(){ValueType=CharacterValueType.Symbol,Symbol=value};
    static CharacterExpressionDefinition Bool(bool value)=>new(){ValueType=CharacterValueType.Boolean,Boolean=value};
    static CharacterExpressionDefinition Equal(CharacterExpressionDefinition a,CharacterExpressionDefinition b)=>new(){Kind=CharacterExpressionKind.Equal,Inputs=new(){a,b}};
    static CharacterExpressionDefinition All(params CharacterExpressionDefinition[] values)=>new(){Kind=CharacterExpressionKind.All,Inputs=new(values)};
    static NpcEffectDefinition Set(string key,CharacterExpressionDefinition value)=>new(){Key=key,Value=value};
    static FlowGraphAsset Flow()
    {
        var flow=new FlowGraphAsset();var entry=new FlowEntryNodeData();var done=new FlowReturnNodeData{Label="Success"};
        flow.Nodes.Add(entry);flow.Nodes.Add(done);flow.ConnectNodes(entry.Id,0,done.Id,0);flow.SaveJsonFields();return flow;
    }
    internal sealed class Fixture
    {
        public readonly CharacterProfile Profile=new(){FeatureLibrary=new CharacterFeatureLibrary()};
        public readonly NpcGraphAsset Graph=new();
        public readonly NpcRootNodeData Root=new();
        public readonly NpcGoalNodeData GoalNode=new();
        public readonly NpcActionNodeData FirstNode=new(),SecondNode=new();
        public readonly NpcTargetSelectorNodeData SelectorNode=new();
        public readonly NpcTemplateNodeData PlanNode=new();
        public readonly NpcTargetSelectorDefinition Selector=new(){DisplayName="Warehouse query",Source=NpcTargetSource.WorldPoints,CandidateLimit=4};
        public readonly NpcActionDefinition First=new(){DisplayName="Choose source",ExecutionGraph=Flow(),Cost=Number(1)};
        public readonly NpcActionDefinition Second=new(){DisplayName="Use source",ExecutionGraph=Flow(),Cost=Fact("input.target.stock")};
        public readonly NpcActionInputDefinition FirstInput=new(){Name="target",InputType="target:point"};
        public readonly NpcActionInputDefinition SecondInput=new(){Name="target",InputType="target:point"};
        public readonly NpcPlanTemplateDefinition Plan=new(){DisplayName="Manual action set"};
        public Fixture()
        {
            Profile.Facts.Facts.Add(new(){Key="private.started",Type=CharacterValueType.Boolean,Initial=new(){ValueType=CharacterValueType.Boolean}});
            Profile.Facts.Facts.Add(new(){Key="private.done",Type=CharacterValueType.Boolean,Initial=new(){ValueType=CharacterValueType.Boolean}});
            First.Inputs.Add(FirstInput);Second.Inputs.Add(SecondInput);
            First.Preconditions=new(){Kind=CharacterExpressionKind.Not,Inputs=new(){Fact("private.started")}};
            First.Predictions.Add(Set("private.started",Bool(true)));
            Second.Preconditions=Fact("private.started");Second.Predictions.Add(Set("private.done",Bool(true)));
            var goal=new NpcGoalDefinition{Utility=Number(1),Completion=Fact("private.done")};
            Graph.CharacterContext=Profile;
            Add(GoalNode,goal);Add(FirstNode,First);Add(SecondNode,Second);Add(SelectorNode,Selector);Add(PlanNode,Plan);Graph.Nodes.Add(Root);
            Graph.ConnectNodes(Root.Id,1,GoalNode.Id,0);Graph.ConnectNodes(Root.Id,2,SelectorNode.Id,0);
            Graph.ConnectNodes(GoalNode.Id,0,FirstNode.Id,0);Graph.ConnectNodes(GoalNode.Id,0,SecondNode.Id,0);Graph.ConnectNodes(GoalNode.Id,1,PlanNode.Id,0);
            Plan.ActionIds=new[]{First.DefinitionId,Second.DefinitionId};
            FirstNode.Bind(Graph);SecondNode.Bind(Graph);
            Graph.ConnectNodes(SelectorNode.Id,0,FirstNode.Id,1);Graph.ConnectNodes(SelectorNode.Id,0,SecondNode.Id,1);
        }
        public void Add(NpcDefinitionNodeData node,NpcDefinitionResource definition){node.DefinitionId=definition.DefinitionId;Graph.Nodes.Add(node);Graph.Definitions.Add(definition);}
        public void Force(string first,string second)
        {
            First.Preconditions=All(First.Preconditions,Equal(Fact("input.target.id"),Text(first)));
            Second.Preconditions=All(Second.Preconditions,Equal(Fact("input.target.id"),Text(second)));
        }
        public void Unwire()=>Graph.Connections=Graph.Connections.Where(e=>e.ToPort==0).ToList();
    }
    static NpcTargetCandidate[] Points()=>new[]{new NpcTargetCandidate("A",Array.Empty<string>(),Vector3.Zero,1,true,true,true,true),new NpcTargetCandidate("B",Array.Empty<string>(),Vector3.Right,2,true,true,true,true)};
    static GoapWorldState Initial(NpcProgram program,double stockA=10,double stockB=1)
    {
        var facts=new List<KeyValuePair<string,GoapValue>>
        {
            new("private.started",GoapValue.Bool(false)),new("private.done",GoapValue.Bool(false)),new("self.location",GoapValue.Id(""))
        };
        foreach(var point in Points())
        {
            facts.Add(new("point."+point.Id+".open",GoapValue.Bool(true)));
            facts.Add(new("point."+point.Id+".available",GoapValue.Bool(true)));
            facts.Add(new("point."+point.Id+".stock",program.Encode("point."+point.Id+".stock",CharacterValue.Numeric(point.Id=="A"?stockA:stockB))));
            facts.Add(new("point."+point.Id+".distance",program.Encode("point."+point.Id+".distance",CharacterValue.Numeric(point.Distance.Value))));
        }
        return new(facts);
    }
    static (NpcPlanSearch Search,NpcBoundAction[] Bound) Search(NpcProgram program,GoapWorldState initial)
    {
        var goal=program.Goals[0];var bound=NpcTargetQuery.Ground(program,goal,initial,Points()).ToArray();
        var search=new NpcPlanSearch(program,goal,initial,bound.Select(b=>new KeyValuePair<string,GoapAction>(b.PlanActionKey,b.Search)));
        for(int n=0;n<100&&search.Status==GoapSearchStatus.Pending;n++)search.Advance(32);
        return(search,bound);
    }
    static string[] Targets((NpcPlanSearch Search,NpcBoundAction[] Bound) result)=>result.Search.Result.Steps.Select(s=>result.Bound.First(b=>b.Search.Id==s.Id).Candidates["target"].Symbol).ToArray();
    void SharedSearch()
    {
        var f=new Fixture();var program=NpcProgram.Compile(f.Graph);var result=Search(program,Initial(program));
        Check(result.Search.Status==GoapSearchStatus.Found,"Shared plan must be found");
        Check(Targets(result).SequenceEqual(new[]{"B","B"}),"Planner must compare branches and choose the cheaper complete B plan");
        var initial=Initial(program);var firstA=result.Bound.First(b=>b.Action.Id==f.First.DefinitionId&&b.Candidates["target"].Symbol=="A");
        var firstB=result.Bound.First(b=>b.Action.Id==f.First.DefinitionId&&b.Candidates["target"].Symbol=="B");
        Check(firstA.Search.TryApply(initial,out var stateA)&&firstB.Search.TryApply(initial,out var stateB)&&stateA.StateKey!=stateB.StateKey,"Shared bindings must participate in search state deduplication");
        var secondB=result.Bound.First(b=>b.Action.Id==f.Second.DefinitionId&&b.Candidates["target"].Symbol=="B");
        Check(!secondB.Search.TryApply(stateA,out _),"A branch must not silently switch from A to B");
        Check(Targets(Search(program,Initial(program,1,10))).SequenceEqual(new[]{"A","A"}),"A new plan must be free to choose A");
        Check(Targets(Search(program,Initial(program))).SequenceEqual(new[]{"B","B"}),"Searches sharing one program must not leak target state");
        var changed=stateA.With(GoapEffect.Set("point.A.open",GoapValue.Bool(false)));
        Check(!firstA.Search.TryApply(changed,out _),"Fixing identity must not freeze availability");
        f.Force("A","B");program=NpcProgram.Compile(f.Graph);
        Check(Search(program,Initial(program)).Search.Status==GoapSearchStatus.NoPlan,"Conflicting same-node targets must make the plan infeasible");
    }
    void IndependentSearch()
    {
        var f=new Fixture();f.Force("A","B");
        var other=new NpcTargetSelectorNodeData{DefinitionId=f.Selector.DefinitionId};f.Graph.Nodes.Add(other);
        f.Graph.ConnectNodes(f.Root.Id,2,other.Id,0);
        f.Graph.Connections=f.Graph.Connections.Where(e=>!(e.ToNode==f.SecondNode.Id&&e.ToPort==1)).ToList();
        f.Graph.ConnectNodes(other.Id,0,f.SecondNode.Id,1);
        var program=NpcProgram.Compile(f.Graph);var result=Search(program,Initial(program));
        Check(result.Search.Status==GoapSearchStatus.Found&&Targets(result).SequenceEqual(new[]{"A","B"}),"Different nodes sharing one query definition must select independently");
        var otherQuery=new NpcTargetSelectorDefinition{Source=NpcTargetSource.WorldDirectory};f.Graph.Definitions.Add(otherQuery);other.DefinitionId=otherQuery.DefinitionId;
        f.Selector.Source=NpcTargetSource.WorldDirectory;program=NpcProgram.Compile(f.Graph);
        var queries=program.TargetSelectors.ToDictionary(p=>p.Key,p=>(IReadOnlyList<NpcTargetCandidate>)(p.Key==f.Selector.DefinitionId?new[]{Points()[0]}:new[]{Points()[1]}));
        // Different scoped queries cannot borrow one another's candidates during grounding.
        var scoped=NpcTargetQuery.Ground(program,program.Goals[0],Initial(program),Points(),queries).ToArray();
        Check(scoped.Count(b=>b.Action.Id==f.First.DefinitionId)==1&&scoped.First(b=>b.Action.Id==f.First.DefinitionId).Candidates["target"].Symbol=="A"
            &&scoped.Count(b=>b.Action.Id==f.Second.DefinitionId)==1&&scoped.First(b=>b.Action.Id==f.Second.DefinitionId).Candidates["target"].Symbol=="B","Scoped query results must not leak through another selector's candidate union");
        var extra=new NpcActionInputDefinition{Name="other",InputType="target:point"};f.First.Inputs.Add(extra);f.FirstNode.Bind(f.Graph);
        f.Graph.ConnectNodes(f.SelectorNode.Id,0,f.FirstNode.Id,2);program=NpcProgram.Compile(f.Graph);
        var grounded=NpcTargetQuery.Ground(program,program.Goals[0],Initial(program),Points()).Where(b=>b.Action.Id==f.First.DefinitionId).ToArray();
        Check(grounded.Length==2&&grounded.All(b=>b.Candidates["target"]==b.Candidates["other"]),"Same-node inputs within one action must share a value without a Cartesian product");
    }
    void InputSourcesAndActionSets()
    {
        var f=new Fixture();f.Unwire();f.FirstInput.Value=Text("A");f.SecondInput.Value=Text("B");
        Reject(()=>NpcProgram.Compile(f.Graph),"An object expression must not bypass the required selector wire");
        f=new Fixture();
        var program=NpcProgram.Compile(f.Graph);
        f.Plan.ActionIds=new[]{f.First.DefinitionId};program=NpcProgram.Compile(f.Graph);
        Check(Search(program,Initial(program)).Search.Status==GoapSearchStatus.NoPlan,"Plan must still restrict actions to its explicit list");
        f.Plan.ActionIds=Array.Empty<string>();Reject(()=>NpcProgram.Compile(f.Graph),"An empty Plan action list must not mean all actions");
        var scalar=new Fixture();
        scalar.First.Inputs.Add(new NpcActionInputDefinition{Name="quantity",InputType="number",Value=Number(3)});
        scalar.First.Inputs.Add(new NpcActionInputDefinition{Name="allowed",InputType="boolean",Value=Bool(true)});
        scalar.First.Inputs.Add(new NpcActionInputDefinition{Name="label",InputType="text",Value=Text("delivery")});
        program=NpcProgram.Compile(scalar.Graph);var initial=Initial(program);var result=Search(program,initial);
        var bound=result.Bound.First(b=>b.Action.Id==scalar.First.DefinitionId);
        var context=program.Contextual(initial,bound);
        Check(program.Read(context,"input.quantity").Number==3&&program.Read(context,"input.allowed").IsTrue,"Numeric and boolean inputs must remain usable without plan variables");
        Check(program.Read(context,"input.label").Symbol=="delivery"&&scalar.FirstNode.GetInputCount()==2,"Ordinary text must remain a value input without a target port");
        var frozen=bound with{Resolved=bound.Action.Inputs.ToDictionary(i=>i.Name,i=>program.Read(context,"input."+i.Name))};
        context=program.Contextual(initial,frozen);
        Check(program.Read(context,"input.quantity").Number==3&&program.Read(context,"input.allowed").IsTrue,"Resolved execution inputs must retain scalar values");
    }
    void SelectorSources()
    {
        var f=new Fixture();f.Selector.Source=NpcTargetSource.FixedObject;f.Selector.FixedObjectId="B";
        f.Selector.Selection=NpcTargetSelection.LowestScore;f.Selector.Score=null;
        var program=NpcProgram.Compile(f.Graph);var result=Search(program,Initial(program));
        Check(result.Search.Status==GoapSearchStatus.Found&&Targets(result).SequenceEqual(new[]{"B","B"}),"A fixed selector must provide exactly its object, ignoring hidden query ranking");
        f.Selector.FixedObjectId="missing";program=NpcProgram.Compile(f.Graph);
        Check(Search(program,Initial(program)).Search.Status==GoapSearchStatus.NoPlan,"Missing fixed objects must yield no candidates");
        f.Selector.FixedObjectId="";Reject(()=>NpcProgram.Compile(f.Graph),"An empty fixed-object selector must be rejected");
        f.Profile.Identity.Parameters.Add(new(){Key="bed",ValueType=CharacterValueType.Symbol,Symbol="B"});
        f.Selector.Source=NpcTargetSource.IdentityParameter;f.Selector.ParameterKey="bed";program=NpcProgram.Compile(f.Graph);
        Check(Targets(Search(program,Initial(program))).SequenceEqual(new[]{"B","B"}),"Actor parameter selectors must use that actor's referenced object");
        f.Selector.Source=NpcTargetSource.CurrentLocation;program=NpcProgram.Compile(f.Graph);
        var initial=Initial(program).With(GoapEffect.Set("self.location",GoapValue.Id("A")));
        Check(Targets(Search(program,initial)).SequenceEqual(new[]{"A","A"}),"Current-location selectors must bind the initial location consistently");
        Check(Search(program,Initial(program)).Search.Status==GoapSearchStatus.NoPlan,"Unknown current location must not select an arbitrary object");
    }
    void WorldFilters()
    {
        var f=new Fixture();var type=WorldTestObjects.StockType();
        f.Selector.ObjectType=type;f.Selector.RequiredCapabilities=new[]{"stock","interaction"};f.Selector.RequiredOperations=new[]{"take_stock"};
        var program=NpcProgram.Compile(f.Graph);var selector=program.TargetSelectors[f.Selector.DefinitionId];var state=Initial(program);
        var point=Points()[0] with{ObjectTypeId=type.Id,Capabilities=new[]{"stock","interaction"},Operations=new[]{"take_stock"}};
        bool Matches(NpcTargetCandidate candidate,NpcTargetSelectorSpec query=null)=>NpcTargetQuery.Matches(program,query??selector,state,candidate);
        Check(Matches(point),"Type, capabilities and operations must all match");
        Check(!Matches(point with{ObjectTypeId="other"}),"Identical tags cannot substitute for object type");
        Check(!Matches(point with{Capabilities=new[]{"interaction"}}),"Every required capability must be declared");
        Check(!Matches(point with{Operations=new[]{"pour_liquid"}}),"Every required operation must be provided");
        Check(!Matches(Points()[0]),"Candidates without world metadata cannot satisfy world filters");
        Check(NpcTargetQuery.Select(program,selector with{Source=NpcTargetSource.FixedObject,FixedObjectId="A"},state,new[]{point with{ObjectTypeId="other"}}).Count==0,"Fixed identity cannot bypass world filters");
        Check(Matches(Points()[0],selector with{ObjectTypeId="",RequiredCapabilities=Array.Empty<string>(),RequiredOperations=Array.Empty<string>()}),"Empty world filters must impose no restriction");
        const string path="res://.godot/npc-selector-validation/world_filters.tres";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(path.GetBaseDir()));f.Graph.SaveJsonFields();
        Check(ResourceSaver.Save(f.Graph,path)==Error.Ok,"World-filter graph must save");
        var loaded=ResourceLoader.Load<NpcGraphAsset>(path,"",ResourceLoader.CacheMode.Ignore);
        var restored=NpcProgram.Compile(loaded,f.Profile).TargetSelectors[f.Selector.DefinitionId];
        Check(restored.ObjectTypeId==type.Id&&restored.RequiredCapabilities.SequenceEqual(new[]{"stock","interaction"})&&restored.RequiredOperations.Single()=="take_stock","World filters must survive serialization");
#if TOOLS
        foreach(var property in new[]{nameof(NpcTargetSelectorDefinition.ObjectType),nameof(NpcTargetSelectorDefinition.RequiredCapabilities),nameof(NpcTargetSelectorDefinition.RequiredOperations)})
        {
            var editor=f.Graph.CreateCustomPropertyEditor(f.Selector,property);
            try{Check(editor is NpcWorldFilterProperty,"World filters must use named registered choices");editor._UpdateProperty();}
            finally{editor.Free();}
        }
#endif
        f.Selector.RequiredCapabilities[0]="liquid";f.Selector.RequiredOperations[0]="other";type.Id="changed";
        Check(Matches(point),"Compiled selectors must freeze IDs and requirement arrays");
        f.Selector.RequiredCapabilities=new[]{"stock","stock"};Reject(()=>NpcProgram.Compile(f.Graph),"Duplicate capability requirements must be rejected");
        f.Selector.RequiredCapabilities=Array.Empty<string>();f.Selector.RequiredOperations=new[]{" "};Reject(()=>NpcProgram.Compile(f.Graph),"Empty operation requirements must be rejected");
        f.Selector.RequiredOperations=Array.Empty<string>();f.Selector.ObjectType=new(){Capabilities=new(){new WorldLiquidCapability()}};
        Reject(()=>NpcProgram.Compile(f.Graph),"Object type must support the selected adapter");
    }
    void CatalogFilters()
    {
        var type=WorldTestObjects.StockType();type.Capability<WorldInteractionCapability>().Operations.Add(new WorldTakeStockInteraction{Id="take",DisplayName="Take"});
        var other=WorldTestObjects.StockType();var place=new WorldLocationDefinition();var level=new WorldLevelDefinition{ScenePath="res://unused.tscn",Location=place};
        var definition=new WorldDefinition{Locations=new(){place},Levels=new(){level},ObjectTypes=new(){type,other}};
        var overridden=type.Capabilities.Select(c=>c is WorldInteractionCapability?new WorldInteractionCapability{Operations=new(){new WorldTakeStockInteraction{Id="withdraw",DisplayName="Withdraw"}}}:c).ToArray();
        var records=new[]{
            new WorldCatalogEntry("base","Base",type,level,place,Array.Empty<string>(),"",Array.AsReadOnly(type.Capabilities.ToArray())),
            new WorldCatalogEntry("override","Override",type,level,place,Array.Empty<string>(),"",Array.AsReadOnly(overridden)),
            new WorldCatalogEntry("other","Other",other,level,place,Array.Empty<string>(),"",Array.AsReadOnly(other.Capabilities.ToArray()))};
        var catalog=new WorldCatalog(definition,records);
        Check(catalog.Query(objectType:type.Id).Count==2&&catalog.Query(objectType:other.Id).Single().Id=="other","Type query and cache must use stable IDs");
        Check(catalog.Query(operations:new[]{"take"}).Single().Id=="base"&&catalog.Query(operations:new[]{"withdraw"}).Single().Id=="override","Operation index must use effective instance overrides");
        Check(catalog.Query(capabilities:new[]{"stock","liquid"}).Count==0&&catalog.Query(capabilities:new[]{"stock","interaction"}).Count==3,"Capability queries require all requested capabilities");
        Check(catalog.Query(objectType:other.Id,operations:new[]{"take"}).Count==0,"Type and operation requirements must intersect");
        Check(catalog.Query(place:place.Id,allTags:new[]{"storage"},objectType:type.Id,capabilities:new[]{"stock"},operations:new[]{"withdraw"}).Single().Id=="override","Location, tags, type, capabilities and operations must compose");
        Check(catalog.Query(capabilities:new[]{"interaction","stock"}).Count==3&&catalog.Query(operations:new[]{"missing"}).Count==0,"Query cache must isolate operation and capability filters");
        level.ScenePath="res://.godot/npc-selector-validation/filters.tscn";
        var generated=new WorldLevelCatalog{LevelId=level.Id,Entries=new(records.Select(r=>r.Encode()))};
        Check(ResourceSaver.Save(generated,level.CatalogPath)==Error.Ok,"Permission-query catalog fixture must save");
        try
        {
            var world=(WorldModule)Framework.ModuleSystem.GetModule<IWorldModule>();world.Configure(definition);
            var f=new Fixture();f.Selector.ObjectType=type;f.Selector.RequiredOperations=new[]{"withdraw"};
            var query=NpcProgram.Compile(f.Graph).TargetSelectors[f.Selector.DefinitionId] with{Source=NpcTargetSource.WorldDirectory,Scope=NpcWorldScope.World};
            var knowledge=new WorldKnowledge(null);
            Check(NpcWorldTargetQuery.Directory(world,knowledge,query,null).Count==0,"World filters must not grant knowledge");
            knowledge.KnownObjects.UnionWith(records.Select(r=>r.Id));
            Check(NpcWorldTargetQuery.Directory(world,knowledge,query,null).Single()=="override","Known-object directory queries must honor effective operation filters");
            knowledge.KnownObjects.Clear();knowledge.KnownLocations.Add(place.Id);place.PublicServices=new[]{"storage"};
            Check(NpcWorldTargetQuery.Directory(world,knowledge,query with{Information=NpcWorldInformation.PublicServices},null).Single()=="override","Public-service queries must combine permission and world filters");
            knowledge.KnownLocations.Clear();
            Check(NpcWorldTargetQuery.Directory(world,knowledge,query with{Information=NpcWorldInformation.Authoritative},null).Single()=="override","Authoritative queries must still honor world filters");
        }
        finally{Framework.ModuleSystem.Shutdown();}
    }
    void ConfigurationSurface()
    {
        var f=new Fixture();
        var retired=new[]{"TargetTags","TargetIdParameter","TargetSelectorId","ShareTargetWithinPlan","RequireAtTarget","ArrivesAtTarget","ReserveTarget","DistanceCost","BindingVersion","Resources"};
        var actionNames=f.First.GetPropertyList().Select(p=>p["name"].AsString()).ToHashSet();
        Check(retired.All(name=>!actionNames.Contains(name)),"Retired action fields must be removed from serialization and the inspector");
        Check(!f.Plan.GetPropertyList().Any(p=>p["name"].AsString()=="Variables"),"Plan must no longer expose or serialize variables");
        var inputNames=f.FirstInput.GetPropertyList().Select(p=>p["name"].AsString()).ToHashSet();
        Check(!inputNames.Contains("Source")&&!inputNames.Contains("SelectorId"),"Target inputs must expose no alternate source or selector-ID field");
        var targetValue=f.FirstInput.GetPropertyList().First(p=>p["name"].AsString()==nameof(NpcActionInputDefinition.Value));
        Check((targetValue["usage"].AsInt64()&((long)PropertyUsageFlags.Editor|(long)PropertyUsageFlags.Storage))==0,"Target input values must be absent from both the inspector and saved resources");
#if TOOLS
        var sourceEditor=new NpcBindingProperty(f.FirstInput,nameof(NpcActionInputDefinition.InputType));
        try
        {
            sourceEditor._UpdateProperty();
            var typeButton=sourceEditor.FindChildren("*",nameof(Button),true,false).OfType<Button>().Single();
            Check(typeButton.Text=="目标 · 场景交互点","The inspector must present a single typed target choice");
        }
        finally{sourceEditor.Free();}
#endif
        bool Visible(string name)=>f.Selector.GetPropertyList().Any(p=>p["name"].AsString()==name&&(p["usage"].AsInt64()&(long)PropertyUsageFlags.Editor)!=0);
        Check(Visible("CandidateLimit")&&!Visible("Score")&&!Visible("ParameterKey"),"Candidate queries must show only relevant parameters");
        f.Selector.Selection=NpcTargetSelection.LowestScore;
        Check(!Visible("CandidateLimit")&&Visible("Score"),"A single scored selection must not show an unused candidate limit");
        f.Selector.Source=NpcTargetSource.FixedObject;
        Check(Visible("FixedObjectId")&&!Visible("Selection")&&!Visible("Score")&&!Visible("CandidateLimit"),"Fixed sources must show their object picker without irrelevant search settings");
#if TOOLS
        var catalogRoot=new Node();
        try
        {
            catalogRoot.AddChild(new GameObject3D{Name="NamedBed",PersistentId="bed-id",Components=new(){new WorldObjectComponent3D{DisplayName="Own bed",ObjectType=WorldTestObjects.StockType()},new WorldInteractionComponent3D()}});
            var choices=NpcTargetCatalog.Points(catalogRoot);
            Check(choices.Count==1&&choices[0].Id=="bed-id"&&choices[0].Name=="Own bed","The fixed-object picker must show names and save stable identities");
            catalogRoot.AddChild(new GameObject3D{Name="Duplicate",PersistentId="bed-id",Components=new(){new WorldObjectComponent3D{ObjectType=WorldTestObjects.StockType()},new WorldInteractionComponent3D()}});
            Check(NpcTargetCatalog.Points(catalogRoot).Count==0,"Ambiguous object identities must not be offered by the picker");
        }
        finally{catalogRoot.Free();}
#endif
        string scaffoldRoot=OS.GetCmdlineUserArgs().FirstOrDefault(arg=>arg.StartsWith("--scaffold-root=",StringComparison.Ordinal))?["--scaffold-root=".Length..]??"res://assets/resources/npc";
        foreach(string name in new[]{"gray_green_resident","ochre_courier"})
        {
            string path=scaffoldRoot.TrimEnd('/')+"/"+name+"/npc_graph.tres";
            var graph=ResourceLoader.Load<NpcGraphAsset>(path,"",ResourceLoader.CacheMode.Ignore);
            Check(graph!=null&&NpcProgram.Compile(graph,f.Profile).Goals.Count==0,"Existing scaffold graph must still load and compile: "+path);
            Check(graph.Definitions.Count==3&&graph.Definitions.All(d=>graph.Nodes.OfType<NpcDefinitionNodeData>().Any(n=>n.DefinitionId==d.DefinitionId)),"Shipped scaffold graphs must contain only their three referenced module definitions: "+path);
        }
    }
    void PortsAndValidation()
    {
        var f=new Fixture();f.FirstInput.Name="renamed";f.FirstNode.Bind(f.Graph);
        Check(f.FirstNode.InputAt(1)==f.FirstInput&&f.FirstNode.GetInputPortName(1)=="renamed","Rename must retain the port identity");
        f.First.Inputs.Remove(f.FirstInput);f.FirstNode.Bind(f.Graph);
        Check(f.FirstNode.GetInputCount()==1&&!f.Graph.Connections.Any(e=>e.ToNode==f.FirstNode.Id&&e.ToPort>0),"Removing a target input must remove its port and wire");
        var extra=new NpcActionInputDefinition{Name="new",InputType="target:point"};f.First.Inputs.Add(extra);f.FirstNode.Bind(f.Graph);
        Check(f.FirstNode.GetInputCount()==2&&f.FirstNode.InputAt(1)==extra&&!f.Graph.Connections.Any(e=>e.ToNode==f.FirstNode.Id&&e.ToPort==1),"A new input must reuse the visible slot without inheriting the removed input's wire");
        f.First.Inputs.Remove(extra);f.First.Inputs.Add(f.FirstInput);f.FirstInput.Name="target";f.FirstNode.Bind(f.Graph);
        Check(NpcProgram.Compile(f.Graph).Goals.Count==1,"Undoing input removal must restore the original wire");
        for(int i=0;i<12;i++)
        {
            f.First.Inputs.Add(extra);f.FirstNode.Bind(f.Graph);
            f.First.Inputs.Remove(extra);f.FirstNode.Bind(f.Graph);
        }
        Check(f.FirstNode.GetInputCount()==2,"Repeated add/remove operations must not accumulate ports");
        f.First.Inputs.Add(extra);f.FirstNode.Bind(f.Graph);
        var other=new NpcTargetSelectorNodeData{DefinitionId=f.Selector.DefinitionId};f.Graph.Nodes.Add(other);
        f.Graph.ConnectNodes(f.Root.Id,2,other.Id,0);f.Graph.ConnectNodes(other.Id,0,f.FirstNode.Id,2);
        f.First.Inputs.Remove(extra);f.First.Inputs.Insert(0,extra);f.FirstNode.Bind(f.Graph);
        Check(f.Graph.HasConnection(f.SelectorNode.Id,0,f.FirstNode.Id,2)&&f.Graph.HasConnection(other.Id,0,f.FirstNode.Id,1),"Reordering inputs must remap wires by identity");
        f.First.Inputs.Remove(extra);f.FirstNode.Bind(f.Graph);
        Check(f.Graph.HasConnection(f.SelectorNode.Id,0,f.FirstNode.Id,1)&&f.FirstNode.GetInputCount()==2,"Removing a preceding input must compact remaining ports and wires");
        f.First.Inputs.Add(new(){Name="amount",InputType="number",Value=Number(3)});f.FirstNode.Bind(f.Graph);
        Check(f.FirstNode.GetInputCount()==2,"Ordinary parameters must not create graph ports");
        f.First.Inputs.Last().ResourceRequirements=new[]{"retired-target-capability"};
        Check(NpcProgram.Compile(f.Graph).Goals[0].Actions[0].Resources.Count==0,"Ordinary parameters must ignore resource requirements left from a previous target type");
        f.Graph.Connections=f.Graph.Connections.Where(e=>!(e.ToNode==f.SecondNode.Id&&e.ToPort==1)).ToList();
        Reject(()=>NpcProgram.Compile(f.Graph),"An unwired selector-node input must be rejected");
        f.Graph.ConnectNodes(f.SelectorNode.Id,0,f.SecondNode.Id,1);f.SecondInput.InputType="text";
        f.SecondNode.Bind(f.Graph);
        Check(f.SecondNode.GetInputCount()==1&&!f.Graph.Connections.Any(e=>e.ToNode==f.SecondNode.Id&&e.ToPort>0),"Changing to an ordinary parameter must remove the target port and wire");
        f.SecondInput.InputType="target:point";f.SecondNode.Bind(f.Graph);
        Check(f.Graph.HasConnection(f.SelectorNode.Id,0,f.SecondNode.Id,1)&&NpcProgram.Compile(f.Graph).Goals.Count==1,"Undoing the type change must restore the same target wire");
        f.Graph.Connections.Add(new GraphConnection{FromNode=f.SelectorNode.Id,FromPort=0,ToNode=f.SecondNode.Id,ToPort=1});
        Reject(()=>NpcProgram.Compile(f.Graph),"Multiple wires to one input must be rejected");
    }
#if TOOLS
    async System.Threading.Tasks.Task CanvasPorts()
    {
        var f=new Fixture();
        var extra=new NpcActionInputDefinition{Name="other",InputType="target:point"};
        f.First.Inputs.Add(extra);f.FirstNode.Bind(f.Graph);
        f.Graph.ConnectNodes(f.SelectorNode.Id,0,f.FirstNode.Id,2);
        var edit=new GraphEdit();AddChild(edit);
        var context=new GraphEditorContext{CurrentGraph=f.Graph,GraphEdit=edit};
        void Refresh()
        {
            foreach(var view in edit.GetChildren().OfType<GraphNode>())
                GraphNodeViewBuilder.RefreshNodeUI(f.Graph.FindNodeById(view.Name),view,context);
        }
        try
        {
            foreach(var node in f.Graph.Nodes)edit.AddChild(GraphNodeViewBuilder.CreateNodeUI(node,context));
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(edit.IsNodeConnected(f.SelectorNode.Id,0,f.FirstNode.Id,2),"Canvas must display both initial target input wires");
            f.First.Inputs.Remove(f.FirstInput);Refresh();
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(edit.GetNode<GraphNode>(f.FirstNode.Id).GetInputPortCount()==2
                &&edit.IsNodeConnected(f.SelectorNode.Id,0,f.FirstNode.Id,1)
                &&!edit.IsNodeConnected(f.SelectorNode.Id,0,f.FirstNode.Id,2),"Canvas must remove obsolete ports and reconnect the remaining wire");
            f.First.Inputs.Insert(0,f.FirstInput);Refresh();
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Check(edit.GetNode<GraphNode>(f.FirstNode.Id).GetInputPortCount()==3
                &&edit.IsNodeConnected(f.SelectorNode.Id,0,f.FirstNode.Id,1)
                &&edit.IsNodeConnected(f.SelectorNode.Id,0,f.FirstNode.Id,2),"Canvas must restore the original wire and port order after undo");
        }
        finally {edit.Free();}
    }
#endif
    void RoundTripAndClipboard()
    {
        var f=new Fixture();f.Graph.SaveJsonFields();
        Check(f.Graph.Validate(out _),"The complete authored graph must pass generic graph validation");
        const string path="res://.godot/npc-selector-validation/graph.tres";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://.godot/npc-selector-validation"));
        Check(ResourceSaver.Save(f.Graph,path)==Error.Ok,"Graph resource must save");
        var loaded=ResourceLoader.Load<NpcGraphAsset>(path,"",ResourceLoader.CacheMode.Ignore);var program=NpcProgram.Compile(loaded,f.Profile);
        Check(Targets(Search(program,Initial(program))).SequenceEqual(new[]{"B","B"}),"Save/load must preserve wires, input identities and shared bindings");
        Check(loaded.Nodes.OfType<NpcActionNodeData>().First(n=>n.Id==f.FirstNode.Id).InputAt(1).InputId==f.FirstInput.InputId,"Input identity must persist in resources");
#if TOOLS
        var edit=new GraphEdit();var context=new GraphEditorContext{CurrentGraph=f.Graph,GraphEdit=edit};
        foreach(var node in f.Graph.Nodes.Where(n=>n is not NpcRootNodeData))
        {
            var view=GraphNodeViewBuilder.CreateNodeUI(node,context);edit.AddChild(view);view.Selected=true;
        }
        var clipboard=new GraphClipboardService();clipboard.Copy(f.Graph,edit);
        var destination=new NpcGraphAsset{CharacterContext=f.Profile};var paste=clipboard.CreatePasteData(destination,Vector2.Zero);
        destination.AddNodeResources(paste.Resources);foreach(var node in paste.Nodes)destination.Nodes.Add(node);foreach(var edge in paste.Connections)destination.Connections.Add(edge);
        var root=new NpcRootNodeData();destination.Nodes.Add(root);
        destination.ConnectNodes(root.Id,1,paste.Nodes.OfType<NpcGoalNodeData>().Single().Id,0);
        destination.ConnectNodes(root.Id,2,paste.Nodes.OfType<NpcTargetSelectorNodeData>().Single().Id,0);
        program=NpcProgram.Compile(destination);
        Check(Targets(Search(program,Initial(program))).SequenceEqual(new[]{"B","B"}),"Copy/paste must remap selector-node identity and preserve internal input wires");
        Check(program.Goals[0].Actions[0].Inputs[0].SelectionNodeId!=f.SelectorNode.Id,"Pasted selection must have its own identity");
        edit.Free();
#endif
    }
    void ExecutionLifecycle()
    {
        var f=new Fixture();f.Selector.RequireReachable=false;
        f.Selector.RequiredCapabilities=new[]{"stock"};f.Selector.RequiredOperations=new[]{"take_stock"};
        f.First.Results.Add(Set("private.started",Bool(true)));f.Second.Results.Add(Set("private.done",Bool(true)));
        var perception=new NpcPerceptionNodeData();f.Add(perception,new NpcPerceptionDefinition{RequireLineOfSight=false});f.Graph.ConnectNodes(f.Root.Id,0,perception.Id,0);
        var hosts=new List<GameObject3D>();
        var blackboard=new GraphBlackboardNode();AddChild(blackboard);
        GameObject3D Host(string name,params Component3D[] components)
        {
            var host=new GameObject3D{Name=name,PersistentId=name,Components=new(components)};
            WorldTestObjects.WithArea(host);hosts.Add(host);AddChild(host);return host;
        }
        void Plan(AiAgentComponent3D agent)
        {
            agent.OnUpdate(0);
            for(int i=0;i<100&&agent.SelectorBindings.Count==0;i++)agent.AdvancePlanning(100);
        }
        try
        {
            Host("BindingWorld",new AiWorldComponent3D());
            var typeA=WorldTestObjects.StockType(10);var typeB=WorldTestObjects.StockType(1);
            foreach(var type in new[]{typeA,typeB})type.Capability<WorldInteractionCapability>().Operations.Add(new WorldTakeStockInteraction{Id="take_stock",DisplayName="Take stock"});
            var pointA=Host("A",WorldTestObjects.Components(typeA)).GetComponent<WorldInteractionComponent3D>();
            var pointB=Host("B",WorldTestObjects.Components(typeB)).GetComponent<WorldInteractionComponent3D>();
            pointB.Owner.Position=Vector3.Right;
            AiAgentComponent3D Actor(string name)=>Host(name,new CharacterDataComponent3D{Profile=f.Profile,Persist=false},new AiAgentComponent3D{Graph=f.Graph,Persist=false}).GetComponent<AiAgentComponent3D>();
            var first=Actor("BindingActor1");var second=Actor("BindingActor2");
            Check(first.Program!=null&&second.Program!=null&&first.Running&&second.Running,"Actors must initialize through the EC lifecycle");
            var adapter=NpcBindingAdapters.Find("point");var query=first.Program.TargetSelectors[f.Selector.DefinitionId] with{ObjectTypeId=typeB.Id};
            Check(adapter.Candidates(first,query).Single().Id=="B","Loaded-object queries must apply type and operation filters before selection");
            Check(!adapter.Candidates(first,query with{Source=NpcTargetSource.FixedObject,FixedObjectId="A"}).Any(),"Fixed sources must obey type filters at runtime");
            first.Character.WorldKnowledge.KnownObjects.UnionWith(new[]{"A","B"});
            Check(adapter.Candidates(first,query with{Source=NpcTargetSource.KnownPoints}).Single().Id=="B","Known-object sources must obey the same world filters");
            Check(!adapter.Matches(first,CharacterValue.Text("A"),query)&&!adapter.Matches(first,CharacterValue.Text("B"),query with{RequiredOperations=new[]{"missing"}}),"Execution validation must recheck type and operation requirements");
            Check(!adapter.Candidates(first,query with{RequiredOperations=new[]{"missing"},RequireReachable=true}).Any(),"Unsupported operations must be eliminated before navigation");
            Plan(first);Check(first.SelectorBindings.Count==1&&first.SelectorBindings.Values.Single().Symbol=="B","Generated plan must store B before starting its first step");
            pointA.Owner.GetComponent<WorldStockComponent3D>().Amount=0;pointB.Owner.GetComponent<WorldStockComponent3D>().Amount=10;Plan(second);
            Check(second.SelectorBindings.Values.Single().Symbol=="A"&&first.SelectorBindings.Values.Single().Symbol=="B","Two NPCs using one graph must retain independent selected targets");
            second.CancelPlan();Check(second.SelectorBindings.Count==0&&first.SelectorBindings.Count==1,"Cancelling one actor must clear only its own bindings");
            pointB.Owner.GetComponent<WorldOpeningHoursComponent3D>().SetOpen(false);first.OnUpdate(0);
            Check(first.LastFailure=="InputUnavailable"&&first.SelectorBindings.Count==0&&first.CurrentAction.Length==0,"An invalid selected object must cancel rather than silently switch to A");
            pointB.Owner.GetComponent<WorldOpeningHoursComponent3D>().SetOpen(true);pointA.Owner.GetComponent<WorldStockComponent3D>().Amount=10;pointB.Owner.GetComponent<WorldStockComponent3D>().Amount=1;
            first.RequestReplan();Plan(first);
            for(int i=0;i<8&&first.CompletedActions<2;i++)first.OnUpdate(.01);
            Check(first.CompletedActions==2&&first.SelectorBindings.Count==0&&first.Read("private.done").IsTrue,"Successful execution must keep shared identity and clear bindings on completion");
            second.RequestReplan();Plan(second);Check(second.SelectorBindings.Count==1,"Replanning must create a fresh binding");
            string selected=second.SelectorBindings.Values.Single().Symbol;var selectedPoint=selected=="A"?pointA:pointB;
            var operation=selectedPoint.Operations[0];selectedPoint.Operations.Clear();second.OnUpdate(0);
            Check(second.LastFailure=="InputUnavailable"&&second.SelectorBindings.Count==0,"A removed required operation must cancel the bound plan instead of switching objects");
            selectedPoint.Operations.Add(operation);second.RequestReplan();Plan(second);
            second.SetRunning(false);Check(second.SelectorBindings.Count==0,"Pausing must release shared target bindings");
        }
        finally
        {
            foreach(var host in hosts.AsEnumerable().Reverse())host.Free();
            blackboard.Free();
            Framework.ModuleSystem.Shutdown();
        }
    }
}
