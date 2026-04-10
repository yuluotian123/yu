#if TOOLS
using Godot;
using System.IO;

/// <summary>
/// 配置表转换编辑器插件。
/// <para>
/// 在编辑器顶部工具栏添加按钮（Build Project 左侧），显示 xlsx 文件夹名，
/// 点击后弹出转换窗口。
/// </para>
/// <para>
/// 热重载安全：在 <see cref="_Notification"/> 中监听
/// <c>NotificationExtensionReloaded</c>，重建工具栏按钮并重新给窗口赋值 Plugin。
/// </para>
/// </summary>
[Tool]
public partial class ConfigPlugin : EditorPlugin
{
    private Button                _toolbarBtn;
    private ConfigConverterWindow _window;

    public override void _EnterTree()
    {
        CreateToolbarButton();
        GD.Print("[ConfigPlugin] 配置表插件已加载。");
    }


    public override void _ExitTree()
    {
        DestroyToolbarButton();
        if (_window != null && GodotObject.IsInstanceValid(_window))
        {
            // 窗口 _ExitTree 会自动解绑内部信号；这里只需 QueueFree
            _window.QueueFree();
        }
        _window = null;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationExtensionReloaded)
        {
            DestroyToolbarButton();
            CreateToolbarButton();

            if (_window != null && GodotObject.IsInstanceValid(_window))
                _window.Plugin = this;

            GD.Print("[ConfigPlugin] 热重载完成，已重新绑定。");
        }
    }

    /// <summary>从窗口同步最新的 xlsx 目录名称到工具栏按钮文字。</summary>
    public void RefreshToolbarLabel(string xlsxDir)
    {
        if (_toolbarBtn != null && GodotObject.IsInstanceValid(_toolbarBtn))
            _toolbarBtn.Text = GetXlsxDirLabel(xlsxDir);
    }

    // ── 私有辅助 ──────────────────────────────────────────────────────────────

    private void CreateToolbarButton()
    {
        _toolbarBtn = new Button
        {
            Text        = GetXlsxDirLabel(ConfigConverterWindow.DefaultXlsxDir),
            TooltipText = "打开配置表转换工具",
            FocusMode   = Control.FocusModeEnum.None,
        };
        _toolbarBtn.Pressed += OpenConverterWindow;
        AddControlToContainer(CustomControlContainer.Toolbar, _toolbarBtn);
    }

    private void DestroyToolbarButton()
    {
        if (_toolbarBtn != null && GodotObject.IsInstanceValid(_toolbarBtn))
        {
            _toolbarBtn.Pressed -= OpenConverterWindow;   // 先解绑，再移除和释放
            RemoveControlFromContainer(CustomControlContainer.Toolbar, _toolbarBtn);
            _toolbarBtn.QueueFree();
        }
        _toolbarBtn = null;
    }

    private void OpenConverterWindow()
    {
        // 窗口不存在或已被销毁，重新创建
        if (_window == null || !GodotObject.IsInstanceValid(_window))
        {
            _window = new ConfigConverterWindow { Plugin = this };
            EditorInterface.Singleton.GetBaseControl().AddChild(_window);
            _window.PopupCentered(new Vector2I(640, 480));
            return;
        }

        if (!_window.Visible)
            _window.PopupCentered(new Vector2I(640, 480));
        else
            _window.GrabFocus();
    }

    private static string GetXlsxDirLabel(string xlsxDir)
    {
        if (string.IsNullOrWhiteSpace(xlsxDir)) return "📂 (未设置)";
        var trimmed = xlsxDir.TrimEnd('/', '\\');
        var name    = Path.GetFileName(trimmed);
        return $"📂 {(string.IsNullOrEmpty(name) ? trimmed : name)}";
    }
}
#endif
