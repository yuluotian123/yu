using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Framework;
using Godot;

namespace GameLogic.Save
{
    internal sealed class SaveModule : Framework.Module, ISaveModule
    {
        private const string SaveDir = "res://saves";

        private readonly Dictionary<string, ISaveable> _registry = new();

        private static readonly JsonSerializerOptions _writeOpts = new() { WriteIndented = true };

        public override int Priority => -100;

        public override void OnInit() { }

        public override void Shutdown() => _registry.Clear();

        public void Register(ISaveable saveable)
        {
            if (saveable == null)
                return;

            if (_registry.ContainsKey(saveable.SaveKey))
            {
                Debugger.Warn($"[SaveModule] SaveKey '{saveable.SaveKey}' already registered.");
                return;
            }

            _registry[saveable.SaveKey] = saveable;
        }

        public void Unregister(ISaveable saveable)
        {
            if (saveable == null)
                return;

            _registry.Remove(saveable.SaveKey);
        }

        public void Save(string slot = "default")
        {
            EnsureSaveDir();

            var root = new JsonObject();
            foreach (var kv in _registry)
            {
                kv.Value.Save();
                root[kv.Key] = AutoStateSerializer.SerializeObject(kv.Value);
            }

            var path = GetPath(slot);
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                Debugger.Error($"[SaveModule] Cannot open file for writing: {path}");
                return;
            }

            file.StoreString(root.ToJsonString(_writeOpts));
            Debugger.Info($"[SaveModule] Saved slot '{slot}' -> {path}");
        }

        public bool Load(string slot = "default")
        {
            var path = GetPath(slot);
            if (!FileAccess.FileExists(path))
                return false;

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                Debugger.Error($"[SaveModule] Cannot open file for reading: {path}");
                return false;
            }

            var root = JsonNode.Parse(file.GetAsText())?.AsObject();
            if (root == null)
                return false;

            foreach (var kv in _registry)
            {
                if (root.TryGetPropertyValue(kv.Key, out var node) && node is JsonObject state)
                {
                    AutoStateSerializer.DeserializeInto(kv.Value, state);
                    kv.Value.Load();
                }
            }

            Debugger.Info($"[SaveModule] Loaded slot '{slot}' <- {path}");
            return true;
        }

        public void Delete(string slot = "default")
        {
            var globalPath = ProjectSettings.GlobalizePath(GetPath(slot));
            if (System.IO.File.Exists(globalPath))
                System.IO.File.Delete(globalPath);
        }

        public bool Exists(string slot = "default") => FileAccess.FileExists(GetPath(slot));

        private static string GetPath(string slot) => $"{SaveDir}/{slot}.json";

        private static void EnsureSaveDir()
        {
            var globalDir = ProjectSettings.GlobalizePath(SaveDir);
            if (!System.IO.Directory.Exists(globalDir))
                System.IO.Directory.CreateDirectory(globalDir);
        }
    }
}
