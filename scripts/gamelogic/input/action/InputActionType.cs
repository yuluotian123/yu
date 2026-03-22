namespace GameLogic.Input
{
    /// <summary>
    /// 输入动作类型枚举。
    /// 对应 Godot InputMap 中定义的 Action 名称。
    /// </summary>
    public enum InputActionType
    {
        // 移动相关
        Move,           // 移动（Vector2）
        Look,           // 视角（Vector2）
        
        // 基础动作
        Jump,           // 跳跃
        Crouch,         // 蹲下
        Sprint,         // 冲刺
        
        // 战斗动作
        Attack,         // 普通攻击
        HeavyAttack,    // 重攻击
        Dodge,          // 闪避
        Block,          // 格挡
        
        // 技能
        Skill1,
        Skill2,
        Skill3,
        Skill4,
        
        // 交互
        Interact,       // 交互
        Pickup,         // 拾取
        
        // UI
        Confirm,        // 确认
        Cancel,         // 取消
        Pause,          // 暂停
    }
}
