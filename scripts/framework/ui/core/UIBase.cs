using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace Framework.UI
{
    /// <summary>
    /// 所有 UI 的纯 C# 基类。
    /// <para>
    /// 提供以下核心能力：
    /// <list type="bullet">
    ///   <item><see cref="UIBindAttribute"/> 反射自动绑定 Godot 场景节点。</item>
    ///   <item>封装 <see cref="IEventModule"/> 事件订阅，销毁时自动清理。</item>
    ///   <item>子 <see cref="UIWidget"/> 的创建与销毁管理。</item>
    ///   <item>便捷的节点查找方法。</item>
    /// </list>
    /// </para>
    /// </summary>
    public abstract class UIBase
    {
        // ──────────────────────────────────────────────
        //  框架内部属性（仅 UIModule / UIWindow / UIWidget 读写）
        // ──────────────────────────────────────────────

        /// <summary>对应的 Godot Control 节点（由框架赋值，不要在业务层直接修改）。</summary>
        public Control Owner { get; internal set; }

        /// <summary>父级 UIBase（UIWidget 的 Owner Window，UIWindow 为 null）。</summary>
        public UIBase Parent { get; internal set; }

        /// <summary>ShowUI 时传入的用户数据（可为 null）。</summary>
        public object[] UserDatas { get; internal set; }

        /// <summary>是否已完成初始化（InternalCreate 之后置为 true）。</summary>
        public bool IsPrepare { get; internal set; }

        // ──────────────────────────────────────────────
        //  私有字段
        // ──────────────────────────────────────────────

        private readonly List<UIWidget> _widgets = new();
        // 每条记录是一个"取消订阅"的动作，由 AddUIEvent 在注册时捕获
        private readonly List<Action> _unsubscribeActions = new();
        private IEventModule _eventModule;

        // ──────────────────────────────────────────────
        //  节点查找（对标 TEngine FindChild / FindChildComponent）
        // ──────────────────────────────────────────────

        /// <summary>在 Owner 下按路径查找节点，找不到返回 null。</summary>
        protected Node FindChild(string path)
            => Owner?.GetNodeOrNull(path);

        /// <summary>在 Owner 下按路径查找组件，找不到返回 default。</summary>
        protected T FindChildComponent<T>(string path) where T : class
            => Owner?.GetNodeOrNull<T>(path);

        // ──────────────────────────────────────────────
        //  事件系统（自动绑定 UI 生命周期）
        // ──────────────────────────────────────────────

        private IEventModule EventModule
            => _eventModule ??= ModuleSystem.GetModule<IEventModule>();

        /// <summary>
        /// 订阅无参数事件。销毁时自动取消订阅。
        /// </summary>
        public void AddUIEvent(int eventId, Action handler)
        {
            EventModule.Subscribe(eventId, handler);
            _unsubscribeActions.Add(() => EventModule.Unsubscribe(eventId, handler));
        }

        /// <summary>
        /// 订阅 1 参数事件。销毁时自动取消订阅。
        /// </summary>
        public void AddUIEvent<T1>(int eventId, Action<T1> handler)
        {
            EventModule.Subscribe(eventId, handler);
            _unsubscribeActions.Add(() => EventModule.Unsubscribe(eventId, handler));
        }

        /// <summary>
        /// 订阅 2 参数事件。销毁时自动取消订阅。
        /// </summary>
        public void AddUIEvent<T1, T2>(int eventId, Action<T1, T2> handler)
        {
            EventModule.Subscribe(eventId, handler);
            _unsubscribeActions.Add(() => EventModule.Unsubscribe(eventId, handler));
        }

        /// <summary>
        /// 订阅 3 参数事件。销毁时自动取消订阅。
        /// </summary>
        public void AddUIEvent<T1, T2, T3>(int eventId, Action<T1, T2, T3> handler)
        {
            EventModule.Subscribe(eventId, handler);
            _unsubscribeActions.Add(() => EventModule.Unsubscribe(eventId, handler));
        }

        /// <summary>
        /// 订阅 4 参数事件。销毁时自动取消订阅。
        /// </summary>
        public void AddUIEvent<T1, T2, T3, T4>(int eventId, Action<T1, T2, T3, T4> handler)
        {
            EventModule.Subscribe(eventId, handler);
            _unsubscribeActions.Add(() => EventModule.Unsubscribe(eventId, handler));
        }

        /// <summary>移除所有已注册的 UI 事件（由框架在销毁时自动调用）。</summary>
        internal void RemoveAllUIEvents()
        {
            foreach (var unsubscribe in _unsubscribeActions)
                unsubscribe();
            _unsubscribeActions.Clear();
        }

        // ──────────────────────────────────────────────
        //  子 Widget 管理
        // ──────────────────────────────────────────────

        /// <summary>
        /// 通过节点路径在 Owner 下查找 Control，并创建绑定的 UIWidget。
        /// </summary>
        protected T CreateWidget<T>(string nodePath) where T : UIWidget, new()
        {
            var node = Owner?.GetNodeOrNull<Control>(nodePath);
            if (node == null)
            {
                Debugger.Error($"[UIBase] CreateWidget 失败：找不到路径 '{nodePath}'（Owner={Owner?.Name}）");
                return default;
            }
            return CreateWidgetInternal<T>(node);
        }

        /// <summary>
        /// 直接绑定已有的 Control 节点创建 UIWidget。
        /// </summary>
        protected T CreateWidget<T>(Control bindNode) where T : UIWidget, new()
        {
            if (bindNode == null)
            {
                Debugger.Error($"[UIBase] CreateWidget 失败：bindNode 为 null");
                return default;
            }
            return CreateWidgetInternal<T>(bindNode);
        }

        private T CreateWidgetInternal<T>(Control node) where T : UIWidget, new()
        {
            var widget = new T();
            widget.Owner = node;
            widget.Parent = this;
            _widgets.Add(widget);

            // 获取所属 UIWindow（向上查找父级直到 UIWindow）
            UIWindow ownerWindow = this as UIWindow ?? (Parent as UIWindow);
            widget.SetOwnerWindow(ownerWindow);

            widget.InternalCreate();
            return widget;
        }

        /// <summary>销毁并移除一个子 Widget。</summary>
        protected void DestroyWidget(UIWidget widget)
        {
            if (widget == null) return;
            widget.InternalDestroy();
            _widgets.Remove(widget);
        }

        // ──────────────────────────────────────────────
        //  [UIBind] 反射自动绑定
        // ──────────────────────────────────────────────

        internal void AutoBind()
        {
            if (Owner == null) return;

            var type = GetType();
            // 遍历含继承层次的所有私有/公有实例字段
            while (type != null && type != typeof(UIBase))
            {
                var fields = type.GetFields(
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);

                foreach (var field in fields)
                {
                    var attr = field.GetCustomAttribute<UIBindAttribute>();
                    if (attr == null) continue;

                    Node node = ResolveNode(field, attr);
                    if (node == null)
                    {
                        Debugger.Warn($"[UIBase] AutoBind 失败：字段 '{field.Name}'，路径无法解析（Owner={Owner.Name}）");
                        continue;
                    }

                    // 类型兼容性检查
                    if (field.FieldType.IsAssignableFrom(node.GetType()))
                        field.SetValue(this, node);
                    else
                        Debugger.Warn($"[UIBase] AutoBind 类型不匹配：字段 '{field.Name}' 期望 {field.FieldType.Name}，实际 {node.GetType().Name}");
                }
                type = type.BaseType;
            }
        }

        private Node ResolveNode(FieldInfo field, UIBindAttribute attr)
        {
            if (attr.Path == "%")
            {
                // UniqueNode：字段名推断节点名，加 % 前缀
                string name = FieldNameToNodeName(field.Name);
                return Owner.GetNodeOrNull("%" + name);
            }
            else if (string.IsNullOrEmpty(attr.Path))
            {
                // 名称推断
                string name = FieldNameToNodeName(field.Name);
                return Owner.GetNodeOrNull(name);
            }
            else
            {
                // 指定精确路径
                return Owner.GetNodeOrNull(attr.Path);
            }
        }

        /// <summary>字段名 → 节点名转换规则：去掉 _ / m_ 前缀，首字母大写。</summary>
        private static string FieldNameToNodeName(string fieldName)
        {
            string name = fieldName;
            if (name.StartsWith("m_")) name = name[2..];
            name = name.TrimStart('_');
            if (name.Length == 0) return name;
            return char.ToUpper(name[0]) + name[1..];
        }

        // ──────────────────────────────────────────────
        //  生命周期（子类 override）
        // ──────────────────────────────────────────────

        /// <summary>绑定成员属性、创建子 Widget（首次创建时调用一次）。</summary>
        public virtual void BindMemberProperty() { }

        /// <summary>注册事件监听（首次创建时调用一次，销毁时自动清理）。</summary>
        public virtual void RegisterEvent() { }

        /// <summary>首次创建完成（节点就绪、AutoBind 完成后调用一次）。</summary>
        protected virtual void OnCreate() { }

        /// <summary>每次 ShowUI / Refresh 时调用，用于刷新显示数据。</summary>
        protected virtual void OnRefresh() { }

        /// <summary>
        /// 每帧驱动（仅在子类 override 且 IsPrepare 时才被 UIModule 调用）。
        /// </summary>
        protected virtual void OnUpdate(double delta) { }

        /// <summary>UI 被销毁前调用（对标 TEngine OnDestroy）。</summary>
        protected virtual void OnDestroy() { }

        /// <summary>UI 显隐状态变更时调用。</summary>
        protected virtual void OnSetVisible(bool visible) { }

        // ──────────────────────────────────────────────
        //  内部框架调用（UIWindow / UIWidget 实现具体逻辑）
        // ──────────────────────────────────────────────

        internal virtual void InternalCreate()
        {
            AutoBind();
            BindMemberProperty();
            RegisterEvent();
            OnCreate();
            IsPrepare = true;
        }

        internal virtual void InternalRefresh()
        {
            OnRefresh();
            foreach (var w in _widgets) w.InternalRefresh();
        }

        internal virtual void InternalUpdate(double delta)
        {
            OnUpdate(delta);
            foreach (var w in _widgets) w.InternalUpdate(delta);
        }

        internal virtual void InternalDestroy()
        {
            // 先销毁子 Widget
            foreach (var w in _widgets) w.InternalDestroy();
            _widgets.Clear();

            RemoveAllUIEvents();
            OnDestroy();
            IsPrepare = false;
            Owner = null;
            Parent = null;
        }

        internal virtual void InternalSetVisible(bool visible)
        {
            if (Owner != null) Owner.Visible = visible;
            OnSetVisible(visible);
            foreach (var w in _widgets) w.InternalSetVisible(visible);
        }
    }
}
