using System;
using System.Linq;
using System.Collections.Generic;
namespace GameLogic.AI;

public sealed record NpcMemoryEntry(string Category,string Subject,double Hours,string Detail);
public sealed class NpcMemoryModel
{
    public List<NpcMemoryEntry> Entries { get; } = new();
    readonly NpcProgram _program;
    public NpcMemoryModel(NpcProgram program)=>_program=program;
    public bool Remember(NpcMemoryEntry entry)
    {if(!_program.MemoryCategories.Contains(entry.Category))return false;Entries.Add(entry);while(Entries.Count>_program.MemoryCapacity)Entries.RemoveAt(0);return true;}
    public void Expire(double hours)=>Entries.RemoveAll(e=>hours-e.Hours>_program.MemoryLifetime);
}
