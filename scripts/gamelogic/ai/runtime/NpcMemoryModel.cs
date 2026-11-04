using System;
using System.Linq;
using System.Collections.Generic;
namespace GameLogic.AI;

public sealed record NpcMemoryEntry(string Key,string Subject,bool Value,double Hours);
public sealed class NpcMemoryModel
{
    readonly Dictionary<(string Key,string Subject),NpcMemoryEntry> _entries=new();
    public IReadOnlyCollection<NpcMemoryEntry> Entries=>_entries.Values;
    public IReadOnlyDictionary<string,NpcMemoryEntrySpec> Definitions=>_program.MemoryEntries;
    readonly NpcProgram _program;
    public NpcMemoryModel(NpcProgram program)=>_program=program;
    public bool TryRead(string key,string subject,out bool value)
    {
        value=false;if(key==null||subject==null||!_program.MemoryEntries.TryGetValue(key,out var definition))return false;
        value=_entries.TryGetValue((key,subject),out var state)?state.Value:definition.InitialValue;return true;
    }
    public bool HasEntry(string key,string subject)=>_entries.ContainsKey((key,subject));
    public bool Write(string key,string subject,bool value,double hours)
    {
        if(key==null||subject==null||!_program.MemoryEntries.ContainsKey(key)||!double.IsFinite(hours))return false;
        _entries[(key,subject)]=new(key,subject,value,hours);return true;
    }
    public void Restore(IEnumerable<NpcMemoryEntry> entries)
    {
        var restored=entries.ToArray();
        NpcProgram.Require(restored.All(e=>e!=null&&e.Key!=null&&e.Subject!=null&&_program.MemoryEntries.ContainsKey(e.Key)&&double.IsFinite(e.Hours))
            &&restored.Select(e=>(e.Key,e.Subject)).Distinct().Count()==restored.Length,"NPC 记忆存档无效。");
        _entries.Clear();foreach(var entry in restored)_entries.Add((entry.Key,entry.Subject),entry);
    }
    public void Expire(double hours)
    {
        foreach(var pair in _entries.Where(p=>_program.MemoryEntries[p.Key.Key].LifetimeHours is >0 and var lifetime&&hours-p.Value.Hours>lifetime).ToArray())
            _entries.Remove(pair.Key);
    }
}
