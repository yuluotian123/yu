using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Framework;
using GameLogic;
using GameLogic.Save;
using Godot;

public class GameState : ISaveable
{
    public string SaveKey => "GameState";

    [JsonIgnore]
    public PlayerState PlayerState { get; set; } 

    private readonly ISaveModule _save;



    public GameState()
    {
        _save = ModuleSystem.GetModule<ISaveModule>();
        PlayerState = new PlayerState();
    }

    public void Init()
    {
        Debugger.Info("Init GameState");
        _save.Register(this);
    }

    public void Clear()
    {
        Debugger.Info("Clear GameState");
        PlayerState = null;
        _save.Unregister(this);
    }


    public void SetPlayerController(GameObject2D playerController)
    {
        Debugger.Info("Set Player Controller in GameState");
        PlayerState.PlayerController = playerController;
    }


    public void Save()
    {

    }
}
