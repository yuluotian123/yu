#if TOOLS
using System;
using Godot;

/// <summary>
/// 编辑器资源槽。只支持点击选择和清空，不处理拖拽。
/// </summary>
/// <remarks>
/// GraphJson 仍然只保存稳定的 <c>res://</c> 路径；控件负责把资源选择结果转换成路径写回节点数据。
/// 独立 Window 下跨面板拖拽会触发焦点和隐藏问题，因此这里刻意不实现 drag/drop。
/// </remarks>
public partial class GraphResourcePathField : HBoxContainer
{
    private readonly Type _resourceType;
    private readonly Action<string> _pathChanged;
    private readonly Func<Resource, string> _getResourceLabel;
    private readonly Button _slotButton;
    private readonly Button _clearButton;
    private EditorFileDialog _dialog;
    private string _path;

    public GraphResourcePathField(
        Type resourceType,
        string path,
        Action<string> pathChanged,
        Func<Resource, string> getResourceLabel = null)
    {
        _resourceType = resourceType ?? typeof(Resource);
        _pathChanged = pathChanged;
        _getResourceLabel = getResourceLabel;
        _path = path ?? string.Empty;

        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _slotButton = new Button
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true,
            TooltipText = "Select resource"
        };
        _slotButton.Pressed += OpenDialog;
        AddChild(_slotButton);

        _clearButton = new Button
        {
            Text = "X",
            TooltipText = "Clear resource"
        };
        _clearButton.Pressed += () => SetPath(string.Empty, notify: true);
        AddChild(_clearButton);

        RefreshDisplay();
    }

    public string Path
    {
        get => _path;
        set => SetPath(value, notify: false);
    }

    public void OpenPicker()
    {
        OpenDialog();
    }

    private void OpenDialog()
    {
        _dialog ??= CreateDialog();
        if (_dialog.GetParent() == null)
            AddChild(_dialog);

        if (!string.IsNullOrWhiteSpace(_path))
            _dialog.CurrentPath = _path;

        _dialog.PopupCentered(new Vector2I(900, 620));
    }

    private EditorFileDialog CreateDialog()
    {
        var dialog = new EditorFileDialog
        {
            Title = $"Select {_resourceType.Name}",
            FileMode = EditorFileDialog.FileModeEnum.OpenFile,
            Access = EditorFileDialog.AccessEnum.Resources
        };
        dialog.AddFilter("*.tres,*.res", "Godot resource");
        dialog.FileSelected += path =>
        {
            if (TryAcceptPath(path, out string acceptedPath))
            {
                SetPath(acceptedPath, notify: true);
                return;
            }

            GD.PushWarning($"[GraphResourcePathField] Selected resource is not a {_resourceType.Name}: {path}");
        };
        return dialog;
    }

    private void SetPath(string value, bool notify)
    {
        _path = value?.Trim() ?? string.Empty;
        RefreshDisplay();
        if (notify)
            _pathChanged?.Invoke(_path);
    }

    private void RefreshDisplay()
    {
        Resource resource = LoadCurrentResource();
        string label = GetResourceLabel(resource);
        _slotButton.Text = string.IsNullOrWhiteSpace(label)
            ? $"Select {_resourceType.Name}"
            : label;

        string tooltip = string.IsNullOrWhiteSpace(_path)
            ? $"Select {_resourceType.Name}"
            : _path;

        TooltipText = tooltip;
        _slotButton.TooltipText = tooltip;
        _clearButton.Disabled = string.IsNullOrWhiteSpace(_path);
    }

    private Resource LoadCurrentResource()
    {
        if (string.IsNullOrWhiteSpace(_path) || !ResourceLoader.Exists(_path))
            return null;

        Resource resource = ResourceLoader.Load(_path);
        return resource != null && _resourceType.IsInstanceOfType(resource) ? resource : null;
    }

    private string GetResourceLabel(Resource resource)
    {
        if (resource != null)
        {
            string customLabel = _getResourceLabel?.Invoke(resource);
            if (!string.IsNullOrWhiteSpace(customLabel))
                return customLabel;
        }

        if (!string.IsNullOrWhiteSpace(_path))
            return _path.GetFile().GetBaseName();

        return string.Empty;
    }

    private bool TryAcceptPath(string candidate, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        string normalized = candidate.Trim();
        if (!normalized.StartsWith("res://", StringComparison.Ordinal))
            return false;

        if (!ResourceLoader.Exists(normalized))
            return false;

        Resource resource = ResourceLoader.Load(normalized);
        if (resource == null || !_resourceType.IsInstanceOfType(resource))
            return false;

        path = normalized;
        return true;
    }
}
#endif
