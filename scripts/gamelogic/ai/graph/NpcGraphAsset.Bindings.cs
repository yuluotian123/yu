using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace GameLogic.AI;

public partial class NpcGraphAsset
{
    public bool MigrateActionBindings()
    {
        bool changed=MigrateTargetSelectors();
        foreach(var action in Definitions.OfType<NpcActionDefinition>())
        {
            if(action.TargetSelectorId.Length==0){if(action.BindingVersion<2){action.BindingVersion=2;changed=true;}continue;}
            string selectorId=action.TargetSelectorId;
            var input=new NpcBindingDefinition{Name="地点",Adapter="point",Source=NpcBindingSource.Selector,SelectorId=selectorId};
            if(action.ShareTargetWithinPlan)
            {
                string variableName=(Resolve(selectorId)?.DisplayName??"地点").Replace(".","·");
                var plans=Definitions.OfType<NpcPlanTemplateDefinition>().Where(p=>p.ActionIds.Contains(action.DefinitionId)).ToArray();
                string baseName=variableName;int suffix=2;
                while(plans.Any(p=>p.Variables.Any(v=>v.Name==variableName&&(v.Source!=NpcBindingSource.Selector||v.SelectorId!=selectorId))))variableName=baseName+suffix++;
                foreach(var plan in plans)if(!plan.Variables.Any(v=>v.Name==variableName))
                    plan.Variables.Add(new(){Name=variableName,Adapter="point",Source=NpcBindingSource.Selector,SelectorId=selectorId});
                if(plans.Length>0){input.Source=NpcBindingSource.PlanVariable;input.Variable=variableName;input.SelectorId="";}
            }
            action.Inputs.Add(input);
            var visited=new HashSet<NpcExpressionDefinition>();
            void Rewrite(NpcExpressionDefinition expression)
            {
                if(expression==null||!visited.Add(expression))return;
                if(expression.Key.StartsWith("target.",StringComparison.Ordinal))expression.Key="input.地点."+expression.Key[7..];
                foreach(var child in expression.Inputs)Rewrite(child);
            }
            Rewrite(action.Preconditions);Rewrite(action.Cost);Rewrite(action.SuccessConditions);
            foreach(var effect in action.Predictions.Concat(action.Results))
            {
                if(effect.Key.StartsWith("target.",StringComparison.Ordinal))effect.Key="input.地点."+effect.Key[7..];Rewrite(effect.Value);
            }
            static NpcExpressionDefinition Fact(string key)=>new(){Kind=NpcExpressionKind.Fact,Key=key};
            static NpcExpressionDefinition All(NpcExpressionDefinition a,NpcExpressionDefinition b)=>a==null?b:new(){Kind=NpcExpressionKind.All,Inputs=new(){a,b}};
            if(action.RequireAtTarget)action.Preconditions=All(action.Preconditions,Fact("input.地点.at"));
            if(action.ArrivesAtTarget)
            {
                action.Predictions.Add(new(){Key="self.location",Value=Fact("input.地点.id")});
                action.SuccessConditions=All(action.SuccessConditions,Fact("input.地点.at"));
            }
            if(action.ReserveTarget)action.Resources.Add(new(){Input="地点",Capability="capacity"});
            if(action.DistanceCost>0)action.Cost=new(){Kind=NpcExpressionKind.Add,Inputs=new(){action.Cost??new(){Number=1},
                new(){Kind=NpcExpressionKind.Multiply,Inputs=new(){Fact("input.地点.distance"),new(){Number=action.DistanceCost}}}}};
            action.TargetSelectorId="";action.ShareTargetWithinPlan=false;action.RequireAtTarget=false;action.ArrivesAtTarget=false;action.ReserveTarget=false;action.DistanceCost=0;action.BindingVersion=2;changed=true;
        }
        foreach(var action in Definitions.OfType<NpcActionDefinition>().Where(a=>a.Inputs.Count==0&&a.ExecutionGraph!=null))
        {
            bool executionChanged=false;
            foreach(var ability in action.ExecutionGraph.Nodes.OfType<FlowActionNodeData>().SelectMany(n=>n.Actions))
            {
                NpcInputReference optional=ability switch{NpcRoamAction roam=>roam.Input,NpcRememberAction remember=>remember.SubjectInput,_=>null};
                if(optional?.Name!="地点")continue;
                optional.Name="";executionChanged=true;
            }
            if(executionChanged){action.ExecutionGraph.SaveJsonFields();changed=true;}
        }
        if(changed){SaveJsonFields();MarkDirty();}
        return changed;
    }
}
