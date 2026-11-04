using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.AI;

namespace GameLogic;

public sealed record DialogueTranscript(string Speaker, string Text, bool IsChoice = false);
public sealed record DialogueAvailableOption(int Port, string Text);

/// <summary>One conversation's cursor. Serialized graph data never holds mutable playback state.</summary>
public sealed class DialogueSession
{
    readonly Dictionary<string, DialogueNodeData> _nodes;
    readonly Dictionary<(string, int), string> _edges;
    readonly NpcMemoryModel _memory;
    readonly string _subject;
    readonly Func<double> _hours;
    readonly List<DialogueTranscript> _history = new();
    readonly List<DialogueAvailableOption> _options = new();
    public DialogueNodeData Current { get; private set; }
    public IReadOnlyList<DialogueTranscript> History => _history;
    public IReadOnlyList<DialogueAvailableOption> Options => _options;
    public bool IsComplete { get; private set; }
    public string Result { get; private set; } = "";
    public string NpcName { get; }
    public string PlayerName { get; }
    public string Title { get; }
    public float CharactersPerSecond { get; }
    public bool AllowCancel { get; }
    public event Action Changed;
    public event Action Finished;

    public DialogueSession(DialogueGraphAsset graph, NpcMemoryModel memory, string subject, string npcName = "对方", string playerName = "你", Func<double> hours = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var validation = GraphValidationService.Validate(graph);
        // Also catches empty graphs, which have no start node to run its validation hook.
        ValidateStructure(graph, validation);
        if (!validation.IsValid) throw new ArgumentException(validation.ToDisplayText());
        var snapshot = GraphJsonHelper.Deserialize<GraphDocument>(GraphJsonHelper.Serialize(graph.Document));
        _nodes = snapshot.Nodes.Cast<DialogueNodeData>().ToDictionary(n => n.Id, StringComparer.Ordinal);
        _edges = snapshot.Connections.Where(e => e.IsAvailable).ToDictionary(e => (e.FromNode, e.FromPort), e => e.ToNode);
        _memory = memory; _subject = subject; _hours = hours ?? (() => 0);
        var keys = _nodes.Values.SelectMany(n => n switch
        {
            DialogueBranchNodeData branch => new[] { branch.MemoryKey }, DialogueSetFlagNodeData set => new[] { set.MemoryKey },
            DialogueChoiceNodeData choice => choice.Options.Select(o => o.RequiredMemory).Where(k => k.Length > 0), _ => Array.Empty<string>()
        }).Distinct().ToArray();
        if (keys.Length > 0 && (memory == null || string.IsNullOrWhiteSpace(subject))) throw new ArgumentException("对话需要 NPC 记忆和玩家的稳定标识。");
        foreach (string key in keys) if (!memory.Definitions.ContainsKey(key)) throw new ArgumentException("NPC 记忆规则未配置条目：" + key);
        NpcName = npcName; PlayerName = playerName; Title = graph.Title;
        CharactersPerSecond = graph.CharactersPerSecond; AllowCancel = graph.AllowCancel;
    }
    public string Resolve(string text) => (text ?? "").Replace("{npc}", NpcName).Replace("{player}", PlayerName);
    public void Start()
    {
        if (Current != null || IsComplete) return;
        Visit(_nodes.Values.Single(n => n is DialogueStartNodeData).Id);
    }
    public bool Advance()
    {
        if (IsComplete || Current is not DialogueLineNodeData) return false;
        Visit(Next(Current.Id, 0)); return true;
    }
    public bool Choose(int port)
    {
        if (IsComplete || Current is not DialogueChoiceNodeData || !_options.Any(o => o.Port == port)) return false;
        _history.Add(new(PlayerName, _options.Single(o => o.Port == port).Text, true));
        Visit(Next(Current.Id, port)); return true;
    }
    public void Cancel() { if (!IsComplete) Complete("Cancelled"); }
    bool Recall(string key)
    {
        _memory.Expire(_hours());
        return _memory.TryRead(key, _subject, out bool value) && value;
    }
    string Next(string node, int port) => _edges.GetValueOrDefault((node, port));
    void Visit(string id)
    {
        _options.Clear();
        for (int step = 0; step < 256; step++)
        {
            if (id == null || !_nodes.TryGetValue(id, out var node)) { Complete("InvalidGraph"); return; }
            Current = node;
            switch (node)
            {
                case DialogueLineNodeData line:
                    _history.Add(new(Resolve(line.Speaker), Resolve(line.Text))); Changed?.Invoke(); return;
                case DialogueChoiceNodeData choice:
                    for (int i = 0; i < choice.Options.Count; i++)
                    {
                        var option = choice.Options[i];
                        if (option.RequiredMemory.Length == 0 || Recall(option.RequiredMemory) == option.RequiredValue)
                            _options.Add(new(i, Resolve(option.Text)));
                    }
                    if (_options.Count == 0) { Complete("NoAvailableChoices"); return; }
                    Changed?.Invoke(); return;
                case DialogueBranchNodeData branch: id = Next(id, Recall(branch.MemoryKey) ? 0 : 1); break;
                case DialogueSetFlagNodeData set:
                    if (!_memory.Write(set.MemoryKey, _subject, set.Value, _hours())) { Complete("MemoryWriteRejected"); return; }
                    id = Next(id, 0); break;
                case DialogueEndNodeData end: Complete(end.Result); return;
                default: id = Next(id, 0); break;
            }
        }
        Complete("ExecutionLimit");
    }
    void Complete(string result)
    {
        IsComplete = true; Result = result; _options.Clear(); Finished?.Invoke();
    }

