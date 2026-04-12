using System;
using System.Collections.Generic;
using Framework;
using Godot;

namespace GameLogic.Input
{
    /// <summary>
    /// 基于 Godot Input 和 InputMap 的输入模块。
    /// 业务侧始终使用基础 action 名，模块内部负责映射到真实的 InputMap action 名。
    /// </summary>
    public class InputModule : Module, IInputModule, IProcessModule
    {
        private sealed class ParsedActionInfo
        {
            public string RawActionName { get; }
            public string BaseActionName { get; }
            public HashSet<string> GroupIds { get; }

            public ParsedActionInfo(string rawActionName, string baseActionName, HashSet<string> groupIds)
            {
                RawActionName = rawActionName;
                BaseActionName = baseActionName;
                GroupIds = groupIds;
            }
        }

        private readonly InputTracker _tracker = new InputTracker();
        private readonly InputLayerManager _layerManager = new InputLayerManager();
        
        // 有效 action 缓存只保留可正常参与运行时查询的解析结果：
        // 1. RawActionName 是 Godot InputMap 里的真实 action 名
        // 2. BaseActionName 是业务侧使用的基础名
        // 3. 两套索引都指向同一个 ParsedActionInfo，避免重复存储
        private readonly Dictionary<string, ParsedActionInfo> _baseActionInfos = new Dictionary<string, ParsedActionInfo>(StringComparer.Ordinal);
        private readonly Dictionary<string, ParsedActionInfo> _rawActionInfos = new Dictionary<string, ParsedActionInfo>(StringComparer.Ordinal);
        private readonly List<ParsedActionInfo> _parsedActions = new List<ParsedActionInfo>();

        private double _currentTime;
        private Vector2 _mousePositionLastFrame = Vector2.Zero;
        private Vector2 _mouseDeltaFrame = Vector2.Zero;
        private bool _hasMousePositionLastFrame;

        /// <summary>
        /// 模块优先级。
        /// </summary>
        public override int Priority => 10;

        /// <summary>
        /// 初始化输入层和 InputMap 缓存。
        /// </summary>
        public override void OnInit()
        {
            _layerManager.AddLayer(InputLayerManager.LayerName.Global, InputLayerManager.LayerPriority.Global);
            _layerManager.AddLayer(InputLayerManager.LayerName.Combat, InputLayerManager.LayerPriority.Combat);
            _layerManager.AddLayer(InputLayerManager.LayerName.UI, InputLayerManager.LayerPriority.UI);
            _layerManager.AddLayer(InputLayerManager.LayerName.Camera, InputLayerManager.LayerPriority.Camera);

            RefreshActionCache();
            Debugger.Info("[InputModule] Initialized with action alias and group parsing.");
        }

        /// <summary>
        /// 清理输入缓存和运行时状态。
        /// </summary>
        public override void Shutdown()
        {
            _tracker.Clear();
            _layerManager.ClearAllHeldLocks();
            _layerManager.ReplaceActionGroups(new Dictionary<string, HashSet<string>>(StringComparer.Ordinal));
            _parsedActions.Clear();
            _baseActionInfos.Clear();
            _rawActionInfos.Clear();
            _mousePositionLastFrame = Vector2.Zero;
            _mouseDeltaFrame = Vector2.Zero;
            _hasMousePositionLastFrame = false;
        }

        /// <summary>
        /// 每帧刷新输入状态、清理 consume，并记录输入事件。
        /// </summary>
        public void Process(double elapseSeconds, double realElapseSeconds)
        {
            _currentTime += realElapseSeconds;
            UpdateMouseDelta();

            _tracker.Update(_currentTime, realElapseSeconds);
            _layerManager.ClearAllConsumed();
            _layerManager.UpdateHeldLocks(IsActionStillActive);
            RecordInputEvents();
        }

        /// <inheritdoc />
        public bool IsPressed(string action)
        {
            if (!TryGetQueryableActionInfo(action, out var actionInfo))
                return false;

            return Godot.Input.IsActionPressed(actionInfo.RawActionName);
        }

        /// <inheritdoc />
        public bool IsJustPressed(string action)
        {
            if (!TryGetQueryableActionInfo(action, out var actionInfo))
                return false;

            return Godot.Input.IsActionJustPressed(actionInfo.RawActionName);
        }

