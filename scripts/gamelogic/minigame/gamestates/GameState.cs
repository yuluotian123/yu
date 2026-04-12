using System.Text.Json.Serialization;
using Framework;
using GameLogic.Save;

//运行时的游戏数据，主要是玩家，敌人，地图等,在加载正式游戏场景时自动注册
public class GameState : ISaveable
{
    public string SaveKey => "GameState";

    private ISaveModule _save;

    [JsonInclude]
    public PlayerState _PlayerState { get; set; }

    public GameState()
    {
        _PlayerState = new PlayerState();

        _save = ModuleSystem.GetModule<ISaveModule>();
    }

    public void Init()
    {
        Debugger.Info("Init GameState");
        _save.Register(this);
    }

    public void Clear()
    {
        Debugger.Info("Clear GameState");
        _save.Unregister(this);
    }


}