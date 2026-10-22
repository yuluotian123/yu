using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLogic.AI;

public partial class AiAgentComponent3D
{
    public string CurrentPlan => _selectedPlan?.Name ?? "";
    public IReadOnlyList<string> QueuedActions => _plan.Select(a => a.Action.Name).ToArray();
    public double ActionElapsed => _actionElapsed;
    public double ActionTimeout => _current?.Action.Timeout ?? 0;
    public double RetryRemaining => Math.Max(0, _retryAt - (World?.RuntimeSeconds ?? 0));
    public double GoalCooldown(string id) => Math.Max(0, _goalBlockedUntil.GetValueOrDefault(id) - (World?.RuntimeSeconds ?? 0));
    public string RuntimeStatus => Program == null || State == null ? "初始化失败"
        : !IsActive ? "组件停用" : !Running ? "AI 已暂停" : World == null ? "缺少 NPC 世界"
        : World.IsActive == false ? "NPC 世界已停用" : Owner.GetTree().Paused ? "游戏已暂停"
        : _pendingCheckpoint!=null ? "等待行动恢复"
        : _current != null ? Navigation?.Status == NpcNavigationStatus.Moving ? "移动中" : "执行行动"
        : _search != null ? "规划中" : _plan.Count > 0 ? "等待下一步"
        : RetryRemaining > 0 ? "等待重试" : _goal == null ? "待机" : "等待规划";
    public string IdleReason => RuntimeStatus switch
    {
        "初始化失败" => LastFailure.Length > 0 ? LastFailure : "角色数据或 AI 图未初始化。",
        "组件停用" => "AiAgent.IsActive 已关闭，控制器不会更新。",
        "AI 已暂停" => "AI 已暂停；恢复后重新选择目标。角色需求仍由世界时钟推进。",
        "缺少 NPC 世界" => "场景中没有已注册的 AiWorldComponent3D。",
        "NPC 世界已停用" => "世界组件已停用，GOAP 规划预算不会推进。",
        "游戏已暂停" => "游戏场景暂停中，NPC 控制器和物理移动不更新。",
        "待机" when Program.Goals.Count == 0 => "尚未配置启用目标；角色保持待机，可在 AI 图中添加目标与计划。",
        "待机" => "所有目标已完成、条件不满足、评分为零或处于失败冷却。请查看「目标与计划」。",
        "等待重试" => $"上次行动或规划失败，{RetryRemaining:0.0} 秒后重试。",
        "等待规划" when Navigation?.MapReady != true => "等待导航地图同步；需要可达地点的计划暂不能开始。",
        "等待行动恢复" => CheckpointStatus,
        "移动中" => IsAbstract ? "远处 NPC 使用抽象模拟。" : "沿导航路径移动。",
        "执行行动" => "正在执行行动子图；等待、交互和原地活动可以没有位移。",
        _ => "AI 正在推进当前目标。"
    };

    public void SetRunning(bool running)
    {
        Running = running;
        if (running) RequestReplan();
        else CancelPlan("Paused");
    }

    /// <summary>Explicit replan clears retry cooldowns so debug edits take effect immediately.</summary>
    public void RequestReplan()
    {
        CancelPlan();
        _goalBlockedUntil.Clear();
        _goal = null;
        _selectAt = _retryAt = _senseAt = 0;
        LastFailure = "";
    }
}
