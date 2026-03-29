using Framework;
using GameLogic;
using GameLogic.Input;
using Godot;

public partial class InputComponent : Component
{
    private IInputModule _inputModule;
    private MovementComponent _movement;

    public override int Priority => ComponentPriority.Input;

    public override void OnInit()
    {
        _inputModule = ModuleSystem.GetModule<IInputModule>();
        _movement = Owner.GetComponent<MovementComponent>();
    }

    public override void OnUpdate(double delta)
    {
        HandleMovementInput();
        HandleCombatInput();
    }
    
    
    private void HandleMovementInput()
    {
        if(_movement == null)
        {
            Debugger.Warn("Player is missing MovementComponent,将会自动添加一个新的MovementComponent");
            _movement = Owner.AddComponent<MovementComponent>();
        }

        // 轮询式输入（持续输入）
        Vector2 v2 = _inputModule.GetVector("combat_left", "combat_right", "combat_up", "combat_down");
        bool isSprint = _inputModule.IsPressed("combat_sprint");
        _movement.SetMovement(v2,isSprint);
    }
    
    private void HandleCombatInput()
    {
        if (_inputModule.IsJustReleased("combat_attack"))
        {
            if(_inputModule.GetHoldTime("combat_attack") > 0.5f)
            {
                GD.Print("Long Attack");
            }
            else
            {
                GD.Print("Short Attack");
            }
        }
    }
}