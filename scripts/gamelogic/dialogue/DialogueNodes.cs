using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic;

public abstract class DialogueNodeData : GraphNodeData
{
    public override List<string> GetGraphTypes() => new() { DialogueGraphAsset.TypeName };
    public override string GetCategory() => "对话";
    public override List<string> GetSearchKeywords() => new() { "dialogue", "对话", "NVL" };
    public override Color GetNodeColor() => new(.66f, .75f, .84f);
    public override int GetInputCount() => 1;
    public override int GetInputMaxConnections(int port) => -1;
    public override int GetOutputCount() => 1;
    public override int GetOutputMaxConnections(int port) => 1;
    public override bool CanBePrime() => false;
    public override void CreateNodeUI(GraphEditorContext context)
    {
        context.GraphNode.AddChild(new Label
        {
            Text = Preview(), CustomMinimumSize = new(220, 36),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
    }
    protected virtual string Preview() => GetDisplayName();
    protected void TextField(VBoxContainer box, GraphEditorContext context, string label, string value, Action<string> setter, bool multiline = false)
    {
        box.AddChild(new Label { Text = label });
        void Changed(string text) { setter(text); context.CurrentGraph.MarkDirty(); RefreshCanvas(context); }
        if (multiline)
        {
            var edit = new TextEdit { Text = value ?? "", CustomMinimumSize = new(280, 150), WrapMode = TextEdit.LineWrappingMode.Boundary };
            edit.TextChanged += () => Changed(edit.Text);
            box.AddChild(edit);
        }
        else
        {
            var edit = new LineEdit { Text = value ?? "" };
            edit.TextChanged += Changed;
            box.AddChild(edit);
        }
    }
    protected void Toggle(VBoxContainer box, GraphEditorContext context, string label, bool value, Action<bool> setter)
    {
        var toggle = new CheckBox { Text = label, ButtonPressed = value };
        toggle.Toggled += next => { setter(next); context.CurrentGraph.MarkDirty(); RefreshCanvas(context); };
        box.AddChild(toggle);
    }
    protected void RefreshCanvas(GraphEditorContext context)
    {
#if TOOLS
        Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(context.GraphNode) && !context.GraphNode.IsQueuedForDeletion() && context.GraphNode.GetParent() == context.GraphEdit)
                GraphNodeViewBuilder.RefreshNodeUI(this, context.GraphNode, context);
        }).CallDeferred();
#endif
    }
    public override void Validate(GraphAsset graph, GraphValidationResult result)
    {
        if (this is DialogueStartNodeData) DialogueSession.ValidateStructure(graph, result);
    }
}

public sealed class DialogueStartNodeData : DialogueNodeData
{
    public override string GetDisplayName() => "开始对话";
    public override int GetInputCount() => 0;
    public override bool CanBePrime() => true;
}

public sealed class DialogueLineNodeData : DialogueNodeData
{
    // Empty speaker is narration; tokens resolve against the participants at runtime.
    public string Speaker { get; set; } = "{npc}";
    public string Text { get; set; } = "";
    public bool NewPage { get; set; }
    public override string GetDisplayName() => string.IsNullOrWhiteSpace(Speaker) ? "旁白" : "台词";
    protected override string Preview() => (Speaker.Length > 0 ? Speaker + "\n" : "") + (Text.Length > 64 ? Text[..64] + "…" : Text);
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var box = new VBoxContainer();
        TextField(box, context, "说话人（留空为旁白；{npc} / {player}）", Speaker, v => Speaker = v);
        TextField(box, context, "正文（纯文本，支持换行）", Text, v => Text = v, true);
        Toggle(box, context, "开始新页", NewPage, v => NewPage = v);
        return box;
    }
}

public sealed class DialogueOption
{
    public string Text { get; set; } = "选项";
    public string RequiredMemory { get; set; } = "";
    public bool RequiredValue { get; set; } = true;
}

