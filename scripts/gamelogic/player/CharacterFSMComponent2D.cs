using Framework;
using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class CharacterFSMComponent2D : Component2D
    {
        private enum CharacterState2D
        {
            Grounded,
            Airborne
        }

        public override int Priority => ComponentPriority.State;

        public string CurrentStateName => _currentState.ToString();

        private ICharacterIntentAbility2D<MoveIntent2D> _move;
        private ICharacterIntentAbility2D<JumpIntent2D> _jump;
        private CharacterBodyMotorComponent2D _motor;
        private CharacterState2D _currentState = CharacterState2D.Airborne;

        public override void OnInit()
        {
            _move = Owner.GetComponent<CharacterMoveComponent2D>();
            _jump = Owner.GetComponent<CharacterJumpComponent2D>();
            _motor = Owner.GetComponent<CharacterBodyMotorComponent2D>();

            if (_move == null)
                Debugger.Warn("[CharacterFSMComponent2D] Missing CharacterMoveComponent2D; move intent will not be approved.");

            if (_jump == null)
                Debugger.Warn("[CharacterFSMComponent2D] Missing CharacterJumpComponent2D; jump intent will not be approved.");

            if (_motor == null)
                Debugger.Warn("[CharacterFSMComponent2D] Missing CharacterBodyMotorComponent2D; state defaults to Airborne.");

            _currentState = _motor?.IsOnFloor == true
                ? CharacterState2D.Grounded
                : CharacterState2D.Airborne;
        }

        public override void OnPhysicsUpdate(double delta)
        {
            RefreshStateFromBody();
            ApproveMoveIntent();
            ApproveJumpIntent();
        }

        private void RefreshStateFromBody()
        {
            if (_motor == null)
            {
                _currentState = CharacterState2D.Airborne;
                return;
            }

            _currentState = _motor.IsOnFloor
                ? CharacterState2D.Grounded
                : CharacterState2D.Airborne;
        }

        private void ApproveMoveIntent()
        {
            if (_move == null)
                return;

            _move.ApproveIntent(_move.RawIntent);
        }

        private void ApproveJumpIntent()
        {
            if (_jump == null)
                return;

            JumpIntent2D rawIntent = _jump.RawIntent;
            _jump.ApproveIntent(rawIntent);

            if (_currentState == CharacterState2D.Grounded && rawIntent.StartRequested)
                _currentState = CharacterState2D.Airborne;
        }
    }
}
