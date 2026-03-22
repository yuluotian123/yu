using System.Collections.Generic;

namespace GameLogic.Input
{
    /// <summary>
    /// 输入层。
    /// 用于分层管理输入（如 UI 层、战斗层、全局层），支持优先级和启用/禁用控制。
    /// </summary>
    public class InputLayer
    {
        public string Name { get; }
        public int Priority { get; }
        public bool IsEnabled { get; set; }

        private readonly HashSet<string> _consumedActions = new HashSet<string>();

        public InputLayer(string name, int priority)
        {
            Name = name;
            Priority = priority;
            IsEnabled = true;
        }

        /// <summary>
        /// 消费动作（阻止低优先级层接收）。
        /// </summary>
        public void ConsumeAction(string action)
        {
            _consumedActions.Add(action);
        }

        /// <summary>
        /// 检查动作是否被消费。
        /// </summary>
        public bool IsActionConsumed(string action)
        {
            return _consumedActions.Contains(action);
        }

        /// <summary>
        /// 清除本帧消费的动作。
        /// </summary>
        public void ClearConsumed()
        {
            _consumedActions.Clear();
        }
    }

    /// <summary>
    /// 输入层管理器。
    /// 支持通过 action 名称前缀自动识别所属层。
    /// 命名约定：
    /// - ui_xxx → UI 层
    /// - combat_xxx → Combat 层
    /// - camera_xxx → Camera 层
    /// - 其他 → Global 层
    /// </summary>
    public class InputLayerManager
    {
        private readonly Dictionary<string, InputLayer> _layers = new Dictionary<string, InputLayer>();
        private readonly List<InputLayer> _sortedLayers = new List<InputLayer>();
        private bool _isDirty = true;

        /// <summary>
        /// 预定义的层优先级。
        /// </summary>
        public static class LayerPriority
        {
            public const int UI = 100;
            public const int Combat = 50;
            public const int Camera = 30;
            public const int Global = 0;
        }

        /// <summary>
        /// 预定义的层名称。
        /// </summary>
        public static class LayerName
        {
            public const string UI = "UI";
            public const string Combat = "Combat";
            public const string Camera = "Camera";
            public const string Global = "Global";
        }

        /// <summary>
        /// 添加输入层。
        /// </summary>
        public void AddLayer(string name, int priority)
        {
            if (!_layers.ContainsKey(name))
            {
                _layers[name] = new InputLayer(name, priority);
                _isDirty = true;
            }
        }

        /// <summary>
        /// 获取输入层。
        /// </summary>
        public InputLayer GetLayer(string name)
        {
            return _layers.TryGetValue(name, out var layer) ? layer : null;
        }

        /// <summary>
        /// 启用输入层。
        /// </summary>
        public void EnableLayer(string name)
        {
            if (_layers.TryGetValue(name, out var layer))
            {
                layer.IsEnabled = true;
            }
        }

        /// <summary>
        /// 禁用输入层。
        /// </summary>
        public void DisableLayer(string name)
        {
            if (_layers.TryGetValue(name, out var layer))
            {
                layer.IsEnabled = false;
            }
        }

        /// <summary>
        /// 检查输入层是否启用。
        /// </summary>
        public bool IsLayerEnabled(string name)
        {
            return _layers.TryGetValue(name, out var layer) && layer.IsEnabled;
        }

        /// <summary>
        /// 获取按优先级排序的启用层列表。
        /// </summary>
        public List<InputLayer> GetSortedEnabledLayers()
        {
            if (_isDirty)
            {
                _sortedLayers.Clear();
                _sortedLayers.AddRange(_layers.Values);
                _sortedLayers.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // 降序
                _isDirty = false;
            }

            return _sortedLayers;
        }

        /// <summary>
        /// 清除所有层的消费状态（每帧调用）。
        /// </summary>
        public void ClearAllConsumed()
        {
            foreach (var layer in _layers.Values)
            {
                layer.ClearConsumed();
            }
        }

        /// <summary>
        /// 检查动作是否被任何高优先级层消费。
        /// </summary>
        public bool IsActionConsumed(string action, int currentPriority)
        {
            foreach (var layer in GetSortedEnabledLayers())
            {
                if (layer.Priority <= currentPriority)
                    break;

                if (layer.IsActionConsumed(action))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 根据 action 名称前缀自动识别所属层。
        /// 命名约定：
        /// - ui_xxx → UI 层
        /// - combat_xxx → Combat 层
        /// - camera_xxx → Camera 层
        /// - 其他 → Global 层
        /// </summary>
        public string GetActionLayer(string action)
        {
            if (action.StartsWith("ui_"))
                return LayerName.UI;
            if (action.StartsWith("combat_"))
                return LayerName.Combat;
            if (action.StartsWith("camera_"))
                return LayerName.Camera;
            
            return LayerName.Global;
        }

        /// <summary>
        /// 检查 action 所属的层是否启用。
        /// </summary>
        public bool IsActionLayerEnabled(string action)
        {
            string layerName = GetActionLayer(action);
            return IsLayerEnabled(layerName);
        }
    }
}
