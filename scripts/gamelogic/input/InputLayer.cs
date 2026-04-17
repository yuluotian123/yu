using System;
using System.Collections.Generic;

namespace GameLogic.Input
{
    /// <summary>
    /// 单个输入层，负责维护当前帧 consume 状态和持续 held consume 状态。
    /// </summary>
    internal class InputLayer
    {
        /// <summary>
        /// 输入层名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 输入层优先级，数值越大优先级越高。
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// 当前输入层是否启用。
        /// </summary>
        public bool IsEnabled { get; set; }

        private readonly HashSet<string> _consumedActions = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _heldConsumedActions = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// 创建一个输入层实例。
        /// </summary>
        public InputLayer(string name, int priority)
        {
            Name = name;
            Priority = priority;
            IsEnabled = true;
        }

        /// <summary>
        /// 将一组 action 标记为当前帧已消费。
        /// </summary>
        public void ConsumeActions(IEnumerable<string> actions)
        {
            foreach (var action in actions)
            {
                if (!string.IsNullOrWhiteSpace(action))
                    _consumedActions.Add(action);
            }
        }

        /// <summary>
        /// 判断当前帧消费集合是否与目标 action 集合有交集。
        /// </summary>
        public bool OverlapsConsumedActions(HashSet<string> actions)
        {
            foreach (var action in actions)
            {
                if (_consumedActions.Contains(action))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 清空当前帧消费状态。
        /// </summary>
        public void ClearConsumed()
        {
            _consumedActions.Clear();
        }

        /// <summary>
        /// 将一组 action 标记为持续 held consume。
        /// </summary>
        public void ConsumeHeldActions(IEnumerable<string> actions)
        {
            foreach (var action in actions)
            {
                if (!string.IsNullOrWhiteSpace(action))
                    _heldConsumedActions.Add(action);
            }
        }

        /// <summary>
        /// 判断持续 held consume 集合是否与目标 action 集合有交集。
        /// </summary>
        public bool OverlapsHeldActions(HashSet<string> actions)
        {
            foreach (var action in actions)
            {
                if (_heldConsumedActions.Contains(action))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 从当前层移除指定的一组 held consume action。
        /// </summary>
        public void RemoveHeldActions(IEnumerable<string> actions)
        {
            foreach (var action in actions)
            {
                if (!string.IsNullOrWhiteSpace(action))
                    _heldConsumedActions.Remove(action);
            }
        }

        /// <summary>
        /// 清理已经失活的 held consume action。
        /// </summary>
        public void RemoveInactiveHeldActions(Func<string, bool> isActionActive)
        {
            var inactiveActions = new List<string>();
            foreach (var action in _heldConsumedActions)
            {
                if (!isActionActive(action))
                    inactiveActions.Add(action);
            }

            foreach (var action in inactiveActions)
            {
                _heldConsumedActions.Remove(action);
            }
        }

        /// <summary>
        /// 清空所有 held consume 状态。
        /// </summary>
        public void ClearHeldConsumed()
        {
            _heldConsumedActions.Clear();
        }
    }

    /// <summary>
    /// 统一管理输入层、action group 扩展和 consume/held consume 规则。
    /// </summary>
    internal class InputLayerManager
    {
        private readonly Dictionary<string, InputLayer> _layers = new Dictionary<string, InputLayer>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _actionGroups = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly List<InputLayer> _sortedLayers = new List<InputLayer>();
        private bool _isDirty = true;

        /// <summary>
        /// 预定义输入层优先级。
        /// </summary>
        public static class LayerPriority
        {
            public const int UI = 100;
            public const int Combat = 50;
            public const int Camera = 30;
            public const int Global = 0;
        }

        /// <summary>
        /// 预定义输入层名称。
        /// </summary>
        public static class LayerName
        {
            public const string UI = "UI";
            public const string Combat = "Combat";
            public const string Camera = "Camera";
            public const string Global = "Global";
        }

        /// <summary>
        /// 注册一个输入层。
        /// </summary>
        public void AddLayer(string name, int priority)
        {
            if (_layers.ContainsKey(name))
                return;

            _layers[name] = new InputLayer(name, priority);
            _isDirty = true;
        }

        /// <summary>
        /// 用新的 action group 配置整体替换当前缓存。
        /// </summary>
        public void ReplaceActionGroups(Dictionary<string, HashSet<string>> actionGroups)
        {
            _actionGroups.Clear();

            if (actionGroups == null)
                return;

            foreach (var entry in actionGroups)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                    continue;

                _actionGroups[entry.Key] = new HashSet<string>(entry.Value, StringComparer.Ordinal);
            }
        }

        /// <summary>
        /// 获取指定名称的输入层。
        /// </summary>
        public InputLayer GetLayer(string name)
        {
            return _layers.TryGetValue(name, out var layer) ? layer : null;
        }

        /// <summary>
        /// 启用一个输入层。
        /// </summary>
        public void EnableLayer(string name)
        {
            if (_layers.TryGetValue(name, out var layer))
                layer.IsEnabled = true;
        }

        /// <summary>
        /// 禁用一个输入层。
        /// </summary>
        public void DisableLayer(string name)
        {
            if (_layers.TryGetValue(name, out var layer))
                layer.IsEnabled = false;
        }

        /// <summary>
        /// 查询输入层是否启用。
        /// </summary>
        public bool IsLayerEnabled(string name)
        {
            return _layers.TryGetValue(name, out var layer) && layer.IsEnabled;
        }

        /// <summary>
        /// 清空所有层的当前帧 consume 状态。
        /// </summary>
        public void ClearAllConsumed()
        {
            foreach (var layer in _layers.Values)
            {
                layer.ClearConsumed();
            }
        }

        /// <summary>
        /// 清空所有层的 held consume 状态。
        /// </summary>
        public void ClearAllHeldLocks()
        {
            foreach (var layer in _layers.Values)
            {
                layer.ClearHeldConsumed();
            }
        }

        /// <summary>
        /// 根据 action 当前是否仍然活跃，刷新 held consume 状态。
        /// </summary>
        public void UpdateHeldLocks(Func<string, bool> isActionActive)
        {
            foreach (var layer in _layers.Values)
            {
                if (!layer.IsEnabled)
                {
                    layer.ClearHeldConsumed();
                    continue;
                }

                layer.RemoveInactiveHeldActions(isActionActive);
            }
        }

        /// <summary>
        /// 将 action 及其同组 action 一起标记为当前帧已消费。
        /// </summary>
        public void ConsumeAction(InputLayer layer, string action)
        {
            if (layer == null || string.IsNullOrWhiteSpace(action))
                return;

            layer.ConsumeActions(ExpandActionGroup(new[] { action }));
        }

        /// <summary>
        /// 尝试在指定层建立 held consume。
        /// 成功后，同组 action 也会一起被占用。
        /// </summary>
        public bool TryAcquireHeldLock(InputLayer layer, IEnumerable<string> actions)
        {
            var requestedActions = ExpandActionGroup(actions);
            if (requestedActions.Count == 0)
                return false;

            if (!CanAcquireHeldLock(layer, requestedActions))
                return false;

            ClearLowerPriorityHeldLocks(layer, requestedActions);
            layer.ConsumeHeldActions(requestedActions);
            return true;
        }

        /// <summary>
        /// 查询某个 action 是否已被更高优先级输入层消费。
        /// 同组 action 会一起参与判断。
        /// </summary>
        public bool IsActionConsumed(string action, int currentPriority, bool includeSamePriority = false)
        {
            var requestedActions = ExpandActionGroup(new[] { action });
            foreach (var layer in GetSortedLayers())
            {
                if (includeSamePriority ? layer.Priority < currentPriority : layer.Priority <= currentPriority)
                    break;

                if (layer.OverlapsConsumedActions(requestedActions))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 按 action 前缀推导所属输入层。
        /// </summary>
        public string GetActionLayer(string action)
        {
            if (action.StartsWith("ui_", StringComparison.Ordinal))
                return LayerName.UI;
            if (action.StartsWith("combat_", StringComparison.Ordinal))
                return LayerName.Combat;
            if (action.StartsWith("camera_", StringComparison.Ordinal))
                return LayerName.Camera;

            return LayerName.Global;
        }

        /// <summary>
        /// 查询 action 所属输入层当前是否启用。
        /// </summary>
        public bool IsActionLayerEnabled(string action)
        {
            string layerName = GetActionLayer(action);
            return IsLayerEnabled(layerName);
        }

        private HashSet<string> ExpandActionGroup(IEnumerable<string> actions)
        {
            var expandedActions = new HashSet<string>(StringComparer.Ordinal);

            foreach (var action in actions)
            {
                if (string.IsNullOrWhiteSpace(action))
                    continue;

                expandedActions.Add(action);

                if (_actionGroups.TryGetValue(action, out var actionGroup))
                    expandedActions.UnionWith(actionGroup);
            }

            return expandedActions;
        }

        private bool CanAcquireHeldLock(InputLayer layer, HashSet<string> requestedActions)
        {
            foreach (var otherLayer in GetSortedLayers())
            {
                if (string.Equals(otherLayer.Name, layer.Name, StringComparison.Ordinal))
                    continue;

                if (!otherLayer.OverlapsHeldActions(requestedActions))
                    continue;

                if (otherLayer.Priority >= layer.Priority)
                    return false;
            }

            return true;
        }

        private void ClearLowerPriorityHeldLocks(InputLayer layer, HashSet<string> requestedActions)
        {
            foreach (var otherLayer in GetSortedLayers())
            {
                if (string.Equals(otherLayer.Name, layer.Name, StringComparison.Ordinal))
                    continue;

                if (otherLayer.Priority < layer.Priority)
                    otherLayer.RemoveHeldActions(requestedActions);
            }
        }

        private List<InputLayer> GetSortedLayers()
        {
            if (_isDirty)
            {
                _sortedLayers.Clear();
                _sortedLayers.AddRange(_layers.Values);
                _sortedLayers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                _isDirty = false;
            }

            return _sortedLayers;
        }
    }
}
