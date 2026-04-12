

using GameLogic;

public partial class PlayerController : GameObjectBase
{
    private InputComponent _inputComponent;
    //储存玩家数据，在运行时会从玩家数据中读取并生成PlayerController中的内容
    private PlayerState _playerState;

    public override void _Ready()
    {
        base._Ready();
        _inputComponent = AddComponent<InputComponent>();   
        _playerState = RootModule.Instance.GameState._PlayerState;

        _playerState.Hp = 1;
    }


}