namespace Framework
{
    /// <summary>
    /// 对象池化接口。
    /// <para>纯 C# 对象若想被 <see cref="IObjectPool{T}"/> 管理，必须实现此接口。</para>
    /// <para>Node 对象池（<see cref="INodePool"/>）中的 Node 可选实现此接口；
    /// 若实现则在取出/回收时自动调用对应方法。</para>
    /// </summary>
    public interface IObjectPoolItem
    {
        /// <summary>
        /// 从对象池中取出时调用，用于重置/初始化对象状态。
        /// </summary>
        void OnSpawn();

        /// <summary>
        /// 回收到对象池时调用，用于清理对象状态、取消事件订阅等。
        /// </summary>
        void OnRecycle();
    }
}
