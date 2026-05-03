using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GameLogic;
using Godot;

/// <summary>
/// MissionGraph .tres 手动测试脚本。
///
/// 用法：
/// 1. 在测试场景里创建一个 Node，挂上本脚本。
/// 2. 在 Inspector 的 TestGraph 字段拖入一个 MissionGraph .tres。
///    默认测试资源可用 res://assets/graphs/missiongraph_runtime_smoke.tres。
/// 3. 运行场景。
///
/// 默认按键：
/// F5：重新启动 TestGraph。
/// A/B/C/D/E：向 MissionManager 发送对应 GameEventType。
/// F6：保存当前 MissionGraph/Mission 状态到内存快照和测试 JSON。
/// F7：优先从测试 JSON 读取，没有文件时从内存快照读取。
/// Delete：取消当前第一个 active mission，用来验证 continues=false 不推进 Sequence。
/// P：打印当前 active missions。
///
/// missiongraph_runtime_smoke.tres 的路径：
/// Entry -> MissionA
/// MissionA -- Sequence --> MissionB -> SubGraphD -> Finish
/// MissionA -- Parallel --> MissionC
/// MissionB 需要两次 B 事件，可用于测试 require 计数存档。
/// 子图 MissionD 由 D 事件完成。
/// </summary>
public partial class MissionGraphRuntimeSmokeTest : Node
{
    [Export]
    public MissionGraph TestGraph { get; set; }

    [Export]
    public string GraphPath { get; set; } = "MissionGraphRuntimeSmoke";

    [Export]
    public bool StartOnReady { get; set; } = true;

    [Export]
    public string SavePath { get; set; } = "res://saves/missiongraph_runtime_smoke.json";

    private MissionManager<object> _missionManager;
    private MissionChainManager _chainManager;
    private MissionChainSaver _chainSaver;
    private List<MissionRecord> _savedMissions = new();
    private List<MissionGraphRuntimeState> _savedChains = new();

    private static readonly JsonSerializerOptions SaveJsonOptions = new()
    {
        WriteIndented = true
    };

    public override void _Ready()
    {
        GraphTypeRegistry.AutoRegisterAll();
        CreateMissionSystem();

        if (StartOnReady)
            StartGraph();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;

        switch (key.Keycode)
        {
            case Key.F5:
                StartGraph();
                break;
            case Key.F6:
                SaveSnapshot();
                break;
            case Key.F7:
                LoadSnapshot();
                break;
            case Key.A:
                Send(GameEventType.A);
                break;
            case Key.B:
                Send(GameEventType.B);
                break;
            case Key.C:
                Send(GameEventType.C);
                break;
            case Key.D:
                Send(GameEventType.D);
                break;
            case Key.E:
                Send(GameEventType.E);
                break;
            case Key.Delete:
                CancelFirstMission();
                break;
            case Key.P:
                PrintActiveMissions();
                break;
        }
    }

    /// <summary>
    /// 启动 Inspector 中绑定的 MissionGraph .tres。
    /// </summary>
    public void StartGraph()
    {
        if (TestGraph == null)
        {
            GD.PushWarning("[MissionGraphRuntimeSmokeTest] TestGraph is empty. Drag a MissionGraph .tres into the inspector.");
            return;
        }

        CreateMissionSystem();

        string graphPath = string.IsNullOrWhiteSpace(GraphPath)
            ? TestGraph.graphName
            : GraphPath;

        MissionGraphRuntime runtime = _chainManager.StartChain(TestGraph, graphPath);
        GD.Print(runtime != null
            ? $"[MissionGraphRuntimeSmokeTest] Started graph: {graphPath}"
            : $"[MissionGraphRuntimeSmokeTest] Failed to start graph: {graphPath}");

        PrintActiveMissions();
    }

    /// <summary>
    /// 保存一份内存快照，同时写入测试 JSON。
    /// 用来快速验证 MissionChainSaver.Save/Load 的状态恢复路径。
    /// </summary>
    public void SaveSnapshot()
    {
        if (_chainSaver == null)
        {
            GD.Print("[MissionGraphRuntimeSmokeTest] Mission saver is not created.");
            return;
        }

        _chainSaver.Save();
        _savedMissions = CloneMissionRecords(_chainSaver.Missions);
        _savedChains = CloneRuntimeStates(_chainSaver.Chains);
        WriteSnapshotJson();

        GD.Print($"[MissionGraphRuntimeSmokeTest] Snapshot saved. Chains={_savedChains.Count}, Missions={_savedMissions.Count}, Path={SavePath}");
        PrintActiveMissions();
    }

    /// <summary>
    /// 从最近一次 F6 保存的测试 JSON 或内存快照读取。
    /// 读取前会故意复用当前 MissionManager，覆盖测试 Load 中清理旧任务的逻辑。
    /// </summary>
    public void LoadSnapshot()
    {
        if (_chainSaver == null)
            CreateMissionSystem();

        if (ReadSnapshotJson())
        {
            LoadSnapshotData();
            GD.Print($"[MissionGraphRuntimeSmokeTest] Snapshot loaded from json. Chains={_chainSaver.Chains.Count}, Missions={_chainSaver.Missions.Count}, Path={SavePath}");
            PrintActiveMissions();
            return;
        }

        if (_savedChains.Count == 0 && _savedMissions.Count == 0)
        {
            GD.Print("[MissionGraphRuntimeSmokeTest] No snapshot json or memory snapshot. Press F6 first.");
            return;
        }

        LoadSnapshotData();
        GD.Print($"[MissionGraphRuntimeSmokeTest] Snapshot loaded from memory. Chains={_chainSaver.Chains.Count}, Missions={_chainSaver.Missions.Count}");
        PrintActiveMissions();
    }

