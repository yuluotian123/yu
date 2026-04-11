using System;
using Godot;

namespace Framework.UI
{
    /// <summary>
    /// Base class for framework-managed UI windows.
    /// </summary>
    public abstract class UIWindow : UIBase
    {
        private SceneTreeTimer _hideTimer;
        private Action<UIWindow> _showCompletedCallbacks;
        private int _sceneLoadVersion;

        public string WindowName { get; internal set; }
        public UILayer Layer { get; internal set; }
        public string AssetPath { get; internal set; }
        public bool FullScreen { get; internal set; }
        public float HideTimeToClose { get; internal set; }
        public bool Active { get; private set; }
        public bool IsVisible => Owner != null && GodotObject.IsInstanceValid(Owner) && Owner.Visible;
        public bool IsLoadDone { get; internal set; }
        public int Depth { get; internal set; }

        internal SceneHandle SceneHandle { get; private set; }

        internal override void InternalCreate()
        {
            if (Owner != null)
                Owner.ZIndex = Depth;

            base.InternalCreate();
        }

        internal override void InternalRefresh()
        {
            base.InternalRefresh();
        }

        internal override void InternalUpdate(double delta)
        {
            if (!IsPrepare || !IsVisible)
                return;

            base.InternalUpdate(delta);
        }

        internal override void InternalDestroy()
        {
            CancelHideTimer();
            ReleaseSceneHandle();

            if (Owner != null && GodotObject.IsInstanceValid(Owner))
                Owner.QueueFree();

            base.InternalDestroy();
            Active = false;
            IsLoadDone = false;
            _showCompletedCallbacks = null;
        }

        internal void SetActive(bool active)
        {
            if (Active == active)
                return;

            Active = active;
            base.InternalSetVisible(active);
        }

        internal void SetNodeVisible(bool visible)
        {
            if (Owner != null && GodotObject.IsInstanceValid(Owner))
                Owner.Visible = visible;
        }

        internal void StartHideTimer(SceneTree tree)
        {
            CancelHideTimer();
            if (HideTimeToClose <= 0f || tree == null)
                return;

            _hideTimer = tree.CreateTimer(HideTimeToClose);
            _hideTimer.Timeout += OnHideTimerTimeout;
        }

        internal void ApplyDepth()
        {
            if (Owner != null && GodotObject.IsInstanceValid(Owner))
                Owner.ZIndex = Depth;
        }

        internal int BeginSceneLoad(SceneHandle sceneHandle)
        {
            ReleaseSceneHandle();
            SceneHandle = sceneHandle;
            IsLoadDone = false;
            return ++_sceneLoadVersion;
        }

        internal bool MatchesSceneLoad(SceneHandle sceneHandle, int loadVersion)
        {
            return ReferenceEquals(SceneHandle, sceneHandle) && _sceneLoadVersion == loadVersion;
        }

        internal void AddShowCompletedCallback(Action<UIWindow> callback)
        {
            _showCompletedCallbacks += callback;
        }

        internal void NotifyShowCompleted()
        {
            var callbacks = _showCompletedCallbacks;
            _showCompletedCallbacks = null;
            callbacks?.Invoke(this);
        }

        internal void ReleaseSceneHandle()
        {
            SceneHandle?.Dispose();
            SceneHandle = null;
        }

        private void CancelHideTimer()
        {
            if (_hideTimer == null)
                return;

            _hideTimer.Timeout -= OnHideTimerTimeout;
            _hideTimer = null;
        }

        private void OnHideTimerTimeout()
        {
            _hideTimer = null;
            ModuleSystem.GetModule<IUIModule>().CloseUI(this);
        }
    }
}
