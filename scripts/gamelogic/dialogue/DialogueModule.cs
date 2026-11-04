using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Framework;
using Framework.UI;
using GameLogic.AI;
using Godot;

namespace GameLogic;

public sealed class DialogueModule : Module, IDialogueModule, IProcessModule, ISaveSection
{
    const string ControlLock = "Dialogue";
    // Compatibility payload for old saves; consume each key when it moves into NPC memory.
    readonly Dictionary<string, bool> _legacyFlags = new(StringComparer.Ordinal);
    readonly List<(Component3D Component, bool Active)> _suspended = new();
    readonly List<CharacterMovementComponent3D> _movement = new();
    readonly Dictionary<string, bool> _layers = new();
    GameObject3D _npc, _player;
    PlayerCharacterInputComponent3D _playerInput;
    bool _wasBlocked;
    double _loadingSeconds;
    Control _previousFocus;
    IUIModule _ui;
    IInputModule _input;
    ISaveModule _save;
    public override int Priority => 90;
    public DialogueSession Active { get; private set; }
    public bool IsActive => Active != null;
    public string LastError { get; private set; } = "";
    public event Action<DialogueSession> Finished;
    public string SectionKey => "dialogue";
    public string EntryKey => "story";
    public int SchemaVersion => 1;

    public override void OnInit()
    {
        _ui = ModuleSystem.GetModule<IUIModule>(); _input = ModuleSystem.GetModule<IInputModule>();
        _save = ModuleSystem.GetModule<ISaveModule>(); _save.RegisterSection(this);
    }
    public bool TryStart(GameObject3D npc, GameObject3D player, string entryId = "talk")
    {
        if (!Live(npc) || !Live(player)) return false;
        var entry = NpcDialogueNodeData.Entries(npc.GetComponent<AiAgentComponent3D>()?.Graph).FirstOrDefault(e => e.EntryId == entryId);
        return entry?.GetSubGraph() is DialogueGraphAsset graph && TryStart(graph, npc, player);
    }
    public bool TryStart(DialogueGraphAsset graph, GameObject3D npc, GameObject3D player)
    {
        if (IsActive || !Live(npc) || !Live(player) || ModuleSystem.GetModule<IWorldModule>().IsSwitching) return false;
        LastError = "";
        DialogueSession session;
        var agent = npc.GetComponent<AiAgentComponent3D>();
        var memory = agent?.Decision?.Memory;
        try
        {
            if (memory != null && !string.IsNullOrWhiteSpace(player.PersistentId))
                foreach (var definition in memory.Definitions.Values)
                    if (definition.LegacyDialogueFlag is { Length: > 0 } oldKey && _legacyFlags.TryGetValue(oldKey, out bool oldValue))
                    {
                        if (memory.HasEntry(definition.Key, player.PersistentId) || memory.Write(definition.Key, player.PersistentId, oldValue, Hours(agent))) _legacyFlags.Remove(oldKey);
                    }
            session = new(graph, memory, player.PersistentId, Name(npc, "对方"), Name(player, "你"), () => Hours(agent));
        }
        catch (Exception error) { LastError = error.Message; GD.PushWarning("无法开始对话：" + LastError); return false; }
        _npc = npc; _player = player; Active = session; _loadingSeconds = 0;
        _previousFocus = player.GetViewport().GuiGetFocusOwner();
        _playerInput = player.GetComponent<PlayerCharacterInputComponent3D>();
        if (_playerInput != null) { _wasBlocked = _playerInput.GameplayBlocked; _playerInput.GameplayBlocked = true; }
        foreach (string layer in new[] { "Combat", "Camera" })
        { _layers[layer] = _input.IsLayerEnabled(layer); _input.DisableLayer(layer); }
        foreach (var host in new[] { player, npc }.Distinct())
        {
            var movement = host.GetComponent<CharacterMovementComponent3D>();
            if (movement != null) { movement.SetControlLock(ControlLock, true, true, 1000); _movement.Add(movement); }
            foreach (var component in new Component3D[] { host.GetComponent<AiAgentComponent3D>(), host.GetComponent<NpcNavigationComponent3D>() })
                if (component != null) { _suspended.Add((component, component.IsActive)); component.IsActive = false; }
        }
        npc.TreeExiting += Cancel; if (player != npc) player.TreeExiting += Cancel;
        session.Finished += End;
        try
        {
            session.Start();
            if (!session.IsComplete) _ui.ShowUI<DialogueWindow>(this, session);
            return true;
        }
        catch (Exception error) { LastError = error.Message; Cancel(); return false; }
    }
    static bool Live(GameObject3D host) => GodotObject.IsInstanceValid(host) && host.IsInsideTree() && !host.IsQueuedForDeletion();
    static string Name(GameObject3D host, string fallback) => host.GetComponent<CharacterDataComponent3D>()?.Profile?.Identity?.DisplayName is { Length: > 0 } name ? name : fallback;
    static double Hours(AiAgentComponent3D agent) => agent?.World?.Clock.TotalHours ?? agent?.SimulationHours ?? 0;
    public bool TryReadMemory(GameObject3D npc, GameObject3D participant, string key, out bool value)
    {
        value = false;
        if (!Live(npc) || !Live(participant) || string.IsNullOrWhiteSpace(participant.PersistentId)) return false;
        var agent = npc.GetComponent<AiAgentComponent3D>(); var memory = agent?.Decision?.Memory;
        if (memory == null) return false;
        memory.Expire(Hours(agent)); return memory.TryRead(key, participant.PersistentId, out value);
    }
    public bool WriteMemory(GameObject3D npc, GameObject3D participant, string key, bool value) => Live(npc) && Live(participant) && !string.IsNullOrWhiteSpace(participant.PersistentId)
        && npc.GetComponent<AiAgentComponent3D>() is { Decision.Memory: { } memory } agent && memory.Write(key, participant.PersistentId, value, Hours(agent));
    public void Cancel() => Active?.Cancel();
    public void ResetSession() { Cancel(); _legacyFlags.Clear(); }
    public void Process(double elapsed, double realElapsed)
    {
        if (!IsActive) return;
        if (!Live(_npc) || !Live(_player) || ModuleSystem.GetModule<IWorldModule>().IsSwitching) { Cancel(); return; }
        _loadingSeconds += realElapsed;
        var window = _ui.GetWindow<DialogueWindow>();
        if (!_ui.HasWindow<DialogueWindow>() || window?.IsLoadDone != true && _loadingSeconds > 15)
        { LastError = "对话界面加载失败。"; Cancel(); }
    }
    void End()
    {
        var session = Active; if (session == null) return;
        Active = null; session.Finished -= End;
        if (GodotObject.IsInstanceValid(_npc)) _npc.TreeExiting -= Cancel;
        if (_player != _npc && GodotObject.IsInstanceValid(_player)) _player.TreeExiting -= Cancel;
        if (GodotObject.IsInstanceValid(_playerInput)) _playerInput.GameplayBlocked = _wasBlocked;
        foreach (var movement in _movement) if (GodotObject.IsInstanceValid(movement)) movement.ClearControlLock(ControlLock);
        foreach (var item in _suspended) if (GodotObject.IsInstanceValid(item.Component)) item.Component.IsActive = item.Active;
        foreach (var layer in _layers) { if (layer.Value) _input.EnableLayer(layer.Key); else _input.DisableLayer(layer.Key); }
        _input.ClearBuffer();
        _movement.Clear(); _suspended.Clear(); _layers.Clear();
        _npc = _player = null; _playerInput = null;
        _ui.CloseUI<DialogueWindow>();
        if (GodotObject.IsInstanceValid(_previousFocus) && _previousFocus.IsInsideTree() && _previousFocus.IsVisibleInTree()) _previousFocus.GrabFocus();
        _previousFocus = null;
        if (session.Result is "NoAvailableChoices" or "InvalidGraph" or "ExecutionLimit" or "MemoryWriteRejected")
        { LastError = "对话未能继续：" + session.Result; GD.PushWarning(LastError); }
        Finished?.Invoke(session);
    }
    internal void PresentationClosed(DialogueSession session) { if (ReferenceEquals(Active, session)) Cancel(); }
    public JsonObject Capture()
    {
        var flags = new JsonObject(); foreach (var flag in _legacyFlags) flags[flag.Key] = flag.Value;
        return new() { ["flags"] = flags };
    }
    public void Restore(JsonObject state, int schemaVersion)
    {
        Cancel(); _legacyFlags.Clear(); if (schemaVersion != 1 || state?["flags"] is not JsonObject flags) return;
        foreach (var flag in flags) if (flag.Value is JsonValue value && value.TryGetValue<bool>(out bool enabled)) _legacyFlags[flag.Key] = enabled;
    }
    public override void Shutdown()
    {
        Cancel(); _save?.UnregisterSection(this); _legacyFlags.Clear(); Finished = null;
    }
}
