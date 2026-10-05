#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameLogic;
using Godot;

public sealed partial class GraphComponentPanel
{
    private readonly Node _owner;
    private readonly Func<GraphAsset> _getGraph;
    private readonly Func<GraphAsset> _getBindingGraph;
    private readonly Action<string, string> _createCallNode;
    private readonly Action<string, string, bool> _createValueNode;
    private readonly ComponentTree _tree = new();
    private readonly Label _hostLabel = new();
    private readonly Label _summaryLabel = new();
    private readonly LineEdit _search = new();
    private readonly OptionButton _hostSelector = new();
    private readonly Button _refreshButton = new();
    private readonly List<Node> _hosts = new();
    private readonly List<Action> _treeActions = new();
    private GodotObject _source;
    private string _sceneContextKey = string.Empty;
    private bool _refreshQueued;
    public event Action<GodotObject> ComponentSelected;
    public event Action<Node> HostSelected;
    public Node SelectedHost => GetSelectedHost();

    private static readonly Color PanelBackground = new(0.075f, 0.085f, 0.105f, 0.98f);
    private static readonly Color PanelBorder = new(0.20f, 0.23f, 0.28f, 0.9f);
    private static readonly Color SubPanelBackground = new(0.055f, 0.065f, 0.08f, 0.95f);
    private static readonly Color PrimaryText = new(0.90f, 0.92f, 0.96f);
    private static readonly Color SecondaryText = new(0.57f, 0.62f, 0.70f);
    private static readonly Color AccentText = new(0.43f, 0.72f, 0.98f);
    private static readonly Color FunctionText = new(0.68f, 0.85f, 1.0f);
    private static readonly Color PropertyText = new(0.77f, 0.87f, 0.76f);

