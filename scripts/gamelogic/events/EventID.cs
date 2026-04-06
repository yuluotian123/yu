using Framework;

namespace GameLogic
{
    /// <summary>
    /// 游戏通知相关事件 ID。
    /// <para>
    /// 使用 <see cref="EventId.Get(string)"/> 字符串方式注册，
    /// 同一字符串在整个 AppDomain 生命周期内始终对应同一个 ID。
    /// </para>
    /// </summary>
    public static class GameUIEvents
    {
        /// <summary>
        /// 游戏通知事件（参数：string message）。
        /// <code>
        /// // 订阅
        /// eventModule.Subscribe&lt;string&gt;(GameNoticeEvents.GameNotice, OnGameNotice);
        /// // 发送
        /// eventModule.Send&lt;string&gt;(GameNoticeEvents.GameNotice, "Hello!");
        /// </code>
        /// </summary>
        public static readonly int GameNotice = EventId.Get("game.notice");

        public static readonly int GameStart = EventId.Get("game.start");
    }

    public static class GameInputEvents
    {
        public static readonly int CharacterMove = EventId.Get("game.character_move");
        public static readonly int CharacterAttack = EventId.Get("game.character_attack");
        public static readonly int CharacterJump = EventId.Get("game.character_jump");
    }
}
