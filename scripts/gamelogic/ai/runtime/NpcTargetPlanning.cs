using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Goap;
using Godot;
namespace GameLogic.AI;

/// <summary>Value snapshot supplied by the world adapter, never a mutable scene/component reference.</summary>
public sealed record NpcTargetCandidate(string Id,IReadOnlyList<string> Tags,Vector3? Position,double? Distance,bool Usable,bool Reachable,bool Perceived,bool Known,string Adapter="point",string ObjectTypeId="",IReadOnlyList<string> Capabilities=null,IReadOnlyList<string> Operations=null,
    string LevelId="",double? TravelHours=null,string CostSource="local_navigation",bool Executable=true)
{public double RankingCost=>TravelHours??Distance??double.PositiveInfinity;}

public static class NpcTargetQuery
{
    public static bool MatchesObject(NpcTargetSelectorSpec selector,string type,IEnumerable<string> capabilities,IEnumerable<string> operations)=>
        (selector.ObjectTypeId.Length==0||selector.ObjectTypeId==type)
        &&(selector.RequiredCapabilities==null||selector.RequiredCapabilities.All(key=>capabilities?.Contains(key)==true))
        &&(selector.RequiredOperations==null||selector.RequiredOperations.All(key=>operations?.Contains(key)==true));
    public static IReadOnlyList<NpcTargetCandidate> Select(NpcProgram program,NpcTargetSelectorSpec selector,GoapWorldState state,IEnumerable<NpcTargetCandidate> points)
    {
        string parameter=selector.Source==NpcTargetSource.IdentityParameter?program.Parameter(selector.ParameterKey).Symbol:"";
        var candidates=points.Where(p=>p.Adapter==selector.Adapter).Where(p=>selector.Source switch
        {
            NpcTargetSource.PerceivedPoints=>p.Perceived,
            NpcTargetSource.WorldPoints=>true,
            NpcTargetSource.IdentityParameter=>parameter.Length>0&&p.Id==parameter,
            NpcTargetSource.FixedObject=>p.Id==selector.FixedObjectId,
            NpcTargetSource.CurrentLocation=>p.Id==program.Read(state,"self.location").Symbol,
            NpcTargetSource.KnownPoints=>p.Known,
            NpcTargetSource.WorldDirectory or NpcTargetSource.CharacterBinding=>true,
            _=>false
        }).Where(p=>Matches(program,selector,state,p)).Select(p=>
        {
            var context=program.TargetContext(state,p.Id,p.Distance,selector.Adapter);
            var score=selector.Selection is NpcTargetSelection.LowestScore or NpcTargetSelection.HighestScore
                ?selector.Score?.Evaluate(k=>program.Read(context,k),program.Parameter)??default:CharacterValue.Numeric(p.RankingCost);
            return(Point:p,Score:score);
        }).Where(p=>p.Score.Type==CharacterValueType.Number);
        candidates=selector.Selection==NpcTargetSelection.HighestScore
            ?candidates.OrderByDescending(p=>p.Score.Number).ThenBy(p=>p.Point.Id,StringComparer.Ordinal)
            :selector.Selection==NpcTargetSelection.LowestScore
                ?candidates.OrderBy(p=>p.Score.Number).ThenBy(p=>p.Point.Id,StringComparer.Ordinal)
                :candidates.OrderBy(p=>p.Point.RankingCost).ThenBy(p=>p.Point.Id,StringComparer.Ordinal);
        int count=selector.Selection==NpcTargetSelection.Candidates?Math.Min(program.Policy.Targets,selector.Limit>0?selector.Limit:program.Policy.Targets):1;
        return candidates.Take(count).Select(p=>p.Point).ToArray();
    }

    public static bool Matches(NpcProgram program,NpcTargetSelectorSpec selector,GoapWorldState state,NpcTargetCandidate point)
    {
        if(point.Adapter!=selector.Adapter||!MatchesObject(selector,point.ObjectTypeId,point.Capabilities,point.Operations)||selector.RequireUsable&&!point.Usable || selector.RequireReachable&&!point.Reachable ||
            selector.MaximumDistance>0&&(!point.Distance.HasValue||point.Distance>selector.MaximumDistance) ||
            !selector.AllTags.All(point.Tags.Contains)||selector.AnyTags.Count>0&&!selector.AnyTags.Any(point.Tags.Contains)||selector.ExcludedTags.Any(point.Tags.Contains))return false;
        var context=program.TargetContext(state,point.Id,point.Distance,selector.Adapter);
        return selector.Filter==null||selector.Filter.Evaluate(k=>program.Read(context,k),program.Parameter).IsTrue;
    }

