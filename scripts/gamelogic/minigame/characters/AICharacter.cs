
using Framework;
using GameLogic;

public partial class AICharacter : Character
{
    private AIComponent _aiComponent;

    public override void _Ready()
    {
        base._Ready();
        _aiComponent = AddComponent<AIComponent>();
        Debugger.Info("Fuck");
    }


}