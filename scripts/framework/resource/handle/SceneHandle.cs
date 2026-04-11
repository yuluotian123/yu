using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace Framework
{
    /// <summary>
    /// Scene-specific handle that wraps a PackedScene resource handle and owns scene instantiation helpers.
    /// </summary>
    public sealed class SceneHandle : IDisposable
    {
        private sealed class BoundNode
        {
            public Node Node { get; }
            public Callable Callback { get; }

            public BoundNode(Node node, Callable callback)
            {
                Node = node;
                Callback = callback;
            }
        }

        private readonly ResourceHandle<PackedScene> _resourceHandle;
        private readonly Dictionary<ulong, BoundNode> _boundNodes = new();
        private Task<SceneHandle> _task;

        internal SceneHandle(ResourceHandle<PackedScene> resourceHandle)
        {
            _resourceHandle = resourceHandle;
        }

        public string Path => _resourceHandle.Path;
        public ResourceHandleStatus Status => _resourceHandle.Status;
        public bool IsDone => _resourceHandle.IsDone;
        public bool IsValid => _resourceHandle.IsValid;
        public float Progress => _resourceHandle.Progress;
        public string Error => _resourceHandle.Error;
        public PackedScene Scene => _resourceHandle.Asset;

        public Task<SceneHandle> Task => _task ??= WaitForCompletionAsync();

        public SceneHandle OnCompleted(Action<SceneHandle> callback)
        {
            _resourceHandle.OnCompleted(_ => callback?.Invoke(this));
            return this;
        }

        public SceneHandle WithCancellation(CancellationToken ct)
        {
            _resourceHandle.WithCancellation(ct);
            return this;
        }

        public Node Instantiate()
        {
            return !IsValid || Scene == null ? null : Scene.Instantiate();
        }

        public TNode Instantiate<TNode>() where TNode : Node
        {
            return !IsValid || Scene == null ? null : Scene.Instantiate<TNode>();
        }

        /// <summary>
        /// Instantiates the scene, attaches it to a parent node, and binds the scene handle
        /// to the instantiated node so it is released automatically on TreeExiting.
        /// </summary>
        public TNode InstantiateAndBind<TNode>(Node parent) where TNode : Node
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            return InstantiateAndBind<TNode>(instance => parent.AddChild(instance));
        }

        /// <summary>
        /// Instantiates the scene, lets the caller attach the node, and then binds
        /// the handle to the instantiated node's lifetime.
        /// </summary>
        public TNode InstantiateAndBind<TNode>(Action<TNode> attachInstance) where TNode : Node
        {
            var instance = Instantiate<TNode>();
            if (instance == null)
                return null;

            try
            {
                attachInstance?.Invoke(instance);
                BindTo(instance);
                return instance;
            }
            catch
            {
                if (GodotObject.IsInstanceValid(instance))
                    instance.QueueFree();
                throw;
            }
        }

        public SceneHandle BindTo(Node node)
        {
            if (node == null
                || !GodotObject.IsInstanceValid(node)
                || Status == ResourceHandleStatus.Released
                || Status == ResourceHandleStatus.Failed
                || Status == ResourceHandleStatus.Cancelled)
                return this;

            var id = node.GetInstanceId();
            if (_boundNodes.ContainsKey(id))
                return this;

            var callback = Callable.From(Release);
            var err = node.Connect(Node.SignalName.TreeExiting, callback, (uint)GodotObject.ConnectFlags.OneShot);
            if (err == Godot.Error.Ok)
                _boundNodes[id] = new BoundNode(node, callback);

            return this;
        }

        public void Release()
        {
            ClearBindings();
            _resourceHandle.Release();
        }

        public void Dispose()
        {
            Release();
        }

        private async Task<SceneHandle> WaitForCompletionAsync()
        {
            await _resourceHandle.Task;
            return this;
        }

        private void ClearBindings()
        {
            foreach (var bound in _boundNodes.Values)
            {
                if (!GodotObject.IsInstanceValid(bound.Node))
                    continue;

                if (bound.Node.IsConnected(Node.SignalName.TreeExiting, bound.Callback))
                    bound.Node.Disconnect(Node.SignalName.TreeExiting, bound.Callback);
            }

            _boundNodes.Clear();
        }
    }
}
