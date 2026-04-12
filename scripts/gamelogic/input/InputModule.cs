using System.Collections.Generic;
using Framework;
using Godot;

namespace GameLogic.Input
{
    /// <summary>
    /// 输入模块实现。
    /// 基于 Godot 原生 Input 系统和 InputEvent 机制，提供动作游戏所需的增强功能：
    /// - 输入缓冲（Input Buffer）：允许提前输入
    /// - 输入层管理（Layer Management）：分层控制输入优先级
    /// - 长按时间追踪（Hold Time Tracking）：追踪按键持续时间
    /// 
    /// 注意：Godot 的输入消费机制通过 _Input() / _UnhandledInput() 和 Viewport.SetInputAsHandled() 实现，
    /// 本模块不重复实现消费机制，而是专注于缓冲和层管理。
    /// 
    /// 改进：使用统一的 InputTracker 替代分离的 InputBuffer 和 HoldTime，解决时序问题。
    /// </summary>
    public class InputModule : Module, IInputModule, IProcessModule
    {
        private readonly InputTracker _tracker = new InputTracker();
        private readonly InputLayerManager _layerManager = new InputLayerManager();
        private List<string> _cachedActions;

        private double _currentTime;
        private Vector2 _mousePositionLastFrame = Vector2.Zero;
        private Vector2 _mouseDeltaFrame = Vector2.Zero;
        private bool _hasMousePositionLastFrame;

        public override int Priority => 10;

        public override void OnInit()
        {
            _layerManager.AddLayer("Global", InputLayerManager.LayerPriority.Global);
            _layerManager.AddLayer("Combat", InputLayerManager.LayerPriority.Combat);
            _layerManager.AddLayer("UI", InputLayerManager.LayerPriority.UI);
            _layerManager.AddLayer("Camera", InputLayerManager.LayerPriority.Camera);

            CacheActions();
            Debugger.Info("[InputModule] Initialized with unified InputTracker.");
        }

        public override void Shutdown()
        {
            _tracker.Clear();
            _cachedActions?.Clear();
            _mousePositionLastFrame = Vector2.Zero;
            _mouseDeltaFrame = Vector2.Zero;
            _hasMousePositionLastFrame = false;
        }

        public void Process(double elapseSeconds, double realElapseSeconds)
        {
            _currentTime += realElapseSeconds;
            UpdateMouseDelta();

            _tracker.Update(_currentTime, realElapseSeconds);
            _layerManager.ClearAllConsumed();
            RecordInputEvents();
        }

        private void CacheActions()
        {
            _cachedActions = new List<string>();
            var actionList = Godot.InputMap.GetActions();

            foreach (var action in actionList)
            {
                _cachedActions.Add(action.ToString());
            }
        }

        private void UpdateMouseDelta()
        {
            if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
            {
                _mouseDeltaFrame = Vector2.Zero;
                _hasMousePositionLastFrame = false;
                return;
            }

            Vector2 currentMousePosition = tree.Root.GetMousePosition();

            if (!_hasMousePositionLastFrame)
            {
                _mouseDeltaFrame = Vector2.Zero;
                _mousePositionLastFrame = currentMousePosition;
                _hasMousePositionLastFrame = true;
                return;
            }

            _mouseDeltaFrame = currentMousePosition - _mousePositionLastFrame;
            _mousePositionLastFrame = currentMousePosition;
        }

        private void RecordInputEvents()
        {
            foreach (var action in _cachedActions)
            {
                if (Godot.Input.IsActionJustPressed(action))
                {
                    _tracker.RecordPress(action, _currentTime);
                }
                else if (Godot.Input.IsActionJustReleased(action))
                {
                    _tracker.RecordRelease(action, _currentTime);
                }
            }
        }

        public bool IsPressed(string action)
        {
            if (!_layerManager.IsActionLayerEnabled(action))
                return false;

            return Godot.Input.IsActionPressed(action);
        }

        public bool IsJustPressed(string action)
        {
            if (!_layerManager.IsActionLayerEnabled(action))
                return false;

            return Godot.Input.IsActionJustPressed(action);
        }

