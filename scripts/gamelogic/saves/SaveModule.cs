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
        private string _pendingSlot;

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

            WriteAtomic(path, root);
            Debugger.Info($"[SaveModule] Saved slot '{slot}' -> {path}");
        }

        private void WriteAtomic(string path, JsonObject root)
        {
            EnsureSaveDir();
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
            if (System.IO.File.Exists(globalPath + ".deleted"))
                System.IO.File.Delete(globalPath + ".deleted");
        }

        public bool Load(string slot = "default")
        {
            _pendingSections = null;
            _pendingSlot = null;
            if (IsDeleted(slot)) return false;
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

            _pendingSlot = NormalizeSlot(slot);
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

        public string[] ListSlots()
        {
            var slots = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string folder in new[] { _saveDir, _legacySaveDir })
            {
                string directory = ProjectSettings.GlobalizePath(folder);
                if (!System.IO.Directory.Exists(directory)) continue;
                foreach (string file in System.IO.Directory.EnumerateFiles(directory))
                {
                    string name = System.IO.Path.GetFileName(file);
                    if (name.EndsWith(".json.bak", StringComparison.Ordinal)) name = name[..^4];
                    if (!name.EndsWith(".json", StringComparison.Ordinal)) continue;
                    string slot = name[..^5];
                    if (!IsDeleted(slot)) slots.Add(slot);
                }
            }
            return System.Linq.Enumerable.ToArray(slots);
        }

        public string ReadSlot(string slot, out string sourcePath)
        {
            sourcePath = GetPath(slot);
            if (IsDeleted(slot)) return null;
            foreach (string candidate in new[] { GetPath(slot), GetPath(slot) + ".bak", GetLegacyPath(slot) })
            {
                if (!FileAccess.FileExists(candidate)) continue;
                sourcePath = candidate;
                using var file = FileAccess.Open(candidate, FileAccess.ModeFlags.Read)
                    ?? throw new System.IO.IOException($"Cannot open {candidate}");
                // Show the actual primary contents, even when damaged, so the GM can repair it.
                return file.GetAsText();
            }
            return null;
        }

        public void WriteSlot(string slot, string json, string expectedJson)
        {
            var root = ValidateEditedDocument(json);
            CheckUnchanged(slot, expectedJson);
            WriteAtomic(GetPath(slot), root);
        }

        public void DeleteSlot(string slot, string expectedJson)
        {
            CheckUnchanged(slot, expectedJson);
            Delete(slot);
        }

        private void CheckUnchanged(string slot, string expectedJson)
        {
            if (!string.Equals(ReadSlot(slot, out _), expectedJson, StringComparison.Ordinal))
                throw new InvalidOperationException("存档已在外部改变，请先重新读取，再提交修改。");
        }

        internal static JsonObject ValidateEditedDocument(string json)
        {
            var root = JsonNode.Parse(json) as JsonObject ?? throw new JsonException("存档根节点必须是对象。");
            if (root["meta"] is not JsonObject meta || meta["format_version"]?.GetValue<int>() != FormatVersion ||
                root["legacy"] is not JsonObject || root["sections"] is not JsonObject sections)
                throw new JsonException("需要 format_version = 2，以及 meta、legacy、sections 对象。");
            foreach (var group in sections)
            {
                if (group.Value is not JsonObject entries) throw new JsonException("存档分组必须是对象。");
                foreach (var entry in entries)
                {
                    if (entry.Value is not JsonObject state || (state["schema_version"]?.GetValue<int>() ?? 0) < 1)
                        throw new JsonException("每个存档条目需要有效的 schema_version。");
                    if (group.Key == "characters")
                    {
                        int schema = state["schema_version"].GetValue<int>();
                        if (schema > 3) throw new JsonException("不支持的角色存档版本。");
                        ValidateVector(state["position"]);
                        ValidateVector(state["facing_direction"]);
                        if (schema >= 3) ValidateVector(state["rotation"]);
                        else if (state["rotation"] != null) _ = state["rotation"].GetValue<float>();
                        if (state["facing"] != null) _ = state["facing"].GetValue<int>();
                        if (state["flags"] != null)
                            foreach (var flag in state["flags"].AsObject()) _ = flag.Value.GetValue<bool>();
                    }
                    if (group.Key == "world" && entry.Key == "time_of_day")
                    {
                        if (state["schema_version"].GetValue<int>() != 1) throw new JsonException("不支持的时间存档版本。");
                        if (state["initialized"] != null) _ = state["initialized"].GetValue<bool>();
                        new WorldClock().Restore(state["total_hours"]?.GetValue<double>() ?? 12,
                            state["day_length_seconds"]?.GetValue<double>() ?? 1200,
                            state["lunar_period_days"]?.GetValue<double>() ?? 29.53059,
                            state["lunar_offset_days"]?.GetValue<double>() ?? 14.265295,
                            state["speed"]?.GetValue<double>() ?? 1, state["paused"]?.GetValue<bool>() ?? false);
                    }
                }
            }
            return root;
        }

        private static void ValidateVector(JsonNode node)
        {
            if (node == null) return;
            var vector = node.AsObject();
            foreach (string axis in new[] { "x", "y", "z" })
                if (vector[axis] != null && !float.IsFinite(vector[axis].GetValue<float>()))
                    throw new JsonException("坐标必须是有限数值。");
        }

        public void Delete(string slot = "default")
        {
            string normalized = NormalizeSlot(slot);
            EnsureSaveDir();
            string globalPath = ProjectSettings.GlobalizePath(GetPath(normalized));
            // A tombstone also suppresses the packaged legacy fallback without modifying project assets.
            System.IO.File.WriteAllText(globalPath + ".deleted", "deleted");
            if (System.IO.File.Exists(globalPath)) System.IO.File.Delete(globalPath);
            if (System.IO.File.Exists(globalPath + ".bak")) System.IO.File.Delete(globalPath + ".bak");
            if (_pendingSlot == normalized) { _pendingSections = null; _pendingSlot = null; }
        }

        private bool IsDeleted(string slot) => FileAccess.FileExists(GetPath(slot) + ".deleted");
        public bool Exists(string slot = "default") => !IsDeleted(slot) &&
            (FileAccess.FileExists(GetPath(slot)) || FileAccess.FileExists(GetPath(slot) + ".bak") ||
             FileAccess.FileExists(GetLegacyPath(slot)));

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
