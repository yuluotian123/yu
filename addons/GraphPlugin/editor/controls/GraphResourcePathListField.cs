#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class GraphResourcePathListField : VBoxContainer
{
    private readonly Type _resourceType;
    private readonly Action<List<string>> _pathsChanged;
    private readonly Func<Resource, string> _getResourceLabel;
    private readonly List<string> _paths;
    private readonly List<GraphResourcePathField> _fields = new();
    private readonly VBoxContainer _rows;

    public GraphResourcePathListField(
        Type resourceType,
        IEnumerable<string> paths,
        Action<List<string>> pathsChanged,
        Func<Resource, string> getResourceLabel = null)
    {
        _resourceType = resourceType ?? typeof(Resource);
        _pathsChanged = pathsChanged;
        _getResourceLabel = getResourceLabel;
        _paths = paths?.ToList() ?? new List<string>();

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 6);

        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(_rows);

        var addButton = new Button
        {
            Text = "+ Add Resource",
            TooltipText = $"Add {_resourceType.Name}"
        };
        addButton.Pressed += () =>
        {
            _paths.Add(string.Empty);
            RebuildRows();
            _fields[^1].OpenPicker();
        };
        AddChild(addButton);

        RebuildRows();
    }

    private void RebuildRows()
    {
        foreach (Node child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }

        _fields.Clear();

        for (int i = 0; i < _paths.Count; i++)
            _rows.AddChild(BuildRow(i));
    }

    private Control BuildRow(int index)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 4);

        var field = new GraphResourcePathField(
            _resourceType,
            _paths[index],
            path =>
            {
                _paths[index] = path;
                NotifyChanged();
            },
            _getResourceLabel)
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _fields.Add(field);
        row.AddChild(field);

        row.AddChild(BuildButton("^", "Move up", index > 0, () => Move(index, index - 1)));
        row.AddChild(BuildButton("v", "Move down", index < _paths.Count - 1, () => Move(index, index + 1)));
        row.AddChild(BuildButton("-", "Remove resource", true, () => Remove(index)));
        return row;
    }

    private static Button BuildButton(string text, string tooltip, bool enabled, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            Disabled = !enabled,
            CustomMinimumSize = new Vector2(30f, 0f)
        };
        button.Pressed += pressed;
        return button;
    }

    private void Move(int from, int to)
    {
        if (from < 0 || from >= _paths.Count || to < 0 || to >= _paths.Count)
            return;

        string value = _paths[from];
        _paths.RemoveAt(from);
        _paths.Insert(to, value);
        NotifyChanged();
        RebuildRows();
    }

    private void Remove(int index)
    {
        if (index < 0 || index >= _paths.Count)
            return;

        _paths.RemoveAt(index);
        NotifyChanged();
        RebuildRows();
    }

    private void NotifyChanged()
    {
        _pathsChanged?.Invoke(_paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .ToList());
    }
}
#endif
