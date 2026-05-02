#if TOOLS
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 黑板编辑面板。
/// </summary>
/// <remarks>
/// 面板负责本地图黑板和场景全局黑板的 UI、增删改、校验与保存。
/// 窗口只需要调用 <see cref="Open"/> 和 <see cref="Close"/>。
/// </remarks>
public sealed class GraphBlackboardPanel
{
    private readonly Window _owner;
    private readonly Func<GraphAsset> _getCurrentGraph;
    private readonly Func<GraphEditorContext> _createContext;
    private Window _window;

    /// <summary>创建黑板面板。</summary>
    public GraphBlackboardPanel(
        Window owner,
        Func<GraphAsset> getCurrentGraph,
        Func<GraphEditorContext> createContext)
    {
        _owner = owner;
        _getCurrentGraph = getCurrentGraph;
        _createContext = createContext;
    }

    /// <summary>打开黑板窗口。</summary>
    public void Open()
    {
        GraphAsset graph = _getCurrentGraph();
        if (graph == null)
            return;

        Close();
        _window = new Window
        {
            Title = "Graph Blackboard",
            Size = new Vector2I(760, 600)
        };
        _window.CloseRequested += () => _window.Hide();
        _owner.AddChild(_window);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        _window.AddChild(margin);

        var tabs = new TabContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        margin.AddChild(tabs);

        Control globalPage = BuildGlobalBlackboardPage();
        globalPage.Name = "Global";
        tabs.AddChild(globalPage);

        Control localPage = BuildLocalBlackboardPage(graph);
        localPage.Name = "Local";
        tabs.AddChild(localPage);

        _window.PopupCentered();
    }

    /// <summary>关闭黑板窗口并释放控件。</summary>
    public void Close()
    {
        if (_window == null || !GodotObject.IsInstanceValid(_window))
        {
            _window = null;
            return;
        }

        _window.QueueFree();
        _window = null;
    }

    /// <summary>查找当前编辑场景中的全局黑板节点。</summary>
    public static List<GraphBlackboardNode> FindBlackboardNodesInEditedScene()
    {
        var results = new List<GraphBlackboardNode>();
        Node root = EditorInterface.Singleton.GetEditedSceneRoot();
        if (root == null)
            return results;

        CollectBlackboardNodes(root, results);
        return results;
    }