        /// <inheritdoc />
        public bool IsJustReleased(string action)
        {
            if (!TryGetQueryableActionInfo(action, out var actionInfo))
                return false;

            return Godot.Input.IsActionJustReleased(actionInfo.RawActionName);
        }

        /// <inheritdoc />
        public bool TryHandlePressed(string action, string handlerLayer = null)
        {
            if (!TryGetHandleableActionInfo(action, handlerLayer, out var actionInfo, out var layer))
                return false;

            if (!Godot.Input.IsActionPressed(actionInfo.RawActionName))
                return false;

            return TryAcquireHeldActions(layer, new[] { actionInfo.BaseActionName });
        }

        /// <inheritdoc />
        public bool TryHandleJustPressed(string action, string handlerLayer = null)
        {
            if (!TryGetHandleableActionInfo(action, handlerLayer, out var actionInfo, out var layer))
                return false;

            if (_layerManager.IsActionConsumed(actionInfo.BaseActionName, layer.Priority))
                return false;

            if (!Godot.Input.IsActionJustPressed(actionInfo.RawActionName))
                return false;

            _layerManager.ConsumeAction(layer, actionInfo.BaseActionName);
            return true;
        }

        /// <inheritdoc />
        public bool TryHandleJustReleased(string action, string handlerLayer = null)
        {
            if (!TryGetHandleableActionInfo(action, handlerLayer, out var actionInfo, out var layer))
                return false;

            if (_layerManager.IsActionConsumed(actionInfo.BaseActionName, layer.Priority))
                return false;

            if (!Godot.Input.IsActionJustReleased(actionInfo.RawActionName))
                return false;

            _layerManager.ConsumeAction(layer, actionInfo.BaseActionName);
            return true;
        }

        /// <inheritdoc />
        public float GetActionStrength(string action)
        {
            if (!TryGetQueryableActionInfo(action, out var actionInfo))
                return 0f;

            return Godot.Input.GetActionStrength(actionInfo.RawActionName);
        }

        /// <inheritdoc />
        public bool TryHandleActionStrength(string action, out float strength, string handlerLayer = null)
        {
            strength = 0f;
            if (!TryGetHandleableActionInfo(action, handlerLayer, out var actionInfo, out var layer))
                return false;

            float currentStrength = Godot.Input.GetActionStrength(actionInfo.RawActionName);
            if (currentStrength <= 0f)
                return false;

            if (!TryAcquireHeldActions(layer, new[] { actionInfo.BaseActionName }))
                return false;

            strength = currentStrength;
            return true;
        }

        /// <inheritdoc />
        public Vector2 GetAxis(string negativeX, string positiveX, string negativeY, string positiveY)
        {
            if (!TryResolveDirectionalActions(negativeX, positiveX, negativeY, positiveY, out var actionInfos))
                return Vector2.Zero;

            if (!AreActionLayersEnabled(actionInfos))
                return Vector2.Zero;

            return ReadAxis(actionInfos);
        }

        /// <inheritdoc />
        public bool TryHandleAxis(
            string negativeX,
            string positiveX,
            string negativeY,
            string positiveY,
            out Vector2 axis,
            string handlerLayer = null)
        {
            axis = Vector2.Zero;
            if (!TryResolveDirectionalActions(negativeX, positiveX, negativeY, positiveY, out var actionInfos))
                return false;

            if (!TryGetEnabledHandlerLayer(actionInfos, handlerLayer, out var layer))
                return false;

            Vector2 currentAxis = ReadAxis(actionInfos);
            if (currentAxis == Vector2.Zero)
                return false;

            if (!TryAcquireHeldActions(layer, CollectBaseActionNames(actionInfos)))
                return false;

            axis = currentAxis;
            return true;
        }

        /// <inheritdoc />
        public Vector2 GetVector(string negativeX, string positiveX, string negativeY, string positiveY, float deadzone = -1f)
        {
            if (!TryResolveDirectionalActions(negativeX, positiveX, negativeY, positiveY, out var actionInfos))
                return Vector2.Zero;

            if (!AreActionLayersEnabled(actionInfos))
                return Vector2.Zero;

            return ReadVector(actionInfos, deadzone);
        }

