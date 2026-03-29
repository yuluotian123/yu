namespace GameLogic
{
    public static class ComponentPriority
    {
        public const int Input = 100;        // 输入处理，最高优先级
        public const int AI = 100;           // 与Input同级
        public const int Animation = 80;     // 动画状态机
        public const int Combat = 60;        // 战斗逻辑
        public const int Movement = 50;      // 移动逻辑
        public const int Health = 40;        // 生命值
        public const int Interaction = 30;   // 交互
        public const int VFX = 10;           // 特效
        public const int Default = 0;        // 默认
    }


/// <summary>
/// 组件接口 - 定义组件的基本行为和优先级
/// </summary>
    public interface IComponent
    {
        /// <summary>
        /// 组件优先级，数值越大越先执行
        /// </summary>
        int Priority { get; }

        void OnInit();
        void OnUpdate(double delta);
        void OnPhysicsUpdate(double delta);
        void OnDestroy();
    }
}