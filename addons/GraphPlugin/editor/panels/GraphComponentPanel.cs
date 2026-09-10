#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameLogic;
using Godot;

public sealed class GraphComponentPanel
{
    private readonly Window _owner;
    private readonly Func<GraphAsset> _getGraph;
    private readonly Action<string, string> _createCallNode;
    private readonly Action<string, string, bool> _createValueNode;
    private readonly VBoxContainer _list = new();
    private readonly Label _hostLabel = new();
    private readonly LineEdit _search = new();
    private readonly OptionButton _hostSelector = new();
    private readonly List<GameObject2D> _hosts = new();
    private GodotObject _source;

    public GraphComponentPanel(
        Window owner,
        Func<GraphAsset> getGraph,
        Action<string, string> createCallNode,
        Action<string, string, bool> createValueNode)
    {
        _owner = owner;
        _getGraph = getGraph;
        _createCallNode = createCallNode;
        _createValueNode = createValueNode;
        // Keep the browser visible when it shares the right side with the inspector.
        Root = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(280, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        Root.AddThemeConstantOverride("separation", 6);
        Root.AddChild(new Label { Text = "Components" });
        _hostSelector.ItemSelected += _ => Refresh();
        Root.AddChild(_hostSelector);
        _search.PlaceholderText = "Search components or actions";
        _search.TextChanged += _ => RefreshList();
        Root.AddChild(_search);
        _hostLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Root.AddChild(_hostLabel);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_list);
        Root.AddChild(scroll);
    }

    public Control Root { get; }

    public void SetSource(GodotObject source)
    {
        _source = source;
    }

    public IReadOnlyList<GraphComponentTypeDescriptor> GetAvailableDescriptors()
    {
        var descriptors = new List<GraphComponentTypeDescriptor>();
        var seen = new HashSet<Type>();
        foreach (GameObject2D host in _hosts)
        {
            if (host?.Components != null)
                foreach (Component2D component in host.Components)
                    AddDescriptor(component, descriptors, seen);
            if (host != null)
                foreach (Component2D component in host.GetAllComponents())
                    AddDescriptor(component, descriptors, seen);
        }

        return descriptors;
    }

    private static void AddDescriptor(Component2D component, List<GraphComponentTypeDescriptor> descriptors, HashSet<Type> seen)
    {
        if (component == null || !seen.Add(component.GetType()))
            return;
        GraphComponentTypeDescriptor descriptor = GraphComponentRegistry.Register(component.GetType());
        if (descriptor != null)
            descriptors.Add(descriptor);
    }

    public void Refresh()
    {
        _hosts.Clear();
        Node sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
        GraphAsset graph = _getGraph();
        CollectHosts(sceneRoot, graph, _hosts);
        if (_hosts.Count == 0)
        {
            // Inspector and scene resources can be different managed wrappers for the same .tres.
            // Keep the component browser usable when the reference cannot be resolved exactly.
            CollectAllHosts(sceneRoot, _hosts);
        }
        GodotObject editedObject = EditorInterface.Singleton.GetInspector()?.GetEditedObject();
        if (editedObject is GameObject2D editedHost && !_hosts.Contains(editedHost))
            _hosts.Add(editedHost);
        if (_source is GameObject2D sourceHost && !_hosts.Contains(sourceHost))
            _hosts.Insert(0, sourceHost);
        else if (_source is Component2D sourceComponent && sourceComponent.Owner is GameObject2D sourceOwner &&
                 !_hosts.Contains(sourceOwner))
            _hosts.Insert(0, sourceOwner);
        _hostSelector.Clear();
        for (int i = 0; i < _hosts.Count; i++)
            _hostSelector.AddItem(_hosts[i].GetPath().ToString(), i);
        _hostSelector.Visible = _hosts.Count > 1;
        if (_hosts.Count == 0)
        {
            _hostLabel.Text = "No GameObject2D host exists in the edited scene.";
            RefreshList();
            return;
        }
        _hostSelector.Select(Mathf.Clamp(_hostSelector.Selected, 0, _hosts.Count - 1));
        _hostLabel.Text = $"Host: {_hosts[_hostSelector.Selected].GetPath()}";
        RefreshList();
    }