    private Control BuildGlobalBlackboardPage()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);

        List<GraphBlackboardNode> nodes = FindBlackboardNodesInEditedScene();
        if (nodes.Count == 0)
        {
            root.AddChild(new Label
            {
                Text = "No GraphBlackboardNode was found in the edited scene. Add one to the scene tree to edit the global blackboard.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
            return root;
        }

        GraphBlackboardNode blackboard = nodes[0];
        root.AddChild(new Label { Text = $"Node: {blackboard.GetPath()}" });

        if (nodes.Count > 1)
        {
            root.AddChild(new Label
            {
                Text = $"Warning: {nodes.Count} GraphBlackboardNode instances found. Editing the first one.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
        }

        root.AddChild(BuildBlackboardEditor(
            blackboard.Entries,
            "Save Global Blackboard",
            () => SaveGlobalBlackboard(blackboard)));

        return root;
    }

    private Control BuildLocalBlackboardPage(GraphAsset graph)
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        root.AddChild(new Label { Text = $"Graph: {graph.ResourcePath}" });
        root.AddChild(BuildBlackboardEditor(
            graph.BlackboardEntries,
            "Save Local Blackboard",
            SaveLocalBlackboard));
        return root;
    }

    private Control BuildBlackboardEditor(
        IList<GraphBlackboardEntry> entries,
        string saveButtonText,
        Action saveAction)
    {
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 6);

        var validationLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(validationLabel);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddChild(scroll);

        var entriesContainer = new VBoxContainer();
        entriesContainer.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(entriesContainer);

        void Refresh()
        {
            RefreshBlackboardEntries(entries, entriesContainer, validationLabel, Refresh);
        }

        Refresh();

        var buttons = new HBoxContainer();
        root.AddChild(buttons);

        var addButton = new Button
        {
            Text = "Add Entry",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        addButton.Pressed += () => ShowAddBlackboardEntryPopup(addButton, entries, Refresh);
        buttons.AddChild(addButton);

        var saveButton = new Button
        {
            Text = saveButtonText,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        saveButton.Pressed += () =>
        {
            if (!GraphBlackboardValidator.TryValidate(entries, out string error))
            {
                ShowBlackboardError(error);
                return;
            }

            saveAction?.Invoke();
            Refresh();
        };
        buttons.AddChild(saveButton);

        return root;
    }

    private void RefreshBlackboardEntries(
        IList<GraphBlackboardEntry> entries,
        VBoxContainer entriesContainer,
        Label validationLabel,
        Action refresh)
    {
        foreach (Node child in entriesContainer.GetChildren())
        {
            entriesContainer.RemoveChild(child);
            child.QueueFree();
        }

        if (GraphBlackboardValidator.TryValidate(entries, out string error))
        {
            validationLabel.Text = entries.Count == 0 ? "No entries." : $"{entries.Count} entries.";
            validationLabel.RemoveThemeColorOverride("font_color");
        }
        else
        {
            validationLabel.Text = error;
            validationLabel.AddThemeColorOverride("font_color", new Color(1f, 0.35f, 0.35f));
        }

        for (int i = 0; i < entries.Count; i++)
            entriesContainer.AddChild(BuildBlackboardEntryRow(entries, i, refresh));
    }

    private Control BuildBlackboardEntryRow(IList<GraphBlackboardEntry> entries, int index, Action refresh)
    {
        GraphBlackboardEntry entry = entries[index];
        entry.Value ??= new GraphStringBlackboardValue();

        var panel = new PanelContainer();
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 6);
        panel.AddChild(content);

        var header = new HBoxContainer();
        content.AddChild(header);

        header.AddChild(new Label { Text = "Key" });
        var keyEdit = new LineEdit
        {
            Text = entry.Key,
            PlaceholderText = "blackboard_key",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        keyEdit.TextChanged += value => entry.Key = value;
        header.AddChild(keyEdit);

        header.AddChild(new Label { Text = entry.Value.DisplayName });

        var replaceButton = new Button { Text = "Type" };
        replaceButton.Pressed += () => ShowReplaceBlackboardValuePopup(replaceButton, entry, refresh);
        header.AddChild(replaceButton);

        var upButton = new Button { Text = "Up", Disabled = index == 0 };
        upButton.Pressed += () =>
        {
            (entries[index - 1], entries[index]) = (entries[index], entries[index - 1]);
            refresh();
        };
        header.AddChild(upButton);

        var downButton = new Button { Text = "Down", Disabled = index == entries.Count - 1 };
        downButton.Pressed += () =>
        {
            (entries[index + 1], entries[index]) = (entries[index], entries[index + 1]);
            refresh();
        };
        header.AddChild(downButton);

        var deleteButton = new Button { Text = "Delete" };
        deleteButton.Pressed += () =>
        {
            entries.RemoveAt(index);
            refresh();
        };
        header.AddChild(deleteButton);

        var descriptionEdit = new LineEdit
        {
            Text = entry.Description,
            PlaceholderText = "Description",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        descriptionEdit.TextChanged += value => entry.Description = value;
        content.AddChild(descriptionEdit);

        Control valueUi = entry.Value.CreateEditUI(_createContext().WithBlackboardEntry(entry));
        valueUi.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.AddChild(valueUi);

        return panel;
    }

    private void ShowAddBlackboardEntryPopup(Control anchor, IList<GraphBlackboardEntry> entries, Action refresh)
    {
        var popup = new SearchablePopup<Type>(
            SubTypeCache.GetSubTypes<GraphBlackboardValue>(),
            type => type.Name);
        popup.OnItemSelected += type =>
        {
            entries.Add(new GraphBlackboardEntry
            {
                Key = CreateUniqueBlackboardKey(entries),
                Value = (GraphBlackboardValue)Activator.CreateInstance(type)
            });
            refresh();
        };
        popup.ShowBelow(anchor);
    }

    private void ShowReplaceBlackboardValuePopup(Control anchor, GraphBlackboardEntry entry, Action refresh)
    {
        var popup = new SearchablePopup<Type>(
            SubTypeCache.GetSubTypes<GraphBlackboardValue>(),
            type => type.Name);
        popup.OnItemSelected += type =>
        {
            entry.Value = (GraphBlackboardValue)Activator.CreateInstance(type);
            refresh();
        };
        popup.ShowBelow(anchor);
    }

    private static string CreateUniqueBlackboardKey(IList<GraphBlackboardEntry> entries)
    {
        const string baseKey = "NewKey";
        if (GraphBlackboardValidator.FindEntry(entries, baseKey) == null)
            return baseKey;

        int index = 1;
        while (GraphBlackboardValidator.FindEntry(entries, $"{baseKey}{index}") != null)
            index++;

        return $"{baseKey}{index}";
    }

    private void SaveGlobalBlackboard(GraphBlackboardNode blackboard)
    {
        if (!GraphBlackboardValidator.TryValidate(blackboard.Entries, out string error))
        {
            ShowBlackboardError(error);
            return;
        }

        blackboard.SaveToJson();
        blackboard.NotifyPropertyListChanged();
        EditorInterface.Singleton.MarkSceneAsUnsaved();
        GD.Print("[GraphBlackboard] Global blackboard updated. Save the scene to persist it.");
    }

    private void SaveLocalBlackboard()
    {
        GraphAsset graph = _getCurrentGraph();
        if (graph == null)
            return;

        if (!GraphBlackboardValidator.TryValidate(graph.BlackboardEntries, out string error))
        {
            ShowBlackboardError(error);
            return;
        }

        graph.MarkDirty();
        graph.SaveJsonFields();
        ResourceSaver.Save(graph, graph.ResourcePath);
        GD.Print($"[GraphBlackboard] Local blackboard saved: {graph.ResourcePath}");
    }

    private void ShowBlackboardError(string message)
    {
        var dialog = new AcceptDialog
        {
            Title = "Blackboard Error",
            DialogText = message
        };
        _owner.AddChild(dialog);
        dialog.PopupCentered();
    }

    private static void CollectBlackboardNodes(Node node, List<GraphBlackboardNode> results)
    {
        if (node is GraphBlackboardNode blackboard)
            results.Add(blackboard);

        foreach (Node child in node.GetChildren())
            CollectBlackboardNodes(child, results);
    }
}
#endif
