using System;
using System.Text;
using Godot;

namespace Framework
{
    /// <summary>
    /// Lightweight runtime overlay for resource profiler snapshots.
    /// Hotkeys:
    /// ` toggle overlay
    /// F10 dump current snapshot to log
    /// </summary>
    public sealed partial class ResourceProfilerOverlay : CanvasLayer
    {
        private const string ToggleActionName = "resource_profiler_toggle";
        private const string DumpActionName = "resource_profiler_dump";

        private readonly Func<ResourceProfilerSnapshot> _snapshotProvider;
        private readonly Action _dumpToLog;
        private readonly float _refreshIntervalSeconds;
        private readonly int _maxRows;

        private PanelContainer _panel;
        private Button _gcCollectButton;
        private RichTextLabel _content;
        private double _elapsedSinceRefresh;
        private bool _prevTogglePressed;
        private bool _prevDumpPressed;

        public ResourceProfilerOverlay(
            Func<ResourceProfilerSnapshot> snapshotProvider,
            Action dumpToLog,
            float refreshIntervalSeconds,
            int maxRows)
        {
            _snapshotProvider = snapshotProvider;
            _dumpToLog = dumpToLog;
            _refreshIntervalSeconds = Math.Max(0.1f, refreshIntervalSeconds);
            _maxRows = Math.Max(4, maxRows);

            Layer = 200;
            ProcessMode = ProcessModeEnum.Always;
            Name = "ResourceProfilerOverlay";
            Visible = false;
        }

        public override void _Ready()
        {
            EnsureInputActions();
            BuildUi();
            UpdateText();
        }

        public override void _Process(double delta)
        {
            HandleHotkeys();

            if (!Visible)
                return;

            _elapsedSinceRefresh += delta;
            if (_elapsedSinceRefresh < _refreshIntervalSeconds)
                return;

            _elapsedSinceRefresh = 0.0;
            UpdateText();
        }

        public void SetOverlayVisible(bool visible)
        {
            Visible = visible;
            if (visible)
                UpdateText();
        }

        private void HandleHotkeys()
        {
            var togglePressed = Input.IsActionPressed(ToggleActionName);
            if (togglePressed && !_prevTogglePressed)
                SetOverlayVisible(!Visible);
            _prevTogglePressed = togglePressed;

            var dumpPressed = Input.IsActionPressed(DumpActionName);
            if (dumpPressed && !_prevDumpPressed)
                _dumpToLog?.Invoke();
            _prevDumpPressed = dumpPressed;
        }

        private void EnsureInputActions()
        {
            EnsureAction(ToggleActionName, Key.Quoteleft);
            EnsureAction(DumpActionName, Key.F10);
        }

        private static void EnsureAction(string actionName, Key key)
        {
            if (!InputMap.HasAction(actionName))
                InputMap.AddAction(actionName);

            foreach (var existingEvent in InputMap.ActionGetEvents(actionName))
            {
                if (existingEvent is InputEventKey existingKey && existingKey.Keycode == key)
                    return;
            }

            InputMap.ActionAddEvent(actionName, new InputEventKey
            {
                Keycode = key,
            });
        }

        private void BuildUi()
        {
            _panel = new PanelContainer();
            _panel.Name = "Panel";
            _panel.OffsetLeft = 16f;
            _panel.OffsetTop = 16f;
            _panel.CustomMinimumSize = new Vector2(640f, 560f);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.06f, 0.08f, 0.10f, 0.92f);
            style.BorderColor = new Color(0.35f, 0.60f, 0.78f, 0.95f);
            style.BorderWidthLeft = 1;
            style.BorderWidthTop = 1;
            style.BorderWidthRight = 1;
            style.BorderWidthBottom = 1;
            style.CornerRadiusTopLeft = 8;
            style.CornerRadiusTopRight = 8;
            style.CornerRadiusBottomRight = 8;
            style.CornerRadiusBottomLeft = 8;
            _panel.AddThemeStyleboxOverride("panel", style);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_top", 10);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_bottom", 10);

            var layout = new VBoxContainer();
            layout.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            layout.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            layout.AddThemeConstantOverride("separation", 8);

            var toolbar = new HBoxContainer();
            toolbar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            toolbar.AddThemeConstantOverride("separation", 8);

            _gcCollectButton = new Button();
            _gcCollectButton.Name = "GcCollectButton";
            _gcCollectButton.Text = "GC Collect";
            _gcCollectButton.TooltipText = "Trigger GC.Collect() and refresh the resource profiler snapshot.";
            _gcCollectButton.Pressed += OnGcCollectPressed;
            toolbar.AddChild(_gcCollectButton);

            _content = new RichTextLabel();
            _content.Name = "Content";
            _content.BbcodeEnabled = false;
            _content.FitContent = false;
            _content.ScrollActive = true;
            _content.SelectionEnabled = true;
            _content.MouseFilter = Control.MouseFilterEnum.Pass;
            _content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _content.CustomMinimumSize = new Vector2(616f, 540f);
            _content.AddThemeColorOverride("default_color", new Color(0.94f, 0.96f, 0.98f));

