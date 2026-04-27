using Framework;
using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class CharacterFSMComponent2D : Component2D
    {
        public const string IsOnFloorKey = "IsOnFloor";
        public const string JumpStartRequestedKey = "JumpStartRequested";
        public const string JumpSustainRequestedKey = "JumpSustainRequested";
        public const string MoveAxisXKey = "MoveAxisX";
        public const string VelocityYKey = "VelocityY";

        public override int Priority => ComponentPriority.State;

        [Export] public HfsmGraphAsset StateGraph { get; set; }
        [Export] public string InitialStateName { get; set; } = string.Empty;

        public HfsmRuntime Runtime { get; private set; }
        public string CurrentStateName => Runtime?.CurrentStateName ?? string.Empty;
        public string CurrentStatePath => Runtime?.CurrentStatePath ?? string.Empty;
        public bool CurrentStateHasTag(string tag) => Runtime?.CurrentStateHasTag(tag) == true;

        private ICharacterIntentAbility2D<MoveIntent2D> _move;
        private ICharacterIntentAbility2D<JumpIntent2D> _jump;
        private CharacterBodyMotorComponent2D _motor;

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

            if (StateGraph == null)
            {
                Debugger.Warn("[CharacterFSMComponent2D] Missing HfsmGraphAsset; intents will still be approved, but state graph will not run.");
                return;
            }

            Runtime = new HfsmRuntime(StateGraph);
            WriteBlackboardInputs();

            if (!Runtime.Start(InitialStateName))
                Debugger.Warn($"[CharacterFSMComponent2D] Failed to start state graph: {StateGraph.ResourcePath}");
        }

        public override void OnPhysicsUpdate(double delta)
        {
            WriteBlackboardInputs();
            Runtime?.Update(delta);
            ApproveMoveIntent();
            ApproveJumpIntent();
        }

        public override void OnDestroy()
        {
            Runtime?.Stop();
            Runtime = null;
        }

        private void WriteBlackboardInputs()
        {
            if (Runtime == null)
                return;

            MoveIntent2D moveIntent = _move?.RawIntent ?? MoveIntent2D.None;
            JumpIntent2D jumpIntent = _jump?.RawIntent ?? JumpIntent2D.None;

            Runtime.SetValue(IsOnFloorKey, _motor?.IsOnFloor == true);
            Runtime.SetValue(JumpStartRequestedKey, jumpIntent.StartRequested);
            Runtime.SetValue(JumpSustainRequestedKey, jumpIntent.SustainRequested);
            Runtime.SetValue(MoveAxisXKey, moveIntent.AxisX);
            Runtime.SetValue(VelocityYKey, _motor?.Velocity.Y ?? 0f);
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
        }
    }
}
