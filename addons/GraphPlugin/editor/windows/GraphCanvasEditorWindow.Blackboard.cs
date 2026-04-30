#if TOOLS
using System;
using System.Collections.Generic;
using Godot;

public partial class GraphCanvasEditorWindow
{
    private Window _blackboardWindow;

    private void OpenBlackboardWindow()
    {
        if (_currentGraph == null)
            return;

        if (_blackboardWindow != null && GodotObject.IsInstanceValid(_blackboardWindow))
            _blackboardWindow.QueueFree();

        _blackboardWindow = new Window
        {
            Title = "Graph Blackboard",
            Size = new Vector2I(760, 600)
        };
        _blackboardWindow.CloseRequested += () => _blackboardWindow.Hide();
        AddChild(_blackboardWindow);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        _blackboardWindow.AddChild(margin);

        var tabs = new TabContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        margin.AddChild(tabs);

        Control globalPage = BuildGlobalBlackboardPage();
        globalPage.Name = "Global";
        tabs.AddChild(globalPage);

        Control localPage = BuildLocalBlackboardPage();
        localPage.Name = "Local";
        tabs.AddChild(localPage);

        _blackboardWindow.PopupCentered();
    }

    private void CloseBlackboardWindow()
    {
        if (_blackboardWindow == null || !GodotObject.IsInstanceValid(_blackboardWindow))
        {
            _blackboardWindow = null;
            return;
        }

        _blackboardWindow.QueueFree();
        _blackboardWindow = null;
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

    private Control BuildLocalBlackboardPage()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        root.AddChild(new Label { Text = $"Graph: {_currentGraph.ResourcePath}" });
        root.AddChild(BuildBlackboardEditor(
            _currentGraph.BlackboardEntries,
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

        var upButton = new Button
        {
            Text = "Up",
            Disabled = index == 0
        };
        upButton.Pressed += () =>
        {
            (entries[index - 1], entries[index]) = (entries[index], entries[index - 1]);
            refresh();
        };
        header.AddChild(upButton);

        var downButton = new Button
        {
            Text = "Down",
            Disabled = index == entries.Count - 1
        };
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

        Control valueUi = entry.Value.CreateEditUI(CreateEditorContext().WithBlackboardEntry(entry));
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

    private string CreateUniqueBlackboardKey(IList<GraphBlackboardEntry> entries)
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
        if (!GraphBlackboardValidator.TryValidate(_currentGraph.BlackboardEntries, out string error))
        {
            ShowBlackboardError(error);
            return;
        }

        _currentGraph.SaveToJson();
        ResourceSaver.Save(_currentGraph, _currentGraph.ResourcePath);
        GD.Print($"[GraphBlackboard] Local blackboard saved: {_currentGraph.ResourcePath}");
    }

    private List<GraphBlackboardNode> FindBlackboardNodesInEditedScene()
    {
        var results = new List<GraphBlackboardNode>();
        Node root = EditorInterface.Singleton.GetEditedSceneRoot();
        if (root == null)
            return results;

        CollectBlackboardNodes(root, results);
        return results;
    }

    private void CollectBlackboardNodes(Node node, List<GraphBlackboardNode> results)
    {
        if (node is GraphBlackboardNode blackboard)
            results.Add(blackboard);

        foreach (Node child in node.GetChildren())
            CollectBlackboardNodes(child, results);
    }

    private void ShowBlackboardError(string message)
    {
        var dialog = new AcceptDialog
        {
            Title = "Blackboard Error",
            DialogText = message
        };
        AddChild(dialog);
        dialog.PopupCentered();
    }
}
#endif
