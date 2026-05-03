#if TOOLS
using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

/// <summary>
/// Builds a reorderable editor list for a mutable item collection.
/// </summary>
public class ReorderableListControl<T> where T : class
{
    private readonly IList<T> _items;
    private readonly Func<T, Control> _buildItemUi;
    private readonly Func<T, string> _getItemLabel;
    private readonly Func<Type, T> _factory;
    private readonly IReadOnlyList<Type> _availableTypes;

    public event Action ListChanged;

    private VBoxContainer _root;
    private VBoxContainer _itemsContainer;
    private readonly Dictionary<int, bool> _itemExpandedStates = new();
    private bool _listExpanded = true;

    public ReorderableListControl(
        IList<T> items,
        Func<T, Control> buildItemUi = null,
        Func<T, string> getItemLabel = null,
        IReadOnlyList<Type> availableTypes = null,
        Func<Type, T> factory = null,
        bool defaultItemExpanded = true,
        bool defaultListExpanded = true)
    {
        _items = items;
        _buildItemUi = buildItemUi;
        _getItemLabel = getItemLabel ?? (item => item.GetType().Name);
        _availableTypes = availableTypes;
        _factory = factory ?? (type => (T)Activator.CreateInstance(type));
        _listExpanded = defaultListExpanded;
        for (int i = 0; i < items.Count; i++)
            _itemExpandedStates[i] = defaultItemExpanded;
    }

    public VBoxContainer Build()
    {
        _root = new VBoxContainer();
        _root.AddThemeConstantOverride("separation", 4);

        var globalHeader = new HBoxContainer();
        var collapseAllBtn = new Button
        {
            Text = _listExpanded ? "Collapse List" : "Expand List",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        collapseAllBtn.Pressed += () =>
        {
            _listExpanded = !_listExpanded;
            collapseAllBtn.Text = _listExpanded ? "Collapse List" : "Expand List";
            _itemsContainer.Visible = _listExpanded;
        };
        globalHeader.AddChild(collapseAllBtn);
        _root.AddChild(globalHeader);

        _itemsContainer = new VBoxContainer();
        _itemsContainer.AddThemeConstantOverride("separation", 2);
        _itemsContainer.Visible = _listExpanded;
        _root.AddChild(_itemsContainer);

        RefreshItemsContainer();

        if (_availableTypes != null && _availableTypes.Count > 0)
        {
            _root.AddChild(new HSeparator());
            _root.AddChild(BuildAddRow());
        }

        return _root;
    }

    public void Refresh() => RefreshItemsContainer();

    private void RefreshItemsContainer()
    {
        foreach (var child in _itemsContainer.GetChildren())
        {
            GraphEditorSignalCleanup.DisconnectSubtree(child);
            _itemsContainer.RemoveChild(child);
            child.QueueFree();
        }

        if (_items.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "(empty)",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
            _itemsContainer.AddChild(emptyLabel);
        }
        else
        {
            for (int i = 0; i < _items.Count; i++)
                _itemsContainer.AddChild(BuildItemRow(i));
        }

        _itemsContainer.UpdateMinimumSize();
        _root.UpdateMinimumSize();
        _itemsContainer.QueueRedraw();
        _root.QueueRedraw();
    }

    private Control BuildItemRow(int index)
    {
        var item = _items[index];
        var row = new VBoxContainer();
        row.AddThemeConstantOverride("separation", 2);

        var header = new HBoxContainer();
        row.AddChild(header);

        if (!_itemExpandedStates.ContainsKey(index))
            _itemExpandedStates[index] = true;

        var collapseBtn = new Button
        {
            Text = _itemExpandedStates[index] ? "v" : ">",
            CustomMinimumSize = new Vector2(28, 0),
            TooltipText = "Collapse/expand"
        };
        header.AddChild(collapseBtn);

        var label = new Label
        {
            Text = $"[{index}]  {_getItemLabel(item)}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.AddChild(label);

        var upBtn = new Button
        {
            Text = "Up",
            Disabled = index == 0,
            TooltipText = "Move up",
            CustomMinimumSize = new Vector2(44, 0)
        };
        upBtn.Pressed += () =>
        {
            SwapItems(index, index - 1);
            RefreshItemsContainer();
            ListChanged?.Invoke();
        };
        header.AddChild(upBtn);

        var downBtn = new Button
        {
            Text = "Down",
            Disabled = index == _items.Count - 1,
            TooltipText = "Move down",
            CustomMinimumSize = new Vector2(54, 0)
        };
        downBtn.Pressed += () =>
        {
            SwapItems(index, index + 1);
            RefreshItemsContainer();
            ListChanged?.Invoke();
        };
        header.AddChild(downBtn);

        var delBtn = new Button
        {
            Text = "Delete",
            TooltipText = "Delete",
            CustomMinimumSize = new Vector2(58, 0)
        };
        delBtn.AddThemeColorOverride("font_color", new Color(1f, 0.35f, 0.35f));
        delBtn.Pressed += () =>
        {
            _items.RemoveAt(index);
            RefreshItemsContainer();
            ListChanged?.Invoke();
        };
        header.AddChild(delBtn);

        var paramUi = _buildItemUi?.Invoke(item);
        if (paramUi != null)
        {
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 14);
            margin.AddChild(paramUi);
            margin.Visible = _itemExpandedStates[index];
            row.AddChild(margin);

            collapseBtn.Pressed += () =>
            {
                _itemExpandedStates[index] = !_itemExpandedStates[index];
                collapseBtn.Text = _itemExpandedStates[index] ? "v" : ">";
                margin.Visible = _itemExpandedStates[index];
            };
        }
        else
        {
            collapseBtn.Visible = false;
        }

        row.AddChild(new HSeparator());
        return row;
    }

    private Control BuildAddRow()
    {
        var addRow = new HBoxContainer();

        var selectBtn = new Button
        {
            Text = "Select Type...",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        selectBtn.Pressed += () =>
        {
            var popup = new SearchablePopup<Type>(
                _availableTypes,
                type => type.Name,
                type => type.Namespace);
            popup.OnItemSelected += type =>
            {
                var newItem = _factory(type);
                _items.Add(newItem);
                RefreshItemsContainer();
                ListChanged?.Invoke();
            };
            popup.ShowBelow(selectBtn);
        };
        addRow.AddChild(selectBtn);

        return addRow;
    }

    private void SwapItems(int a, int b)
    {
        if (a < 0 || b < 0 || a >= _items.Count || b >= _items.Count)
            return;

        (_items[a], _items[b]) = (_items[b], _items[a]);
    }
}

/// <summary>
/// Reflection helper that caches concrete subclasses for the editor lifetime.
/// </summary>
public static class SubTypeCache
{
    private static readonly Dictionary<Type, IReadOnlyList<Type>> _cache = new();

    public static IReadOnlyList<Type> GetSubTypes<TBase>() where TBase : class
        => GetSubTypes(typeof(TBase));

    public static IReadOnlyList<Type> GetSubTypes(Type baseType)
    {
        if (_cache.TryGetValue(baseType, out var cached))
            return cached;

        var result = new List<Type>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsClass && !type.IsAbstract && baseType.IsAssignableFrom(type))
                        result.Add(type);
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Skip assemblies that can not be reflected.
            }
        }

        var readOnly = result.AsReadOnly();
        _cache[baseType] = readOnly;
        return readOnly;
    }
}
#endif
