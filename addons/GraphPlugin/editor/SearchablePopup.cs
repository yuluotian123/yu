#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public class SearchablePopup<T> where T : class
{
    private readonly IReadOnlyList<T> _items;
    private readonly Func<T, string> _getLabel;
    private readonly Func<T, string> _getGroup;
    public event Action<T> OnItemSelected;
    private PopupPanel _popup;
    private LineEdit _searchBox;
    private Tree _tree;
    private List<T> _filteredItems;

    public SearchablePopup(IReadOnlyList<T> items, Func<T, string> getLabel, Func<T, string> getGroup = null)
    {
        _items = items;
        _getLabel = getLabel;
        _getGroup = getGroup;
    }

    public void ShowBelow(Control control)
    {
        _popup = new PopupPanel
        {
            Transient = false,
            Exclusive = false
        };

        BuildUI();

        var editorInterface = EditorInterface.Singleton;
        var baseControl = editorInterface.GetBaseControl();
        baseControl.AddChild(_popup);

        _searchBox.Text = "";
        RefreshTree();

        var screenPos = control.GetScreenPosition();
        _popup.Position = new Vector2I((int)screenPos.X, (int)(screenPos.Y + control.Size.Y));
        _popup.Size = new Vector2I(400, 300);
        _popup.Show();
        _searchBox.CallDeferred("grab_focus");
    }

    private void BuildUI()
    {
        var vbox = new VBoxContainer();
        _popup.AddChild(vbox);

        _searchBox = new LineEdit { PlaceholderText = "Search..." };
        _searchBox.TextChanged += _ => RefreshTree();
        _searchBox.GuiInput += OnSearchBoxInput;
        vbox.AddChild(_searchBox);

        _tree = new Tree
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HideRoot = true
        };
        _tree.ItemActivated += OnTreeItemActivated;
        vbox.AddChild(_tree);

        _popup.AboutToPopup += () =>
        {
            _searchBox.Text = "";
            _searchBox.GrabFocus();
            RefreshTree();
        };
    }

    private void RefreshTree()
    {
        _tree.Clear();
        var root = _tree.CreateItem();
        var query = _searchBox.Text;
        _filteredItems = string.IsNullOrWhiteSpace(query)
            ? _items.ToList()
            : _items
                .Where(x => FuzzyMatcher.Match(_getLabel(x), query))
                .OrderByDescending(x => FuzzyMatcher.Score(_getLabel(x), query))
                .ToList();

        if (_getGroup == null)
        {
            for (int i = 0; i < _filteredItems.Count; i++)
            {
                var treeItem = _tree.CreateItem(root);
                treeItem.SetText(0, _getLabel(_filteredItems[i]));
                treeItem.SetMetadata(0, i);
            }
        }
        else
        {
            var groups = _filteredItems.GroupBy(_getGroup).OrderBy(g => g.Key);
            int index = 0;
            foreach (var group in groups)
            {
                var groupItem = _tree.CreateItem(root);
                groupItem.SetText(0, group.Key ?? "(Ungrouped)");
                groupItem.SetSelectable(0, false);
                foreach (var item in group)
                {
                    var treeItem = _tree.CreateItem(groupItem);
                    treeItem.SetText(0, _getLabel(item));
                    treeItem.SetMetadata(0, index++);
                }
            }
        }
    }

    private void OnSearchBoxInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed)
        {
            if (key.Keycode == Key.Down)
            {
                _tree.GrabFocus();
                _tree.GetRoot()?.GetFirstChild()?.Select(0);
            }
            else if (key.Keycode == Key.Escape)
            {
                _popup.QueueFree();
            }
        }
    }

    private void OnTreeItemActivated()
    {
        var selected = _tree.GetSelected();
        if (selected != null)
        {
            int index = selected.GetMetadata(0).AsInt32();
            if (index >= 0 && index < _filteredItems.Count)
            {
                OnItemSelected?.Invoke(_filteredItems[index]);
                _popup.QueueFree();
            }
        }
    }
}

public static class FuzzyMatcher
{
    public static bool Match(string text, string query)
    {
        if (string.IsNullOrEmpty(query))
            return true;

        text = text.ToLower();
        query = query.ToLower();
        int pos = 0;
        foreach (var c in query)
        {
            pos = text.IndexOf(c, pos);
            if (pos < 0)
                return false;
            pos++;
        }
        return true;
    }

    public static int Score(string text, string query)
    {
        text = text.ToLower();
        query = query.ToLower();
        int score = 0;
        int pos = 0;
        foreach (var c in query)
        {
            int newPos = text.IndexOf(c, pos);
            if (newPos < 0)
                return 0;
            score += 100 - (newPos - pos);
            pos = newPos + 1;
        }
        return score;
    }
}
#endif
