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
        private readonly string _saveDir;
        private readonly string _legacySaveDir;
        private const int FormatVersion = 2;

        private readonly Dictionary<string, ISaveable> _registry = new();
        private readonly Dictionary<string, ISaveSection> _sections = new();
        private JsonObject _pendingSections;

        private static readonly JsonSerializerOptions _writeOpts = new() { WriteIndented = true };

        public SaveModule() : this("user://saves", "res://saves") { }

        internal SaveModule(string saveDir, string legacySaveDir)
        {
            _saveDir = saveDir;
            _legacySaveDir = legacySaveDir;
        }

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

            if (_registry.TryGetValue(saveable.SaveKey, out var registered) && ReferenceEquals(registered, saveable))
                _registry.Remove(saveable.SaveKey);
        }

        public void RegisterSection(ISaveSection section)
        {
            if (section == null || string.IsNullOrWhiteSpace(section.SectionKey) || string.IsNullOrWhiteSpace(section.EntryKey))
                return;

            string key = MakeSectionKey(section.SectionKey, section.EntryKey);
            if (_sections.TryGetValue(key, out var registered) && !ReferenceEquals(registered, section))
                throw new InvalidOperationException($"Save section '{key}' is already registered.");
            _sections[key] = section;

            if (_pendingSections?[section.SectionKey] is JsonObject group && group[section.EntryKey] is JsonObject state)
            {
                int schema = state["schema_version"]?.GetValue<int>() ?? 1;
                section.Restore(state, schema);
            }
        }

        public void UnregisterSection(ISaveSection section)
        {
            if (section == null)
                return;
            string key = MakeSectionKey(section.SectionKey, section.EntryKey);
            if (_sections.TryGetValue(key, out var registered) && ReferenceEquals(registered, section))
                _sections.Remove(key);
        }

        public void Save(string slot = "default")
        {
            var path = GetPath(slot);
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

            string globalPath = ProjectSettings.GlobalizePath(path);
            string tempPath = $"{globalPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (var stream = new System.IO.FileStream(tempPath, System.IO.FileMode.CreateNew,
                           System.IO.FileAccess.Write, System.IO.FileShare.None))
                {
                    JsonSerializer.Serialize(stream, root, _writeOpts);
                    stream.Flush(flushToDisk: true);
                }

                if (System.IO.File.Exists(globalPath))
                {
                    // Do not replace a usable backup with a corrupt primary file.
                    string backup = TryReadSave(path, out _) ? $"{globalPath}.bak" : null;
                    System.IO.File.Replace(tempPath, globalPath, backup);
                }
                else
                    System.IO.File.Move(tempPath, globalPath);
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
            Debugger.Info($"[SaveModule] Saved slot '{slot}' -> {path}");
        }

        public bool Load(string slot = "default")
        {
            _pendingSections = null;
            var path = GetPath(slot);
            JsonObject root = null;
            foreach (string candidate in new[] { path, $"{path}.bak", GetLegacyPath(slot) })
            {
                if (!TryReadSave(candidate, out root))
                    continue;
                path = candidate;
                break;
            }
            if (root == null)
                return false;

            int version = root["meta"]?["format_version"]?.GetValue<int>() ?? 1;
            if (version > FormatVersion)
            {
                Debugger.Warn($"[SaveModule] Unsupported format version {version}: {path}");
                return false;
            }

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

        private static bool TryReadSave(string path, out JsonObject root)
        {
            root = null;
            if (!FileAccess.FileExists(path))
                return false;
            try
            {
                using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                if (file == null)
                    throw new System.IO.IOException($"Cannot open file: {FileAccess.GetOpenError()}");
                var parsed = JsonNode.Parse(file.GetAsText()) as JsonObject
                    ?? throw new JsonException("Save root must be an object.");
                if (parsed["meta"] != null)
                {
                    var meta = parsed["meta"].AsObject();
                    if ((meta["format_version"]?.GetValue<int>() ?? 1) < 1)
                        throw new JsonException("Invalid save format version.");
                    if (parsed["legacy"] is not JsonObject || parsed["sections"] is not JsonObject)
                        throw new JsonException("Save envelope is incomplete.");
                }
                if (parsed["sections"] != null)
                {
                    foreach (var group in parsed["sections"].AsObject())
                    {
                        if (group.Value is not JsonObject entries)
                            throw new JsonException("Save section group must be an object.");
                        foreach (var entry in entries)
                        {
                            if (entry.Value is not JsonObject state)
                                throw new JsonException("Save section entry must be an object.");
                            if ((state["schema_version"]?.GetValue<int>() ?? 1) < 1)
                                throw new JsonException("Invalid section schema version.");
                        }
                    }
                }
                root = parsed;
                return true;
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException
                or FormatException or System.IO.IOException or UnauthorizedAccessException)
            {
                Debugger.Warn($"[SaveModule] Cannot read '{path}': {exception.Message}");
                return false;
            }
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

        private string GetPath(string slot) => $"{_saveDir}/{NormalizeSlot(slot)}.json";

        private string GetLegacyPath(string slot) => $"{_legacySaveDir}/{NormalizeSlot(slot)}.json";

        private static string NormalizeSlot(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
                return "default";

            string value = slot.Trim();
            if (value == "." || value == ".." || value.Contains('/') || value.Contains('\\'))
                throw new ArgumentException("Save slot must be a file name, not a path.", nameof(slot));

            foreach (char character in System.IO.Path.GetInvalidFileNameChars())
            {
                if (value.Contains(character))
                    throw new ArgumentException("Save slot contains an invalid file name character.", nameof(slot));
            }

            return value;
        }

        private void EnsureSaveDir()
        {
            var globalDir = ProjectSettings.GlobalizePath(_saveDir);
            if (!System.IO.Directory.Exists(globalDir))
                System.IO.Directory.CreateDirectory(globalDir);
        }

        private static string MakeSectionKey(string sectionKey, string entryKey) => $"{sectionKey}:{entryKey}";
    }
}
