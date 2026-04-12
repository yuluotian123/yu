using GameLogic;

public partial class PlayerController : GameObjectBase
{
    private InputComponent _inputComponent;
    //private CameraComponent _cameraComponent;
    private PlayerState _playerState;

    public override void _Ready()
    {
        base._Ready();
        _inputComponent = AddComponent<InputComponent>();
        //_cameraComponent = AddComponent<CameraComponent>();
        _playerState = RootModule.Instance.GameState._PlayerState;
    }
}
