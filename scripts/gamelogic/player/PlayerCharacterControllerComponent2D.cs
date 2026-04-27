using Godot;
using Framework;

namespace GameLogic
{
    /// <summary>
    /// 玩家角色控制器，负责读取 InputModule 并写入角色能力的原始 intent。
    /// </summary>
    [GlobalClass]
    public partial class PlayerCharacterControllerComponent2D : Component2D
    {
        public override int Priority => ComponentPriority.Input;

        [ExportGroup("输入映射")]
        /// <summary>向左移动的输入 action 名称。</summary>
        [Export] public string MoveLeftAction { get; set; } = "player_move_left";

        /// <summary>向右移动的输入 action 名称。</summary>
        [Export] public string MoveRightAction { get; set; } = "player_move_right";

        /// <summary>跳跃的输入 action 名称。</summary>
        [Export] public string JumpAction { get; set; } = "player_jump";

        private ICharacterIntentAbility2D<MoveIntent2D> _move;
        private ICharacterIntentAbility2D<JumpIntent2D> _jump;

        public override void OnInit()
        {
            _move = Owner.GetComponent<CharacterMoveComponent2D>();
            _jump = Owner.GetComponent<CharacterJumpComponent2D>();
        }

        public override void OnPhysicsUpdate(double delta)
        {
            var input = ModuleSystem.GetModule<IInputModule>();
            if (input == null)
            {
                _move?.SetIntent(MoveIntent2D.None);
                _jump?.SetIntent(JumpIntent2D.None);
                return;
            }

            float left = input.GetActionStrength(MoveLeftAction);
            float right = input.GetActionStrength(MoveRightAction);
            float inputX = right - left;

            _move?.SetIntent(new MoveIntent2D(inputX));
            _jump?.SetIntent(new JumpIntent2D(
                startRequested: input.IsJustPressed(JumpAction),
                sustainRequested: input.IsPressed(JumpAction)));
        }
    }
}