    public GraphComponentPanel(
        Node owner,
        Func<GraphAsset> getGraph,
        Action<string, string> createCallNode,
        Action<string, string, bool> createValueNode,
        Func<GraphAsset> getBindingGraph = null)
    {
        _owner = owner;
        _getGraph = getGraph;
        _getBindingGraph = getBindingGraph ?? getGraph;
        _createCallNode = createCallNode;
        _createValueNode = createValueNode;
        // Keep the browser visually distinct when it shares the window with the graph.
        var frame = new PanelContainer
        {
            // Keep the browser compact by default. The surrounding
            // HSplitContainer still lets the user widen it when member names
            // need more room.
            CustomMinimumSize = new Vector2(170, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        frame.AddThemeStyleboxOverride("panel", CreateStyle(PanelBackground, PanelBorder, 1, 5, 10));
        Root = frame;

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 7);
        frame.AddChild(content);

        var header = new HBoxContainer();
        var title = new Label
        {
            Text = "Components",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", PrimaryText);
        header.AddChild(title);

        _refreshButton.Icon = GetEditorIcon("Reload");
        _refreshButton.Text = _refreshButton.Icon == null ? "Refresh" : string.Empty;
        _refreshButton.Flat = true;
        _refreshButton.FocusMode = Control.FocusModeEnum.None;
        _refreshButton.CustomMinimumSize = new Vector2(28, 28);
        _refreshButton.TooltipText = "Refresh components from the current blueprint host";
        _refreshButton.Pressed += Refresh;
        header.AddChild(_refreshButton);
        content.AddChild(header);

        var divider = new HSeparator();
        divider.AddThemeColorOverride("color", PanelBorder);
        content.AddChild(divider);

        var hostPanel = new PanelContainer();
        hostPanel.AddThemeStyleboxOverride("panel", CreateStyle(SubPanelBackground, PanelBorder, 1, 4, 7));
        var hostContent = new VBoxContainer();
        hostContent.AddThemeConstantOverride("separation", 4);
        hostPanel.AddChild(hostContent);

        var hostCaption = new Label { Text = "HOST" };
        hostCaption.AddThemeFontSizeOverride("font_size", 10);
        hostCaption.AddThemeColorOverride("font_color", AccentText);
        hostContent.AddChild(hostCaption);

        _hostSelector.ItemSelected += _ => { Refresh(); HostSelected?.Invoke(GetSelectedHost()); };
        _hostSelector.CustomMinimumSize = new Vector2(0, 28);
        _hostSelector.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hostContent.AddChild(_hostSelector);

        _hostLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hostLabel.ClipText = true;
        _hostLabel.AddThemeFontSizeOverride("font_size", 11);
        _hostLabel.AddThemeColorOverride("font_color", SecondaryText);
        hostContent.AddChild(_hostLabel);
        content.AddChild(hostPanel);

        _search.PlaceholderText = "Search components or actions";
        _search.ClearButtonEnabled = true;
        _search.CustomMinimumSize = new Vector2(0, 30);
        _search.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _search.AddThemeColorOverride("font_color", PrimaryText);
        _search.AddThemeColorOverride("font_placeholder_color", SecondaryText);
        _search.AddThemeStyleboxOverride("normal", CreateStyle(SubPanelBackground, PanelBorder, 1, 4, 7));
        _search.AddThemeStyleboxOverride("focus", CreateStyle(SubPanelBackground, new Color(0.30f, 0.58f, 0.85f), 1, 4, 7));
        _search.TextChanged += _ => QueueRefreshList();
        content.AddChild(_search);

        _summaryLabel.AddThemeFontSizeOverride("font_size", 10);
        _summaryLabel.AddThemeColorOverride("font_color", SecondaryText);
        _summaryLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(_summaryLabel);

        _tree.DragDataFactory = item =>
        {
            Variant metadata = item?.GetMetadata(0) ?? default;
            return metadata;
        };
        _tree.HideRoot = true;
        _tree.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _tree.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _tree.CustomMinimumSize = new Vector2(0, 180);
        _tree.AddThemeColorOverride("font_color", PrimaryText);
        _tree.AddThemeColorOverride("font_hovered_color", PrimaryText);
        _tree.AddThemeColorOverride("font_selected_color", PrimaryText);
        _tree.AddThemeColorOverride("selection_color", new Color(0.15f, 0.31f, 0.48f, 0.90f));
        _tree.AddThemeColorOverride("guide_color", new Color(0.25f, 0.29f, 0.36f, 0.80f));
        _tree.AddThemeConstantOverride("item_start_padding", 7);
        _tree.AddThemeConstantOverride("item_end_padding", 7);
        _tree.AddThemeConstantOverride("button_margin", 4);
        _tree.AddThemeStyleboxOverride("panel", CreateStyle(SubPanelBackground, PanelBorder, 1, 4, 4));
        _tree.ItemActivated += OnTreeItemActivated;
        _tree.ItemSelected += OnComponentSelected;
        content.AddChild(_tree);
    }

    public Control Root { get; }

    public GodotObject Source => _source;
    public bool HasValidHost => GetSelectedHost() != null;

    public void SetSource(GodotObject source)
    {
        _source = IsValidObject(source) ? source : null;
    }

    public bool RefreshIfSceneChanged()
    {
        string sceneContextKey = GetSceneContextKey();
        bool sourceInvalidated = _source != null && !IsValidObject(_source);
        if (!sourceInvalidated && string.Equals(sceneContextKey, _sceneContextKey, StringComparison.Ordinal))
            return false;

        Refresh();
        return true;
    }

    public bool HasComponentType(string componentTypeName)
    {
        if (!GraphComponentRegistry.TryGet(componentTypeName, out GraphComponentTypeDescriptor descriptor))
            return false;
        return GetHostDescriptors(GetSelectedHost()).Any(available =>
            descriptor.ComponentType.IsAssignableFrom(available.ComponentType));
    }

    public IReadOnlyList<GraphComponentTypeDescriptor> GetAvailableDescriptors()
    {
        return GetHostDescriptors(GetSelectedHost());
    }

    public static bool SceneContainsGraphHost(Node sceneRoot, GraphAsset graph)
    {
        if (!IsValidObject(sceneRoot) || !IsValidObject(graph))
            return false;

        var hosts = new List<Node>();
        CollectHosts(sceneRoot, graph, hosts);
        return hosts.Count > 0;
    }

    private static void AddDescriptor(GodotObject component, List<GraphComponentTypeDescriptor> descriptors, HashSet<Type> seen)
    {
        GraphComponentTypeDescriptor descriptor = ResolveDescriptor(component);
        if (descriptor == null || !seen.Add(descriptor.ComponentType))
            return;
        descriptors.Add(descriptor);
    }

    public void Refresh()
    {
        Node previousHost = GetSelectedHost();
        string previousHostPath = GetSafePath(previousHost);
        if (!IsValidObject(_source))
            _source = null;

        _hosts.Clear();
        Node sceneRoot = GetEditedSceneRoot();
        GraphAsset graph = _getBindingGraph();
        if (!IsValidObject(graph))
            graph = null;

        // Only the currently edited scene is eligible. A graph opened from an
        // Inspector resource may not expose its component owner, so the exact
        // reference scan below remains the source of truth.
        if (_source is Node sourceNode &&
            IsInEditedScene(sourceNode, sceneRoot) &&
            IsComponentHost(sourceNode) &&
            ReferencesGraph(sourceNode, graph))
            AddHost(sourceNode, _hosts);
        else if (_source is GameObject2D sourceHost &&
                 IsInEditedScene(sourceHost, sceneRoot) &&
                 ReferencesGraph(sourceHost, graph))
            AddHost(sourceHost, _hosts);
        if (_source is Component2D sourceComponent &&
            sourceComponent.Owner is GameObject2D sourceOwner &&
            IsInEditedScene(sourceOwner, sceneRoot) &&
            ReferencesGraph(sourceOwner, graph))
            AddHost(sourceOwner, _hosts);

        if (sceneRoot != null)
            CollectHosts(sceneRoot, graph, _hosts);
        _sceneContextKey = GetSceneContextKey(sceneRoot);
        _hostSelector.Clear();
        for (int i = 0; i < _hosts.Count; i++)
            _hostSelector.AddItem(_hosts[i].GetPath().ToString(), i);
        _hostSelector.Visible = _hosts.Count > 1;
        if (_hosts.Count == 0)
        {
            _hostLabel.Text = "No component host exists in the edited scene.";
            RefreshList();
            return;
        }
        int selectedIndex = _hosts.FindIndex(host => host.GetPath().ToString() == previousHostPath);
        _hostSelector.Select(selectedIndex >= 0 ? selectedIndex : 0);
        _hostLabel.Text = _hosts[_hostSelector.Selected].GetPath().ToString();
        RefreshList();
    }

    private void RefreshList()
    {
        _refreshQueued = false;
        if (!GodotObject.IsInstanceValid(_tree) || _tree.IsQueuedForDeletion()) return;
        _tree.Clear();
        _treeActions.Clear();
        string query = _search.Text?.Trim() ?? string.Empty;
        GraphComponentRegistry.EnsureScanned();

        // Component nodes are intentionally instance-scoped, like Blueprint
        // component members. Without a host there is no valid component target.
        if (_hosts.Count == 0)
        {
            _hostLabel.Text = "No blueprint host found.";
            _summaryLabel.Text = "Open the owning GameObject2D scene, then press Refresh.";
            return;
        }

        Node host = GetSelectedHost();
        List<GodotObject> components = GetHostComponents(host);

        foreach (GodotObject component in components)
        {
            var descriptor = ResolveDescriptor(component);
            if (descriptor != null) AddDescriptorSection(descriptor, query, component);
        }
        _summaryLabel.Text = $"{components.Count} component(s) · Select for Details; double-click a member to add a node";
        if (_treeActions.Count == 0)
        {
            _summaryLabel.Text = components.Count == 0
                ? "No components found on this host."
                : "No matching component members.";
        }
    }

    private void QueueRefreshList()
    {
        if (_refreshQueued) return;
        _refreshQueued = true;
        Callable.From(RefreshList).CallDeferred();
    }

    private void AddDescriptorSection(GraphComponentTypeDescriptor descriptor, string query, GodotObject component)
    {
        if (descriptor == null)
            return;
        bool componentMatches = Matches(query, descriptor.DisplayName, descriptor.TypeName);
        TreeItem root = _tree.GetRoot() ?? _tree.CreateItem();
        TreeItem componentItem = _tree.CreateItem(root);
        componentItem.SetText(0, descriptor.DisplayName);
        componentItem.SetSelectable(0, true);
        componentItem.SetTooltipText(0, "Select to edit this component's properties; drag to bind an action");
        // Only serialized components are editable defaults. Runtime clones remain graph targets.
        if (TryGetProperty(GetSelectedHost(), "Components", out Variant exported)
            && exported.VariantType == Variant.Type.Array && exported.AsGodotArray().Contains(Variant.From(component)))
            componentItem.SetMeta("inspector_target", component);
        componentItem.SetMetadata(0, new Godot.Collections.Dictionary
        {
            ["kind"] = "graph_component",
            ["component_type"] = descriptor.TypeName,
            ["component_slot"] = 0
        });
        componentItem.SetCustomColor(0, AccentText);
        componentItem.SetCustomFontSize(0, 12);
        SetEditorIcon(componentItem, "Node");
        TreeItem functionsItem = null;
        TreeItem propertiesItem = null;
        bool hasMember = false;
        bool supportsValueNodes = _getGraph() is not GameLogic.HfsmGraphAsset;
        foreach (GraphComponentValueDescriptor value in descriptor.Values)
        {
            if (!supportsValueNodes)
                continue;
            if (!componentMatches && !Matches(query, value.DisplayName, value.MemberId))
                continue;
            hasMember = true;
            if (value.CanRead)
            {
                string typeName = descriptor.TypeName;
                string memberId = value.MemberId;
                propertiesItem ??= CreateCategory(componentItem, "Properties");
                AddTreeAction(propertiesItem, $"Get {value.DisplayName}", $"{memberId}  |  Get", "Property", PropertyText, () => _createValueNode?.Invoke(typeName, memberId, false));
            }
            if (value.CanWrite)
            {
                string typeName = descriptor.TypeName;
                string memberId = value.MemberId;
                propertiesItem ??= CreateCategory(componentItem, "Properties");
                AddTreeAction(propertiesItem, $"Set {value.DisplayName}", $"{memberId}  |  Set", "Property", PropertyText, () => _createValueNode?.Invoke(typeName, memberId, true));
            }
        }
        foreach (GraphComponentActionDescriptor action in descriptor.Actions)
        {
            if (!componentMatches && !Matches(query, action.DisplayName, action.MemberId))
                continue;
            hasMember = true;
            string typeName = descriptor.TypeName;
            string actionId = action.MemberId;
            functionsItem ??= CreateCategory(componentItem, "Functions");
            AddTreeAction(functionsItem, action.DisplayName, $"{action.MemberId}  |  Call", "Method", FunctionText, () => _createCallNode?.Invoke(typeName, actionId));
        }
        if (!hasMember && !componentMatches)
        {
            componentItem.Free();
            return;
        }
        if (!hasMember)
            componentItem.SetText(0, $"{descriptor.DisplayName}  (no graph members)");
    }

    private TreeItem CreateCategory(TreeItem parent, string text)
    {
        TreeItem category = _tree.CreateItem(parent);
        category.SetText(0, text);
        category.SetSelectable(0, false);
        category.SetMetadata(0, -1);
        category.SetCustomColor(0, SecondaryText);
        category.SetCustomFontSize(0, 10);
        SetEditorIcon(category, text == "Functions" ? "Method" : "Property");
        return category;
    }

    private void AddTreeAction(
        TreeItem parent,
        string label,
        string metadata,
        string iconName,
        Color textColor,
        Action action)
    {
        TreeItem item = _tree.CreateItem(parent);
        item.SetText(0, label);
        item.SetTooltipText(0, metadata);
        item.SetMetadata(0, _treeActions.Count);
        item.SetCustomColor(0, textColor);
        SetEditorIcon(item, iconName);
        _treeActions.Add(action);
    }

    private static StyleBoxFlat CreateStyle(
        Color background,
        Color border,
        int borderWidth,
        int cornerRadius,
        int contentMargin)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            ContentMarginLeft = contentMargin,
            ContentMarginTop = contentMargin,
            ContentMarginRight = contentMargin,
            ContentMarginBottom = contentMargin
        };
        return style;
    }

