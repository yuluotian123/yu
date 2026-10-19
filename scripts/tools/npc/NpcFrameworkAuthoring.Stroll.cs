using GameLogic;
using System;
using System.Linq;
using System.Collections.Generic;
using GameLogic.AI;
using Godot;

public partial class NpcFrameworkAuthoring
{
    public const string ResidentGraphPath="res://assets/resources/npc/gray_green_resident/npc_graph.tres";
    public static void ConfigureResidentStroll(NpcGraphAsset graph)
    {
        graph.MigrateActionBindings();
        var goalNode=graph.Nodes.OfType<NpcGoalNodeData>().Single(n=>n.DefinitionId=="gray_green_resident.goal.activity");
        var goal=(NpcGoalDefinition)graph.Resolve(goalNode.DefinitionId);goal.DisplayName="散步放松";
        var stroll=(NpcActionDefinition)graph.Resolve("gray_green_resident.action.activity");
        var oldInput=stroll.Inputs[0];
        string selectorId=oldInput.Source==NpcBindingSource.Selector?oldInput.SelectorId:graph.Definitions.OfType<NpcPlanTemplateDefinition>().SelectMany(p=>p.Variables).First(v=>v.Name==oldInput.Variable).SelectorId;
        var selector=(NpcTargetSelectorDefinition)graph.Resolve(selectorId);
        selector.DisplayName="散步地点 · 镇上或湖边";selector.Source=NpcTargetSource.KnownPoints;
        selector.AllTags=new[]{"stroll"};selector.AnyTags=new[]{"town","lakeside"};selector.ExcludedTags=Array.Empty<string>();
        selector.Filter=null;selector.Selection=NpcTargetSelection.Candidates;selector.Score=null;selector.MaximumDistance=0;selector.CandidateLimit=2;selector.RequireUsable=true;selector.RequireReachable=true;
        var perception=graph.Definitions.OfType<NpcPerceptionDefinition>().Single();
        perception.KnownPointIds=perception.KnownPointIds.Concat(new[]{"town_stroll","lakeside_stroll"}).Distinct(StringComparer.Ordinal).ToArray();
        FlowGraphAsset Flow(GraphActionBase action)
        {
            var flow=new FlowGraphAsset{Nodes=new List<GraphNodeData>{new FlowEntryNodeData{Id="entry",Position=new(0,0)},new FlowActionNodeData{Id="activity",Position=new(260,0),Actions=new(){action}},new FlowReturnNodeData{Id="success",Label="Success",Position=new(540,0)},new FlowReturnNodeData{Id="failure",Label="Failure",Position=new(540,180)}}};
            flow.ConnectNodes("entry",0,"activity",0);flow.ConnectNodes("activity",0,"success",0);flow.ConnectNodes("activity",1,"failure",0);flow.SaveJsonFields();return flow;
        }
        T Definition<T>(string id,Func<T> create)where T:NpcDefinitionResource
        {
            if(graph.Resolve(id) is T found)return found;var definition=create();definition.DefinitionId=id;graph.Definitions.Add(definition);return definition;
        }
        void Node(NpcDefinitionResource definition,NpcDefinitionNodeData node,int port,Vector2 position)
        {
            var existing=graph.Nodes.OfType<NpcDefinitionNodeData>().FirstOrDefault(n=>n.DefinitionId==definition.DefinitionId);
            if(existing==null){node.DefinitionId=definition.DefinitionId;graph.Nodes.Add(node);existing=node;graph.MarkDirty();}
            existing.Position=position;
            if(!graph.HasConnection(goalNode.Id,port,existing.Id,0))graph.ConnectNodes(goalNode.Id,port,existing.Id,0);
        }
        var move=Definition("gray_green_resident.action.go_stroll",()=>new NpcActionDefinition());
        move.DisplayName="前往散步地点";
        NpcBindingDefinition Input()=>new NpcActionInputDefinition{Name="地点",Adapter="point",Source=NpcBindingSource.PlanVariable,Variable="散步地点",ResourceRequirements=new[]{"capacity"}};
        move.Inputs=new(){Input()};move.Resources.Clear();move.SuccessConditions=Fact("input.地点.at");
        move.DurationSeconds=0;move.TimeoutSeconds=60;move.Cost=Expr(CharacterExpressionKind.Add,Lit(1),Expr(CharacterExpressionKind.Multiply,Fact("input.地点.distance"),Lit(.2)));move.Preconditions=Fact("input.地点.open");move.ExecutionGraph=Flow(new NpcMoveToTargetAction());move.Predictions=new(){new(){Key="self.location",Value=Fact("input.地点.id")}};move.Results.Clear();
        Node(move,new NpcActionNodeData{Id="activity_move"},0,goalNode.Position+new Vector2(780,0));
        stroll.DisplayName="散步";stroll.Inputs=new(){Input()};stroll.Resources.Clear();stroll.SuccessConditions=null;
        stroll.DurationSeconds=8;stroll.TimeoutSeconds=20;stroll.Cost=Lit(1);stroll.Preconditions=Expr(CharacterExpressionKind.All,Fact("input.地点.open"),Fact("input.地点.at"));
        stroll.ExecutionGraph=Flow(new NpcRoamAction{Radius=.6});
        stroll.Predictions=new(){Set("needs.fun.value",.15),new(){Key="private.completed_visits",Mode=NpcEffectMode.Add,Value=Lit(1)}};
        stroll.Results=new(stroll.Predictions);
        graph.Nodes.OfType<NpcActionNodeData>().Single(n=>n.DefinitionId==stroll.DefinitionId).Position=goalNode.Position+new Vector2(780,220);
        graph.Nodes.OfType<NpcTargetSelectorNodeData>().Single(n=>n.DefinitionId==selector.DefinitionId).Position=goalNode.Position+new Vector2(1170,0);
        var inPlace=Definition("gray_green_resident.action.stroll_in_place",()=>new NpcActionDefinition());
        inPlace.DisplayName="原地散步";inPlace.Inputs.Clear();inPlace.Resources.Clear();inPlace.SuccessConditions=null;
        inPlace.DurationSeconds=8;inPlace.TimeoutSeconds=20;inPlace.Cost=Lit(1);inPlace.Preconditions=null;
        inPlace.ExecutionGraph=Flow(new NpcRoamAction{Radius=0,Input=new(){Name=""}});inPlace.Predictions=new(){Set("needs.fun.value",.15)};inPlace.Results=new(inPlace.Predictions);
        Node(inPlace,new NpcActionNodeData{Id="activity_in_place"},0,goalNode.Position+new Vector2(780,440));
        var dry=(NpcPlanTemplateDefinition)graph.Resolve("gray_green_resident.template.activity");
        dry.Variables=new(){new(){Name="散步地点",Adapter="point",Source=NpcBindingSource.Selector,SelectorId=selector.DefinitionId}};
        dry.DisplayName="无雨 · 镇上或湖边散步";dry.Priority=0;dry.Eligibility=Expr(CharacterExpressionKind.AtMost,Fact("world.rain"),Lit(.05));dry.ActionIds=new[]{move.DefinitionId,stroll.DefinitionId};
        graph.Nodes.OfType<NpcTemplateNodeData>().Single(n=>n.DefinitionId==dry.DefinitionId).Position=goalNode.Position+new Vector2(390,0);
        var rain=Definition("gray_green_resident.plan.stroll_in_place",()=>new NpcPlanTemplateDefinition());
        rain.DisplayName="下雨 · 原地散步";rain.Priority=10;rain.Eligibility=Expr(CharacterExpressionKind.Greater,Fact("world.rain"),Lit(.05));rain.ActionIds=new[]{inPlace.DefinitionId};
        Node(rain,new NpcTemplateNodeData{Id="activity_rain_plan"},1,goalNode.Position+new Vector2(390,260));
        graph.SaveJsonFields();NpcProgram.Compile(graph);
    }
}