        /// <inheritdoc />
        public bool TryHandleVector(
            string negativeX,
            string positiveX,
            string negativeY,
            string positiveY,
            out Vector2 vector,
            string handlerLayer = null,
            float deadzone = -1f)
        {
            vector = Vector2.Zero;
            if (!TryResolveDirectionalActions(negativeX, positiveX, negativeY, positiveY, out var actionInfos))
                return false;

            if (!TryGetEnabledHandlerLayer(actionInfos, handlerLayer, out var layer))
                return false;

            Vector2 currentVector = ReadVector(actionInfos, deadzone);
            if (currentVector == Vector2.Zero)
                return false;

            if (!TryAcquireHeldActions(layer, CollectBaseActionNames(actionInfos)))
                return false;

            vector = currentVector;
            return true;
        }

        /// <inheritdoc />
        public Vector2 GetMouseDelta()
        {
            return _mouseDeltaFrame;
        }

        /// <inheritdoc />
        public bool IsBuffered(string action, float bufferTime)
        {
            string baseAction = NormalizeBaseActionName(action);
            return _tracker.IsBuffered(baseAction, bufferTime, _currentTime);
        }

        /// <inheritdoc />
        public float GetHoldTime(string action)
        {
            string baseAction = NormalizeBaseActionName(action);
            return _tracker.GetHoldTime(baseAction);
        }

        /// <inheritdoc />
        public void EnableLayer(string layerName)
        {
            _layerManager.EnableLayer(layerName);
        }

        /// <inheritdoc />
        public void DisableLayer(string layerName)
        {
            _layerManager.DisableLayer(layerName);
        }

        /// <inheritdoc />
        public bool IsLayerEnabled(string layerName)
        {
            return _layerManager.IsLayerEnabled(layerName);
        }

        /// <inheritdoc />
        public void RefreshActionCache()
        {
            ClearActionCache();

            var duplicateBaseActions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var actionVariant in Godot.InputMap.GetActions())
            {
                if (!TryParseActionName(actionVariant.ToString(), out var parsedAction))
                    continue;

                if (duplicateBaseActions.Contains(parsedAction.BaseActionName))
                    continue;

                if (_baseActionInfos.TryGetValue(parsedAction.BaseActionName, out var existingAction))
                {
                    duplicateBaseActions.Add(parsedAction.BaseActionName);
                    RemoveParsedAction(existingAction);

                    Debugger.Warn(
                        $"[InputModule] Duplicate base action name '{parsedAction.BaseActionName}' detected in InputMap. " +
                        "This base action will be skipped until the conflict is resolved.");
                    continue;
                }

                AddParsedAction(parsedAction);
            }

            _layerManager.ReplaceActionGroups(BuildActionGroupsFromParsedActions());
        }

        /// <inheritdoc />
        public void ConsumeAction(string action, string handlerLayer = null)
        {
            string baseAction = NormalizeBaseActionName(action);
            if (!TryGetEnabledHandlerLayer(baseAction, handlerLayer, out var layer))
                return;

            _layerManager.ConsumeAction(layer, baseAction);
        }

        /// <inheritdoc />
        public bool IsActionConsumed(string action, string handlerLayer = null)
        {
            string baseAction = NormalizeBaseActionName(action);
            if (!TryGetEnabledHandlerLayer(baseAction, handlerLayer, out var layer))
                return false;

            return _layerManager.IsActionConsumed(baseAction, layer.Priority);
        }

        /// <inheritdoc />
        public void ClearBuffer()
        {
            _tracker.Clear();
        }

        /// <inheritdoc />
        public void ConsumeBufferedAction(string action)
        {
            string baseAction = NormalizeBaseActionName(action);
            _tracker.Consume(baseAction);
        }

        /// <inheritdoc />
        public void SimulateInputEvent(InputEvent @event)
        {
            Godot.Input.ParseInputEvent(@event);
        }

        /// <inheritdoc />
        public void SimulateActionPress(string action, float strength = 1.0f)
        {
            if (!TryResolveActionInfo(action, out var actionInfo))
                return;

            var actionEvent = new InputEventAction
            {
                Action = actionInfo.RawActionName,
                Pressed = true,
                Strength = strength
            };
            Godot.Input.ParseInputEvent(actionEvent);
        }

