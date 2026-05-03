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
        [Export] public string DashAction { get; set; } = "player_dash";
        [Export] public string AttackAction { get; set; } = "player_attack";

        private ICharacterIntentAbility2D<MoveIntent2D> _move;
        private ICharacterIntentAbility2D<JumpIntent2D> _jump;
        private HfsmComponent2D _hfsm;
        private CharacterBodyMotorComponent2D _motor;

        public override void OnInit()
        {
            _move = Owner.GetComponent<CharacterMoveComponent2D>();
            _jump = Owner.GetComponent<CharacterJumpComponent2D>();
            _hfsm = Owner.GetComponent<HfsmComponent2D>();
            _motor = Owner.GetComponent<CharacterBodyMotorComponent2D>();
        }

        public override void OnPhysicsUpdate(double delta)
        {
            var input = ModuleSystem.GetModule<IInputModule>();
            if (input == null)
            {
                ClearIntents();
                return;
            }

            float left = input.GetActionStrength(MoveLeftAction);
            float right = input.GetActionStrength(MoveRightAction);
            float inputX = right - left;

            var moveIntent = new MoveIntent2D(inputX);
            var jumpIntent = new JumpIntent2D(
                startRequested: input.IsJustPressed(JumpAction),
                sustainRequested: input.IsPressed(JumpAction));
            bool dashStartRequested = input.IsJustPressed(DashAction);
            bool attackStartRequested = input.IsJustPressed(AttackAction);

            _move?.SetIntent(moveIntent);
            _jump?.SetIntent(jumpIntent);
            WriteHfsmInputs(moveIntent, jumpIntent, dashStartRequested, attackStartRequested);

            _move?.ApproveIntent(moveIntent);
            _jump?.ApproveIntent(jumpIntent);
        }

        private void ClearIntents()
        {
            _move?.SetIntent(MoveIntent2D.None);
            _jump?.SetIntent(JumpIntent2D.None);
            WriteHfsmInputs(MoveIntent2D.None, JumpIntent2D.None, false, false);

            _move?.ApproveIntent(MoveIntent2D.None);
            _jump?.ApproveIntent(JumpIntent2D.None);
        }

        private void WriteHfsmInputs(
            MoveIntent2D moveIntent,
            JumpIntent2D jumpIntent,
            bool dashStartRequested,
            bool attackStartRequested)
        {
            if (_hfsm == null)
                return;

            bool isOnFloor = _motor?.IsOnFloor == true;
            float velocityY = _motor?.Velocity.Y ?? 0f;

            SetHfsmValue(CharacterHfsmBlackboardKeys.IsOnFloor, isOnFloor);
            SetHfsmValue(CharacterHfsmBlackboardKeys.JumpStartRequested, jumpIntent.StartRequested);
            SetHfsmValue(CharacterHfsmBlackboardKeys.JumpSustainRequested, jumpIntent.SustainRequested);
            SetHfsmValue(CharacterHfsmBlackboardKeys.MoveAxisX, moveIntent.AxisX);
            SetHfsmValue(CharacterHfsmBlackboardKeys.VelocityY, velocityY);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.DashStartRequested, dashStartRequested);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.AttackStartRequested, attackStartRequested);
        }

        private void SetHfsmValue<T>(string key, T value)
        {
            _hfsm.SetValue(key, value);

            // Locomotion is a child HFSM graph. Write directly into the child too so
            // parent-local fallback entries cannot shadow child blackboard inputs.
            _hfsm.Runtime?.ChildRuntime?.SetValue(key, value);
        }
    }
}
