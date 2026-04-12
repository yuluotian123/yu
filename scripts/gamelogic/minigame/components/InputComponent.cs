using Framework;
using GameLogic;
using GameLogic.Input;
using Godot;

//处理camera输入
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
        if (_inputModule == null)
            return;

        OnCameraInputUpdate(delta);
    }

    public override void OnDestroy()
    {
        OnCameraInputDestroy();
    }
}