        /// <inheritdoc />
        public void SimulateActionRelease(string action)
        {
            if (!TryResolveActionInfo(action, out var actionInfo))
                return;

            var actionEvent = new InputEventAction
            {
                Action = actionInfo.RawActionName,
                Pressed = false,
                Strength = 0f
            };
            Godot.Input.ParseInputEvent(actionEvent);
        }

        private void ClearActionCache()
        {
            _parsedActions.Clear();
            _baseActionInfos.Clear();
            _rawActionInfos.Clear();
        }

        private void AddParsedAction(ParsedActionInfo parsedAction)
        {
            _parsedActions.Add(parsedAction);
            _baseActionInfos[parsedAction.BaseActionName] = parsedAction;
            _rawActionInfos[parsedAction.RawActionName] = parsedAction;
        }

        private void RemoveParsedAction(ParsedActionInfo parsedAction)
        {
            _parsedActions.Remove(parsedAction);
            _baseActionInfos.Remove(parsedAction.BaseActionName);
            _rawActionInfos.Remove(parsedAction.RawActionName);
        }

        private Dictionary<string, HashSet<string>> BuildActionGroupsFromParsedActions()
        {
            var groupIdToBaseActions = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var parsedAction in _parsedActions)
            {
                foreach (var groupId in parsedAction.GroupIds)
                {
                    if (!groupIdToBaseActions.TryGetValue(groupId, out var baseActions))
                    {
                        baseActions = new HashSet<string>(StringComparer.Ordinal);
                        groupIdToBaseActions[groupId] = baseActions;
                    }

                    baseActions.Add(parsedAction.BaseActionName);
                }
            }

