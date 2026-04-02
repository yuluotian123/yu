using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Framework;
using Godot.Collections;

namespace GameLogic
{
    /// <summary>
    /// 内部可以挂接多个component的游戏组件基类，支持在编辑器中配置组件（内联或外部资源），并在运行时自动实例化和管理组件生命周期。
    /// 支持运行时动态添加/移除组件，组件优先级控制更新顺序，集成对象池接口以支持复用和资源管理。
    /// </summary>
    public abstract partial class GameObjectBase : Node, IObjectPoolItem
    {
        /// <summary>
        /// 在编辑器中配置的组件（支持内联和外部资源）
        /// </summary>
        [Export] public Array<Component> Components { get; set; } = new();

        private System.Collections.Generic.Dictionary<Type, Component> _runtimeComponents = new();
        private List<Component> _sortedComponents = new();


        public override void _Ready()
        {
            // 从配置创建组件
            InitializeComponents();

            foreach (var component in _sortedComponents)
                component.OnInit();
        }

        private void InitializeComponents()
        {
            if (Components == null) 
            {
                Debugger.Warn("there is no component in this gameobject.");
                return;
            }

            Debugger.Info("GameObject" + Components.Count());

            foreach (var component in Components)
            {
                if (component == null)
                continue;

                Component instance = null;

                // 检查是否内联，如果内联则直接使用，不然则克隆组件（避免多个实例共享同一配置）
                if (component.ResourcePath.Contains("::"))
                    instance = component;
                else
                    instance = component.Clone();

                instance.Owner = this;

                _runtimeComponents[instance.GetType()] = instance;
            }

            _sortedComponents = _runtimeComponents.Values
                .OrderByDescending(c => c.Priority)
                .ToList();
        }

        public override void _Process(double delta)
        {
            foreach (var component in _sortedComponents)
                component.OnUpdate(delta);
        }

        public override void _PhysicsProcess(double delta)
        {
            foreach (var component in _sortedComponents)
                component.OnPhysicsUpdate(delta);
        }

        public override void _ExitTree()
        {
            Debugger.Info($"GameObject '{Name}' exiting tree, destroying components.");
            foreach (var component in _sortedComponents)
                component.OnDestroy();

            _runtimeComponents.Clear();
            _sortedComponents.Clear();
            Components.Clear();
        }

        //ObjectPoolItem接口实现
        public virtual void OnSpawn()
        {
            // 如果组件已存在，只需重新初始化
            if (_runtimeComponents.Count == 0)
            {
                InitializeComponents(); // 首次生成才初始化
            }
            else
            {
                // 复用现有组件，只重置状态
                foreach (var component in _sortedComponents)
                    component.OnInit(); // 或 component.Reset()
            }
        }

        public virtual void OnRecycle()
        {
            foreach (var component in _runtimeComponents.Values)
                component.OnDestroy();

            //_runtimeComponents.Clear();
            //_sortedComponents.Clear();
            //Components.Clear();
        }

        public T AddComponent<T>() where T : Component, new()
        {
            var component = new T { Owner = this };
            _runtimeComponents[typeof(T)] = component;

            _sortedComponents = _runtimeComponents.Values
                .OrderByDescending(c => c.Priority)
                .ToList();

            if (IsNodeReady()) component.OnInit();
            return component;
        }

        public T GetComponent<T>() where T : Component
        {
            return _runtimeComponents.TryGetValue(typeof(T), out var component) ? (T)component : null;
        }

        public bool HasComponent<T>() where T : Component
        {
            Debugger.Info(_runtimeComponents.Count().ToString());
            return _runtimeComponents.ContainsKey(typeof(T));
        }

        public void RemoveComponent<T>() where T : Component
        {
            if (_runtimeComponents.TryGetValue(typeof(T), out var component))
            {
                component.OnDestroy();
                _runtimeComponents.Remove(typeof(T));
                _sortedComponents.Remove(component);
            }
        }
    }
}