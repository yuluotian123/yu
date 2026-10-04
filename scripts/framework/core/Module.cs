namespace Framework
{
    public abstract class Module
    {
        /// <summary>
        /// 获取游戏框架模块优先级。
        /// </summary>
        /// <remarks>Higher priorities update first. Shutdown follows reverse initialization order.</remarks>
        public virtual int Priority => 0;

        /// <summary>
        /// 初始化游戏框架接口。
        /// </summary>
        public abstract void OnInit();

        /// <summary>
        /// 关闭并清理游戏框架模块。
        /// </summary>
        public abstract void Shutdown();
    }

    public interface IProcessModule
    {
        /// <summary>
        /// 游戏框架模块轮询。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
        /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>

        void Process(double elapseSeconds, double realElapseSeconds);
    }

}
