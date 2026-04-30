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
        private CharacterDashComponent2D _dash;
        private CharacterAttackComponent2D _attack;
        private HfsmComponent2D _hfsm;
        private CharacterBodyMotorComponent2D _motor;

        public override void OnInit()
        {
            _move = Owner.GetComponent<CharacterMoveComponent2D>();
            _jump = Owner.GetComponent<CharacterJumpComponent2D>();
            _dash = Owner.GetComponent<CharacterDashComponent2D>();
            _attack = Owner.GetComponent<CharacterAttackComponent2D>();
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
            var dashIntent = new DashIntent2D(
                startRequested: input.IsJustPressed(DashAction),
                directionX: inputX);
            var attackIntent = new AttackIntent2D(
                startRequested: input.IsJustPressed(AttackAction),
                sustainRequested: input.IsPressed(AttackAction));

            _move?.SetIntent(moveIntent);
            _jump?.SetIntent(jumpIntent);
            _dash?.SetIntent(dashIntent);
            _attack?.SetIntent(attackIntent);
            WriteHfsmInputs(moveIntent, jumpIntent, dashIntent, attackIntent);

            _move?.ApproveIntent(moveIntent);
            _jump?.ApproveIntent(jumpIntent);
            _dash?.ApproveIntent(DashIntent2D.None);
            _attack?.ApproveIntent(AttackIntent2D.None);
        }

        private void ClearIntents()
        {
            _move?.SetIntent(MoveIntent2D.None);
            _jump?.SetIntent(JumpIntent2D.None);
            _dash?.SetIntent(DashIntent2D.None);
            _attack?.SetIntent(AttackIntent2D.None);
            WriteHfsmInputs(MoveIntent2D.None, JumpIntent2D.None, DashIntent2D.None, AttackIntent2D.None);

            _move?.ApproveIntent(MoveIntent2D.None);
            _jump?.ApproveIntent(JumpIntent2D.None);
            _dash?.ApproveIntent(DashIntent2D.None);
            _attack?.ApproveIntent(AttackIntent2D.None);
        }

        private void WriteHfsmInputs(
            MoveIntent2D moveIntent,
            JumpIntent2D jumpIntent,
            DashIntent2D dashIntent,
            AttackIntent2D attackIntent)
        {
            if (_hfsm == null)
                return;

            _hfsm.SetValue(CharacterHfsmBlackboardKeys.IsOnFloor, _motor?.IsOnFloor == true);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.JumpStartRequested, jumpIntent.StartRequested);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.JumpSustainRequested, jumpIntent.SustainRequested);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.MoveAxisX, moveIntent.AxisX);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.VelocityY, _motor?.Velocity.Y ?? 0f);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.DashStartRequested, dashIntent.StartRequested && _dash?.CanStartDash == true);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.AttackStartRequested, attackIntent.StartRequested && _attack?.CanStartAttack == true);
        }
    }
}
