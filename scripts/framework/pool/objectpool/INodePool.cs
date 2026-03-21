using Godot;

namespace Framework
{
    /// <summary>
    /// Godot Node 对象池接口。
    /// <para>
    /// 专用于管理 <see cref="Node"/> 类型（及其子类）的对象池。
    /// Node 实例由 <see cref="PackedScene"/> 实例化，回收时设置 <c>Visible = false</c> 并暂留在父节点下，
    /// 取出时重新设为可见并调用可选的 <see cref="IObjectPoolItem.OnSpawn"/>。
    /// </para>
    /// <para>
    /// 若池中的 Node 实现了 <see cref="IObjectPoolItem"/>，则在 Spawn/Recycle 时自动调用对应方法。
    /// </para>
    /// <example>
    /// <code>
    /// // 创建子弹 Node 池，父节点为当前场景根节点
    /// var pool = poolModule.CreateNodePool("res://scenes/bullet.tscn", GetTree().Root);
    ///
    /// var bullet = pool.Spawn() as Bullet;
    /// // ...使用 bullet...
    /// pool.Recycle(bullet);
    /// </code>
    /// </example>
    /// </summary>
    public interface INodePool
    {
        /// <summary>
        /// 获取对象池名称。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 获取此池所使用的 PackedScene 资源路径。
        /// </summary>
        string ScenePath { get; }

        /// <summary>
        /// 获取当前池中闲置（隐藏）的 Node 数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 获取或设置对象池容量上限。
        /// <para>当回收的 Node 数量超过此上限时，多余的 Node 将被 <see cref="Node.QueueFree"/> 销毁（或在 <see cref="AllowOverflow"/> 为 true 时扩容）。</para>
        /// </summary>
        int Capacity { get; set; }

        /// <summary>
        /// 获取或设置是否允许超出容量时自动扩容（即不销毁多余回收的 Node）。
        /// <para>默认为 false，超容量时被回收的 Node 直接 <see cref="Node.QueueFree"/>。</para>
        /// </summary>
        bool AllowOverflow { get; set; }

        /// <summary>
        /// 获取或设置自动释放空闲 Node 的间隔（秒）。
        /// <para>小于等于 0 时禁用自动释放。</para>
        /// </summary>
        float AutoReleaseInterval { get; set; }

        /// <summary>
        /// 从对象池中取出一个 Node。
        /// <para>若池中有空闲 Node 则直接取出（设为可见），否则由 PackedScene 实例化新节点并加入父节点。</para>
        /// <para>若 Node 实现了 <see cref="IObjectPoolItem"/>，则自动调用 <see cref="IObjectPoolItem.OnSpawn"/>。</para>
        /// </summary>
        /// <returns>取出的 Node 实例。</returns>
        Node Spawn();

        /// <summary>
        /// 将 Node 回收到对象池。
        /// <para>若 Node 实现了 <see cref="IObjectPoolItem"/>，则先调用 <see cref="IObjectPoolItem.OnRecycle"/>，
        /// 再将节点设为不可见（<c>Visible = false</c>）。</para>
        /// <para>若池已满且 <see cref="AllowOverflow"/> 为 false，Node 将被 <see cref="Node.QueueFree"/> 销毁。</para>
        /// </summary>
        /// <param name="node">要回收的 Node。</param>
        void Recycle(Node node);

        /// <summary>
        /// 立即释放池中所有闲置（隐藏）的 Node（调用 <see cref="Node.QueueFree"/>）。
        /// </summary>
        void ReleaseAllUnused();
    }
}