    private static Texture2D GetEditorIcon(string name)
    {
        try
        {
            // Godot's EditorIcons set does not contain the semantic names
            // "Method" and "Property". Use the stable Node icon for those
            // categories instead of asking the theme for missing entries.
            string iconName = name == "Method" || name == "Property" ? "Node" : name;
            return EditorInterface.Singleton.GetEditorTheme()?.GetIcon(iconName, "EditorIcons");
        }
        catch
        {
            return null;
        }
    }

    private static void SetEditorIcon(TreeItem item, string name)
    {
        Texture2D icon = GetEditorIcon(name);
        if (icon != null)
            item.SetIcon(0, icon);
    }

    private sealed partial class ComponentTree : Tree
    {
        public Func<TreeItem, Variant> DragDataFactory { get; set; }

        public override Variant _GetDragData(Vector2 atPosition)
        {
            TreeItem item = GetItemAtPosition(atPosition);
            Variant data = DragDataFactory?.Invoke(item) ?? default;
            if (data.VariantType != Variant.Type.Dictionary)
                return default;
            var preview = new Label
            {
                Text = item?.GetText(0) ?? "Component",
                CustomMinimumSize = new Vector2(160, 28)
            };
            SetDragPreview(preview);
            return data;
        }
    }

    private void OnTreeItemActivated()
    {
        TreeItem selected = _tree.GetSelected();
        if (selected == null)
            return;
        Variant metadata = selected.GetMetadata(0);
        if (metadata.VariantType != Variant.Type.Int)
            return;
        int index = metadata.AsInt32();
        if (index >= 0 && index < _treeActions.Count)
        {
            Action action = _treeActions[index];
            GraphAsset graph = _getGraph();
            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(Root) && !Root.IsQueuedForDeletion() && graph == _getGraph())
                    action?.Invoke();
            }).CallDeferred();
        }
    }

    private void OnComponentSelected()
    {
        TreeItem item = _tree.GetSelected();
        while (item != null && !item.HasMeta("inspector_target")) item = item.GetParent();
        if (item == null) return;
        GodotObject target = item.GetMeta("inspector_target").AsGodotObject();
        GraphAsset graph = _getGraph();
        Node host = GetSelectedHost();
        Callable.From(() =>
        {
            if (IsValidObject(target) && GodotObject.IsInstanceValid(Root) && !Root.IsQueuedForDeletion()
                && graph == _getGraph() && host == GetSelectedHost()) ComponentSelected?.Invoke(target);
        }).CallDeferred();
    }

    private static bool Matches(string query, params string[] values) =>
        string.IsNullOrWhiteSpace(query) || values.Any(value => value?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

    private static void AddComponent(GodotObject component, List<GodotObject> components, HashSet<string> componentKeys)
    {
        if (component == null)
            return;
        GraphComponentTypeDescriptor descriptor = ResolveDescriptor(component);
        string key = descriptor?.TypeName ?? component.GetInstanceId().ToString();
        if (componentKeys.Add(key))
            components.Add(component);
    }

    private static void AddHost(Node host, List<Node> hosts)
    {
        if (IsValidObject(host) && !hosts.Contains(host))
            hosts.Add(host);
    }

    private static Node GetEditedSceneRoot()
    {
        try
        {
            Node editedRoot = EditorInterface.Singleton.GetEditedSceneRoot();
            return IsValidObject(editedRoot) ? editedRoot : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetSceneContextKey() => GetSceneContextKey(GetEditedSceneRoot());

    private static string GetSceneContextKey(Node sceneRoot)
    {
        if (!IsValidObject(sceneRoot))
            return string.Empty;
        try
        {
            return $"{sceneRoot.GetInstanceId()}|{sceneRoot.SceneFilePath}|{sceneRoot.GetPath()}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsInEditedScene(Node node, Node sceneRoot)
    {
        if (!IsValidObject(node) || !IsValidObject(sceneRoot))
            return false;
        if (node == sceneRoot)
            return true;
        try
        {
            return sceneRoot.IsAncestorOf(node);
        }
        catch
        {
            return false;
        }
    }

    private Node GetSelectedHost()
    {
        if (_hosts.Count == 0)
            return null;
        Node host = _hosts[Mathf.Clamp(_hostSelector.Selected, 0, _hosts.Count - 1)];
        return IsValidObject(host) ? host : null;
    }

    private static List<GodotObject> GetHostComponents(Node host)
    {
        var components = new List<GodotObject>();
        var componentKeys = new HashSet<string>(StringComparer.Ordinal);
        if (IsValidObject(host) && TryGetProperty(host, "Components", out Variant componentArray) &&
            componentArray.VariantType == Variant.Type.Array)
        {
            foreach (Variant item in componentArray.AsGodotArray())
                AddComponent(item.AsGodotObject(), components, componentKeys);
        }
        if (host is GameObject2D gameObject && IsValidObject(gameObject))
        {
            // Running or tool-enabled objects may expose cloned component instances.
            foreach (Component2D component in gameObject.GetAllComponents())
                AddComponent(component, components, componentKeys);
        }
        return components;
    }

    private static List<GraphComponentTypeDescriptor> GetHostDescriptors(Node host)
    {
        var descriptors = new List<GraphComponentTypeDescriptor>();
        var seen = new HashSet<Type>();
        foreach (GodotObject component in GetHostComponents(host))
            AddDescriptor(component, descriptors, seen);
        return descriptors;
    }

    private static GraphComponentTypeDescriptor ResolveDescriptor(GodotObject component)
    {
        if (!IsValidObject(component))
            return null;
        if (component is Component2D typedComponent)
            return GraphComponentRegistry.Register(typedComponent.GetType());
        Script script = component?.GetScript().AsGodotObject() as Script;
        return script != null && GraphComponentRegistry.TryGetByScriptPath(script.ResourcePath, out GraphComponentTypeDescriptor descriptor)
            ? descriptor
            : null;
    }

    private static bool IsComponentHost(Node node)
    {
        if (!IsValidObject(node))
            return false;
        if (node is GameObject2D)
            return true;

        if (TryGetProperty(node, "Components", out Variant value) && value.VariantType == Variant.Type.Array)
            return true;

        // During C# reload a scene node may still be exposed as a native Node2D
        // wrapper. Its script path remains available and identifies the host.
        Script script = node?.GetScript().AsGodotObject() as Script;
        return string.Equals(
            script?.ResourcePath,
            "res://scripts/gamelogic/gameobject/GameObject2D.cs",
            StringComparison.Ordinal);
    }

    private static void CollectHosts(Node node, GraphAsset graph, List<Node> hosts)
    {
        if (node == null)
            return;
        if (IsComponentHost(node) && ReferencesGraph(node, graph))
            AddHost(node, hosts);
        foreach (Node child in node.GetChildren())
            CollectHosts(child, graph, hosts);
    }

    private static bool ReferencesGraph(Node host, GraphAsset graph)
    {
        if (graph == null || host == null)
            return false;
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        // Inspect the host itself as well as its component collection. This
        // covers scene wrappers that expose the exported array only after a
        // tool-script refresh.
        if (ContainsReference(host, graph, visited, 0))
            return true;
        foreach (GodotObject component in GetHostComponents(host))
        {
            if (ContainsReference(component, graph,
                new HashSet<object>(ReferenceEqualityComparer.Instance), 0))
                return true;
        }
        return false;
    }

    private static bool ContainsReference(object value, GraphAsset graph, HashSet<object> visited, int depth)
    {
        if (value == null || depth > 4)
            return false;
        if (value is GodotObject godotValue && !IsValidObject(godotValue))
            return false;
        if (ReferenceEquals(value, graph) || value is Resource resource && SameResource(resource, graph))
            return true;
        if (value is string || value.GetType().IsValueType || !visited.Add(value))
            return false;

        if (value is GodotObject godotObject)
        {
            foreach (Godot.Collections.Dictionary property in godotObject.GetPropertyList())
            {
                string propertyName = property["name"].AsString();
                if (string.IsNullOrWhiteSpace(propertyName) || propertyName == "script")
                    continue;
                Variant child;
                try { child = godotObject.Get(propertyName); } catch { continue; }
                if (ContainsVariant(child, graph, visited, depth + 1))
                    return true;
            }
            return false;
        }

        foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;
            object child;
            try { child = property.GetValue(value); } catch { continue; }
            if (child is System.Collections.IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                    if (ContainsReference(item, graph, visited, depth + 1)) return true;
            }
            else if (ContainsReference(child, graph, visited, depth + 1))
                return true;
        }
        return false;
    }

    private static bool ContainsVariant(Variant value, GraphAsset graph, HashSet<object> visited, int depth)
    {
        if (value.VariantType == Variant.Type.Object)
            return ContainsReference(value.AsGodotObject(), graph, visited, depth);
        if (value.VariantType != Variant.Type.Array)
            return false;
        foreach (Variant item in value.AsGodotArray())
        {
            if (ContainsVariant(item, graph, visited, depth))
                return true;
        }
        return false;
    }

    private static bool TryGetProperty(GodotObject target, string propertyName, out Variant value)
    {
        value = default;
        if (!IsValidObject(target))
            return false;
        foreach (Godot.Collections.Dictionary property in target.GetPropertyList())
        {
            string candidate = property["name"].AsString();
            if (!string.Equals(candidate, propertyName, StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                value = target.Get(candidate);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    private static bool SameResource(Resource left, Resource right)
    {
        if (!IsValidObject(left) || !IsValidObject(right))
            return false;
        string leftPath = left.ResourcePath;
        string rightPath = right.ResourcePath;
        return !string.IsNullOrWhiteSpace(leftPath) &&
               string.Equals(leftPath, rightPath, StringComparison.Ordinal);
    }

    private static bool IsValidObject(GodotObject value) =>
        value != null && GodotObject.IsInstanceValid(value);

    private static string GetSafePath(Node node)
    {
        if (!IsValidObject(node))
            return string.Empty;
        try
        {
            return node.GetPath().ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
#endif
