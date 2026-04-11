using System;
using System.Collections.Generic;
using Godot;

namespace Framework.UI
{
    /// <summary>
    /// Default UI module implementation.
    /// </summary>
    public class UIModule : Module, IUIModule, IProcessModule
    {
        private readonly Dictionary<UILayer, CanvasLayer> _layers = new();
        private readonly List<UIWindow> _windows = new();
        private readonly Dictionary<string, UIWindow> _windowMap = new();

        private IResourceModule _resource;
        private SceneTree _tree;

        public override int Priority => 100;
        public int WindowCount => _windows.Count;

        public override void OnInit()
        {
            _resource = ModuleSystem.GetModule<IResourceModule>();
            _tree = Engine.GetMainLoop() as SceneTree;

            var root = _tree?.Root.GetNodeOrNull<Node>("Root/UICanvas");
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
                CreateCanvasLayer(layer, root);
        }

        public override void Shutdown()
        {
            CloseAll();
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

        public void ShowUI<T>(params object[] userData) where T : UIWindow, new()
        {
            ShowUIAsync<T>(null, userData);
        }

        public void ShowUIAsync<T>(Action<T> onComplete = null, params object[] userData) where T : UIWindow, new()
        {
            var name = typeof(T).FullName;
            if (string.IsNullOrEmpty(name))
                return;

            if (_windowMap.TryGetValue(name, out var existingWindow))
            {
                existingWindow.UserDatas = userData;
                QueueShowCallback(existingWindow, onComplete);
                MoveToTop(existingWindow);

                if (existingWindow.IsPrepare)
                    ActivateWindow(existingWindow);

                return;
            }

            var window = new T();
            ApplyWindowAttribute(window);
            window.WindowName = name;
            window.UserDatas = userData;
            window.Depth = NextDepth(window.Layer);

            QueueShowCallback(window, onComplete);
            _windows.Add(window);
            _windowMap[name] = window;

            BeginWindowLoad(window);
        }

        public void CloseUI<T>() where T : UIWindow
        {
            var name = typeof(T).FullName;
            if (!string.IsNullOrEmpty(name) && _windowMap.TryGetValue(name, out var win))
                CloseWindowInternal(win);
        }

        public void CloseUI(UIWindow window)
        {
            if (window != null)
                CloseWindowInternal(window);
        }

        public void HideUI<T>() where T : UIWindow
        {
            var name = typeof(T).FullName;
            if (string.IsNullOrEmpty(name) || !_windowMap.TryGetValue(name, out var win))
                return;

            win.SetActive(false);
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
        {
            return _windowMap.ContainsKey(typeof(T).FullName);
        }

        public T GetWindow<T>() where T : UIWindow
        {
            _windowMap.TryGetValue(typeof(T).FullName, out var win);
            return win as T;
        }

        public string GetTopWindowName()
        {
            for (int i = _windows.Count - 1; i >= 0; i--)
            {
                var win = _windows[i];
                if (win.IsVisible && win.IsPrepare)
                    return win.WindowName;
            }

            return string.Empty;
        }

        public string GetTopWindowName(UILayer layer)
        {
            for (int i = _windows.Count - 1; i >= 0; i--)
            {
                var win = _windows[i];
                if (win.Layer == layer && win.IsVisible && win.IsPrepare)
                    return win.WindowName;
            }

            return string.Empty;
        }

        public bool IsAnyLoading()
        {
            foreach (var window in _windows)
            {
                if (!window.IsLoadDone)
                    return true;
            }

            return false;
        }


        private void BeginWindowLoad(UIWindow win)
        {
            if (string.IsNullOrEmpty(win.AssetPath))
            {
                Debugger.Warn($"[UIModule] Window '{win.WindowName}' has no AssetPath.");
                CompleteWindowLoad(win, null, loadVersion: 0);
                return;
            }

            var sceneHandle = _resource.LoadSceneAsync(win.AssetPath);
            var loadVersion = win.BeginSceneLoad(sceneHandle);
            sceneHandle.OnCompleted(handle => CompleteWindowLoad(win, handle, loadVersion));
        }

        private void CompleteWindowLoad(UIWindow win, SceneHandle sceneHandle, int loadVersion)
        {
            if (sceneHandle != null && !IsCurrentLoad(win, sceneHandle, loadVersion))
            {
                sceneHandle.Dispose();
                return;
            }

            if (sceneHandle != null && !sceneHandle.IsValid)
                Debugger.Warn($"[UIModule] Window '{win.WindowName}' failed to load scene '{win.AssetPath}'. Error={sceneHandle.Error}");

            if (sceneHandle != null)
                win.Owner = CreateWindowControl(win, sceneHandle);

            win.IsLoadDone = true;
            win.InternalCreate();
            ActivateWindow(win);
        }

        private Control CreateWindowControl(UIWindow win, SceneHandle sceneHandle)
        {
            var layer = GetCanvasLayer(win.Layer);
            if (layer == null)
            {
                Debugger.Warn($"[UIModule] Missing canvas layer '{win.Layer}' for window '{win.WindowName}'.");
                return sceneHandle.Instantiate<Control>();
            }

            return sceneHandle.InstantiateAndBind<Control>(control => AttachControlToLayer(layer, control));
        }

        private void ActivateWindow(UIWindow win)
        {
            win.SetActive(true);
            win.InternalRefresh();
            RecalcDepthAndVisibility();
            win.NotifyShowCompleted();
        }

        private bool IsCurrentLoad(UIWindow win, SceneHandle sceneHandle, int loadVersion)
        {
            return _windowMap.TryGetValue(win.WindowName, out var current)
                && ReferenceEquals(current, win)
                && win.MatchesSceneLoad(sceneHandle, loadVersion);
        }

        private void CloseWindowInternal(UIWindow win)
        {
            if (win == null)
                return;

            _windows.Remove(win);
            _windowMap.Remove(win.WindowName);
            win.InternalDestroy();
            RecalcDepthAndVisibility();
        }

        private void CreateCanvasLayer(UILayer layer, Node root)
        {
            var canvasLayer = new CanvasLayer
            {
                Layer = (int)layer,
                Name = $"UILayer_{layer}"
            };

            _layers[layer] = canvasLayer;
            root?.CallDeferred(Node.MethodName.AddChild, canvasLayer);
        }

        private CanvasLayer GetCanvasLayer(UILayer layer)
        {
            return _layers.TryGetValue(layer, out var canvasLayer) ? canvasLayer : null;
        }

        private void AttachControlToLayer(CanvasLayer layer, Control control)
        {
            if (layer == null || control == null)
                return;

            if (layer.IsInsideTree())
                layer.AddChild(control);
            else
                layer.CallDeferred(Node.MethodName.AddChild, control);
        }

        private void MoveToTop(UIWindow win)
        {
            _windows.Remove(win);
            _windows.Add(win);
        }

        private void QueueShowCallback<T>(UIWindow win, Action<T> onComplete) where T : UIWindow
        {
            if (onComplete == null)
                return;

            win.AddShowCompletedCallback(window => onComplete(window as T));
        }

        private void RecalcDepthAndVisibility()
        {
            var layerDepths = new Dictionary<UILayer, int>();
            foreach (var win in _windows)
            {
                if (!layerDepths.ContainsKey(win.Layer))
                    layerDepths[win.Layer] = 0;

                win.Depth = layerDepths[win.Layer]++;
                win.ApplyDepth();
            }

            var blockedLayers = new HashSet<UILayer>();
            for (int i = _windows.Count - 1; i >= 0; i--)
            {
                var win = _windows[i];
                if (!win.IsPrepare)
                    continue;

                if (!win.Active)
                {
                    win.SetNodeVisible(false);
                    continue;
                }

                if (blockedLayers.Contains(win.Layer))
                {
                    win.SetNodeVisible(false);
                    continue;
                }

                win.SetNodeVisible(true);
                if (win.FullScreen)
                    blockedLayers.Add(win.Layer);
            }
        }

        private int NextDepth(UILayer layer)
        {
            var max = 0;
            foreach (var window in _windows)
            {
                if (window.Layer == layer && window.Depth >= max)
                    max = window.Depth + 1;
            }

            return max;
        }

        private static void ApplyWindowAttribute(UIWindow win)
        {
            var attributes = win.GetType().GetCustomAttributes(typeof(WindowAttribute), false);
            if (attributes.Length == 0)
            {
                Debugger.Warn($"[UIModule] {win.GetType().Name} is missing [Window], using defaults.");
                win.Layer = UILayer.Normal;
                win.AssetPath = string.Empty;
                win.FullScreen = false;
                win.HideTimeToClose = 10f;
                return;
            }

            var attribute = (WindowAttribute)attributes[0];
            win.Layer = attribute.Layer;
            win.AssetPath = attribute.AssetPath;
            win.FullScreen = attribute.FullScreen;
            win.HideTimeToClose = attribute.HideTimeToClose;
        }
    }
}
