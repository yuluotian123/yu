using Framework.Pool;
using Godot;
namespace GTFramework.Pool
{
    public partial class PoolableNode : Node, IPoolable
    {
        public virtual void OnSpawned() { }
        public virtual void OnDespawned() { }
    }
}