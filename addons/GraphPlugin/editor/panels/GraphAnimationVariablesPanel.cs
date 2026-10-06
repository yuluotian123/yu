#if TOOLS
using System;
using System.Collections.Generic;
using GameLogic;
using Godot;

/// <summary>
/// Small AnimBP-style read-only view of the variables published by the
/// CharacterAnimationInstance and its locomotion state machine.
/// </summary>
public sealed class GraphAnimationVariablesPanel
{
    private readonly Func<GraphAsset> _getGraph;
    private readonly Tree _tree = new();
    private readonly Label _stateLabel = new();
    private readonly Label _animationLabel = new();
    private CharacterAnimationComponent3D _source;
    private bool _hostAvailable;
    private string _lastSnapshot = string.Empty;

    public GraphAnimationVariablesPanel(Func<GraphAsset> getGraph)
    {
        _getGraph = getGraph;
        var frame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(340, 0),
            SizeFlagsHorizontal = Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        frame.AddThemeStyleboxOverride("panel", CreateStyle(
            new Color(0.075f, 0.085f, 0.105f, 0.98f),
            new Color(0.20f, 0.23f, 0.28f, 0.9f), 1, 5, 10));
        Root = frame;

        var content = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 6);
        frame.AddChild(content);

        var title = new Label { Text = "AnimInstance Variables" };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.96f));
        content.AddChild(title);

        _stateLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _stateLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _stateLabel.AddThemeFontSizeOverride("font_size", 11);
        _stateLabel.AddThemeColorOverride("font_color", new Color(0.43f, 0.72f, 0.98f));
        content.AddChild(_stateLabel);
        _animationLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _animationLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _animationLabel.AddThemeFontSizeOverride("font_size", 11);
        _animationLabel.AddThemeColorOverride("font_color", new Color(0.68f, 0.85f, 1.0f));
        content.AddChild(_animationLabel);

        _tree.HideRoot = true;
        _tree.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _tree.Columns = 4;
        _tree.SetColumnTitle(0, "Variable");
        _tree.SetColumnTitle(1, "Type");
        _tree.SetColumnTitle(2, "Value");
        _tree.SetColumnTitle(3, "Source");
        _tree.SetColumnTitlesVisible(true);
        _tree.ScrollHorizontalEnabled = true;
        _tree.ScrollVerticalEnabled = true;
        // Fit all four columns inside the compact dock. Ignore cell text when
        // measuring columns; full names, values and sources remain in tooltips.
        _tree.SetColumnCustomMinimumWidth(0, 90);
        _tree.SetColumnCustomMinimumWidth(1, 50);
        _tree.SetColumnCustomMinimumWidth(2, 60);
        _tree.SetColumnCustomMinimumWidth(3, 70);
        for (int column = 0; column < _tree.Columns; column++)
            _tree.SetColumnClipContent(column, true);
        _tree.SetColumnExpand(0, true);
        _tree.SetColumnExpand(1, false);
        _tree.SetColumnExpand(2, true);
        _tree.SetColumnExpand(3, true);
        _tree.SetColumnExpandRatio(0, 3);
        _tree.SetColumnExpandRatio(2, 3);
        _tree.SetColumnExpandRatio(3, 4);
        _tree.SetColumnTitleTooltipText(0, "AnimInstance variable name");
        _tree.SetColumnTitleTooltipText(1, "Runtime value type");
        _tree.SetColumnTitleTooltipText(2, "Current value");
        _tree.SetColumnTitleTooltipText(3, "Component binding source");
        _tree.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.96f));
        _tree.AddThemeColorOverride("font_hovered_color", new Color(0.90f, 0.92f, 0.96f));
        _tree.AddThemeConstantOverride("item_start_padding", 5);
        _tree.AddThemeConstantOverride("item_end_padding", 5);
        content.AddChild(_tree);
        Refresh();
    }

    public Control Root { get; }

    public bool IsDebugVisible => Root.Visible;

    public void SetDebugVisible(bool visible)
    {
        if (Root.Visible == visible)
            return;
        Root.Visible = visible;
    }

    public void SetSource(GodotObject source)
    {
        _source = source as CharacterAnimationComponent3D;
        _lastSnapshot = string.Empty;
        Refresh();
    }

    public void SetHostAvailable(bool available)
    {
        if (_hostAvailable == available)
            return;
        _hostAvailable = available;
        Refresh();
    }

    public void RefreshIfChanged()
    {
        CharacterAnimationInstance instance = _hostAvailable ? _source?.AnimationInstance : null;
        GraphAsset graph = _getGraph?.Invoke();
        string snapshot = instance == null
            ? _hostAvailable ? $"editor|{graph?.GraphJson?.GetHashCode() ?? 0}" : string.Empty
            : $"{instance.CurrentStatePath}|{instance.ActiveAnimation}|{instance.ActiveRequestKey}|{instance.ActiveRequestPriority}|{instance.Variables.Count}";
        if (!string.Equals(snapshot, _lastSnapshot, StringComparison.Ordinal))
            Refresh();
    }

    public void Refresh()
    {
        _tree.Clear();
        _lastSnapshot = string.Empty;
        if (!_hostAvailable)
        {
            _stateLabel.Text = "State: <no AnimInstance host>";
            _animationLabel.Text = "Animation: <none>";
            return;
        }
        CharacterAnimationInstance instance = _hostAvailable ? _source?.AnimationInstance : null;
        GraphAsset graph = _getGraph?.Invoke();
        if (instance == null && graph is not HfsmGraphAsset)
        {
            _stateLabel.Text = "State: <no AnimInstance host>";
            _animationLabel.Text = "Animation: <none>";
            return;
        }

        _stateLabel.Text = instance == null
            ? "State: <editor preview>"
            : $"State: {Display(instance.CurrentStatePath)}";
        _animationLabel.Text = instance == null
            ? "Animation: <editor preview>"
            : $"Animation: {Display(instance.ActiveAnimation)}  |  Override: {Display(instance.ActiveRequestKey)} [P{instance.ActiveRequestPriority}]  |  Requests: {instance.ActiveRequestCount}";
        TreeItem root = _tree.CreateItem();
        if (instance != null)
        {
            foreach (KeyValuePair<string, object> variable in instance.Variables)
            {
                AddVariable(root, variable.Key, variable.Value?.GetType().Name ?? "null", variable.Value,
                    instance.VariableSources.TryGetValue(variable.Key, out string source) ? source : "Local");
            }
        }
        else
        {
            foreach (GraphBlackboardEntry entry in graph?.BlackboardEntries ?? new List<GraphBlackboardEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                    continue;
                string source = entry.Binding == null
                    ? "Local"
                    : $"Component: {entry.Binding.ComponentTypeName}.{entry.Binding.MemberId}";
                AddVariable(root, entry.Key, entry.Value?.ValueType?.Name ?? "unknown", entry.Value?.GetObjectValue(), source);
            }
        }

        _lastSnapshot = instance == null
            ? $"editor|{graph?.GraphJson?.GetHashCode() ?? 0}"
            : $"{instance.CurrentStatePath}|{instance.ActiveAnimation}|{instance.ActiveRequestKey}|{instance.ActiveRequestPriority}|{instance.Variables.Count}";
    }

    private void AddVariable(TreeItem root, string key, string type, object value, string source)
    {
        TreeItem item = _tree.CreateItem(root);
        item.SetText(0, key);
        item.SetText(1, type);
        item.SetText(2, value?.ToString() ?? "null");
        item.SetText(3, source);
        item.SetTooltipText(0, key);
        item.SetTooltipText(1, type);
        item.SetTooltipText(2, value?.ToString() ?? "null");
        item.SetTooltipText(3, source);
    }

    private static string Display(string value) => string.IsNullOrWhiteSpace(value) ? "<none>" : value;

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
