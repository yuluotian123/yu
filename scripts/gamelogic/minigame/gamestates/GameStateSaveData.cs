using System.Collections.Generic;
using System.Text.Json.Serialization;
using GameLogic;

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
        playerControllerData = gameState.PlayerState.PlayerController.Save();
        playerUnitData.Clear();
        var units = gameState.PlayerState.GetArmyComponent()?.Units;
        foreach (var unit in units)
        {
            playerUnitData.Add(((SerializableGameObject2D)unit).Save());
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
