using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace GameLogic.AI;

/// <summary>AI-only memory, need hysteresis and schedule projection.</summary>
public sealed class NpcDecisionState
{
    readonly NpcProgram _program;
    readonly CharacterState _character;
    readonly Dictionary<string,bool> _urgent=new(StringComparer.Ordinal);
    public NpcMemoryModel Memory {get;}
    public NpcDecisionState(NpcProgram program,CharacterState character){_program=program;_character=character;Memory=new(program);Refresh();}
    public void Refresh()
    {
        foreach(var n in _program.Needs)
        {
            double v=_character.Needs.Values[n.Id];bool previous=_urgent.GetValueOrDefault(n.Id);
            _urgent[n.Id]=n.Higher?(previous?v>n.Exit:v>=n.Enter):(previous?v<n.Exit:v<=n.Enter);
        }
        if(double.IsFinite(_character.LastHours))Memory.Expire(_character.LastHours);
    }
    public CharacterValue Read(string key)
    {
        foreach(var n in _program.Needs)
        {
            if(key=="needs."+n.Id+".urgent")return CharacterValue.Bool(_urgent.GetValueOrDefault(n.Id));
            if(key=="needs."+n.Id+".urgency")return CharacterValue.Numeric(Math.Pow(Math.Clamp(n.Higher?(_character.Needs.Values[n.Id]-n.Min)/(n.Max-n.Min):(n.Max-_character.Needs.Values[n.Id])/(n.Max-n.Min),0,1),n.Exponent));
        }
        if(key.StartsWith("schedule."))
        {
            string tag=key[9..];double hour=double.IsFinite(_character.LastHours)?_character.LastHours%24:12;
            var active=_program.Windows.Where(w=>w.Start==w.End||(w.Start<w.End?hour>=w.Start&&hour<w.End:hour>=w.Start||hour<w.End)).ToArray();
            int priority=active.Select(w=>w.Priority).DefaultIfEmpty(int.MinValue).Max();
            return CharacterValue.Bool(active.Any(w=>w.Priority==priority&&w.Tags.Contains(tag)));
        }
        return _character.Read(key);
    }
    public JsonObject Capture()=>new(){["urgent"]=JsonSerializer.SerializeToNode(_urgent),["memory_entries"]=JsonSerializer.SerializeToNode(Memory.Entries)};
    public void Restore(JsonObject data)
    {
        // The previous dialogue-memory payload has the same entry shape.
        var entries=(data?["memory_entries"]??data?["memory_facts"])?.Deserialize<List<NpcMemoryEntry>>()??new();
        var urgent=data?["urgent"]?.Deserialize<Dictionary<string,bool>>()??new();
        Memory.Restore(entries);_urgent.Clear();foreach(var n in _program.Needs)_urgent[n.Id]=urgent.GetValueOrDefault(n.Id);Refresh();
    }
}
