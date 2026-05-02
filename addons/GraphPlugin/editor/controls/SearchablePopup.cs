#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// 编辑器通用搜索弹窗，支持模糊搜索和分组显示。
/// </summary>
public class SearchablePopup<T> where T : class
{
    private readonly IReadOnlyList<T> _items;
    private readonly Func<T, string> _getLabel;
    private readonly Func<T, string> _getGroup;
    private readonly Func<T, string> _getSearchText;
    public event Action<T> OnItemSelected;
    private PopupPanel _popup;
    private LineEdit _searchBox;
    private Tree _tree;
    private List<T> _filteredItems;
    private readonly List<T> _treeItems = new();

    /// <summary>
    /// 创建搜索弹窗。
    /// </summary>
    /// <param name="items">可选择的数据源。</param>
    /// <param name="getLabel">显示文本。</param>
    /// <param name="getGroup">可选分组文本。</param>
    /// <param name="getSearchText">可选额外搜索关键字，不直接显示。</param>
    public SearchablePopup(
        IReadOnlyList<T> items,
        Func<T, string> getLabel,
        Func<T, string> getGroup = null,
        Func<T, string> getSearchText = null)
    {
        _items = items;
        _getLabel = getLabel;
        _getGroup = getGroup;
        _getSearchText = getSearchText;
    }

    /// <summary>把弹窗显示到指定控件下方。</summary>
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
        _treeItems.Clear();
        var root = _tree.CreateItem();
        var query = _searchBox.Text;
        _filteredItems = string.IsNullOrWhiteSpace(query)
            ? _items.ToList()
            : _items
                .Where(x => FuzzyMatcher.Match(BuildSearchText(x), query))
                .OrderByDescending(x => FuzzyMatcher.Score(BuildSearchText(x), query))
                .ToList();

        if (_getGroup == null)
        {
            for (int i = 0; i < _filteredItems.Count; i++)
            {
                CreateSelectableItem(root, _filteredItems[i]);
            }
        }
        else
        {
            var groups = _filteredItems.GroupBy(_getGroup).OrderBy(g => g.Key);
            foreach (var group in groups)
            {
                var groupItem = _tree.CreateItem(root);
                groupItem.SetText(0, group.Key ?? "(Ungrouped)");
                groupItem.SetSelectable(0, false);
                foreach (var item in group)
                    CreateSelectableItem(groupItem, item);
            }
        }
    }

    private void CreateSelectableItem(TreeItem parent, T item)
    {
        var treeItem = _tree.CreateItem(parent);
        treeItem.SetText(0, _getLabel(item));
        treeItem.SetMetadata(0, _treeItems.Count);
        _treeItems.Add(item);
    }

    private string BuildSearchText(T item)
    {
        string label = _getLabel(item) ?? string.Empty;
        string group = _getGroup?.Invoke(item) ?? string.Empty;
        string extra = _getSearchText?.Invoke(item) ?? string.Empty;
        return $"{label} {group} {extra}";
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
            if (index >= 0 && index < _treeItems.Count)
            {
                OnItemSelected?.Invoke(_treeItems[index]);
                _popup.QueueFree();
            }
        }
    }
}

/// <summary>
/// 简单的子序列模糊匹配工具。
/// </summary>
public static class FuzzyMatcher
{
    /// <summary>判断 query 是否可以按顺序匹配 text。</summary>
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

    /// <summary>计算匹配分数，字符越紧凑分数越高。</summary>
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