    private void RefreshList()
    {
        ClearList();
        string query = _search.Text?.Trim() ?? string.Empty;
        GraphComponentRegistry.EnsureScanned();

        // Component nodes are intentionally instance-scoped, like Blueprint
        // component members. Without a host there is no valid component target.
        if (_hosts.Count == 0)
        {
            _list.AddChild(new Label { Text = "Select/open a GameObject2D host to use component nodes." });
            return;
        }

        GameObject2D host = _hosts[Mathf.Clamp(_hostSelector.Selected, 0, _hosts.Count - 1)];
        var components = new List<Component2D>();
        var componentTypes = new HashSet<Type>();
        if (host.Components != null)
        {
            foreach (Component2D component in host.Components)
                AddComponent(component, components, componentTypes);
        }
        // In an instantiated/editing scene GameObject2D may expose cloned runtime
        // components while the exported resource array is empty or stale.
        foreach (Component2D component in host.GetAllComponents())
            AddComponent(component, components, componentTypes);

        foreach (Component2D component in components)
        {
            if (component == null)
                continue;
            // Register the concrete instance type as well as relying on the initial
            // assembly scan. This handles C# hot reload and resources loaded late by
            // the editor.
            GraphComponentTypeDescriptor descriptor = GraphComponentRegistry.Register(component.GetType());
            if (descriptor == null)
                continue;
            AddDescriptorSection(descriptor, query);
        }
        if (_list.GetChildCount() == 0)
        {
            string message = components.Count == 0
                ? "No components found on this host."
                : "No matching component members.";
            _list.AddChild(new Label { Text = message });
        }
    }

    private void AddDescriptorSection(GraphComponentTypeDescriptor descriptor, string query)
    {
        if (descriptor == null)
            return;
        bool componentMatches = Matches(query, descriptor.DisplayName, descriptor.TypeName);
        var section = new VBoxContainer();
        section.AddChild(new Label { Text = descriptor.DisplayName });
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
                var getButton = new Button { Text = $"  Get  {value.DisplayName} ({value.MemberId})" };
                string typeName = descriptor.TypeName;
                string memberId = value.MemberId;
                getButton.Pressed += () => _createValueNode?.Invoke(typeName, memberId, false);
                section.AddChild(getButton);
            }
            if (value.CanWrite)
            {
                var setButton = new Button { Text = $"  Set  {value.DisplayName} ({value.MemberId})" };
                string typeName = descriptor.TypeName;
                string memberId = value.MemberId;
                setButton.Pressed += () => _createValueNode?.Invoke(typeName, memberId, true);
                section.AddChild(setButton);
            }
        }
        foreach (GraphComponentActionDescriptor action in descriptor.Actions)
        {
            if (!componentMatches && !Matches(query, action.DisplayName, action.MemberId))
                continue;
            hasMember = true;
            var button = new Button { Text = $"  Call  {action.DisplayName} ({action.MemberId})" };
            string typeName = descriptor.TypeName;
            string actionId = action.MemberId;
            button.Pressed += () => _createCallNode?.Invoke(typeName, actionId);
            section.AddChild(button);
        }
        if (!hasMember && !componentMatches)
        {
            section.QueueFree();
            return;
        }
        if (!hasMember)
            section.AddChild(new Label { Text = "  No graph members" });
        _list.AddChild(section);
    }

    private void ClearList()
    {
        foreach (Node child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static bool Matches(string query, params string[] values) =>
        string.IsNullOrWhiteSpace(query) || values.Any(value => value?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

    private static void AddComponent(Component2D component, List<Component2D> components, HashSet<Type> componentTypes)
    {
        if (component == null || !componentTypes.Add(component.GetType()))
            return;
        components.Add(component);
    }

    private static void CollectHosts(Node node, GraphAsset graph, List<GameObject2D> hosts)
    {
        if (node == null)
            return;
        if (node is GameObject2D gameObject && ReferencesGraph(gameObject, graph))
            hosts.Add(gameObject);
        foreach (Node child in node.GetChildren())
            CollectHosts(child, graph, hosts);
    }

    private static void CollectAllHosts(Node node, List<GameObject2D> hosts)
    {
        if (node == null)
            return;
        if (node is GameObject2D gameObject && !hosts.Contains(gameObject))
            hosts.Add(gameObject);
        foreach (Node child in node.GetChildren())
            CollectAllHosts(child, hosts);
    }

    private static bool ReferencesGraph(GameObject2D host, GraphAsset graph)
    {
        if (graph == null || host.Components == null)
            return false;
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (Component2D component in host.Components)
        {
            if (ContainsReference(component, graph, visited, 0))
                return true;
        }
        return false;
    }

    private static bool ContainsReference(object value, GraphAsset graph, HashSet<object> visited, int depth)
    {
        if (value == null || depth > 4)
            return false;
        if (ReferenceEquals(value, graph) || SameResource(value as GraphAsset, graph))
            return true;
        if (value is string || value.GetType().IsValueType || !visited.Add(value))
            return false;
        foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;
            object child;
            try { child = property.GetValue(value); } catch { continue; }
            if (child is System.Collections.IEnumerable enumerable && child is not Godot.Resource)
            {
                foreach (object item in enumerable)
                    if (ContainsReference(item, graph, visited, depth + 1)) return true;
            }
            else if (ContainsReference(child, graph, visited, depth + 1))
                return true;
        }
        return false;
    }

    private static bool SameResource(GraphAsset left, GraphAsset right)
    {
        if (left == null || right == null)
            return false;
        string leftPath = left.ResourcePath;
        string rightPath = right.ResourcePath;
        return !string.IsNullOrWhiteSpace(leftPath) &&
               string.Equals(leftPath, rightPath, StringComparison.Ordinal);
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
#endif
