using Framework;
using Godot;

namespace GameLogic
{
    /// <summary>
    /// Reads player input and writes character ability intents.
    /// </summary>
    [GlobalClass]
    public partial class PlayerCharacterControllerComponent2D : Component2D
    {
        public override int Priority => ComponentPriority.Input;

        [ExportGroup("Input Actions")]
        [Export] public string MoveLeftAction { get; set; } = "player_move_left";
        [Export] public string MoveRightAction { get; set; } = "player_move_right";
        [Export] public string JumpAction { get; set; } = "player_jump";

        private CharacterCommandBufferComponent2D _commands;

        public override void OnInit()
        {
            _commands = Owner.GetComponent<CharacterCommandBufferComponent2D>();
        }

        public override void OnPhysicsUpdate(double delta)
        {
            var input = ModuleSystem.GetModule<IInputModule>();
            if (input == null)
            {
                _commands?.Submit(CharacterCommand2D.None, ComponentPriority.Input);
                return;
            }

            float left = input.GetActionStrength(MoveLeftAction);
            float right = input.GetActionStrength(MoveRightAction);
            float inputX = right - left;

            bool jumpPressed = input.IsJustPressed(JumpAction);
            _commands?.Submit(new CharacterCommand2D(
                inputX,
                jumpPressed,
                input.IsPressed(JumpAction)), ComponentPriority.Input);
        }
    }
}
