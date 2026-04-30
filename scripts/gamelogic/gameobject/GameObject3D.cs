using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Framework;
using Godot;
using Godot.Collections;

namespace GameLogic
{
    public partial class GameObject3D : Node3D, IObjectPoolItem, IGameObject
    {
        [JsonIgnore]  public string PersistentId { get; set; } = string.Empty;

        [Export] public Array<Component3D> Components { get; set; } = new();

        private readonly System.Collections.Generic.Dictionary<Type, Component3D> _runtimeComponents = new();
        private List<Component3D> _sortedComponents = new();

        public Vector3 WorldPosition => GlobalPosition;

        public Vector3 WorldRotation => GlobalRotation;

        public Vector3 WorldPosition3D => WorldPosition;

        public Vector3 WorldRotation3D => WorldRotation;

        public void SetWorldPosition(Vector3 position)
        {
            GlobalPosition = position;
        }

        public void SetWorldRotation(Vector3 rotation)
        {
            GlobalRotation = rotation;
        }

        public void SetWorldPosition3D(Vector3 position) => SetWorldPosition(position);

        public void SetWorldRotation3D(Vector3 rotation) => SetWorldRotation(rotation);

        public T AddComponent<T>() where T : Component3D, new()
        {
            var component = new T { Owner = this };
            _runtimeComponents[typeof(T)] = component;
            SortComponents();

            if (IsNodeReady())
                component.OnInit();

            return component;
        }

        public T GetComponent<T>() where T : Component3D
        {
            Type componentType = typeof(T);
            if (_runtimeComponents.TryGetValue(componentType, out var component))
                return (T)component;

            for (int i = 0; i < _sortedComponents.Count; i++)
            {
                if (_sortedComponents[i] is T typedComponent)
                    return typedComponent;
            }

            return null;
        }

        public bool HasComponent<T>() where T : Component3D
        {
            return GetComponent<T>() != null;
        }

        public IReadOnlyList<Component3D> GetAllComponents() => _sortedComponents;

        IReadOnlyList<IComponent> IGameObject.GetAllComponents() => _sortedComponents;

        public bool HasComponent(Type componentType)
        {
            return GetComponent(componentType) != null;
        }

        public IComponent GetComponent(Type componentType)
        {
            if (componentType == null)
                return null;

            if (_runtimeComponents.TryGetValue(componentType, out var component))
                return component;

            for (int i = 0; i < _sortedComponents.Count; i++)
            {
                if (componentType.IsInstanceOfType(_sortedComponents[i]))
                    return _sortedComponents[i];
            }

            return null;
        }

        public void RemoveComponent<T>() where T : Component3D
        {
            if (!_runtimeComponents.TryGetValue(typeof(T), out var component))
                return;

            component.OnDestroy();
            _runtimeComponents.Remove(typeof(T));
            _sortedComponents.Remove(component);
        }

        public void RemoveComponent(Type componentType)
        {
            if (componentType == null || !_runtimeComponents.TryGetValue(componentType, out var component))
                return;

            component.OnDestroy();
            _runtimeComponents.Remove(componentType);
            _sortedComponents.Remove(component);
        }

        public bool TryGetTransformNode3D(out Node3D node)
        {
            node = this;
            return true;
        }

        public override void _Ready()
        {
            PersistentIdUtility.EnsurePersistentId(this);
            InitializeComponents();

            for (int i = 0; i < _sortedComponents.Count; i++)
                _sortedComponents[i].OnInit();
        }

        public override void _Process(double delta)
        {
            for (int i = 0; i < _sortedComponents.Count; i++)
            {
                if (!_sortedComponents[i].IsActive) continue;
                _sortedComponents[i].OnUpdate(delta);
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            for (int i = 0; i < _sortedComponents.Count; i++)
            {
                if (!_sortedComponents[i].IsActive) continue;
                _sortedComponents[i].OnPhysicsUpdate(delta);
            }
        }

        public override void _ExitTree()
        {
            Debugger.Info($"GameObject '{Name}' exiting tree, destroying components.");

            for (int i = 0; i < _sortedComponents.Count; i++)
                _sortedComponents[i].OnDestroy();

            _runtimeComponents.Clear();
            _sortedComponents.Clear();
            Components.Clear();
        }

        public virtual void OnSpawn()
        {
            PersistentIdUtility.EnsurePersistentId(this);

            if (_runtimeComponents.Count == 0)
            {
                InitializeComponents();
                return;
            }

            for (int i = 0; i < _sortedComponents.Count; i++)
                _sortedComponents[i].OnInit();
        }

        public virtual void OnRecycle()
        {
            foreach (var component in _runtimeComponents.Values)
                component.OnDestroy();
        }

        private void InitializeComponents()
        {
            if (Components == null)
            {
                Debugger.Warn("there is no component in this gameobject.");
                return;
            }

            if (_runtimeComponents.Count > 0)
                return;

            foreach (var component in Components)
            {
                if (component == null)
                    continue;

                Component3D instance;
                if (component.ResourcePath.Contains("::") && component.ResourceLocalToScene)
                    instance = component;
                else
                    instance = component.Clone();

                instance.Owner = this;
                _runtimeComponents[instance.GetType()] = instance;
            }

            SortComponents();
        }

        private void SortComponents()
        {
            _sortedComponents = _runtimeComponents.Values
                .OrderByDescending(c => c.Priority)
                .ToList();
        }
    }
}
