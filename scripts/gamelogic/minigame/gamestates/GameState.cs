using System.Text.Json.Serialization;
using Framework;
using GameLogic.Save;

/// <summary>
/// 运行时的游戏存档根对象。
/// </summary>
public class GameState : ISaveable
{
    /// <summary>
    /// 获取当前存档对象在 SaveModule 中使用的唯一键。
    /// </summary>
    public string SaveKey => "GameState";

    /// <summary>
    /// 获取或设置玩家存档状态。
    /// </summary>
    [JsonInclude]
    public PlayerState _PlayerState { get; set; }

    private readonly ISaveModule _save;

    /// <summary>
    /// 初始化游戏状态对象并准备默认玩家存档。
    /// </summary>
    public GameState()
    {
        _PlayerState = new PlayerState();
        _save = ModuleSystem.GetModule<ISaveModule>();
    }

    /// <summary>
    /// 将当前游戏状态注册到存档模块。
    /// </summary>
    public void Init()
    {
        Debugger.Info("Init GameState");
        _save.Register(this);
    }

    /// <summary>
    /// 将当前游戏状态从存档模块中注销。
    /// </summary>
    public void Clear()
    {
        Debugger.Info("Clear GameState");
        _save.Unregister(this);
    }

    /// <summary>
    /// 在写档前执行 GameState 级别的收口逻辑。
    /// </summary>
    public void Save()
    {
        // RTS 运行时数据由 PlayerArmyComponent 在状态变化时直接写回 PlayerState。
    }

    /// <summary>
    /// 读档完成后的回调。
    /// </summary>
    public void Load()
    {
        // 当前阶段只负责让数据进入 GameState，不在此处恢复场景对象。
    }
}
