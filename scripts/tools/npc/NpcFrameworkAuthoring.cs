using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic;
using GameLogic.AI;
using Godot;

/// <summary>Rebuilds the two editable examples. Not used by gameplay.</summary>
public partial class NpcFrameworkAuthoring : Node
{
    public override async void _Ready()
    {
        try
        {
            if(OS.GetCmdlineUserArgs().Contains("--action-inputs-only"))
            {
                foreach(string folder in new[]{"gray_green_resident","ochre_courier"})
                {
                    string path=$"res://assets/resources/npc/{folder}/npc_graph.tres";
                    var graph=GD.Load<NpcGraphAsset>(path);graph.MigrateActionBindings();NpcProgram.Compile(graph);Save(graph,path);
                    GD.Print("NPC_ACTION_INPUTS_MIGRATED "+folder);
                }
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                GC.Collect();GC.WaitForPendingFinalizers();
                GetTree().Quit();return;
            }
            if(OS.GetCmdlineUserArgs().Contains("--resident-stroll-only"))
            {
                var graph=GD.Load<NpcGraphAsset>(ResidentGraphPath);ConfigureResidentStroll(graph);Save(graph,ResidentGraphPath);
                GD.Print("NPC_RESIDENT_STROLL_CONFIGURED");GetTree().Quit();return;
            }
            if(OS.GetCmdlineUserArgs().Contains("--target-selectors-only"))
            {
                foreach(string folder in new[]{"gray_green_resident","ochre_courier"})
                {
                    string path=$"res://assets/resources/npc/{folder}/npc_graph.tres";
                    var graph=GD.Load<NpcGraphAsset>(path);bool changed=graph.MigrateTargetSelectors();
                    NpcProgram.Compile(graph);if(changed)Save(graph,path);
                    GD.Print($"NPC_TARGET_MIGRATED {folder}: {graph.Definitions.OfType<NpcTargetSelectorDefinition>().Count()} selectors");
                }
                GetTree().Quit();return;
            }
            if(OS.GetCmdlineUserArgs().Contains("--needs-preset-only"))
            {
                ApplyCommonNeedsToExamples();GD.Print("NPC_NEEDS_PRESET_APPLIED");GetTree().Quit();return;
            }
            SaveFrames();
            Build("gray_green_resident","灰绿居民",false);
            Build("ochre_courier","赭衣跑腿员",true);
            GD.Print("NPC_AUTHORING_OK");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    static void ApplyCommonNeedsToExamples()
    {
        var preset=GD.Load<NpcNeedsDefinition>(NpcNeedsDefinition.CommonPresetPath);
        foreach(string folder in new[]{"gray_green_resident","ochre_courier"})
        {
            string path=$"res://assets/resources/npc/{folder}/npc_graph.tres";
            var graph=GD.Load<NpcGraphAsset>(path);
            graph.Definitions.OfType<NpcNeedsDefinition>().Single().Needs=new NpcNeedsDefinition().MergePreset(preset);
            var visited=new HashSet<NpcExpressionDefinition>();
            string RenameKey(string key)=>key.Replace("needs.purpose.","needs.fun.",StringComparison.Ordinal);
            void Rename(NpcExpressionDefinition expression)
            {
                if(expression==null||!visited.Add(expression))return;
                expression.Key=RenameKey(expression.Key);foreach(var child in expression.Inputs)Rename(child);
            }
            foreach(var goal in graph.Definitions.OfType<NpcGoalDefinition>())
            {Rename(goal.Activation);Rename(goal.Utility);Rename(goal.Completion);}
            foreach(var action in graph.Definitions.OfType<NpcActionDefinition>())
            {
                Rename(action.Preconditions);Rename(action.Cost);
                foreach(var effect in action.Predictions.Concat(action.Results)){effect.Key=RenameKey(effect.Key);Rename(effect.Value);}
            }
            foreach(var template in graph.Definitions.OfType<NpcPlanTemplateDefinition>())Rename(template.Eligibility);
            graph.MarkDirty();NpcProgram.Compile(graph);Save(graph,path);
            GD.Print($"NPC_NEEDS_PRESET {folder}: thirst,hunger,fun,fatigue");
        }
    }
    static void SaveFrames()
    {
        var frames=new SpriteFrames();frames.RemoveAnimation("default");
        foreach(var (name,count,fps) in new[]{("idle",24,8d),("walk",13,12d)})
        {
            frames.AddAnimation(name);frames.SetAnimationSpeed(name,fps);
            for(int i=0;i<count;i++)frames.AddFrame(name,GD.Load<Texture2D>($"res://assets/textures/sprites/npc/ochre_courier/{name}/{name}_{i:00}.png"));
        }
        Save(frames,"res://assets/resources/npc/ochre_courier/sprite_frames.tres");
    }
    public static void Save(Resource resource,string path)
    {
        if(resource is GraphAsset graph)graph.SaveJsonFields();
        var error=ResourceSaver.Save(resource,path);if(error!=Error.Ok)throw new Exception($"Save {path}: {error}");
    }
    static NpcExpressionDefinition Lit(double value)=>new(){Kind=NpcExpressionKind.Literal,Number=value};
    static NpcExpressionDefinition Fact(string key)=>new(){Kind=NpcExpressionKind.Fact,Key=key};
    static NpcExpressionDefinition Expr(NpcExpressionKind kind,params NpcExpressionDefinition[] children)=>new(){Kind=kind,Inputs=new(children)};
    static NpcScalarDefinition Scalar(string key,double value)=>new(){Key=key,Number=value,ValueType=NpcValueType.Number};
    static NpcScalarDefinition Symbol(string key,string value)=>new(){Key=key,Symbol=value,ValueType=NpcValueType.Symbol};
    static NpcEffectDefinition Set(string key,double value)=>new(){Key=key,Value=Lit(value)};
    static FlowGraphAsset Execution()
    {
        var flow=new FlowGraphAsset();
        flow.Nodes=new List<GraphNodeData>{new FlowEntryNodeData{Id="entry",Position=new(0,0)},new FlowActionNodeData{Id="steps",Position=new(240,0),Actions=new(){new NpcMoveToTargetAction(),new NpcWaitAction()}},new FlowReturnNodeData{Id="success",Label="Success",Position=new(500,0)},new FlowReturnNodeData{Id="failure",Label="Failure",Position=new(500,200)}};
        flow.ConnectNodes("entry",0,"steps",0);flow.ConnectNodes("steps",0,"success",0);flow.ConnectNodes("steps",1,"failure",0);flow.SaveJsonFields();return flow;
    }
    static void Build(string folder,string name,bool courier)
    {
        var graph=new NpcGraphAsset();var root=new NpcRootNodeData{Id="character",Position=new(0,250)};graph.Nodes.Add(root);
        int moduleRow=0,goalRow=0;
        T Module<T>(NpcDefinitionNodeData node,T definition) where T:NpcDefinitionResource
        {
            definition.DefinitionId=folder+"."+node.GetType().Name;node.DefinitionId=definition.DefinitionId;node.Id=folder+"_"+node.GetType().Name;node.Position=new(280,moduleRow++*145);
            graph.Definitions.Add(definition);graph.Nodes.Add(node);graph.MarkDirty();graph.ConnectNodes(root.Id,0,node.Id,0);return definition;
        }
        Module(new NpcIdentityNodeData(),new NpcIdentityDefinition{DisplayName=name,Tags=new[]{courier?"courier":"resident"},SpriteFrames=GD.Load<SpriteFrames>($"res://assets/resources/npc/{folder}/sprite_frames.tres"),Parameters=new(){Symbol("home",courier?"courier_rest":"resident_bed")}});
        Module(new NpcPersonalityNodeData(),new NpcPersonalityDefinition{Dimensions=new(){Scalar("sociability",courier?.75:.2),Scalar("caution",courier?.65:.5)},Traits=new[]{courier?"cheerful":"reserved"},Preferences=new(){Scalar("activity",courier?.8:.35)}});
        var needs=Module(new NpcNeedsNodeData(),new NpcNeedsDefinition());
        needs.Needs=needs.MergePreset(GD.Load<NpcNeedsDefinition>(NpcNeedsDefinition.CommonPresetPath));
        Module(new NpcScheduleNodeData(),new NpcScheduleDefinition{Windows=new(){new(){WindowId="day",StartHour=7,EndHour=21,Tags=new[]{"daytime"}},new(){WindowId="night",StartHour=21,EndHour=7,Tags=new[]{"night"}}}});
        Module(new NpcPerceptionNodeData(),new NpcPerceptionDefinition{ViewRange=18,KnownPointIds=new[]{"food_shop","resident_bed","courier_rest","courier_delivery_west","courier_delivery_east","shelter_west"}});
        Module(new NpcRelationsNodeData(),new NpcRelationsDefinition{Dimensions=new(){Scalar("trust",0),Scalar("affinity",0)}});
        Module(new NpcMemoryNodeData(),new NpcMemoryDefinition{Categories=new[]{"visited","conversation","danger"}});
        Module(new NpcAffectNodeData(),new NpcAffectDefinition{Dimensions=new(){Scalar("mood",0)}});
        Module(new NpcPolicyNodeData(),new NpcPolicyDefinition());
        Module(new NpcFactsNodeData(),new NpcFactSchema{Facts=new(){new(){Key="private.completed_visits",Maximum=100000,Initial=Scalar("",0)}}});
        void Goal(string id,string title,string need,string tag,double weight,string fixedTarget="")
        {
            string prefix="needs."+need+".value";
            var goal=new NpcGoalDefinition{DefinitionId=folder+".goal."+id,DisplayName=title,Activation=Expr(NpcExpressionKind.AtLeast,Fact(prefix),Lit(.65)),Utility=Expr(NpcExpressionKind.Multiply,Fact(prefix),Lit(weight)),Completion=Expr(NpcExpressionKind.AtMost,Fact(prefix),Lit(.25))};
            var goalNode=new NpcGoalNodeData{Id=id,DefinitionId=goal.DefinitionId,Position=new(600,goalRow*320)};
            var effect=Set(prefix,.15);
            var count=new NpcEffectDefinition{Key="private.completed_visits",Mode=NpcEffectMode.Add,Value=Lit(1)};
            var stock=new NpcEffectDefinition{Key="input.地点.stock",Mode=NpcEffectMode.Add,Value=Lit(-1)};
            var selector=new NpcTargetSelectorDefinition{DisplayName=title+" · 目标",AllTags=new[]{tag},Source=fixedTarget.Length>0?NpcTargetSource.IdentityParameter:NpcTargetSource.PerceivedPoints,ParameterKey=fixedTarget};
            var selectorNode=new NpcTargetSelectorNodeData{Id=id+"_targets",DefinitionId=selector.DefinitionId,Position=new(1360,goalRow*320)};
            var action=new NpcActionDefinition{DefinitionId=folder+".action."+id,DisplayName=title+" · 到达并停留",Inputs=new(){new(){Name="地点",Adapter="point",Source=NpcBindingSource.Selector,SelectorId=selector.DefinitionId}},Resources=new(){new(){Input="地点"}},
                SuccessConditions=Fact("input.地点.at"),Cost=Expr(NpcExpressionKind.Add,Lit(1),Expr(NpcExpressionKind.Multiply,Fact("input.地点.distance"),Lit(.2))),DurationSeconds=need=="fatigue"?4:2,TimeoutSeconds=60,ExecutionGraph=Execution(),Preconditions=Expr(NpcExpressionKind.All,Fact("input.地点.open"),Expr(NpcExpressionKind.AtLeast,Fact("input.地点.stock"),Lit(1))),Predictions=new(){effect,count,stock,new(){Key="self.location",Value=Fact("input.地点.id")}},Results=new(){effect,count,stock}};
            var actionNode=new NpcActionNodeData{Id=id+"_action",DefinitionId=action.DefinitionId,Position=new(930,goalRow*320)};
            var template=new NpcPlanTemplateDefinition{DefinitionId=folder+".template."+id,DisplayName=title+" · 首选计划",ActionIds=new[]{action.DefinitionId}};
            var templateNode=new NpcTemplateNodeData{Id=id+"_template",DefinitionId=template.DefinitionId,Position=new(930,goalRow*320+145)};
            graph.Definitions.Add(goal);graph.Definitions.Add(action);graph.Definitions.Add(template);graph.Definitions.Add(selector);
            graph.Nodes.Add(goalNode);graph.Nodes.Add(actionNode);graph.Nodes.Add(templateNode);graph.Nodes.Add(selectorNode);graph.MarkDirty();
            graph.ConnectNodes(root.Id,1,goalNode.Id,0);graph.ConnectNodes(goalNode.Id,0,actionNode.Id,0);graph.ConnectNodes(goalNode.Id,1,templateNode.Id,0);graph.ConnectNodes(root.Id,2,selectorNode.Id,0);goalRow++;
        }
        Goal("eat","补充食物","hunger","food",1.8);
        Goal("rest","回家休息","fatigue","rest",1.3,"home");
        Goal("activity",courier?"投递跑腿":"在镇中散心","fun",courier?"delivery":"leisure",courier?1.5:.9);
        if(!courier)ConfigureResidentStroll(graph);
        graph.MarkDirty();NpcProgram.Compile(graph);Save(graph,$"res://assets/resources/npc/{folder}/npc_graph.tres");
    }
}
