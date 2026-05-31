using System.Collections.Generic;
using Godot;

public interface IStatePseudoNodeData
{
    string Id { get; set; }
}

public class AnyStateNodeData : GraphNodeData, IStatePseudoNodeData
{
    public string IgnoredStateNames { get; set; } = string.Empty;
    public string IgnoredTags { get; set; } = string.Empty;

    public override List<string> GetGraphTypes() => new() { StateGraphAsset.GraphTypeName };
    public override string GetDisplayName() => "Any State";
    public override Color GetNodeColor() => new(0.95f, 0.62f, 0.24f);
    public override int GetInputCount() => 0;
    public override int GetOutputCount() => 1;
    public override bool CanBePrime() => false;
    public override string GetOutputPortName(int port) => "Out";

    public virtual bool CanTransitionFrom(IStateNodeData currentState)
    {
        if (currentState == null)
            return false;

        if (StateTagUtility.ContainsTag(IgnoredStateNames, currentState.StateName) ||
            StateTagUtility.ContainsTag(IgnoredStateNames, currentState.Id))
            return false;

        foreach (string tag in currentState.GetTags())
        {
            if (StateTagUtility.ContainsTag(IgnoredTags, tag))
                return false;
        }

        return true;
    }

    public override void CreateNodeUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(160f, 0f) };
        root.AddChild(new Label
        {
            Text = "Global transitions",
            HorizontalAlignment = HorizontalAlignment.Center
        });
        int ignoredCount = StateTagUtility.ParseTags(IgnoredStateNames).Count + StateTagUtility.ParseTags(IgnoredTags).Count;
        if (ignoredCount > 0)
            root.AddChild(new Label { Text = $"Ignored {ignoredCount}", HorizontalAlignment = HorizontalAlignment.Center });

        context.GraphNode.AddChild(root);
    }

    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(190f, 0f) };
        root.AddThemeConstantOverride("separation", 6);
        root.AddChild(new Label
        {
            Text = "Global transitions",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var ignoredStates = new LineEdit
        {
            PlaceholderText = "Ignore states",
            Text = IgnoredStateNames
        };
        ignoredStates.TextChanged += value => IgnoredStateNames = value;
        root.AddChild(ignoredStates);

        var ignoredTags = new LineEdit
        {
            PlaceholderText = "Ignore tags",
            Text = IgnoredTags
        };
        ignoredTags.TextChanged += value => IgnoredTags = value;
        root.AddChild(ignoredTags);

        return root;
    }

    public override void CreateUI(GraphEditorContext context)
    {
        context.GraphNode.AddChild(CreateInspectorUI(context));
    }
}

public class StateReturnNodeData : GraphNodeData, IStatePseudoNodeData
{
    public string Label { get; set; } = "Return";

    public override List<string> GetGraphTypes() => new() { StateGraphAsset.GraphTypeName };
    public override string GetDisplayName() => string.IsNullOrWhiteSpace(Label) ? "Return" : Label;
    public override string GetMenuName() => "Return";
    public override Color GetNodeColor() => new(0.66f, 0.56f, 0.92f);
    public override int GetInputCount() => 1;
    public override int GetOutputCount() => 1;
    public override int GetInputMaxConnections(int port) => -1;
    public override int GetOutputMaxConnections(int port) => -1;
    public override bool CanBePrime() => false;
    public override string GetOutputPortName(int port) => "Return";

    public override void CreateNodeUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(140f, 0f) };
        root.AddChild(new Label
        {
            Text = "Resolves immediately",
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipText = true
        });

        context.GraphNode.AddChild(root);
    }

    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(170f, 0f) };
        root.AddThemeConstantOverride("separation", 6);
        var labelEdit = new LineEdit
        {
            PlaceholderText = "Return label",
            Text = Label
        };
        labelEdit.TextChanged += value =>
        {
            Label = value;
            if (context.GraphNode != null)
                context.GraphNode.Title = GetDisplayName();
        };
        root.AddChild(labelEdit);

        root.AddChild(new Label
        {
            Text = "Resolves immediately",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        return root;
    }

    public override void CreateUI(GraphEditorContext context)
    {
        context.GraphNode.AddChild(CreateInspectorUI(context));
    }
}
