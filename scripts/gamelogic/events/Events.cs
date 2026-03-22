using Framework;

namespace GameLogic.UI
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
}
