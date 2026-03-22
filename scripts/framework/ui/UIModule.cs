using System;
using System.Collections.Generic;
using Godot;

namespace Framework.UI
{
    /// <summary>UIModule 核心实现（skeleton）。</summary>
    public class UIModule : Module, IUIModule, IProcessModule
    {
        public static UIModule Instance { get; private set; }
        public override int Priority => 100;

        // ── CanvasLayer 容器 ──
        private readonly Dictionary<UILayer, CanvasLayer> _layers = new();

        // ── 窗口列表（按 Depth 升序，末尾为最顶层）──
        private readonly List<UIWindow> _windows = new();

        // ── 按名称快速查找 ──
        private readonly Dictionary<string, UIWindow> _windowMap = new();

        // ── 资源模块引用 ──
        private IResourceModule _resource;

        // ── 场景树引用（由 OnInit 填充）──
        private SceneTree _tree;

        public int WindowCount => _windows.Count;

        // ───────────────────────────────────────────
        //  Module 生命周期
        // ───────────────────────────────────────────

        public override void OnInit()
        {
            Instance = this;
            _resource = ModuleSystem.GetModule<IResourceModule>();

            // 获取场景树（需由外部在 OnInit 之前设置，或通过 RootModule 注入）
            _tree = Engine.GetMainLoop() as SceneTree;
            var root = _tree.Root.GetNode("Root/UICanvas");
            

            // 创建各层 CanvasLayer
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
                CreateCanvasLayer(layer, root);
        }

        public override void Shutdown()
        {
            CloseAll();
            Instance = null;
        }

        public void Process(double elapsed, double realElapsed)
        {
            for (int i = _windows.Count - 1; i >= 0; i--)
            {
                var win = _windows[i];
                if (win.IsPrepare && win.IsVisible)
                    win.InternalUpdate(elapsed);
            }
        }

        // ───────────────────────────────────────────
        //  CanvasLayer 管理
        // ───────────────────────────────────────────

        private void CreateCanvasLayer(UILayer layer,Node root)
        {
            var cl = new CanvasLayer { Layer = (int)layer, Name = $"UILayer_{layer}" };
            _layers[layer] = cl;
            // 使用 CallDeferred 避免在 _Ready() 树初始化阶段直接调用 AddChild 导致 blocked 错误
            root?.CallDeferred(Node.MethodName.AddChild, cl);
        }

        private CanvasLayer GetCanvasLayer(UILayer layer)
            => _layers.TryGetValue(layer, out var cl) ? cl : null;

        // ───────────────────────────────────────────
        //  IUIModule 实现
        // ───────────────────────────────────────────


        public void ShowUI<T>(params object[] userData) where T : UIWindow, new()
            => ShowUIAsync<T>(null, userData);

        public void ShowUIAsync<T>(Action<T> onComplete = null, params object[] userData) where T : UIWindow, new()
        {
            string name = typeof(T).FullName;
            UIWindow win;

            // 窗口已存在：置顶并刷新
            if (_windowMap.TryGetValue(name, out win))
            {
                win.UserDatas = userData;
                MoveToTop(win);
                if (win.IsPrepare)
                {
                    win.SetActive(true);
                    win.InternalRefresh();
                    RecalcDepthAndVisibility();
                    onComplete?.Invoke(win as T);
                }
                // 若还在加载中，回调会在加载完成后由 OnWindowLoaded 触发
                return;
            }

            // 首次创建
            win = new T();
            ApplyWindowAttribute(win);
            win.WindowName = name;
            win.UserDatas = userData;
            win.Depth = NextDepth(win.Layer);

            _windows.Add(win);
            _windowMap[name] = win;

            // 异步加载 PackedScene
            if (!string.IsNullOrEmpty(win.AssetPath))
            {
                var handle = _resource.LoadAssetAsync<PackedScene>(win.AssetPath);
                handle.OnCompleted(h => OnWindowLoaded(win, h.Asset, onComplete));
            }
            else
            {
                Debugger.Warn($"[UIModule] 窗口 {name} AssetPath 为空，跳过加载。");
                OnWindowLoaded(win, null, onComplete);
            }
        }

        public void CloseUI<T>() where T : UIWindow
        {
            string name = typeof(T).FullName;
            if (_windowMap.TryGetValue(name, out var win))
                CloseWindowInternal(win);
        }

        public void CloseUI(UIWindow window)
        {
            if (window != null) CloseWindowInternal(window);
        }

        public void HideUI<T>() where T : UIWindow
        {
            string name = typeof(T).FullName;
            if (!_windowMap.TryGetValue(name, out var win)) return;

            // SetActive(false) 同时把 Owner.Visible 置 false 并触发 OnSetVisible 回调
            win.SetActive(false);

            // 重算其他窗口的全屏遮挡状态（本窗口 Active=false，不会被恢复）
            RecalcDepthAndVisibility();

            if (win.HideTimeToClose > 0f)
                win.StartHideTimer(_tree);
            else
                CloseWindowInternal(win);
        }

        public void CloseAll(UILayer? layer = null)
        {
            for (int i = _windows.Count - 1; i >= 0; i--)
            {
                var win = _windows[i];
                if (layer == null || win.Layer == layer.Value)
                    CloseWindowInternal(win);
            }
        }

        public bool HasWindow<T>() where T : UIWindow
            => _windowMap.ContainsKey(typeof(T).FullName);

        public T GetWindow<T>() where T : UIWindow
        {
            _windowMap.TryGetValue(typeof(T).FullName, out var win);
            return win as T;
        }

