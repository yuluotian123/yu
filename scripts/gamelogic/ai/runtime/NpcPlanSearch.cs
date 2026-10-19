using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Goap;

namespace GameLogic.AI;

/// <summary>Searches each eligible plan's action set independently, within the normal GOAP budgets.</summary>
public sealed class NpcPlanSearch
{
    readonly NpcProgram _program;
    readonly GoapWorldState _initial;
    readonly GoapGoal _goal;
    readonly KeyValuePair<string,GoapAction>[] _actions;
    readonly Queue<NpcPlanSpec> _plans;
    GoapPlanner _search;
    NpcPlanSpec _candidate;
    bool _limited;
    int _expanded;
    public long SourceVersion { get; }
    public GoapSearchStatus Status { get; private set; } = GoapSearchStatus.Pending;
    public GoapPlan Result { get; private set; }
    public NpcPlanSpec ChosenPlan { get; private set; }
    public int Expanded => _expanded + (_search?.Expanded ?? 0);
    public string FailureReason { get; private set; } = "";

    public NpcPlanSearch(NpcProgram program,NpcGoalSpec goal,GoapWorldState initial,
        IEnumerable<KeyValuePair<string,GoapAction>> actions,long sourceVersion=0)
    {
        _program=program;_initial=initial;_actions=actions.ToArray();SourceVersion=sourceVersion;
        _goal=new(goal.Id,s=>goal.Completion.Evaluate(k=>program.Read(s,k),program.Parameter).IsTrue);
        _plans=new(goal.Plans.Where(p=>p.Eligibility==null||p.Eligibility.Evaluate(k=>program.Read(initial,k),program.Parameter).IsTrue)
            .OrderByDescending(p=>p.Priority).ThenBy(p=>p.Id,StringComparer.Ordinal));
        if(_goal.IsMet(initial))
        {
            Result=new GoapPlanner(initial,_goal,Array.Empty<GoapAction>(),sourceVersion).Plan;
            Status=GoapSearchStatus.AlreadySatisfied;return;
        }
        if(_plans.Count==0){Status=GoapSearchStatus.NoPlan;FailureReason="NoEligiblePlan";return;}
        Next();
    }

    void Next()
    {
        if(_plans.Count==0)
        {
            Status=_limited?GoapSearchStatus.LimitReached:GoapSearchStatus.NoPlan;
            FailureReason=Status.ToString();_search=null;return;
        }
        _candidate=_plans.Dequeue();
        var allowed=_candidate.ActionIds.ToHashSet(StringComparer.Ordinal);
        _search=new(_initial,_goal,_actions.Where(a=>(allowed.Contains(a.Key)||allowed.Any(id=>a.Key==_candidate.Id+"::"+id))).Select(a=>a.Value),SourceVersion,
            new(_program.Policy.Expanded,_program.Policy.Depth));
    }

    public GoapSearchStatus Advance(int nodeBudget,double millisecondBudget=double.PositiveInfinity)
    {
        if(Status!=GoapSearchStatus.Pending)return Status;
        var status=_search.Advance(nodeBudget,millisecondBudget);
        if(status==GoapSearchStatus.Pending)return Status;
        if(status is GoapSearchStatus.Found or GoapSearchStatus.AlreadySatisfied)
        {
            Result=_search.Plan;ChosenPlan=_candidate;Status=status;return Status;
        }
        _expanded+=_search.Expanded;_limited|=status==GoapSearchStatus.LimitReached;
        Next(); // The next plan advances on the next slice, preserving per-frame budgets.
        return Status;
    }

    public void Invalidate()
    {
        _search?.Invalidate();_plans.Clear();Result=null;ChosenPlan=null;Status=GoapSearchStatus.Invalidated;
    }
}
