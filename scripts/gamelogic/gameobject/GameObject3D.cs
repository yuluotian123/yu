using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Framework;
using Godot;
using Godot.Collections;

namespace GameLogic
{
    [Tool]
    public partial class GameObject3D : Node3D, IObjectPoolItem, IGameObject
    {
        [Export] [JsonIgnore] public string PersistentId { get; set; } = string.Empty;

        [Export] public Array<Component3D> Components { get; set; } = new();

        private readonly System.Collections.Generic.Dictionary<Type, Component3D> _runtimeComponents = new();
        private List<Component3D> _sortedComponents = new();
        private bool _componentsInitialized;
        public string InitializationError {get;private set;}=string.Empty;
        private double _editorElapsed;
        private bool _editorSaving;

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
            if (_runtimeComponents.ContainsKey(typeof(T)))
                throw new InvalidOperationException($"Component '{typeof(T).Name}' already exists.");
            var component = new T { Owner = this };
            _runtimeComponents[typeof(T)] = component;
            SortComponents();

            if (_componentsInitialized)
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

            if (_componentsInitialized)
                component.OnDestroy();
            _runtimeComponents.Remove(typeof(T));
            _sortedComponents.Remove(component);
        }

        public void RemoveComponent(Type componentType)
        {
            if (componentType == null || !_runtimeComponents.TryGetValue(componentType, out var component))
                return;

            if (_componentsInitialized)
                component.OnDestroy();
            _runtimeComponents.Remove(componentType);
            _sortedComponents.Remove(component);
        }

        public bool TryGetTransformNode3D(out Node3D node)
        {
            node = this;
            return true;
        }

        public override void _EnterTree()
        {
            if (!Engine.IsEditorHint() && Components.OfType<WorldLevelContextComponent3D>().Any())
                WorldIdentity.Bind(this);
        }

        public override void _Ready()
        {
            if (Engine.IsEditorHint()) return;
            PersistentIdUtility.EnsurePersistentId(this);
            InitializeComponents();
            ActivateComponents();
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint())
            {
                if (_editorSaving) return;
                // Components 持有这个原生数组，转换后的视图不拥有它，不能 Dispose。
                var definitions = (Godot.Collections.Array)Components;
                foreach (Variant item in definitions)
                    if (item.AsGodotObject() is Component3D component) component.OnEditorFrameUpdate(this, delta);
                _editorElapsed += delta;
                if (_editorElapsed < .1) return;
                _editorElapsed = 0;
                foreach (Variant item in definitions)
                    if (item.AsGodotObject() is Component3D component) component.OnEditorUpdate(this);
                return;
            }
            if (!_componentsInitialized)
                return;
            for (int i = 0; i < _sortedComponents.Count; i++)
            {
                if (!_sortedComponents[i].IsActive) continue;
                _sortedComponents[i].OnUpdate(delta);
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            if (Engine.IsEditorHint()) return;
            if (!_componentsInitialized)
                return;
            for (int i = 0; i < _sortedComponents.Count; i++)
            {
                if (!_sortedComponents[i].IsActive) continue;
                _sortedComponents[i].OnPhysicsUpdate(delta);
            }
        }

        public override void _Notification(int what)
        {
            // 临时渲染资源必须在 PackedScene 序列化前解绑，下一帧自动恢复预览。
            if (!Engine.IsEditorHint()) return;
            if (what == NotificationEditorPostSave) { _editorSaving = false; return; }
            if (what != NotificationEditorPreSave) return;
#if TOOLS
            if (EditorInterface.Singleton.GetEditedSceneRoot() == this) WorldAuthoringCompiler.Prepare(this);
#endif
            _editorSaving = true;
            var definitions = (Godot.Collections.Array)Components;
            foreach (Variant item in definitions)
                if (item.AsGodotObject() is Component3D component) component.OnEditorPreSave(this);
        }

        public override void _ExitTree()
        {
            if (Engine.IsEditorHint())
            {
                foreach (Variant item in (Godot.Collections.Array)Components)
                    if (item.AsGodotObject() is Component3D component) component.OnEditorExit(this);
                return;
            }
            Debugger.Info($"GameObject '{Name}' exiting tree, destroying components.");

            DeactivateComponents();

            _runtimeComponents.Clear();
            _sortedComponents.Clear();
        }

        public virtual void OnSpawn()
        {
            PersistentIdUtility.EnsurePersistentId(this);

            InitializeComponents();
            if (IsNodeReady())
                ActivateComponents();
        }

        public virtual void OnRecycle()
        {
            DeactivateComponents();
        }

        private void ActivateComponents()
        {
            if (_componentsInitialized)
                return;
            _componentsInitialized = true;
            InitializationError=string.Empty;
            int initialized=0;
            try
            {
                for (; initialized < _sortedComponents.Count; initialized++)
                    _sortedComponents[initialized].OnInit();
            }
            catch(Exception error)
            {
                InitializationError=error.Message;_componentsInitialized=false;
                for(int i=initialized;i>=0;i--)
                    try{_sortedComponents[i].OnDestroy();}catch(Exception cleanup){Debugger.Warn(cleanup.Message);}
                throw;
            }
        }

        private void DeactivateComponents()
        {
            if (!_componentsInitialized)
                return;
            _componentsInitialized = false;
            for (int i = 0; i < _sortedComponents.Count; i++)
                _sortedComponents[i].OnDestroy();
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