            layout.AddChild(toolbar);
            layout.AddChild(_content);
            margin.AddChild(layout);
            _panel.AddChild(margin);
            AddChild(_panel);
        }

        private void UpdateText()
        {
            if (_content == null || _snapshotProvider == null)
                return;

            var snapshot = _snapshotProvider();
            var builder = new StringBuilder();

            builder.AppendLine("Resource Profiler");
            builder.AppendLine("` Toggle Overlay | F10 Dump To Log");
            builder.AppendLine($"Captured: {snapshot.CreatedAtUtc:HH:mm:ss} UTC");
            builder.AppendLine();

            builder.AppendLine("Summary");
            builder.AppendLine($"  CacheCount: {snapshot.CacheCount}");
            builder.AppendLine($"  PendingCancels: {snapshot.PendingCancelCount}");
            builder.AppendLine($"  Handles: live={snapshot.LiveHandleCount} loading={snapshot.LoadingHandleCount} succeed={snapshot.SucceedHandleCount} failed={snapshot.FailedHandleCount} cancelled={snapshot.CancelledHandleCount} released={snapshot.ReleasedHandleCount}");
            builder.AppendLine($"  Loader: active={snapshot.Loader.ActiveCount}/{snapshot.Loader.MaxConcurrent} waiting={snapshot.Loader.WaitingCount} tasks={snapshot.Loader.TaskCount}");
            builder.AppendLine();

            AppendTasks(builder, snapshot);
            AppendCacheEntries(builder, snapshot);
            AppendHandles(builder, snapshot);

            _content.Text = builder.ToString();
        }

        private void AppendTasks(StringBuilder builder, ResourceProfilerSnapshot snapshot)
        {
            builder.AppendLine("Tasks");
            if (snapshot.Loader.Tasks == null || snapshot.Loader.Tasks.Count == 0)
            {
                builder.AppendLine("  (none)");
                builder.AppendLine();
                return;
            }

            var count = Math.Min(_maxRows, snapshot.Loader.Tasks.Count);
            for (var i = 0; i < count; i++)
            {
                var task = snapshot.Loader.Tasks[i];
                builder.AppendLine(
                    $"  {i + 1,2}. {TrimPath(task.Path)} | started={task.IsStarted} done={task.IsDone} progress={task.Progress:0.00} req={task.RequestCount} active={task.ActiveRequestCount}");
            }

            if (snapshot.Loader.Tasks.Count > count)
                builder.AppendLine($"  ... {snapshot.Loader.Tasks.Count - count} more");

            builder.AppendLine();
        }

        private void AppendCacheEntries(StringBuilder builder, ResourceProfilerSnapshot snapshot)
        {
            builder.AppendLine("Cache");
            if (snapshot.CacheEntries == null || snapshot.CacheEntries.Count == 0)
            {
                builder.AppendLine("  (empty)");
                builder.AppendLine();
                return;
            }

            var count = Math.Min(_maxRows, snapshot.CacheEntries.Count);
            for (var i = 0; i < count; i++)
            {
                var entry = snapshot.CacheEntries[i];
                builder.AppendLine(
                    $"  {i + 1,2}. ref={entry.RefCount,2} lru={entry.LruIndex,2} {entry.ResourceTypeName,-16} {TrimPath(entry.Path)}");
            }

            if (snapshot.CacheEntries.Count > count)
                builder.AppendLine($"  ... {snapshot.CacheEntries.Count - count} more");

            builder.AppendLine();
        }

        private void AppendHandles(StringBuilder builder, ResourceProfilerSnapshot snapshot)
        {
            builder.AppendLine("Handles");
            if (snapshot.Handles == null || snapshot.Handles.Count == 0)
            {
                builder.AppendLine("  (none)");
                return;
            }

            var count = Math.Min(_maxRows, snapshot.Handles.Count);
            for (var i = 0; i < count; i++)
            {
                var handle = snapshot.Handles[i];
                builder.AppendLine(
                    $"  #{handle.HandleId,3} {handle.Status,-9} {handle.RequestedTypeName,-16} ref={handle.OwnsReference,-5} p={handle.Progress:0.00} {TrimPath(handle.Path)}");

                if (!string.IsNullOrEmpty(handle.Error))
                    builder.AppendLine($"      error: {handle.Error}");
            }

            if (snapshot.Handles.Count > count)
                builder.AppendLine($"  ... {snapshot.Handles.Count - count} more");
        }

        private void OnGcCollectPressed()
        {
            GC.Collect();
            Debugger.Info("[ResourceProfiler] GC.Collect() triggered from overlay.");
            _elapsedSinceRefresh = 0.0;
            UpdateText();
        }

        private static string TrimPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "<empty>";

            const int maxLength = 72;
            if (path.Length <= maxLength)
                return path;

            return "..." + path[^ (maxLength - 3)..];
        }
    }
}
