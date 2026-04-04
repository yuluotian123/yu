using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GameLogic.Save;
using Godot;

namespace Framework
{
    /// <summary>
    /// 存档管理模块实现。
    /// 将所有注册的 <see cref="ISaveable"/> 对象的 public 属性（及 [JsonInclude] 字段）
    /// 序列化为 JSON 文件，并在读档时回写到对应对象。
    /// 
    /// 存档文件路径：res://saves/{slot}.json
    /// </summary>
    internal sealed class SaveModule : Module, ISaveModule
    {
        private const string SaveDir = "res://saves";

        private readonly Dictionary<string, ISaveable> _registry = new();

        private static readonly JsonSerializerOptions _writeOpts = new() { WriteIndented = true };

        public override int Priority => 0;

        public override void OnInit() { }

        public override void Shutdown() => _registry.Clear();

        // ── 注册 ──────────────────────────────────────────────────────────────

        public void Register(ISaveable saveable)
        {
            if (saveable == null) return;
            if (_registry.ContainsKey(saveable.SaveKey))
            {
                Debugger.Warn($"[SaveModule] SaveKey '{saveable.SaveKey}' already registered.");
                return;
            }
            _registry[saveable.SaveKey] = saveable;
        }

        public void Unregister(ISaveable saveable)
        {
            if (saveable == null) return;
            _registry.Remove(saveable.SaveKey);
        }

        // ── 存档 ──────────────────────────────────────────────────────────────

        public void Save(string slot = "default")
        {
            EnsureSaveDir();

            var root = new JsonObject();
            foreach (var kv in _registry)
                root[kv.Key] = SerializeObject(kv.Value);

            var path = GetPath(slot);
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                Debugger.Error($"[SaveModule] Cannot open file for writing: {path}");
                return;
            }
            file.StoreString(root.ToJsonString(_writeOpts));
            Debugger.Info($"[SaveModule] Saved slot '{slot}' → {path}");
        }

        // ── 读档 ──────────────────────────────────────────────────────────────

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
            if (root == null) return false;

            foreach (var kv in _registry)
            {
                if (root.TryGetPropertyValue(kv.Key, out var node) && node is JsonObject jObj)
                    DeserializeInto(kv.Value, jObj);
            }

            Debugger.Info($"[SaveModule] Loaded slot '{slot}' ← {path}");
            return true;
        }

        // ── 工具方法 ──────────────────────────────────────────────────────────

        public void Delete(string slot = "default")
        {
            var globalPath = ProjectSettings.GlobalizePath(GetPath(slot));
            if (System.IO.File.Exists(globalPath))
                System.IO.File.Delete(globalPath);
        }

        public bool Exists(string slot = "default") => FileAccess.FileExists(GetPath(slot));

        // ── 内部序列化 ────────────────────────────────────────────────────────

        private static JsonObject SerializeObject(ISaveable obj)
        {
            var json = JsonHelper.Serialize(obj);
            return JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
        }

        private static void DeserializeInto(ISaveable target, JsonObject jObj)
        {
            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (var prop in type.GetProperties(flags))
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;

                var include = prop.GetCustomAttribute<JsonIncludeAttribute>();
                bool shouldLoad = include != null || (prop.GetMethod?.IsPublic == true && prop.CanWrite);

                if (shouldLoad && prop.CanWrite && jObj.TryGetPropertyValue(prop.Name, out var node))
                {
                    var value = JsonHelper.Deserialize(node?.ToJsonString(), prop.PropertyType);
                    if (value != null)
                        prop.SetValue(target, value);
                }
            }

            foreach (var field in type.GetFields(flags))
            {
                if (field.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
                if (field.GetCustomAttribute<JsonIncludeAttribute>() != null
                    && jObj.TryGetPropertyValue(field.Name, out var node))
                {
                    var value = JsonHelper.Deserialize(node?.ToJsonString(), field.FieldType);
                    if (value != null)
                        field.SetValue(target, value);
                }
            }
        }

        private static string GetPath(string slot) => $"{SaveDir}/{slot}.json";

        private static void EnsureSaveDir()
        {
            var globalDir = ProjectSettings.GlobalizePath(SaveDir);
            if (!System.IO.Directory.Exists(globalDir))
                System.IO.Directory.CreateDirectory(globalDir);
        }
    }
}
