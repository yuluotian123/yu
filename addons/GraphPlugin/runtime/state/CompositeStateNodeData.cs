using System.Collections.Generic;
using System;
using Godot;

public class CompositeStateNodeData : SubGraphNodeData, IStateNodeData
{
    private StateGraphAsset _cachedSubGraph;

    public string StateName { get; set; } = "Composite";
    public bool IsDefault { get; set; }
    public string Tags { get; set; } = string.Empty;

    public override List<string> GetGraphTypes() => new() { StateGraphAsset.GraphTypeName };

    public override string GetDisplayName()
    {
        string stateName = string.IsNullOrWhiteSpace(StateName) ? "Composite" : StateName;
        return string.IsNullOrWhiteSpace(SubGraphPath)
            ? $"{stateName} [Composite]"
            : $"{stateName} [{SubGraphPath.GetFile().GetBaseName()}]";
    }

    public override Color GetNodeColor() => IsDefault ? new Color(0.35f, 0.8f, 0.5f) : new Color(0.45f, 0.45f, 0.95f);
    public override int GetInputMaxConnections(int port) => -1;
    public override int GetOutputMaxConnections(int port) => -1;
    public override string GetOutputPortName(int port) => "Out";

    public override GraphAsset GetSubGraph()
    {
        if (_cachedSubGraph != null)
            return _cachedSubGraph;

        if (string.IsNullOrWhiteSpace(SubGraphPath))
            return null;

        if (!ResourceLoader.Exists(SubGraphPath))
        {
            GD.PushWarning($"[StateGraph] Sub graph resource does not exist: {SubGraphPath}");
            return null;
        }

        _cachedSubGraph = ResourceLoader.Load<StateGraphAsset>(SubGraphPath);
        if (_cachedSubGraph == null)
            GD.PushWarning($"[StateGraph] Resource is not a StateGraphAsset: {SubGraphPath}");

        return _cachedSubGraph;
    }

    public override void InvalidateCache()
    {
        _cachedSubGraph = null;
        base.InvalidateCache();
    }

    public override GraphAsset CreateSubGraphAsset()
    {
        return new StateGraphAsset();
    }

    public override Type GetSubGraphType()
    {
        return typeof(StateGraphAsset);
    }

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

    public override void CreateNodeUI(GraphEditorContext context)
    {
        var root = new VBoxContainer
        {
            Name = "SubGraphContent",
            CustomMinimumSize = new Vector2(180f, 0f)
        };

        root.AddChild(new Label
        {
            Text = IsDefault ? "Default Composite" : "Composite",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var pathLabel = new Label
        {
            Name = "PathLabel",
            Text = string.IsNullOrEmpty(SubGraphPath) ? "Unbound State SubGraph" : SubGraphPath.GetFile().GetBaseName(),
            ClipText = true,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        root.AddChild(pathLabel);

        context.GraphNode.AddChild(root);
    }

    public override Control CreateInspectorUI(GraphEditorContext context)
    {
        var root = new VBoxContainer
        {
            Name = "SubGraphContent",
            CustomMinimumSize = new Vector2(190f, 0f)
        };
        root.AddThemeConstantOverride("separation", 6);

        var nameEdit = new LineEdit
        {
            PlaceholderText = "State name",
            Text = StateName
        };
        nameEdit.TextChanged += value =>
        {
            StateName = value;
            if (context.GraphNode != null)
                context.GraphNode.Title = GetDisplayName();
        };
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
        root.AddChild(new HSeparator());

        var pathLabel = new Label
        {
            Name = "PathLabel",
            Text = string.IsNullOrEmpty(SubGraphPath) ? "Unbound State SubGraph" : GetDisplayName(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        root.AddChild(pathLabel);

        return root;
    }

    public override void CreateUI(GraphEditorContext context)
    {
        context.GraphNode.AddChild(CreateInspectorUI(context));
    }
}