            var actionGroups = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var groupEntry in groupIdToBaseActions)
            {
                foreach (var baseAction in groupEntry.Value)
                {
                    if (!actionGroups.TryGetValue(baseAction, out var groupedActions))
                    {
                        groupedActions = new HashSet<string>(StringComparer.Ordinal);
                        actionGroups[baseAction] = groupedActions;
                    }

                    groupedActions.UnionWith(groupEntry.Value);
                }
            }

            return actionGroups;
        }

        private bool TryParseActionName(string rawActionName, out ParsedActionInfo parsedAction)
        {
            parsedAction = null;
            if (string.IsNullOrWhiteSpace(rawActionName))
                return false;

            string[] parts = rawActionName.Split('|', StringSplitOptions.None);
            string baseActionName = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(baseActionName))
            {
                Debugger.Warn($"[InputModule] Invalid InputMap action name '{rawActionName}'. Base action name is empty.");
                return false;
            }

            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 1; index < parts.Length; index++)
            {
                string groupId = parts[index].Trim();
                if (string.IsNullOrWhiteSpace(groupId))
                {
                    Debugger.Warn($"[InputModule] Invalid group id in InputMap action '{rawActionName}'. Empty group ids are ignored.");
                    continue;
                }

                groupIds.Add(groupId);
            }

            parsedAction = new ParsedActionInfo(rawActionName, baseActionName, groupIds);
            return true;
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
            foreach (var parsedAction in _parsedActions)
            {
                if (Godot.Input.IsActionJustPressed(parsedAction.RawActionName))
                {
                    _tracker.RecordPress(parsedAction.BaseActionName, _currentTime);
                }
                else if (Godot.Input.IsActionJustReleased(parsedAction.RawActionName))
                {
                    _tracker.RecordRelease(parsedAction.BaseActionName, _currentTime);
                }
            }
        }

        private bool TryGetQueryableActionInfo(string action, out ParsedActionInfo actionInfo)
        {
            actionInfo = null;
            if (!TryResolveActionInfo(action, out actionInfo))
                return false;

            return _layerManager.IsActionLayerEnabled(actionInfo.BaseActionName);
        }

        private bool TryGetHandleableActionInfo(
            string action,
            string handlerLayer,
            out ParsedActionInfo actionInfo,
            out InputLayer layer)
        {
            layer = null;
            if (!TryResolveActionInfo(action, out actionInfo))
                return false;

            return TryGetEnabledHandlerLayer(actionInfo.BaseActionName, handlerLayer, out layer);
        }

        private bool TryResolveDirectionalActions(
            string negativeX,
            string positiveX,
            string negativeY,
            string positiveY,
            out ParsedActionInfo[] actionInfos)
        {
            actionInfos = new ParsedActionInfo[4];
            return TryResolveActionInfo(negativeX, out actionInfos[0]) &&
                   TryResolveActionInfo(positiveX, out actionInfos[1]) &&
                   TryResolveActionInfo(negativeY, out actionInfos[2]) &&
                   TryResolveActionInfo(positiveY, out actionInfos[3]);
        }

        private bool AreActionLayersEnabled(ParsedActionInfo[] actionInfos)
        {
            foreach (var actionInfo in actionInfos)
            {
                if (!_layerManager.IsActionLayerEnabled(actionInfo.BaseActionName))
                    return false;
            }

            return true;
        }

        private bool TryAcquireHeldActions(InputLayer layer, IEnumerable<string> baseActions)
        {
            // action group 的展开统一放在 LayerManager 里，
            // 分组来源则由 InputMap action 名里的 groupId 解析结果驱动。
            return _layerManager.TryAcquireHeldLock(layer, baseActions);
        }

        private bool IsActionStillActive(string action)
        {
            if (!TryResolveActionInfo(action, out var actionInfo))
                return false;

            return Godot.Input.GetActionStrength(actionInfo.RawActionName) > 0f ||
                   Godot.Input.IsActionPressed(actionInfo.RawActionName);
        }

        private bool TryResolveActionInfo(string action, out ParsedActionInfo actionInfo)
        {
            string baseAction = NormalizeBaseActionName(action);
            return _baseActionInfos.TryGetValue(baseAction, out actionInfo);
        }

        private string NormalizeBaseActionName(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return string.Empty;

            if (_rawActionInfos.TryGetValue(action, out var parsedAction))
                return parsedAction.BaseActionName;

            int separatorIndex = action.IndexOf('|');
            if (separatorIndex >= 0)
                return action.Substring(0, separatorIndex);

            return action;
        }

        private bool TryGetEnabledHandlerLayer(ParsedActionInfo[] actionInfos, string handlerLayer, out InputLayer layer)
        {
            layer = null;
            if (actionInfos == null || actionInfos.Length == 0)
                return false;

            if (!string.IsNullOrWhiteSpace(handlerLayer))
                return TryGetEnabledHandlerLayer(actionInfos[0].BaseActionName, handlerLayer, out layer);

            string expectedLayerName = _layerManager.GetActionLayer(actionInfos[0].BaseActionName);
            for (int index = 1; index < actionInfos.Length; index++)
            {
                string currentLayerName = _layerManager.GetActionLayer(actionInfos[index].BaseActionName);
                if (!string.Equals(expectedLayerName, currentLayerName, StringComparison.Ordinal))
                {
                    Debugger.Warn("[InputModule] Directional actions belong to different default layers. Please align their naming prefixes or pass an explicit handlerLayer.");
                    return false;
                }
            }

            return TryGetEnabledHandlerLayer(actionInfos[0].BaseActionName, null, out layer);
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

            return _layerManager.GetActionLayer(NormalizeBaseActionName(action));
        }

        private static Vector2 ReadAxis(ParsedActionInfo[] actionInfos)
        {
            float x = Godot.Input.GetAxis(actionInfos[0].RawActionName, actionInfos[1].RawActionName);
            float y = Godot.Input.GetAxis(actionInfos[2].RawActionName, actionInfos[3].RawActionName);
            return new Vector2(x, y);
        }

        private static Vector2 ReadVector(ParsedActionInfo[] actionInfos, float deadzone)
        {
            return Godot.Input.GetVector(
                actionInfos[0].RawActionName,
                actionInfos[1].RawActionName,
                actionInfos[2].RawActionName,
                actionInfos[3].RawActionName,
                deadzone);
        }

        private static string[] CollectBaseActionNames(ParsedActionInfo[] actionInfos)
        {
            var baseActions = new string[actionInfos.Length];
            for (int index = 0; index < actionInfos.Length; index++)
            {
                baseActions[index] = actionInfos[index].BaseActionName;
            }

            return baseActions;
        }
    }
}
