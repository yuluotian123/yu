#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic.AI;

public static class NpcPlanActionCatalog
{
    public static IReadOnlyList<NpcGoalNodeData> Owners(NpcGraphAsset graph, NpcPlanTemplateDefinition plan)
    {
        var nodes = graph.Nodes.OfType<NpcTemplateNodeData>().Where(n => graph.Resolve(n.DefinitionId) == plan).Select(n => n.Id).ToHashSet();
        return graph.Connections.Where(e => e.IsAvailable && e.FromPort == 1 && e.ToPort == 0 && nodes.Contains(e.ToNode))
            .Select(e => graph.FindNodeById(e.FromNode)).OfType<NpcGoalNodeData>().Distinct().ToArray();
    }

    public static IReadOnlyList<NpcActionDefinition> Available(NpcGraphAsset graph, NpcPlanTemplateDefinition plan)
    {
        var owners = Owners(graph, plan);
        if (owners.Count == 0) return Array.Empty<NpcActionDefinition>();
        NpcActionDefinition[] Actions(NpcGoalNodeData goal) => graph.GetOutgoingConnections(goal.Id, 0)
            .Where(e => e.IsAvailable && e.ToPort == 0).Select(e => graph.FindNodeById(e.ToNode)).OfType<NpcActionNodeData>()
            .Select(n => graph.Resolve(n.DefinitionId)).OfType<NpcActionDefinition>().Where(a => a.Enabled).DistinctBy(a => a.DefinitionId).ToArray();
        var available = Actions(owners[0]);
        // A shared plan must be valid in every goal that uses it.
        foreach (var owner in owners.Skip(1))
        {
            var allowed = Actions(owner).Select(a => a.DefinitionId).ToHashSet();
            available = available.Where(a => allowed.Contains(a.DefinitionId)).ToArray();
        }
        return available;
    }
}

[Tool]
public partial class NpcPlanActionsProperty : EditorProperty
{
    readonly VBoxContainer _body = new();
    readonly NpcGraphAsset _graph;
    readonly NpcPlanTemplateDefinition _owner;
    string[] _shown = Array.Empty<string>();
    string _catalogStamp;
    bool _built;

    public NpcPlanActionsProperty() { AddChild(_body); SetBottomEditor(_body); }
    public NpcPlanActionsProperty(NpcGraphAsset graph, NpcPlanTemplateDefinition owner) : this()
    { _graph = graph; _owner = owner; }

    public override void _UpdateProperty()
    {
        if (!GodotObject.IsInstanceValid(_owner)) return;
        Label = "可用行动";
        var stamp = string.Join("\n", NpcPlanActionCatalog.Owners(_graph, _owner).Select(n => n.Id)) + "\n" +
            string.Join("\n", NpcPlanActionCatalog.Available(_graph, _owner).Select(a => a.DefinitionId + ":" + a.DisplayName));
        if (_built && _shown.SequenceEqual(_owner.ActionIds) && stamp == _catalogStamp) return;
        _catalogStamp = stamp; _shown = _owner.ActionIds.ToArray(); Rebuild();
    }

    void Commit(string[] actions)
    {
        _shown = actions.Distinct(StringComparer.Ordinal).ToArray();
        EmitChanged(nameof(NpcPlanTemplateDefinition.ActionIds), Variant.From(_shown));
        Callable.From(Rebuild).CallDeferred();
    }

    static string ActionName(NpcActionDefinition action) => string.IsNullOrWhiteSpace(action.DisplayName) ? "未命名行动" : action.DisplayName;

    void Pick(Control anchor, string replacing, Action<NpcActionDefinition> selected)
    {
        var choices = NpcPlanActionCatalog.Available(_graph, _owner)
            .Where(a => a.DefinitionId == replacing || !_shown.Contains(a.DefinitionId)).ToArray();
        if (choices.Length == 0) return;
        var popup = new SearchablePopup<NpcActionDefinition>(choices, ActionName, _ => "候选行动", ActionName);
        popup.OnItemSelected += action =>
        {
            if (GodotObject.IsInstanceValid(this) && !IsQueuedForDeletion() &&
                (action.DefinitionId == replacing || !_shown.Contains(action.DefinitionId)) &&
                NpcPlanActionCatalog.Available(_graph, _owner).Any(a => a.DefinitionId == action.DefinitionId)) selected(action);
        };
        popup.ShowBelow(anchor);
    }

    void Rebuild()
    {
        if (!GodotObject.IsInstanceValid(_body) || _body.IsQueuedForDeletion()) return;
        foreach (Node child in _body.GetChildren())
        { GraphEditorSignalCleanup.PrepareForRemoval(child); _body.RemoveChild(child); }
        _built = true;
        var owners = NpcPlanActionCatalog.Owners(_graph, _owner);
        var actions = NpcPlanActionCatalog.Available(_graph, _owner);
        _body.AddChild(new Label
        {
            Text = owners.Count == 0 ? "请先将此计划连接到 Goal 的“计划”端口。" : owners.Count > 1
                ? "此计划由多个目标共用，可选择它们共同拥有的候选行动。" : "从所属目标的行动库中选择此计划可使用的行动。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        _body.AddChild(new Label { Text = "GOAP 根据当前状态、前置条件和成本组合执行顺序；这里的行动不必全部执行，也可按需重复执行。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        for (int i = 0; i < _shown.Length; i++)
        {
            int index = i;
            var action = actions.FirstOrDefault(a => a.DefinitionId == _shown[i]);
            var existing = _graph.Resolve(_shown[i]) as NpcActionDefinition;
            var entry = new VBoxContainer();
            var choose = new Button
            {
                Text = action != null ? ActionName(action) : existing != null ? "不可用：" + ActionName(existing) : "行动引用已失效",
                TooltipText = action != null ? ActionName(action) : "请重新选择或恢复此行动。",
                Disabled = actions.Count == 0, ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            choose.Pressed += () => Pick(choose, _shown[index], selected =>
            { if (index < _shown.Length) { var next = _shown.ToArray(); next[index] = selected.DefinitionId; Commit(next); } });
            entry.AddChild(choose);
            if (action == null) entry.AddChild(new Label
            {
                Text = "原引用已保留；请重新选择行动，或检查目标连线及行动是否启用。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
            var remove = new Button { Text = "移除", TooltipText = "从此计划的可用行动集合中移除" };
            remove.Pressed += () => Commit(_shown.Where((_, n) => n != index).ToArray());
            entry.AddChild(remove); _body.AddChild(entry);
        }
        if (_shown.Length == 0) _body.AddChild(new Label { Text = "尚未选择可用行动。" });
        var add = new Button { Text = "+ 添加可用行动", Disabled = !actions.Any(a => !_shown.Contains(a.DefinitionId)) };
        add.Pressed += () => Pick(add, "", action => Commit(_shown.Append(action.DefinitionId).ToArray()));
        _body.AddChild(add);
        if (owners.Count > 0 && actions.Count == 0) _body.AddChild(new Label
        {
            Text = "暂无可选行动：请从所属 Goal 的“候选行动”端口连接并启用 Action。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
    }
}
#endif
