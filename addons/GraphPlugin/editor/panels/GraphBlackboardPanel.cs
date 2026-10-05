#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic;
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
    private readonly Node _owner;
    private readonly Func<GraphAsset> _getCurrentGraph;
    private readonly Func<GraphEditorContext> _createContext;
    private readonly Func<bool> _hasHost;
    private Window _window;
    private bool _hostStateAtOpen;

    /// <summary>创建黑板面板。</summary>
    public GraphBlackboardPanel(
        Node owner,
        Func<GraphAsset> getCurrentGraph,
        Func<GraphEditorContext> createContext,
        Func<bool> hasHost = null)
    {
        _owner = owner;
        _getCurrentGraph = getCurrentGraph;
        _createContext = createContext;
        _hasHost = hasHost ?? (() => false);
    }

    /// <summary>打开黑板窗口。</summary>
    public void Open()
    {
        GraphAsset graph = _getCurrentGraph();
        if (graph == null)
            return;

        Close();
        _hostStateAtOpen = _hasHost();
        _window = new Window
        {
            Title = "Blackboard",
            Size = new Vector2I(900, 700),
            MinSize = new Vector2I(720, 520)
        };
        _window.CloseRequested += () => _window.Hide();
        _owner.AddChild(_window);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        _window.AddChild(margin);

        var shell = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        shell.AddThemeConstantOverride("separation", 10);
        margin.AddChild(shell);

        var heading = new VBoxContainer();
        heading.AddThemeConstantOverride("separation", 2);
        var title = new Label { Text = "BLACKBOARD" };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color(0.92f, 0.94f, 0.98f));
        heading.AddChild(title);
        var subtitle = new Label
        {
            Text = "Define graph variables and bind them to the owning blueprint host.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        subtitle.AddThemeFontSizeOverride("font_size", 11);
        subtitle.AddThemeColorOverride("font_color", new Color(0.58f, 0.64f, 0.73f));
        heading.AddChild(subtitle);
        shell.AddChild(heading);

        var tabs = new TabContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        tabs.AddThemeConstantOverride("side_margin", 8);
        shell.AddChild(tabs);

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

    public void RefreshIfHostChanged()
    {
        if (_window == null || !GodotObject.IsInstanceValid(_window) || !_window.Visible)
            return;
        bool hasHost = _hasHost();
        if (hasHost == _hostStateAtOpen)
            return;
        Open();
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
        root.AddThemeConstantOverride("separation", 8);

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
        buttons.Alignment = BoxContainer.AlignmentMode.End;
        root.AddChild(buttons);

        var addButton = new Button
        {
            Text = "Add Entry",
            CustomMinimumSize = new Vector2(120, 32)
        };
        addButton.Pressed += () => ShowAddBlackboardEntryPopup(addButton, entries, Refresh);
        buttons.AddChild(addButton);

        var saveButton = new Button
        {
            Text = saveButtonText,
            CustomMinimumSize = new Vector2(170, 32)
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
            GraphEditorSignalCleanup.DisconnectSubtree(child);
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

    private Control BuildBlackboardEntryRow(
        IList<GraphBlackboardEntry> entries,
        int index,
        Action refresh)
    {
        GraphBlackboardEntry entry = entries[index];
        entry.Value ??= new GraphStringBlackboardValue();

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", CreateCardStyle());
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);

        var header = new HBoxContainer();
        content.AddChild(header);

        var keyLabel = new Label { Text = "KEY", VerticalAlignment = VerticalAlignment.Center };
        keyLabel.AddThemeFontSizeOverride("font_size", 10);
        keyLabel.AddThemeColorOverride("font_color", new Color(0.43f, 0.72f, 0.98f));
        header.AddChild(keyLabel);
        var keyEdit = new LineEdit
        {
            Text = entry.Key,
            PlaceholderText = "blackboard_key",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        keyEdit.TextChanged += value => entry.Key = value;
        header.AddChild(keyEdit);

        var typeLabel = new Label { Text = entry.Value.DisplayName, VerticalAlignment = VerticalAlignment.Center };
        typeLabel.AddThemeColorOverride("font_color", new Color(0.68f, 0.85f, 1.0f));
        header.AddChild(typeLabel);

        var replaceButton = new Button { Text = "Change Type", CustomMinimumSize = new Vector2(100, 28) };
        replaceButton.Pressed += () => ShowReplaceBlackboardValuePopup(replaceButton, entry, refresh);
        header.AddChild(replaceButton);

        var upButton = new Button { Text = "↑", TooltipText = "Move entry up", Disabled = index == 0 };
        upButton.Pressed += () =>
        {
            (entries[index - 1], entries[index]) = (entries[index], entries[index - 1]);
            refresh();
        };
        header.AddChild(upButton);

        var downButton = new Button { Text = "↓", TooltipText = "Move entry down", Disabled = index == entries.Count - 1 };
        downButton.Pressed += () =>
        {
            (entries[index + 1], entries[index]) = (entries[index], entries[index + 1]);
            refresh();
        };
        header.AddChild(downButton);

        var deleteButton = new Button { Text = "Delete", CustomMinimumSize = new Vector2(64, 28) };
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

        BuildBindingEditor(content, entry, refresh);

        return panel;
    }

    private void BuildBindingEditor(
        VBoxContainer content,
        GraphBlackboardEntry entry,
        Action refresh)
    {
        GraphComponentRegistry.EnsureScanned();
        var binding = entry.Binding;
        bool hostAvailable = _hasHost();
        var bindingPanel = new PanelContainer();
        bindingPanel.AddThemeStyleboxOverride("panel", CreateSectionStyle());
        var bindingContent = new VBoxContainer();
        bindingContent.AddThemeConstantOverride("separation", 6);
        bindingPanel.AddChild(bindingContent);
        content.AddChild(bindingPanel);

        var bindingHeader = new HBoxContainer();
        var bindingTitle = new Label { Text = "COMPONENT BINDING" };
        bindingTitle.AddThemeFontSizeOverride("font_size", 10);
        bindingTitle.AddThemeColorOverride("font_color", new Color(0.43f, 0.72f, 0.98f));
        bindingHeader.AddChild(bindingTitle);
        var enabled = new CheckButton
        {
            Text = binding == null ? "Enable" : "Enabled",
            ButtonPressed = binding != null,
            Disabled = !hostAvailable
        };
        bindingHeader.AddChild(enabled);
        bindingContent.AddChild(bindingHeader);
        if (!hostAvailable)
        {
            var hostWarning = new Label
            {
                Text = "No blueprint host found. Open the owning scene to edit component bindings.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            hostWarning.AddThemeColorOverride("font_color", new Color(1f, 0.68f, 0.35f));
            hostWarning.AddThemeFontSizeOverride("font_size", 11);
            bindingContent.AddChild(hostWarning);
        }
        var available = _createContext()?.AvailableComponentTypes;
        var componentTypes = (available ?? Array.Empty<GraphComponentTypeDescriptor>())
            .Where(value => value != null && value.Values.Count > 0).ToList();
        var fields = new GridContainer { Columns = 2 };
        fields.AddThemeConstantOverride("h_separation", 8);
        fields.AddThemeConstantOverride("v_separation", 6);
        bindingContent.AddChild(fields);
        fields.AddChild(new Label { Text = "Component" });
        var component = new OptionButton { Disabled = binding == null || !hostAvailable, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        for (int i = 0; i < componentTypes.Count; i++)
            component.AddItem(componentTypes[i].DisplayName, i);
        fields.AddChild(component);
        fields.AddChild(new Label { Text = "Member" });
        var member = new OptionButton { Disabled = binding == null || !hostAvailable, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        fields.AddChild(member);
        fields.AddChild(new Label { Text = "Direction" });
        var direction = new OptionButton { Disabled = binding == null || !hostAvailable, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        direction.AddItem("Component to Blackboard", (int)GraphComponentBindingDirection.ComponentToBlackboard);
        direction.AddItem("Blackboard to Component", (int)GraphComponentBindingDirection.BlackboardToComponent);
        direction.AddItem("Two Way", (int)GraphComponentBindingDirection.TwoWay);
        fields.AddChild(direction);

        int componentIndex = componentTypes.FindIndex(value => value.TypeName == binding?.ComponentTypeName || value.ComponentType.Name == binding?.ComponentTypeName);
        if (componentIndex >= 0)
            component.Select(componentIndex);

        void PopulateMembers()
        {
            member.Clear();
            GraphComponentTypeDescriptor type = component.Selected >= 0 && component.Selected < componentTypes.Count
                ? componentTypes[(int)component.Selected]
                : null;
            if (type == null)
                return;
            var compatibleValues = type.Values.Where(value => IsCompatible(entry, value)).ToList();
            for (int i = 0; i < compatibleValues.Count; i++)
                member.AddItem($"{compatibleValues[i].DisplayName} ({compatibleValues[i].MemberId})", i);
            int selected = compatibleValues.FindIndex(value => value.MemberId == binding?.MemberId);
            if (selected < 0 && compatibleValues.Count > 0)
                selected = 0;
            if (selected >= 0 && selected < compatibleValues.Count)
            {
                member.Select(selected);
                if (binding != null)
                    binding.MemberId = compatibleValues[selected].MemberId;
            }
        }

        PopulateMembers();
        if (binding != null)
            direction.Select((int)binding.Direction);

        enabled.Toggled += pressed =>
        {
            if (!pressed)
            {
                entry.Binding = null;
                refresh();
                return;
            }
            int selectedComponent = componentTypes.FindIndex(type => type.Values.Any(value => IsCompatible(entry, value)));
            if (selectedComponent < 0)
            {
                enabled.SetPressedNoSignal(false);
                ShowBlackboardError($"Blackboard key '{entry.Key}' has no component member compatible with {entry.Value?.ValueType?.Name ?? "its value type"}.");
                return;
            }
            entry.Binding = new GraphComponentBinding
            {
                ComponentTypeName = componentTypes[selectedComponent].TypeName,
                MemberId = componentTypes[selectedComponent].Values.First(value => IsCompatible(entry, value)).MemberId
            };
            refresh();
        };
        component.ItemSelected += index =>
        {
            if (entry.Binding == null)
                return;
            entry.Binding.ComponentTypeName = componentTypes[(int)index].TypeName;
            entry.Binding.MemberId = componentTypes[(int)index].Values.FirstOrDefault(value => IsCompatible(entry, value))?.MemberId ?? string.Empty;
            PopulateMembers();
        };
        member.ItemSelected += index =>
        {
            if (entry.Binding != null && component.Selected >= 0 && component.Selected < componentTypes.Count)
            {
                var compatibleValues = componentTypes[(int)component.Selected].Values
                    .Where(value => IsCompatible(entry, value)).ToList();
                if (index >= 0 && index < compatibleValues.Count)
                    entry.Binding.MemberId = compatibleValues[(int)index].MemberId;
            }
        };
        direction.ItemSelected += index =>
        {
            if (entry.Binding != null)
                entry.Binding.Direction = (GraphComponentBindingDirection)(int)index;
        };
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

    private static bool IsCompatible(GraphBlackboardEntry entry, GraphComponentValueDescriptor descriptor)
    {
        return entry?.Value != null && descriptor != null &&
               GraphBlackboardValidator.IsCompatible(entry.Value.ValueType, descriptor.ValueType);
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
        if (GraphSaveService.SaveGraphResource(_owner, graph))
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

    private static StyleBoxFlat CreateCardStyle()
    {
        return CreateStyle(new Color(0.075f, 0.085f, 0.105f, 0.98f), new Color(0.20f, 0.23f, 0.28f, 0.9f), 1, 5, 10);
    }

    private static StyleBoxFlat CreateSectionStyle()
    {
        return CreateStyle(new Color(0.055f, 0.065f, 0.08f, 0.95f), new Color(0.16f, 0.20f, 0.26f, 0.9f), 1, 4, 8);
    }

    private static StyleBoxFlat CreateStyle(Color background, Color border, int width, int radius, int margin)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = width,
            BorderWidthTop = width,
            BorderWidthRight = width,
            BorderWidthBottom = width,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = margin,
            ContentMarginTop = margin,
            ContentMarginRight = margin,
            ContentMarginBottom = margin
        };
    }
}
#endif
