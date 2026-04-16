using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Framework;
using GameLogic;
using GameLogic.Save;
using Godot;

public class GameStateSaveData : ISaveable
{
    public string SaveKey => "GameState";

    private GameState gameState => RootModule.Instance.GameState;
    
    [JsonInclude]
    private SerializableGameObjectData playerControllerData = new SerializableGameObjectData();

    [JsonInclude]
    private List<SerializableGameObjectData> playerUnitData = new List<SerializableGameObjectData>();

    public void Save()
    {
        playerControllerData = gameState.PlayerState.PlayerController.GetSerializationComponent().Save();
        playerUnitData.Clear();
        var units = gameState.PlayerState.GetArmyComponent()?.Units;
        foreach (var unit in units)
        {
            playerUnitData.Add(((SerializableGameObject2D)unit).GetSerializationComponent().Save());
        }
    }

        public bool HasData() => playerControllerData != null && playerUnitData.Count > 0;
        public int GetUnitCount() => playerUnitData?.Count ?? 0;

    public SerializableGameObjectData GetPlayerControllerData()
    {
        if (playerControllerData == null)
            return null;

        return playerControllerData;
    }

    public SerializableGameObjectData GetPlayerUnitData(int index)
    {
        if (playerUnitData == null || index < 0 || index >= playerUnitData.Count)
            return null;

        return playerUnitData[index];
    }
}

public class GameState
{
    private readonly ISaveModule _save;

    public PlayerState PlayerState;
    public GameStateSaveData SaveData;



    public GameState()
    {
        _save = ModuleSystem.GetModule<ISaveModule>();

        PlayerState = new PlayerState();
        SaveData = new GameStateSaveData();
    }

    public void Init()
    {
        Debugger.Info("Init GameState");
        _save.Register(SaveData);
    }

    public void Clear()
    {
        Debugger.Info("Clear GameState");
        PlayerState = null;
        _save.Unregister(SaveData);
    }


    public void SetPlayerController(SerializableGameObject2D playerController)
    {
        Debugger.Info("Set Player Controller in GameState");
        PlayerState.PlayerController = playerController;

        Debugger.Info(SaveData.HasData() ? "Player controller data is available for saving." : "No player controller data to save.");
    }
}
