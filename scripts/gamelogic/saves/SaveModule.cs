using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Framework;
using Godot;

namespace GameLogic
{
    internal sealed class SaveModule : Framework.Module, ISaveModule
    {
        private const string SaveDir = "user://saves";
        private const string LegacySaveDir = "res://saves";
        private const int FormatVersion = 2;

        private readonly Dictionary<string, ISaveable> _registry = new();
        private readonly Dictionary<string, ISaveSection> _sections = new();
        private JsonObject _pendingSections;

        private static readonly JsonSerializerOptions _writeOpts = new() { WriteIndented = true };

        public override int Priority => -100;

        public override void OnInit() { }

        public override void Shutdown()
        {
            _registry.Clear();
            _sections.Clear();
            _pendingSections = null;
        }

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

        public void RegisterSection(ISaveSection section)
        {
            if (section == null || string.IsNullOrWhiteSpace(section.SectionKey) || string.IsNullOrWhiteSpace(section.EntryKey))
                return;

            string key = MakeSectionKey(section.SectionKey, section.EntryKey);
            _sections[key] = section;

            if (_pendingSections?[section.SectionKey] is JsonObject group && group[section.EntryKey] is JsonObject state)
            {
                int schema = state["schema_version"]?.GetValue<int>() ?? 1;
                section.Restore(state, schema);
            }
        }

        public void UnregisterSection(ISaveSection section)
        {
            if (section != null)
                _sections.Remove(MakeSectionKey(section.SectionKey, section.EntryKey));
        }

        public void Save(string slot = "default")
        {
            EnsureSaveDir();

            var root = new JsonObject
            {
                ["meta"] = new JsonObject
                {
                    ["format_version"] = FormatVersion,
                    ["saved_at_unix_ms"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                },
                ["legacy"] = new JsonObject(),
                ["sections"] = new JsonObject()
            };

            JsonObject legacy = root["legacy"].AsObject();
            foreach (var kv in _registry)
            {
                kv.Value.Save();
                legacy[kv.Key] = AutoStateSerializer.SerializeObject(kv.Value);
            }

            JsonObject sections = root["sections"].AsObject();
            foreach (var kv in _sections)
            {
                ISaveSection section = kv.Value;
                if (section == null)
                    continue;

                if (sections[section.SectionKey] is not JsonObject group)
                {
                    group = new JsonObject();
                    sections[section.SectionKey] = group;
                }

                JsonObject state = section.Capture() ?? new JsonObject();
                state["schema_version"] = section.SchemaVersion;
                group[section.EntryKey] = state;
            }

            var path = GetPath(slot);
            string tempPath = $"{path}.tmp";
            using (var file = FileAccess.Open(tempPath, FileAccess.ModeFlags.Write))
            {
                if (file == null)
                {
                    Debugger.Error($"[SaveModule] Cannot open file for writing: {tempPath}");
                    return;
                }

                file.StoreString(root.ToJsonString(_writeOpts));
            }

            string globalPath = ProjectSettings.GlobalizePath(path);
            string globalTempPath = ProjectSettings.GlobalizePath(tempPath);
            string globalBackupPath = $"{globalPath}.bak";
            if (System.IO.File.Exists(globalPath))
                System.IO.File.Copy(globalPath, globalBackupPath, true);
            System.IO.File.Move(globalTempPath, globalPath, true);
            Debugger.Info($"[SaveModule] Saved slot '{slot}' -> {path}");
        }

        public bool Load(string slot = "default")
        {
            _pendingSections = null;
            var path = GetPath(slot);
            if (!FileAccess.FileExists(path))
            {
                string backupPath = $"{path}.bak";
                path = FileAccess.FileExists(backupPath) ? backupPath : GetLegacyPath(slot);
            }

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

            JsonObject legacy = root["legacy"] as JsonObject ?? root;
            foreach (var kv in _registry)
            {
                Debugger.Info($"[SaveModule] Loading '{kv.Key}' from slot '{slot}'");
                if (legacy.TryGetPropertyValue(kv.Key, out var node) && node is JsonObject state)
                {
                    AutoStateSerializer.DeserializeInto(kv.Value, state);
                    kv.Value.Load();
                }
            }

            _pendingSections = root["sections"] as JsonObject;
            foreach (var section in _sections.Values)
            {
                if (_pendingSections?[section.SectionKey] is JsonObject group && group[section.EntryKey] is JsonObject state)
                {
                    int schema = state["schema_version"]?.GetValue<int>() ?? 1;
                    section.Restore(state, schema);
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
            if (System.IO.File.Exists($"{globalPath}.bak"))
                System.IO.File.Delete($"{globalPath}.bak");
        }

        public bool Exists(string slot = "default") =>
            FileAccess.FileExists(GetPath(slot)) ||
            FileAccess.FileExists($"{GetPath(slot)}.bak") ||
            FileAccess.FileExists(GetLegacyPath(slot));

        private static string GetPath(string slot) => $"{SaveDir}/{NormalizeSlot(slot)}.json";

        private static string GetLegacyPath(string slot) => $"{LegacySaveDir}/{NormalizeSlot(slot)}.json";

        private static string NormalizeSlot(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
                return "default";

            string value = slot.Trim();
            if (value == "." || value == ".." || value.Contains('/') || value.Contains('\\'))
                return "default";

            foreach (char character in System.IO.Path.GetInvalidFileNameChars())
            {
                if (value.Contains(character))
                    return "default";
            }

            return value;
        }

        private static void EnsureSaveDir()
        {
            var globalDir = ProjectSettings.GlobalizePath(SaveDir);
            if (!System.IO.Directory.Exists(globalDir))
                System.IO.Directory.CreateDirectory(globalDir);
        }

        private static string MakeSectionKey(string sectionKey, string entryKey) => $"{sectionKey}:{entryKey}";
    }
}
