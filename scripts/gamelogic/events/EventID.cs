using Framework;

namespace GameLogic
{
    /// <summary>
    /// 游戏 UI 事件 ID 定义。
    /// </summary>
    public static class GameUIEvents
    {
        public static readonly int GameNotice = EventId.Get("game.notice");
        public static readonly int GameStart = EventId.Get("game.start");
    }

    /// <summary>
    /// 游戏输入事件 ID 定义。
    /// </summary>
    public static class GameInputEvents
    {
        public static readonly int CharacterMove = EventId.Get("game.character_move");
        public static readonly int CharacterAttack = EventId.Get("game.character_attack");
        public static readonly int CharacterJump = EventId.Get("game.character_jump");
    }

    /// <summary>
    /// RTS 系统事件 ID 定义。
    /// </summary>
    public static class GameRtsEvents
    {
        public static readonly int ArmyRosterChanged = EventId.Get("game.rts.army_roster_changed");
        public static readonly int ArmySelectionChanged = EventId.Get("game.rts.army_selection_changed");
    }
}