    public static IReadOnlyList<NpcBoundAction> Ground(NpcProgram program,NpcGoalSpec goal,GoapWorldState initial,IReadOnlyList<NpcTargetCandidate> points,IReadOnlyDictionary<string,IReadOnlyList<NpcTargetCandidate>> queries=null)
    {
        var candidates=new Dictionary<string,IReadOnlyList<NpcTargetCandidate>>(StringComparer.Ordinal);
        var positions=points.ToDictionary(p=>(p.Adapter,p.Id),p=>p.Position);
        var result=new List<NpcBoundAction>();
        foreach(var plan in goal.Plans.Where(p=>p.Eligibility==null||p.Eligibility.Evaluate(k=>program.Read(initial,k),program.Parameter).IsTrue))
        foreach(var action in goal.Actions.Where(a=>plan.ActionIds.Contains(a.Id)))
        {
            var slots=action.Inputs.Where(i=>i.IsTarget).ToArray();
            var values=new Dictionary<string,CharacterValue>(StringComparer.Ordinal);
            var selections=new Dictionary<string,CharacterValue>(StringComparer.Ordinal);
            void Expand(int index)
            {
                if(index<slots.Length)
                {
                    var input=slots[index];
                    string queryKey=NpcProgram.QueryKey(action,input);
                    if(!candidates.TryGetValue(queryKey,out var choices))
                    {
                        var source=queries?.GetValueOrDefault(queryKey)??queries?.GetValueOrDefault(input.SelectorId)??points;
                        if(action.Graph?.Interaction is {} interaction&&interaction.Input==input.Name)source=source.Where(p=>p.Operations?.Contains(interaction.Operation)==true).ToArray();
                        candidates[queryKey]=choices=Select(program,program.TargetSelectors[input.SelectorId],initial,source);
                    }
                    foreach(var point in choices)
                    {
                        var value=CharacterValue.Text(point.Id);
                        string shared=input.SelectionNodeId.Length>0?input.SelectionNodeId:null;
                        if(shared!=null&&selections.TryGetValue(shared,out var previous)&&previous!=value)continue;
                        bool added=shared!=null&&selections.TryAdd(shared,value);
                        values[input.Name]=value;Expand(index+1);values.Remove(input.Name);
                        if(added)selections.Remove(shared);
                    }
                    return;
                }
                if(result.Count>=program.Policy.Expanded)throw new InvalidOperationException("GroundingLimitReached: 请缩小选择器候选或提高搜索预算。");
                var bound=new NpcBoundAction(action,plan,new System.Collections.ObjectModel.ReadOnlyDictionary<string,CharacterValue>(new Dictionary<string,CharacterValue>(values)),null);
                double? Distance(GoapWorldState state,string name,CharacterValue value)
                {
                    string adapter=action.Inputs.First(i=>i.Name==name).Adapter;
                    string from=program.Read(state,"self.location").Symbol;if(from==value.Symbol)return 0;
                    var target=points.FirstOrDefault(p=>p.Id==value.Symbol&&p.Adapter==adapter);if(target==null)return null;
                    var origin=points.FirstOrDefault(p=>p.Id==from&&p.Adapter==adapter);
                    return origin!=null&&origin.LevelId==target.LevelId&&positions.TryGetValue((adapter,from),out var position)&&position.HasValue&&target.Position.HasValue?position.Value.DistanceTo(target.Position.Value):target.Distance;
                }
                double Cost(GoapWorldState state)
                {
                    var context=program.Contextual(state,bound,(name,value)=>Distance(state,name,value));if(context==null)return double.PositiveInfinity;
                    var cost=action.Cost?.Evaluate(k=>program.Read(context,k),program.Parameter)??CharacterValue.Numeric(1);
                    return cost.Type==CharacterValueType.Number&&cost.Number>=0?cost.Number:double.PositiveInfinity;
                }
                string key=plan.Id+"::"+action.Id+"@"+string.Join("+",slots.Select(i=>Uri.EscapeDataString(values[i.Name].ToString())));
                var search=new GoapAction(key,"",0,s=>program.Predict(s,bound,(name,value)=>Distance(s,name,value)),Cost);
                result.Add(bound with{Search=search});
            }
            Expand(0);
        }
        return result;
    }
}
