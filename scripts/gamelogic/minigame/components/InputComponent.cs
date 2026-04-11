using Framework;
using GameLogic;
using GameLogic.Input;
using Godot;

public partial class InputComponent : Component
{
    private IInputModule _inputModule;

    public override int Priority => ComponentPriority.Input;

    public override void OnInit()
    {
        _inputModule = ModuleSystem.GetModule<IInputModule>();
    }

    public override void OnUpdate(double delta)
    {

    }
}