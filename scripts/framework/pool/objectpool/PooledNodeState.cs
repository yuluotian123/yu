using System.Collections.Generic;
using Godot;

namespace Framework
{
    internal sealed class PooledNodeState
    {
        private readonly List<NodeState> _nodes = new();

        public PooledNodeState(Node root) => Capture(root);

        private void Capture(Node node)
        {
            _nodes.Add(new NodeState(node));
            foreach (Node child in node.GetChildren(includeInternal: true))
                Capture(child);
        }

        public void Suspend()
        {
            foreach (var state in _nodes)
                state.Suspend();
        }

        public void Restore()
        {
            // Restore children before allowing the root to process again.
            for (int i = _nodes.Count - 1; i >= 0; i--)
                _nodes[i].Restore();
        }

        private sealed class NodeState
        {
            private readonly Node _node;
            private readonly Node.ProcessModeEnum _processMode;
            private readonly bool _visible;
            private readonly CollisionObject2D.DisableModeEnum _disable2D;
            private readonly CollisionObject3D.DisableModeEnum _disable3D;

            public NodeState(Node node)
            {
                _node = node;
                _processMode = node.ProcessMode;
                _visible = node is CanvasItem item ? item.Visible : node is Node3D spatial && spatial.Visible;
                if (node is CollisionObject2D collision2D)
                    _disable2D = collision2D.DisableMode;
                if (node is CollisionObject3D collision3D)
                    _disable3D = collision3D.DisableMode;
            }

            public void Suspend()
            {
                if (!GodotObject.IsInstanceValid(_node) || _node.IsQueuedForDeletion())
                    return;
                if (_node is CollisionObject2D collision2D)
                    collision2D.DisableMode = CollisionObject2D.DisableModeEnum.Remove;
                if (_node is CollisionObject3D collision3D)
                    collision3D.DisableMode = CollisionObject3D.DisableModeEnum.Remove;
                _node.ProcessMode = Node.ProcessModeEnum.Disabled;
                if (_node is CanvasItem item)
                    item.Visible = false;
                if (_node is Node3D spatial)
                    spatial.Visible = false;
            }

            public void Restore()
            {
                if (!GodotObject.IsInstanceValid(_node) || _node.IsQueuedForDeletion())
                    return;
                _node.ProcessMode = _processMode;
                if (_node is CollisionObject2D collision2D)
                    collision2D.DisableMode = _disable2D;
                if (_node is CollisionObject3D collision3D)
                    collision3D.DisableMode = _disable3D;
                if (_node is CanvasItem item)
                    item.Visible = _visible;
                if (_node is Node3D spatial)
                    spatial.Visible = _visible;
            }
        }
    }
}
