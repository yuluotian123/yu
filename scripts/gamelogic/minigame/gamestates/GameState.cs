using System.Text.Json.Serialization;
using Framework;
using GameLogic.Save;

//运行时的游戏数据，主要是玩家，敌人，地图等,在加载正式游戏场景时自动注册
public class GameState : ISaveable
{
    public string SaveKey => "GameState";

    private ISaveModule _save;

    [JsonInclude]
    public PlayerState MPlayerState{get;set;}

    public GameState()
    {
        MPlayerState = new PlayerState();

        _save = ModuleSystem.GetModule<ISaveModule>();
    }

    public void Init()
    {
        _save.Register(this);
    }

    public void Clear()
    {        
        _save.Unregister(this);
    }


}