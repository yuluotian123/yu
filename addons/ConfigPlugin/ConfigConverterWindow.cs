#if TOOLS
using Framework;
using Godot;
using System;

/// <summary>
/// 配置表转换窗口（编辑器内弹出）。
/// 所有信号绑定均使用具名方法，避免 Godot C# 热重载后 lambda delegate 失效。
/// </summary>
[Tool]
public partial class ConfigConverterWindow : Window
{
    // ── 默认路径常量 ──────────────────────────────────────────────────────────
    public const string DefaultXlsxDir = "res://assets/config/xlsx/";
    public const string DefaultJsonDir = "res://assets/config/tables/";
    public const string DefaultCsDir   = "res://scripts/generated/config/";

    /// <summary>由 ConfigPlugin 在 _EnterTree / 热重载后主动赋值。</summary>
    public ConfigPlugin Plugin { get; set; }

    // ── UI 控件 ───────────────────────────────────────────────────────────────
    private LineEdit _xlsxDirEdit;
    private LineEdit _jsonDirEdit;
    private LineEdit _csDirEdit;
    private LineEdit _namespaceEdit;
    private TextEdit _logEdit;
    private Button   _convertBtn;

    // 根容器
    private MarginContainer _margin;

    // 路径选择对话框
    private EditorFileDialog _folderDialog;
    // 当前正在选择路径的目标输入框（由具名方法赋值）
    private LineEdit _folderDialogTarget;

    public ConfigConverterWindow() { }

    public override void _ExitTree()
    {
        // 解绑所有信号，避免程序集卸载时 delegate 仍被 GDNative 层持有
        // 不解绑会导致 Godot #78513：Failed to unload assemblies
        CloseRequested -= OnCloseRequested;
        SizeChanged    -= SyncRootSize;

        if (_xlsxDirEdit  != null) _xlsxDirEdit.TextChanged  -= OnXlsxDirTextChanged;
        if (_convertBtn   != null) _convertBtn.Pressed        -= OnConvertPressed;
        if (_folderDialog != null) _folderDialog.DirSelected  -= OnFolderSelected;
    }

    public override void _Ready()
    {
        Title       = "配置表转换工具";
        Exclusive   = false;
        Unresizable = false;
        MinSize     = new Vector2I(500, 380);

        CloseRequested += OnCloseRequested;

        BuildUI();
        LoadSettings();     // 从 EditorSettings 恢复上次路径

        SyncRootSize();
        SizeChanged += SyncRootSize;
    }

    public static string GetXlsxPath()
    {
         var es = EditorInterface.Singleton.GetEditorSettings();
        return ReadSetting(es,KeyXlsx,      DefaultXlsxDir);
    }

    // ── 构建 UI ───────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        _margin = new MarginContainer();
        _margin.AddThemeConstantOverride("margin_left",   8);
        _margin.AddThemeConstantOverride("margin_right",  8);
        _margin.AddThemeConstantOverride("margin_top",    8);
        _margin.AddThemeConstantOverride("margin_bottom", 8);
        AddChild(_margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 6);
        _margin.AddChild(root);

        // ── xlsx 源文件夹 ──────────────────────────────────────────────────────
        _xlsxDirEdit = MakeLineEdit(DefaultXlsxDir);
        _xlsxDirEdit.TextChanged += OnXlsxDirTextChanged;
        var xlsxBrowseBtn = new Button { Text = "浏览..." };
        xlsxBrowseBtn.Pressed += OnXlsxBrowsePressed;
        root.AddChild(MakeRow("xlsx 源文件夹：", _xlsxDirEdit, xlsxBrowseBtn));

        // ── JSON 输出目录 ──────────────────────────────────────────────────────
        _jsonDirEdit = MakeLineEdit(DefaultJsonDir);
        var jsonBrowseBtn = new Button { Text = "浏览..." };
        jsonBrowseBtn.Pressed += OnJsonBrowsePressed;
        root.AddChild(MakeRow("JSON 输出目录：", _jsonDirEdit, jsonBrowseBtn));

