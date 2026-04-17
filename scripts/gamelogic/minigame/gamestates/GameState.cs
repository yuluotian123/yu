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

public class GameState
{
    private readonly ISaveModule _save;

    public PlayerState PlayerState;
    public GameStateSaveData SaveData;

    private readonly Dictionary<string, SerializableGameObject2D> registeredObjects = new Dictionary<string, SerializableGameObject2D>();



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
        registeredObjects.Clear();
        PlayerState = null;
        _save.Unregister(SaveData);
    }


    public void SetPlayerController(SerializableGameObject2D playerController)
    {
        Debugger.Info("[GameState] Set Player Controller in GameState");
        PlayerState.PlayerController = playerController;
    }

    public void RegisterSeriableGameObject(SerializableGameObject2D obj)
    {
        if (obj == null || string.IsNullOrEmpty(obj.PersistentId))
        {
            Debugger.Warn("[GameState] Cannot register serializable object without a valid PersistentId.");
            return;
        }

        if (registeredObjects.ContainsKey(obj.PersistentId))
        {
            Debugger.Warn($"[GameState] Object with PersistentId '{obj.PersistentId}' is already registered. Overwriting.");
            registeredObjects[obj.PersistentId] = obj;
            return;
        }

        registeredObjects.Add(obj.PersistentId, obj);
    }

    public void UnregisterSeriableGameObject(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debugger.Warn("[GameState] Cannot unregister serializable object with an empty PersistentId.");
            return;
        }

        if (registeredObjects.ContainsKey(id))
        {
            registeredObjects.Remove(id);
            return;
        }

        Debugger.Warn($"[GameState] Object with PersistentId '{id}' is not registered. Cannot unregister.");
    }

    public SerializableGameObject2D GetRegisteredSeriableGameObject(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        foreach(var kvp in registeredObjects)
        {
            Debugger.Info(kvp.Key + " : " + id);
        }

        if (registeredObjects.TryGetValue(id, out var obj))
        {
            return obj;
        }


        return null;
    }
}
