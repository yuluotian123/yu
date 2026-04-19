using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Framework;
using GameLogic;

namespace GameLogic
{
    public class GameState
    {
        private readonly ISaveModule _save;

        private readonly Dictionary<string, IGameObject> registeredObjects = new Dictionary<string, IGameObject>();



        public GameState()
        {
            _save = ModuleSystem.GetModule<ISaveModule>();
        }

        public void Init()
        {
            Debugger.Info("Init GameState");
        }

        public void Clear()
        {
            Debugger.Info("Clear GameState");
            registeredObjects.Clear();
        }


        public void RegisterSeriableGameObject(SerializableGameObject2D obj)
        {
            RegisterSeriableGameObjectInternal(obj);
        }

        public void RegisterSeriableGameObject(SerializableGameObject3D obj)
        {
            RegisterSeriableGameObjectInternal(obj);
        }

        private void RegisterSeriableGameObjectInternal(IGameObject obj)
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

        public SerializableGameObject2D GetRegisteredSeriableGameObject2D(string id)
        {
            return GetRegisteredSeriableGameObject<SerializableGameObject2D>(id);
        }

        public SerializableGameObject3D GetRegisteredSeriableGameObject3D(string id)
        {
            return GetRegisteredSeriableGameObject<SerializableGameObject3D>(id);
        }

        private T GetRegisteredSeriableGameObject<T>(string id) where T : class, IGameObject
        {
            if (string.IsNullOrEmpty(id))
                return null;

            return registeredObjects.TryGetValue(id, out var obj) ? obj as T : null;
        }
    }
}
