using System;
using System.Linq;
using GameLogic.AI;
using Godot;

namespace GameLogic;

public static class DialogueMemoryEditor
{
    public static void Field(VBoxContainer box, GraphEditorContext context, string label, string value, Action<string> set, bool optional = false)
    {
        box.AddChild(new Label { Text = label });
        var npc = context.RootGraph as NpcGraphAsset ?? (context.ResolveHost?.Invoke() as GameObject3D)?.Components.OfType<AiAgentComponent3D>().FirstOrDefault()?.Graph;
        var root = npc?.Nodes.OfType<NpcRootNodeData>().FirstOrDefault();
        var definitions = root == null ? Array.Empty<NpcMemoryEntryDefinition>() : npc.Connections
            .Where(e => e.IsAvailable && e.FromNode == root.Id && e.FromPort == 0)
            .Select(e => npc.FindNodeById(e.ToNode)).OfType<NpcMemoryNodeData>()
            .Select(n => npc.Resolve(n.DefinitionId)).OfType<NpcMemoryDefinition>().Where(m => m.Enabled)
            .SelectMany(m => m.Entries).Where(e => e != null).DistinctBy(e => e.Key).ToArray();
        var choices = new OptionButton();
        int offset = optional ? 1 : 0;
        if (optional) choices.AddItem("始终显示");
        foreach (var entry in definitions) choices.AddItem((entry.DisplayName.Length > 0 ? entry.DisplayName : entry.Key) + " · " + entry.Key);
        int selected = Array.FindIndex(definitions, d => d.Key == value);
        if (selected >= 0) choices.Select(selected + offset);
        else if (optional && value.Length == 0) choices.Select(0);
        else { choices.AddItem(value.Length == 0 ? "请选择记忆条目" : "条目未配置：" + value); choices.Select(choices.ItemCount - 1); }
        choices.ItemSelected += index =>
        {
            if (optional && index == 0) set("");
            else if (index >= offset && index - offset < definitions.Length) set(definitions[(int)index - offset].Key);
            else return;
            context.CurrentGraph.MarkDirty();
        };
        box.AddChild(choices);
        box.AddChild(new Label { Text = "属于当前 NPC，记忆对象为本次交谈的玩家。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        if (definitions.Length == 0) box.AddChild(new Label { Text = "先在 NPC 图的「记忆规则 → Entries（记忆条目）」配置条目，再从 NPC 图进入此对话子图。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
    }
}
