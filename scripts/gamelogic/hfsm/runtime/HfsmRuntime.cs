using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace GameLogic
{
    public sealed class HfsmRuntime : StateGraphRuntime
    {
        private static readonly Dictionary<string, Type> ComponentTypeCache = new(StringComparer.Ordinal);

        public HfsmRuntime(HfsmGraphAsset graph, GraphBlackboardNode globalBlackboard = null)
            : this(graph, null, new GraphExecutionContext(graph, new GraphBlackboardRuntime(globalBlackboard)))
        {
        }

        public HfsmRuntime(HfsmGraphAsset graph, HfsmComponent2D owner, GraphBlackboardNode globalBlackboard = null)
            : this(graph, owner, new GraphExecutionContext(graph, new GraphBlackboardRuntime(globalBlackboard)))
        {
        }

        public HfsmRuntime(HfsmGraphAsset graph, GraphBlackboardRuntime parentBlackboard)
            : this(graph, null, CreateContext(graph, parentBlackboard, null))
        {
        }

        public HfsmRuntime(HfsmGraphAsset graph, HfsmComponent2D owner, GraphBlackboardRuntime parentBlackboard)
            : this(graph, owner, CreateContext(graph, parentBlackboard, null))
        {
        }

        private HfsmRuntime(
            HfsmGraphAsset graph,
            HfsmComponent2D owner,
            GraphBlackboardRuntime parentBlackboard,
            GraphExecutionContext parentContext)
            : this(graph, owner, CreateContext(graph, parentBlackboard, parentContext))
        {
        }

        private HfsmRuntime(HfsmGraphAsset graph, HfsmComponent2D owner, GraphExecutionContext context)
            : base(graph, context)
        {
            Owner = owner;
            AddUserDataFirst(this);
            AddUserData(owner);
            AddUserData(owner?.Owner);

            base.StateChanged += OnStateGraphStateChanged;
            base.StateEntered += OnStateGraphStateEntered;
            base.StateUpdated += OnStateGraphStateUpdated;
            base.StateExited += OnStateGraphStateExited;
        }

        public HfsmComponent2D Owner { get; }
        public GameObject2D GameObject => Owner?.Owner;
        public new HfsmGraphAsset Graph => base.Graph as HfsmGraphAsset;
        public new IHfsmStateNodeData CurrentState => base.CurrentState as IHfsmStateNodeData;
        public new HfsmRuntime ChildRuntime => base.ChildRuntime as HfsmRuntime;

        public new event Action<HfsmRuntime, IHfsmStateNodeData, IHfsmStateNodeData, HfsmTransitionConnection> StateChanged;
        public new event Action<HfsmRuntime, IHfsmStateNodeData> StateEntered;
        public new event Action<HfsmRuntime, IHfsmStateNodeData> StateUpdated;
        public new event Action<HfsmRuntime, IHfsmStateNodeData> StateExited;

        public T GetComponent<T>() where T : Component2D
        {
            return GameObject?.GetComponent<T>();
        }

        public Component2D GetComponent(Type componentType)
        {
            return GameObject?.GetComponent(componentType) as Component2D;
        }

        public Component2D GetComponent(string componentTypeName)
        {
            Type componentType = ResolveComponentType(componentTypeName);
            return componentType == null ? null : GetComponent(componentType);
        }

        protected override StateGraphRuntime CreateChildRuntime(StateGraphAsset subGraph)
        {
            return subGraph is HfsmGraphAsset hfsmSubGraph
                ? new HfsmRuntime(hfsmSubGraph, Owner, Blackboard, Context)
                : base.CreateChildRuntime(subGraph);
        }

        private void OnStateGraphStateChanged(
            StateGraphRuntime runtime,
            IStateNodeData previousState,
            IStateNodeData nextState,
            StateTransitionConnection transition)
        {
            StateChanged?.Invoke(
                runtime as HfsmRuntime ?? this,
                previousState as IHfsmStateNodeData,
                nextState as IHfsmStateNodeData,
                transition as HfsmTransitionConnection);
        }

        private void OnStateGraphStateEntered(StateGraphRuntime runtime, IStateNodeData state)
        {
            StateEntered?.Invoke(runtime as HfsmRuntime ?? this, state as IHfsmStateNodeData);
        }

        private void OnStateGraphStateUpdated(StateGraphRuntime runtime, IStateNodeData state)
        {
            StateUpdated?.Invoke(runtime as HfsmRuntime ?? this, state as IHfsmStateNodeData);
        }

        private void OnStateGraphStateExited(StateGraphRuntime runtime, IStateNodeData state)
        {
            StateExited?.Invoke(runtime as HfsmRuntime ?? this, state as IHfsmStateNodeData);
        }

        private void AddUserDataFirst(object value)
        {
            if (value == null)
                return;

            Context.UserData.Remove(value);
            Context.UserData.Insert(0, value);
        }

        private void AddUserData(object value)
        {
            if (value != null && !Context.UserData.Contains(value))
                Context.UserData.Add(value);
        }

        private static GraphExecutionContext CreateContext(
            HfsmGraphAsset graph,
            GraphBlackboardRuntime parentBlackboard,
            GraphExecutionContext parentContext)
        {
            var context = new GraphExecutionContext(
                graph,
                parentBlackboard?.ForkSharedLocals() ?? new GraphBlackboardRuntime());

            if (parentContext != null)
            {
                for (int i = 0; i < parentContext.UserData.Count; i++)
                    context.UserData.Add(parentContext.UserData[i]);
            }

            return context;
        }

        private static Type ResolveComponentType(string componentTypeName)
        {
            if (string.IsNullOrWhiteSpace(componentTypeName))
                return null;

            string normalizedName = componentTypeName.Trim();
            if (ComponentTypeCache.TryGetValue(normalizedName, out Type cachedType))
                return cachedType;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(type => type != null).ToArray();
                }

                foreach (Type type in types)
                {
                    if (type == null ||
                        type.IsAbstract ||
                        !typeof(Component2D).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    if (!string.Equals(type.Name, normalizedName, StringComparison.Ordinal) &&
                        !string.Equals(type.FullName, normalizedName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ComponentTypeCache[normalizedName] = type;
                    return type;
                }
            }

            return null;
        }
    }
}