        public string GetTopWindowName()
        {
            for (int i = _windows.Count - 1; i >= 0; i--)
                if (_windows[i].IsVisible && _windows[i].IsPrepare)
                    return _windows[i].WindowName;
            return string.Empty;
        }

        public string GetTopWindowName(UILayer layer)
        {
            for (int i = _windows.Count - 1; i >= 0; i--)
            {
                var w = _windows[i];
                if (w.Layer == layer && w.IsVisible && w.IsPrepare)
                    return w.WindowName;
            }
            return string.Empty;
        }

        public bool IsAnyLoading()
        {
            foreach (var w in _windows)
                if (!w.IsLoadDone) return true;
            return false;
        }

        // ── CloseWindowByName（供 UIWindow 内部计时器回调）──
        internal void CloseWindowByName(string name)
        {
            if (_windowMap.TryGetValue(name, out var win))
                CloseWindowInternal(win);
        }

        // ───────────────────────────────────────────
        //  内部辅助方法
        // ───────────────────────────────────────────

        private void OnWindowLoaded<T>(UIWindow win, PackedScene scene, Action<T> onComplete) where T : UIWindow
        {
            if (scene != null)
            {
                var control = scene.Instantiate<Control>();
                win.Owner = control;
                var cl = GetCanvasLayer(win.Layer);
                if (cl != null)
                {
                    // CanvasLayer 可能是 CallDeferred 加入树的，安全起见也用 CallDeferred 挂载 control
                    if (cl.IsInsideTree())
                        cl.AddChild(control);
                    else
                        cl.CallDeferred(Node.MethodName.AddChild, control);
                }
            }

            win.IsLoadDone = true;
            win.InternalCreate();
            win.SetActive(true);
            win.InternalRefresh();
            RecalcDepthAndVisibility();
            onComplete?.Invoke(win as T);
        }

        private void CloseWindowInternal(UIWindow win)
        {
            _windows.Remove(win);
            _windowMap.Remove(win.WindowName);
            win.InternalDestroy();
            RecalcDepthAndVisibility();
        }

        private void MoveToTop(UIWindow win)
        {
            _windows.Remove(win);
            _windows.Add(win);
        }

        /// <summary>
        /// 重新计算所有窗口的 Depth 和渲染可见性（处理全屏遮挡逻辑）。
        /// <para>
        /// 对齐 TEngine 设计：
        /// <list type="bullet">
        ///   <item>只有 <see cref="UIWindow.Active"/> == true 的窗口才会被恢复渲染（SetNodeVisible(true)）；</item>
        ///   <item>HideUI 后 Active == false，此方法不会将其恢复，确保隐藏意图不被覆盖；</item>
        ///   <item>全屏窗口遮挡下层窗口时只改 Owner.Visible，不改 Active。</item>
        /// </list>
        /// </para>
        /// </summary>
        private void RecalcDepthAndVisibility()
        {
            // 第一步：按层分组，各层独立计算深度
            var layerCounters = new Dictionary<UILayer, int>();
            foreach (var win in _windows)
            {
                if (!layerCounters.ContainsKey(win.Layer))
                    layerCounters[win.Layer] = 0;

                win.Depth = layerCounters[win.Layer]++;
                win.ApplyDepth();
            }

            // 第二步：全屏遮挡处理。
            // _windows 按 Depth 升序，末尾为最顶层；从顶往下扫描。
            //
            // 规则：
            //   - 同层中，遇到第一个全屏且 Active 的窗口 → 它下面所有同层窗口 SetNodeVisible(false)
            //   - 未被遮挡且 Active 的窗口 → SetNodeVisible(true)（恢复渲染）
            //   - Active == false 的窗口 → SetNodeVisible(false)，不参与恢复逻辑
            var fullScreenFound = new HashSet<UILayer>();
            for (int i = _windows.Count - 1; i >= 0; i--)
            {
                var win = _windows[i];
                if (!win.IsPrepare) continue;

                if (!win.Active)
                {
                    // 业务层主动隐藏：确保渲染也是隐藏的，不参与遮挡恢复
                    win.SetNodeVisible(false);
                    continue;
                }

                if (fullScreenFound.Contains(win.Layer))
                {
                    // 被上方全屏窗口遮挡：隐藏渲染，但不改 Active
                    win.SetNodeVisible(false);
                }
                else
                {
                    // 未被遮挡：恢复渲染
                    win.SetNodeVisible(true);
                    // 本窗口是全屏窗口：标记该层已被全屏遮挡
                    if (win.FullScreen)
                        fullScreenFound.Add(win.Layer);
                }
            }
        }

        private int NextDepth(UILayer layer)
        {
            int max = 0;
            foreach (var w in _windows)
                if (w.Layer == layer && w.Depth >= max)
                    max = w.Depth + 1;
            return max;
        }

        private static void ApplyWindowAttribute(UIWindow win)
        {
            var attr = win.GetType().GetCustomAttributes(typeof(WindowAttribute), false);
            if (attr.Length == 0)
            {
                Debugger.Warn($"[UIModule] {win.GetType().Name} 未标记 [Window] 特性，使用默认值。");
                win.Layer = UILayer.Normal;
                win.AssetPath = string.Empty;
                win.FullScreen = false;
                win.HideTimeToClose = 10f;
                return;
            }
            var wa = (WindowAttribute)attr[0];
            win.Layer = wa.Layer;
            win.AssetPath = wa.AssetPath;
            win.FullScreen = wa.FullScreen;
            win.HideTimeToClose = wa.HideTimeToClose;
        }
    }
}
