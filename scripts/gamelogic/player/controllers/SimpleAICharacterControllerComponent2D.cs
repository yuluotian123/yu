using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class SimpleAICharacterControllerComponent2D : Component2D
    {
        public override int Priority => ComponentPriority.Input;

        [Export] public float PatrolDistance { get; set; } = 120f;
        [Export] public int StartDirection { get; set; } = 1;
        [Export] public bool ReverseAtEdges { get; set; } = true;
        [Export] public float EdgeLookAhead { get; set; } = 18f;
        [Export] public float TurnPauseDuration { get; set; } = 0.12f;
        [Export] public float JumpInterval { get; set; } = 1.8f;
        [Export] public float JumpSustainDuration { get; set; } = 0.12f;

        private ICharacterIntentAbility2D<MoveIntent2D> _move;
        private ICharacterIntentAbility2D<JumpIntent2D> _jump;
        private ICharacterIntentAbility2D<DashIntent2D> _dash;
        private ICharacterIntentAbility2D<AttackIntent2D> _attack;
        private HfsmComponent2D _hfsm;
        private CharacterBodyMotorComponent2D _motor;
        private Vector2 _spawnPosition;
        private int _direction;
        private float _turnPauseTimer;
        private float _jumpCooldownTimer;
        private float _jumpSustainTimer;

        public override void OnInit()
        {
            _move = Owner.GetComponent<CharacterMoveComponent2D>();
            _jump = Owner.GetComponent<CharacterJumpComponent2D>();
            _dash = Owner.GetComponent<CharacterDashComponent2D>();
            _attack = Owner.GetComponent<CharacterAttackComponent2D>();
            _hfsm = Owner.GetComponent<HfsmComponent2D>();
            _motor = Owner.GetComponent<CharacterBodyMotorComponent2D>();
            _spawnPosition = Owner.GlobalPosition;
            _direction = StartDirection >= 0 ? 1 : -1;
            _jumpCooldownTimer = JumpInterval;
            _jumpSustainTimer = 0f;
        }

        public override void OnPhysicsUpdate(double delta)
        {
            float dt = (float)delta;
            UpdateDirection();

            bool jumpStartRequested = UpdateJump(dt);
            float moveAxis = UpdateMoveAxis(dt);

            var moveIntent = new MoveIntent2D(moveAxis);
            var jumpIntent = new JumpIntent2D(
                startRequested: jumpStartRequested,
                sustainRequested: _jumpSustainTimer > 0f);

            _move?.SetIntent(moveIntent);
            _jump?.SetIntent(jumpIntent);
            _dash?.SetIntent(DashIntent2D.None);
            _attack?.SetIntent(AttackIntent2D.None);
            WriteHfsmInputs(moveIntent, jumpIntent);
            _move?.ApproveIntent(moveIntent);
            _jump?.ApproveIntent(jumpIntent);
            _dash?.ApproveIntent(DashIntent2D.None);
            _attack?.ApproveIntent(AttackIntent2D.None);
        }

        private float UpdateMoveAxis(float dt)
        {
            if (_turnPauseTimer > 0f)
            {
                _turnPauseTimer = Mathf.Max(0f, _turnPauseTimer - dt);
                return 0f;
            }

            return _direction;
        }

        private void UpdateDirection()
        {
            if (_motor == null || !_motor.IsOnFloor)
                return;

            bool reachedPatrolEdge = Mathf.Abs(Owner.GlobalPosition.X - _spawnPosition.X) >= PatrolDistance;
            bool noGroundAhead = ReverseAtEdges && !_motor.HasGroundAhead(_direction, EdgeLookAhead);

            if (reachedPatrolEdge || noGroundAhead)
            {
                _direction *= -1;
                _turnPauseTimer = TurnPauseDuration;
            }
        }

        private bool UpdateJump(float dt)
        {
            _jumpCooldownTimer = Mathf.Max(0f, _jumpCooldownTimer - dt);
            _jumpSustainTimer = Mathf.Max(0f, _jumpSustainTimer - dt);

            if (_motor == null || !_motor.IsOnFloor || _jumpCooldownTimer > 0f)
                return false;

            _jumpSustainTimer = JumpSustainDuration;
            _jumpCooldownTimer = JumpInterval;
            return true;
        }

        private void WriteHfsmInputs(MoveIntent2D moveIntent, JumpIntent2D jumpIntent)
        {
            if (_hfsm == null)
                return;

            _hfsm.SetValue(CharacterHfsmBlackboardKeys.IsOnFloor, _motor?.IsOnFloor == true);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.JumpStartRequested, jumpIntent.StartRequested);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.JumpSustainRequested, jumpIntent.SustainRequested);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.MoveAxisX, moveIntent.AxisX);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.VelocityY, _motor?.Velocity.Y ?? 0f);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.DashStartRequested, false);
            _hfsm.SetValue(CharacterHfsmBlackboardKeys.AttackStartRequested, false);
        }
    }
}