    private void LoadSnapshotData()
    {
        _chainSaver.Chains = CloneRuntimeStates(_savedChains);
        _chainSaver.Missions = CloneMissionRecords(_savedMissions);
        _chainSaver.Load();
    }

    /// <summary>
    /// 向 MissionManager 广播一个测试消息。
    /// MissionRequireTemplateWithCondition 可以用 A/B/C/D/E 这些事件来完成任务。
    /// </summary>
    public void Send(GameEventType eventType)
    {
        if (_missionManager == null)
            CreateMissionSystem();

        GD.Print($"[MissionGraphRuntimeSmokeTest] Send event: {eventType}");
        _missionManager.SendMessage(new GameMessage(eventType));
        PrintActiveMissions();
    }

    /// <summary>
    /// 取消当前第一个任务。
    /// 这会走 MissionChainManager.OnMissionRemoved(..., isFinished:false)，用于验证 NoOutput 语义。
    /// </summary>
    public void CancelFirstMission()
    {
        Mission<object> mission = _missionManager?.GetMissions().FirstOrDefault();
        if (mission == null)
        {
            GD.Print("[MissionGraphRuntimeSmokeTest] No active mission to cancel.");
            return;
        }

        GD.Print($"[MissionGraphRuntimeSmokeTest] Cancel mission: {mission.id}");
        _missionManager.RemoveMission(mission.id);
        PrintActiveMissions();
    }

    /// <summary>
    /// 打印当前 MissionManager 中的 active missions。
    /// </summary>
    public void PrintActiveMissions()
    {
        if (_missionManager == null)
        {
            GD.Print("[MissionGraphRuntimeSmokeTest] Mission system is not created.");
            return;
        }

        Mission<object>[] missions = _missionManager.GetMissions();
        string missionIds = missions.Length == 0
            ? "(none)"
            : string.Join(", ", missions.Select(mission => mission.id));

        GD.Print($"[MissionGraphRuntimeSmokeTest] Active missions: {missionIds}");
    }

    private void CreateMissionSystem()
    {
        _missionManager = new MissionManager<object>();
        _chainManager = new MissionChainManager(_missionManager);
        _missionManager.AddComponent(_chainManager);

        _chainSaver = new MissionChainSaver(_missionManager);
        _missionManager.AddComponent(_chainSaver);
    }

    private static List<MissionRecord> CloneMissionRecords(IEnumerable<MissionRecord> records)
    {
        return records?
            .Where(record => record != null)
            .Select(record => new MissionRecord
            {
                MissionId = record.MissionId,
                HandleStatuses = record.HandleStatuses?.ToList() ?? new List<string>()
            })
            .ToList() ?? new List<MissionRecord>();
    }

    private static List<MissionGraphRuntimeState> CloneRuntimeStates(IEnumerable<MissionGraphRuntimeState> states)
    {
        return states?
            .Where(state => state != null)
            .Select(CloneRuntimeState)
            .ToList() ?? new List<MissionGraphRuntimeState>();
    }

    private static MissionGraphRuntimeState CloneRuntimeState(MissionGraphRuntimeState state)
    {
        return new MissionGraphRuntimeState
        {
            GraphPath = state.GraphPath,
            GraphResourcePath = state.GraphResourcePath,
            ActiveMissionIds = state.ActiveMissionIds?.ToList() ?? new List<string>(),
            PendingSubGraphPaths = state.PendingSubGraphPaths?.ToList() ?? new List<string>(),
            ChildStates = CloneRuntimeStates(state.ChildStates)
        };
    }

    private void WriteSnapshotJson()
    {
        if (string.IsNullOrWhiteSpace(SavePath))
        {
            GD.PushWarning("[MissionGraphRuntimeSmokeTest] SavePath is empty.");
            return;
        }

        EnsureSaveDirectory();

        var data = new MissionSmokeSaveData
        {
            SaveKey = _chainSaver.SaveKey,
            Missions = CloneMissionRecords(_savedMissions),
            Chains = CloneRuntimeStates(_savedChains)
        };

        using Godot.FileAccess file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError($"[MissionGraphRuntimeSmokeTest] Failed to open save file: {SavePath}");
            return;
        }

        file.StoreString(JsonSerializer.Serialize(data, SaveJsonOptions));
    }

    private bool ReadSnapshotJson()
    {
        if (string.IsNullOrWhiteSpace(SavePath) || !Godot.FileAccess.FileExists(SavePath))
            return false;

        using Godot.FileAccess file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"[MissionGraphRuntimeSmokeTest] Failed to read save file: {SavePath}");
            return false;
        }

        MissionSmokeSaveData data = JsonSerializer.Deserialize<MissionSmokeSaveData>(file.GetAsText());
        if (data == null)
            return false;

        _savedMissions = CloneMissionRecords(data.Missions);
        _savedChains = CloneRuntimeStates(data.Chains);
        return true;
    }

    private void EnsureSaveDirectory()
    {
        string directory = SavePath.GetBaseDir();
        if (string.IsNullOrWhiteSpace(directory))
            return;

        string globalDirectory = ProjectSettings.GlobalizePath(directory);
        if (!System.IO.Directory.Exists(globalDirectory))
            System.IO.Directory.CreateDirectory(globalDirectory);
    }

    private sealed class MissionSmokeSaveData
    {
        public string SaveKey { get; set; } = "mission_system";
        public List<MissionRecord> Missions { get; set; } = new();
        public List<MissionGraphRuntimeState> Chains { get; set; } = new();
    }
}