        // ── C# 代码输出目录 ────────────────────────────────────────────────────
        _csDirEdit = MakeLineEdit(DefaultCsDir);
        var csBrowseBtn = new Button { Text = "浏览..." };
        csBrowseBtn.Pressed += OnCsBrowsePressed;
        root.AddChild(MakeRow("C# 代码输出目录：", _csDirEdit, csBrowseBtn));

        // ── 命名空间 ──────────────────────────────────────────────────────────
        _namespaceEdit = MakeLineEdit("Generated.Config");
        root.AddChild(MakeRow("C# 命名空间：", _namespaceEdit, null));

        root.AddChild(new HSeparator());

        // ── 转换按钮 ──────────────────────────────────────────────────────────
        _convertBtn = new Button
        {
            Text                = "开始转换",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _convertBtn.Pressed += OnConvertPressed;
        root.AddChild(_convertBtn);

        // ── 日志区 ────────────────────────────────────────────────────────────
        _logEdit = new TextEdit
        {
            Editable            = false,
            WrapMode            = TextEdit.LineWrappingMode.Boundary,
            SizeFlagsVertical   = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        root.AddChild(_logEdit);

        // ── 文件夹选择对话框 ──────────────────────────────────────────────────
        _folderDialog = new EditorFileDialog
        {
            FileMode = EditorFileDialog.FileModeEnum.OpenDir,
            Access   = EditorFileDialog.AccessEnum.Filesystem,
            Title    = "选择文件夹",
        };
        _folderDialog.DirSelected += OnFolderSelected;
        AddChild(_folderDialog);
    }

    private static LineEdit MakeLineEdit(string defaultVal) => new LineEdit
    {
        Text                = defaultVal,
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    };

    private static HBoxContainer MakeRow(string labelText, LineEdit edit, Button browseBtn)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(150, 0) });
        row.AddChild(edit);
        if (browseBtn != null)
            row.AddChild(browseBtn);
        return row;
    }

    // ── 浏览按钮具名方法（避免热重载后 lambda 失效）──────────────────────────

    private void OnXlsxBrowsePressed()  => OpenFolderDialog(_xlsxDirEdit);
    private void OnJsonBrowsePressed() => OpenFolderDialog(_jsonDirEdit);
    private void OnCsBrowsePressed()   => OpenFolderDialog(_csDirEdit);

    private void OpenFolderDialog(LineEdit target)
    {
        _folderDialogTarget = target;
        var currentPath = ProjectSettings.GlobalizePath(target.Text.Trim());
        if (System.IO.Directory.Exists(currentPath))
            _folderDialog.CurrentDir = currentPath;
        _folderDialog.PopupCentered(new Vector2I(900, 600));
    }

    // ── 事件：具名方法 ────────────────────────────────────────────────────────

    private void OnCloseRequested()
    {
        SaveSettings();
        Hide();
    }

    private void OnXlsxDirTextChanged(string txt)
    {
        Plugin?.RefreshToolbarLabel(txt);
        SaveSettings();
    }

    private void OnFolderSelected(string dir)
    {
        if (_folderDialogTarget == null) return;
        _folderDialogTarget.Text = ProjectSettings.LocalizePath(dir);
        if (_folderDialogTarget == _xlsxDirEdit)
            Plugin?.RefreshToolbarLabel(dir);
        SaveSettings();
    }

    private void OnConvertPressed()
    {

        _logEdit.Clear();
        _convertBtn.Disabled = true;

        var xlsxDir = GlobalizePath(_xlsxDirEdit.Text.Trim());
        var jsonDir = GlobalizePath(_jsonDirEdit.Text.Trim());
        var csDir   = GlobalizePath(_csDirEdit.Text.Trim());
        var ns      = _namespaceEdit.Text.Trim();

        if (!System.IO.Directory.Exists(xlsxDir))
        {
            Log($"[错误] xlsx 源文件夹不存在：{xlsxDir}");
            _convertBtn.Disabled = false;
            return;
        }


        var converter = new XlsxConverter();
        int success   = 0;
        int failed    = 0;

        var files = System.IO.Directory.GetFiles(xlsxDir, "*.xlsx",
                        System.IO.SearchOption.TopDirectoryOnly);

        if (files.Length == 0)
        {
            Log($"[提示] 目录下没有找到 xlsx 文件：{xlsxDir}");
            _convertBtn.Disabled = false;
            return;
        }

        foreach (var file in files)
        {
            if (System.IO.Path.GetFileName(file).StartsWith("~")) continue;

            try
            {
                var result = converter.Convert(new XlsxConvertOptions
                {
                    XlsxPath      = file,
                    JsonOutputDir = string.IsNullOrWhiteSpace(jsonDir) ? null : jsonDir,
                    CsOutputDir   = string.IsNullOrWhiteSpace(csDir)   ? null : csDir,
                    Namespace     = ns,
                    Overwrite     = true
                });
                Log($"[成功] {result}");
                success++;
            }
            catch (Exception ex)
            {
                Log($"[失败] {System.IO.Path.GetFileName(file)}：{ex.Message}");
                failed++;
            }
        }

        Log($"\n转换完成：成功 {success} 个，失败 {failed} 个。");
        EditorInterface.Singleton.GetResourceFilesystem().Scan();
        _convertBtn.Disabled = false;
    }

    // ── EditorSettings 持久化 ─────────────────────────────────────────────────

    private const string KeyXlsx      = "config_plugin/xlsx_dir";
    private const string KeyJson      = "config_plugin/json_dir";
    private const string KeyCs        = "config_plugin/cs_dir";
    private const string KeyNamespace = "config_plugin/namespace";

    /// <summary>从 EditorSettings 读取上次保存的路径，填充输入框。</summary>
    private void LoadSettings()
    {
        var es = EditorInterface.Singleton.GetEditorSettings();

        _xlsxDirEdit.Text   = ReadSetting(es, KeyXlsx,      DefaultXlsxDir);
        _jsonDirEdit.Text   = ReadSetting(es, KeyJson,      DefaultJsonDir);
        _csDirEdit.Text     = ReadSetting(es, KeyCs,        DefaultCsDir);
        _namespaceEdit.Text = ReadSetting(es, KeyNamespace, "Generated.Config");

        // 同步工具栏按钮标签
        Plugin?.RefreshToolbarLabel(_xlsxDirEdit.Text);
    }

    /// <summary>将当前路径写入 EditorSettings 持久化。</summary>
    private void SaveSettings()
    {
        var es = EditorInterface.Singleton.GetEditorSettings();
        es.SetSetting(KeyXlsx,      _xlsxDirEdit.Text);
        es.SetSetting(KeyJson,      _jsonDirEdit.Text);
        es.SetSetting(KeyCs,        _csDirEdit.Text);
        es.SetSetting(KeyNamespace, _namespaceEdit.Text);
    }

    private static string ReadSetting(EditorSettings es, string key, string fallback)
    {
        if (!es.HasSetting(key)) return fallback;
        var val = es.GetSetting(key).AsString();
        return string.IsNullOrWhiteSpace(val) ? fallback : val;
    }

    // ── 辅助 ──────────────────────────────────────────────────────────────────

    private void SyncRootSize()
    {
        if (_margin == null || !GodotObject.IsInstanceValid(_margin)) return;
        _margin.Position = Vector2.Zero;
        _margin.Size     = new Vector2(Size.X, Size.Y);
    }

    private void Log(string msg)
    {
        _logEdit.Text += msg + "\n";
        _logEdit.ScrollVertical = (int)_logEdit.GetVScrollBar().MaxValue;
    }

    private static string GlobalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        return path.StartsWith("res://") || path.StartsWith("user://")
            ? ProjectSettings.GlobalizePath(path)
            : path;
    }
}
#endif
