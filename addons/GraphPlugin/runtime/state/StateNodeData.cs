using System.Collections.Generic;
using Godot;

public class StateNodeData : GraphNodeData, IStateNodeData
{
    public string StateName { get; set; } = "State";
    public bool IsDefault { get; set; }
    public string Tags { get; set; } = string.Empty;

    public override List<string> GetGraphTypes() => new() { StateGraphAsset.GraphTypeName };
    public override string GetDisplayName() => string.IsNullOrWhiteSpace(StateName) ? "State" : StateName;
    public override Color GetNodeColor() => IsDefault ? new Color(0.3f, 0.75f, 0.45f) : new Color(0.35f, 0.55f, 0.9f);
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;
    public override int GetInputMaxConnections(int port) => -1;
    public override int GetOutputMaxConnections(int port) => -1;
    public override string GetOutputPortName(int port) => "Out";

    public virtual bool HasTag(string tag)
    {
        return StateTagUtility.ContainsTag(Tags, tag);
    }

    public virtual IReadOnlyList<string> GetTags()
    {
        return StateTagUtility.ParseTags(Tags);
    }

    public virtual bool CanEnter(StateGraphRuntime runtime)
    {
        return true;
    }

    public virtual void OnEnter(StateGraphRuntime runtime)
    {
        if (runtime != null)
            Execute(runtime.Context);
    }

    public virtual void OnUpdate(StateGraphRuntime runtime, double delta)
    {
    }

    public virtual bool TryGetCompletion(StateGraphRuntime runtime, out NodeCompletion completion)
    {
        completion = default;
        return false;
    }

    public virtual void OnExit(StateGraphRuntime runtime)
    {
    }

    public override void CreateUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(190f, 0f) };

        var nameEdit = new LineEdit
        {
            PlaceholderText = "State name",
            Text = StateName
        };
        nameEdit.TextChanged += value => StateName = value;
        root.AddChild(nameEdit);

        var defaultCheck = new CheckBox
        {
            Text = "Default",
            ButtonPressed = IsDefault
        };
        defaultCheck.Toggled += value => IsDefault = value;
        root.AddChild(defaultCheck);

        var tagEdit = new LineEdit
        {
            PlaceholderText = "Tags",
            Text = Tags
        };
        tagEdit.TextChanged += value => Tags = value;
        root.AddChild(tagEdit);

        context.GraphNode.AddChild(root);
    }
}
