using GameLogic;

/// <summary>
/// 玩家控制器，负责承载输入组件和玩家军队组件。
/// </summary>
public partial class PlayerController : GameObjectBase
{
    private InputComponent _inputComponent;
    //private PlayerArmyComponent _playerArmyComponent;

    /// <summary>
    /// 在节点就绪后创建并初始化玩家控制器所需组件。
    /// </summary>
    public override void _Ready()
    {
        _inputComponent = AddComponent<InputComponent>();
        base._Ready();
        //_playerArmyComponent = AddComponent<PlayerArmyComponent>();
    }
}
