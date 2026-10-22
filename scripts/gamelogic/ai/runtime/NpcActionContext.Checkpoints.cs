using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLogic.AI;

public sealed partial record NpcActionContext
{
    readonly NpcCheckpointLayout _checkpointLayout=NpcCheckpointLayout.Create(Action.Execution);
    readonly Dictionary<string,NpcTaskProgress> _progress=new(StringComparer.Ordinal);
    internal NpcTaskProgress Progress(GraphSharedAction action)
    {
        if(_checkpointLayout?.Steps.TryGetValue(action,out var key)!=true)return new();
        if(!_progress.TryGetValue(key,out var progress))_progress[key]=progress=new();
        return progress;
    }
    internal NpcExecutionCheckpoint CaptureExecution()
    {
        if(_checkpointLayout==null||StartState==null)return null;
        return new()
        {
            Start=StartState.Facts.Select(f=>new NpcSavedFact(f.Key,f.Value.Kind,f.Value.Integer,f.Value.Symbol)).ToList(),
            Progress=_progress.ToDictionary(p=>p.Key,p=>new NpcTaskProgress{Complete=p.Value.Complete,Elapsed=p.Value.Elapsed,Duration=p.Value.Duration},StringComparer.Ordinal),
            Numbers=_numbers.ToDictionary(p=>_checkpointLayout.Numbers[p.Key],p=>p.Value,StringComparer.Ordinal),
            Locals=new(_localNumbers,StringComparer.Ordinal)
        };
    }
    internal void RestoreExecution(NpcExecutionCheckpoint checkpoint)
    {
        NpcProgram.Require(_checkpointLayout!=null&&checkpoint.Progress!=null&&checkpoint.Numbers!=null&&checkpoint.Locals!=null,"行动不支持语义恢复。");
        var steps=_checkpointLayout.Steps.Values.ToHashSet(StringComparer.Ordinal);
        var numbers=_checkpointLayout.Numbers.ToDictionary(p=>p.Value,p=>p.Key,StringComparer.Ordinal);
        NpcProgram.Require(checkpoint.Progress.All(p=>steps.Contains(p.Key)&&p.Value?.Valid==true)
            &&checkpoint.Numbers.All(p=>numbers.ContainsKey(p.Key)&&double.IsFinite(p.Value))
            &&checkpoint.Locals.All(p=>double.IsFinite(p.Value)&&Action.Execution.BlackboardEntries.Any(e=>e.Key==p.Key&&e.Value is GraphFloatBlackboardValue&&e.Binding==null)),"行动进度或图内数值无效。");
        foreach(var pair in checkpoint.Progress)_progress.Add(pair.Key,pair.Value);
        foreach(var pair in checkpoint.Numbers)_numbers.Add(numbers[pair.Key],pair.Value);
        // Replaying the pure calculation nodes rebuilds the Float blackboard as well.
        foreach(var pair in checkpoint.Locals)_localNumbers.Add(pair.Key,pair.Value);
    }
}