        public bool IsJustReleased(string action)
        {
            if (!_layerManager.IsActionLayerEnabled(action))
                return false;

            return Godot.Input.IsActionJustReleased(action);
        }

        public bool TryHandleJustPressed(string action, string handlerLayer = null)
        {
            if (!TryGetEnabledHandlerLayer(action, handlerLayer, out var layer))
                return false;

            if (_layerManager.IsActionConsumed(action, layer.Priority))
                return false;

            if (!Godot.Input.IsActionJustPressed(action))
                return false;

            layer.ConsumeAction(action);
            return true;
        }

        public bool TryHandleJustReleased(string action, string handlerLayer = null)
        {
            if (!TryGetEnabledHandlerLayer(action, handlerLayer, out var layer))
                return false;

            if (_layerManager.IsActionConsumed(action, layer.Priority))
                return false;

            if (!Godot.Input.IsActionJustReleased(action))
                return false;

            layer.ConsumeAction(action);
            return true;
        }

        public float GetActionStrength(string action)
        {
            if (!_layerManager.IsActionLayerEnabled(action))
                return 0f;

            return Godot.Input.GetActionStrength(action);
        }

        public Vector2 GetAxis(string negativeX, string positiveX, string negativeY, string positiveY)
        {
            float x = Godot.Input.GetAxis(negativeX, positiveX);
            float y = Godot.Input.GetAxis(negativeY, positiveY);
            return new Vector2(x, y);
        }

        public Vector2 GetVector(string negativeX, string positiveX, string negativeY, string positiveY, float deadzone = -1f)
        {
            return Godot.Input.GetVector(negativeX, positiveX, negativeY, positiveY, deadzone);
        }

        public Vector2 GetMouseDelta()
        {
            return _mouseDeltaFrame;
        }

        public bool IsBuffered(string action, float bufferTime)
        {
            return _tracker.IsBuffered(action, bufferTime, _currentTime);
        }

        public float GetHoldTime(string action)
        {
            return _tracker.GetHoldTime(action);
        }

        public void EnableLayer(string layerName)
        {
            _layerManager.EnableLayer(layerName);
        }

        public void DisableLayer(string layerName)
        {
            _layerManager.DisableLayer(layerName);
        }

        public bool IsLayerEnabled(string layerName)
        {
            return _layerManager.IsLayerEnabled(layerName);
        }

        public void ConsumeAction(string action, string handlerLayer = null)
        {
            if (!TryGetEnabledHandlerLayer(action, handlerLayer, out var layer))
                return;

            layer.ConsumeAction(action);
        }

        public bool IsActionConsumed(string action, string handlerLayer = null)
        {
            if (!TryGetEnabledHandlerLayer(action, handlerLayer, out var layer))
                return false;

            return _layerManager.IsActionConsumed(action, layer.Priority);
        }

        public void ClearBuffer()
        {
            _tracker.Clear();
        }

        public void ConsumeBufferedAction(string action)
        {
            _tracker.Consume(action);
        }

        public void SimulateInputEvent(InputEvent @event)
        {
            Godot.Input.ParseInputEvent(@event);
        }

        public void SimulateActionPress(string action, float strength = 1.0f)
        {
            var actionEvent = new InputEventAction
            {
                Action = action,
                Pressed = true,
                Strength = strength
            };
            Godot.Input.ParseInputEvent(actionEvent);
        }

        public void SimulateActionRelease(string action)
        {
            var actionEvent = new InputEventAction
            {
                Action = action,
                Pressed = false,
                Strength = 0f
            };
            Godot.Input.ParseInputEvent(actionEvent);
        }

        private bool TryGetEnabledHandlerLayer(string action, string handlerLayer, out InputLayer layer)
        {
            string layerName = ResolveHandlerLayerName(action, handlerLayer);
            layer = _layerManager.GetLayer(layerName);

            if (layer == null || !layer.IsEnabled)
                return false;

            return true;
        }

        private string ResolveHandlerLayerName(string action, string handlerLayer)
        {
            if (!string.IsNullOrWhiteSpace(handlerLayer))
                return handlerLayer;

            return _layerManager.GetActionLayer(action);
        }
    }
}