    public static void ValidateStructure(GraphAsset graph, GraphValidationResult result)
    {
        if (graph is not DialogueGraphAsset dialogue) { result.AddError("需要对话图资源。"); return; }
        if (!float.IsFinite(dialogue.CharactersPerSecond) || dialogue.CharactersPerSecond <= 0)
            result.AddError("对话文字速度必须大于零。");
        var starts = graph.Nodes.OfType<DialogueStartNodeData>().ToArray();
        if (starts.Length != 1) result.AddError("对话图必须有且仅有一个开始节点。");
        var edges = graph.Connections.Where(e => e.IsAvailable).ToArray();
        foreach (var node in graph.Nodes)
        {
            if (node is not DialogueNodeData) { result.AddError("对话图包含非对话节点。", node?.Id); continue; }
            for (int port = 0; port < node.GetOutputCount(); port++)
                if (edges.Count(e => e.FromNode == node.Id && e.FromPort == port) != 1)
                    result.AddError($"{node.GetDisplayName()} 的输出 {port + 1} 需要且只能连接一个节点。", node.Id);
            if (node is DialogueLineNodeData line)
            {
                if (string.IsNullOrWhiteSpace(line.Text)) result.AddError("台词/旁白正文不能为空。", node.Id);
                if (line.Speaker == null) result.AddError("说话人请使用空字符串，不可为 null。", node.Id);
            }
            if (node is DialogueChoiceNodeData choice && (choice.Options == null || choice.Options.Count is < 1 or > 8 ||
                choice.Options.Any(o => o == null || string.IsNullOrWhiteSpace(o.Text) || o.RequiredMemory == null)))
                result.AddError("选项节点需要 1–8 个非空选项。", node.Id);
            if (node is DialogueBranchNodeData { MemoryKey: var branchKey } && string.IsNullOrWhiteSpace(branchKey) ||
                node is DialogueSetFlagNodeData { MemoryKey: var setKey } && string.IsNullOrWhiteSpace(setKey))
                result.AddError("请选择 NPC 记忆条目。", node.Id);
        }
        var nodes = graph.Nodes.OfType<DialogueNodeData>().Where(n => !string.IsNullOrWhiteSpace(n.Id)).GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First());
        var visited = new HashSet<string>(); var visiting = new HashSet<string>();
        bool Cycle(string id)
        {
            if (!nodes.TryGetValue(id, out var node) || node is DialogueLineNodeData or DialogueChoiceNodeData or DialogueEndNodeData) return false;
            if (visiting.Contains(id)) return true;
            if (!visited.Add(id)) return false;
            visiting.Add(id);
            bool cycle = edges.Where(e => e.FromNode == id).Any(e => Cycle(e.ToNode));
            visiting.Remove(id); return cycle;
        }
        if (nodes.Keys.Any(Cycle)) result.AddError("对话包含不等待台词或选项的无限循环。");
        if (!nodes.Values.Any(n => n is DialogueEndNodeData)) result.AddError("对话图需要结束节点。");
    }
}
