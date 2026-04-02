#if TOOLS
using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

/// <summary>
/// 通用可排序列表控件构建器。
/// 泛型参数 T 为列表元素类型。
/// 
/// 使用方式：
///   var builder = new ReorderableListControl&lt;MyItem&gt;(
///       items,                               // 数据源（List&lt;T&gt; 或任何 IList&lt;T&gt;）
///       item  => item.CreateEditUi(),        // 为每个元素生成参数 UI 的委托
///       types => types.Name,                 // 在下拉框中显示的名称（可选）
///       availableTypes                       // 可添加的类型列表（可选）
///   );
///   var control = builder.Build();          // 构建并返回 VBoxContainer
/// </summary>
public class ReorderableListControl<T> where T : class
{
    // ── 数据 ────────────────────────────────────────────────────────────────
    private readonly IList<T> _items;

    // ── 委托 ────────────────────────────────────────────────────────────────
    /// <summary>为列表中的每个元素生成参数编辑 UI</summary>
    private readonly Func<T, Control> _buildItemUi;

    /// <summary>为每个元素生成标题文字（可选，默认使用类型名）</summary>
    private readonly Func<T, string> _getItemLabel;

    /// <summary>创建新元素的工厂方法（用于「添加」按钮）</summary>
    private readonly Func<Type, T> _factory;

    /// <summary>可选：可添加的类型列表</summary>
    private readonly IReadOnlyList<Type> _availableTypes;

    /// <summary>当列表内容发生变化时触发</summary>
    public event Action ListChanged;

    // ── 内部 UI ──────────────────────────────────────────────────────────────
    private VBoxContainer _root;
    private VBoxContainer _itemsContainer;

    // ── 构造 ────────────────────────────────────────────────────────────────

    /// <param name="items">数据源引用（直接修改该列表）</param>
    /// <param name="buildItemUi">为元素生成参数 UI 的委托，可为 null（不显示参数区域）</param>
    /// <param name="getItemLabel">生成每行标题文字的委托，可为 null（默认用类型名）</param>
    /// <param name="availableTypes">下拉添加时的候选类型，为 null 或空则不显示「添加」行</param>
    /// <param name="factory">根据类型创建新元素，为 null 时使用 Activator.CreateInstance</param>
    public ReorderableListControl(
        IList<T> items,
        Func<T, Control> buildItemUi = null,
        Func<T, string> getItemLabel = null,
        IReadOnlyList<Type> availableTypes = null,
        Func<Type, T> factory = null)
    {
        _items = items;
        _buildItemUi = buildItemUi;
        _getItemLabel = getItemLabel ?? (item => item.GetType().Name);
        _availableTypes = availableTypes;
        _factory = factory ?? (type => (T)Activator.CreateInstance(type));
    }

    // ── 公开方法 ───────────────────────────────────────────────────────────

    /// <summary>构建并返回可排序列表控件</summary>
    public VBoxContainer Build()
    {
        _root = new VBoxContainer();
        _root.AddThemeConstantOverride("separation", 4);

        // 列表容器
        _itemsContainer = new VBoxContainer();
        _itemsContainer.AddThemeConstantOverride("separation", 2);
        _root.AddChild(_itemsContainer);

        RefreshItemsContainer();

        // 添加行（仅当提供了候选类型时）
        if (_availableTypes != null && _availableTypes.Count > 0)
        {
            _root.AddChild(new HSeparator());
            _root.AddChild(BuildAddRow());
        }

        return _root;
    }

    /// <summary>手动刷新整个列表 UI（外部修改数据后可调用）</summary>
    public void Refresh() => RefreshItemsContainer();

    // ── 私有构建 ───────────────────────────────────────────────────────────

    private void RefreshItemsContainer()
    {
        foreach (var child in _itemsContainer.GetChildren())
        {
            _itemsContainer.RemoveChild(child);
            child.QueueFree();
        }

        if (_items.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "（暂无元素）",
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

        // ── 标题行 ──────────────────────────────────────────────────────
        var header = new HBoxContainer();
        row.AddChild(header);

        var label = new Label
        {
            Text = $"[{index}]  {_getItemLabel(item)}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.AddChild(label);

        // ↑ 上移
        var upBtn = new Button
        {
            Text = "↑",
            Disabled = index == 0,
            TooltipText = "上移",
            CustomMinimumSize = new Vector2(28, 0)
        };
        upBtn.Pressed += () =>
        {
            SwapItems(index, index - 1);
            RefreshItemsContainer();
            ListChanged?.Invoke();
        };
        header.AddChild(upBtn);

        // ↓ 下移
        var downBtn = new Button
        {
            Text = "↓",
            Disabled = index == _items.Count - 1,
            TooltipText = "下移",
            CustomMinimumSize = new Vector2(28, 0)
        };
        downBtn.Pressed += () =>
        {
            SwapItems(index, index + 1);
            RefreshItemsContainer();
            ListChanged?.Invoke();
        };
        header.AddChild(downBtn);

        // ✕ 删除
        var delBtn = new Button
        {
            Text = "✕",
            TooltipText = "删除",
            CustomMinimumSize = new Vector2(28, 0)
        };
        delBtn.AddThemeColorOverride("font_color", new Color(1f, 0.35f, 0.35f));
        delBtn.Pressed += () =>
        {
            _items.RemoveAt(index);
            RefreshItemsContainer();
            ListChanged?.Invoke();
        };
        header.AddChild(delBtn);

        // ── 参数 UI ─────────────────────────────────────────────────────
        var paramUi = _buildItemUi?.Invoke(item);
        if (paramUi != null)
        {
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 14);
            margin.AddChild(paramUi);
            row.AddChild(margin);
        }

        row.AddChild(new HSeparator());
        return row;
    }

    private Control BuildAddRow()
    {
        var addRow = new HBoxContainer();

        var selectBtn = new Button { Text = "选择类型...", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        selectBtn.Pressed += () =>
        {
            var popup = new SearchablePopup<Type>(
                _availableTypes,
                type => type.Name,
                type => type.Namespace
            );
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
        if (a < 0 || b < 0 || a >= _items.Count || b >= _items.Count) return;
        (_items[a], _items[b]) = (_items[b], _items[a]);
    }
}

/// <summary>
/// 静态工具类：通过反射扫描子类型，结果按基类缓存，整个编辑器生命周期只扫描一次。
/// </summary>
public static class SubTypeCache
{
    private static readonly Dictionary<Type, IReadOnlyList<Type>> _cache = new();

    /// <summary>
    /// 获取所有继承自 <typeparamref name="TBase"/> 的具体（非抽象）类型列表。
    /// 结果被静态缓存，多次调用不会重复反射。
    /// </summary>
    public static IReadOnlyList<Type> GetSubTypes<TBase>() where TBase : class
        => GetSubTypes(typeof(TBase));

    /// <summary>
    /// 获取所有继承自 <paramref name="baseType"/> 的具体（非抽象）类型列表。
    /// </summary>
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
                // 跳过无法加载的程序集
            }
        }

        var readOnly = result.AsReadOnly();
        _cache[baseType] = readOnly;
        return readOnly;
    }
}
#endif