public sealed class DialogueChoiceNodeData : DialogueNodeData
{
    public string Prompt { get; set; } = "";
    public List<DialogueOption> Options { get; set; } = new() { new() { Text = "继续交谈" }, new() { Text = "告别" } };
    public override string GetDisplayName() => "选项分支";
    public override Color GetNodeColor() => new(.84f, .73f, .45f);
    public override int GetOutputCount() => Options?.Count ?? 0;
    public override string GetOutputPortName(int port) => Options != null && port < Options.Count ? Options[port]?.Text ?? "空选项" : "";
    protected override string Preview() => Prompt.Length > 0 ? Prompt : "每个选项连接一条剧情分支";
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var box = new VBoxContainer();
        TextField(box, context, "提示（可留空）", Prompt, v => Prompt = v);
        for (int i = 0; i < Options.Count; i++)
        {
            int index = i;
            var option = Options[i];
            box.AddChild(new HSeparator());
            TextField(box, context, $"选项 {i + 1}", option.Text, v => option.Text = v);
            DialogueMemoryEditor.Field(box, context, "显示条件记忆", option.RequiredMemory, v => option.RequiredMemory = v, true);
            Toggle(box, context, "条件值为真时显示", option.RequiredValue, v => option.RequiredValue = v);
            var remove = new Button { Text = "删除此选项及其连线" };
            remove.Pressed += () =>
            {
                foreach (var edge in context.CurrentGraph.Connections.Where(e => e.FromNode == Id).ToArray())
                    if (edge.FromPort == index) context.CurrentGraph.Connections.Remove(edge);
                    else if (edge.FromPort > index) edge.FromPort--;
                Options.RemoveAt(index);
                Refresh(context);
            };
            box.AddChild(remove);
        }
        var add = new Button { Text = "添加选项", Disabled = Options.Count >= 8 };
        add.Pressed += () => { Options.Add(new()); Refresh(context); };
        box.AddChild(add);
        return box;
    }
    void Refresh(GraphEditorContext context)
    {
        context.CurrentGraph.MarkDirty();
#if TOOLS
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(context.GraphNode) || context.GraphNode.IsQueuedForDeletion()) return;
            GraphNodeViewBuilder.RefreshNodeUI(this, context.GraphNode, context);
            context.GraphEdit.ClearConnections();
            foreach (var edge in context.CurrentGraph.Connections)
                context.GraphEdit.ConnectNode(edge.FromNode, edge.FromPort, edge.ToNode, edge.ToPort);
            context.InspectNode?.Invoke(context.GraphNode);
        }).CallDeferred();
#endif
    }
}

public sealed class DialogueBranchNodeData : DialogueNodeData
{
    public string MemoryKey { get; set; } = "";
    public override string GetDisplayName() => "记忆条件";
    public override int GetOutputCount() => 2;
    public override string GetOutputPortName(int port) => port == 0 ? "真" : "假";
    protected override string Preview() => MemoryKey;
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var box = new VBoxContainer(); DialogueMemoryEditor.Field(box, context, "判断记忆", MemoryKey, v => MemoryKey = v); return box;
    }
}

public sealed class DialogueSetFlagNodeData : DialogueNodeData
{
    // Keep the serialized node type ID while moving its state into NPC memory.
    public string MemoryKey { get; set; } = "";
    public bool Value { get; set; } = true;
    public override string GetDisplayName() => "写入记忆";
    protected override string Preview() => MemoryKey + " = " + Value;
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var box = new VBoxContainer(); DialogueMemoryEditor.Field(box, context, "写入记忆", MemoryKey, v => MemoryKey = v);
        Toggle(box, context, "记忆值为真", Value, v => Value = v); return box;
    }
}

public sealed class DialogueEndNodeData : DialogueNodeData
{
    public string Result { get; set; } = "Completed";
    public override string GetDisplayName() => "结束对话";
    public override int GetOutputCount() => 0;
    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var box = new VBoxContainer(); TextField(box, context, "结束结果", Result, v => Result = v); return box;
    }
}
