
public partial class Player : Character
{

    private InputComponent _inputComponent;


    public override void _Ready()
    {
        base._Ready();      

        _inputComponent = AddComponent<InputComponent>();
    }


}