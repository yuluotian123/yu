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
}
