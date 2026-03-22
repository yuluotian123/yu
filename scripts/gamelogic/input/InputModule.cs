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
    /// </summary>
    public class InputModule : Module, IInputModule, IProcessModule
    {
        private readonly InputBuffer _buffer = new InputBuffer();
        private readonly InputLayerManager _layerManager = new InputLayerManager();
        private readonly Dictionary<string, double> _holdTimes = new Dictionary<string, double>();

        private double _currentTime;
        private const float MaxBufferTime = 1f; // 最大缓冲时间

        public override int Priority => 10; // 输入模块优先级较高

        public override void OnInit()
        {
            // 初始化默认输入层
            _layerManager.AddLayer("Global", InputLayerManager.LayerPriority.Global);
            _layerManager.AddLayer("Combat", InputLayerManager.LayerPriority.Combat);
            _layerManager.AddLayer("UI", InputLayerManager.LayerPriority.UI);
            _layerManager.AddLayer("Camera", InputLayerManager.LayerPriority.Camera);

            Debugger.Info("[InputModule] Initialized with default layers.");
        }

        public override void Shutdown()
        {
            _buffer.Clear();
            _holdTimes.Clear();
        }

        public void Process(double elapseSeconds, double realElapseSeconds)
        {
            _currentTime += realElapseSeconds;

            // 清理过期的缓冲记录
            _buffer.CleanExpired(_currentTime, MaxBufferTime);

            // 清除层消费状态
            _layerManager.ClearAllConsumed();

            // 更新持续按下时间
            UpdateHoldTimes(realElapseSeconds);

            // 记录本帧按下的动作到缓冲
            RecordJustPressedActions();
        }

        private void UpdateHoldTimes(double deltaTime)
        {
            var toRemove = new List<string>();

            foreach (var kvp in _holdTimes)
            {
                string action = kvp.Key;
                if (Godot.Input.IsActionPressed(action))
                {
                    _holdTimes[action] += deltaTime;
                }
                else
                {
                    toRemove.Add(action);
                }
            }

            foreach (var action in toRemove)
            {
                _holdTimes.Remove(action);
            }
        }

        private void RecordJustPressedActions()
        {
            // 从 Godot InputMap 获取所有动作并记录按下事件
            var actions = GetAllActions();
            foreach (var action in actions)
            {
                if (Godot.Input.IsActionJustPressed(action))
                {
                    _buffer.RecordPress(action, _currentTime);
                    
                    if (!_holdTimes.ContainsKey(action))
                    {
                        _holdTimes[action] = 0;
                    }
                }
            }
        }

        private List<string> GetAllActions()
        {
            var actions = new List<string>();
            var actionList = Godot.InputMap.GetActions();
            
            foreach (var action in actionList)
            {
                actions.Add(action.ToString());
            }
            
            return actions;
        }

        // ==================== IInputModule 接口实现 ====================

        public bool IsPressed(string action)
        {
            // 检查 action 所属的层是否启用
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

        public bool IsBuffered(string action, float bufferTime)
        {
            return _buffer.IsBuffered(action, bufferTime, _currentTime);
        }

        public float GetHoldTime(string action)
        {
            return _holdTimes.TryGetValue(action, out var time) ? (float)time : 0f;
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

        public void ClearBuffer()
        {
            _buffer.Clear();
        }

        public void ConsumeBufferedAction(string action)
        {
            _buffer.Consume(action);
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
    }
}
