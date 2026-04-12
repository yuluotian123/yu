using Godot;

namespace GameLogic
{
    /// <summary>
    /// 组件基类 - 既是配置也是运行时实例,可能会面临一些gc问题
    /// </summary>
    [GlobalClass]
    public abstract partial class Component : Resource, IComponent
    {
        public GameObjectBase Owner { get; set; }
        public abstract int Priority { get; }

        public virtual void OnInit() { }
        public virtual void OnUpdate(double delta) { }
        public virtual void OnPhysicsUpdate(double delta) { }
        public virtual void OnDestroy() { }

        /// <summary>
        /// 创建组件的运行时副本（用于实例化）
        /// </summary>
        public virtual Component Clone()
        {
            return (Component)Duplicate();
        }
    }
}