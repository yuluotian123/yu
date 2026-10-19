using System;
using System.Linq;
using System.Text;
using GameLogic.AI;
using Godot;

namespace GameLogic;

public partial class NpcGmComponent3D
{
    void RefreshOverview(AiAgentComponent3D a)
    {
        var nav = a.Navigation;
        var move = a.Owner.GetComponent<CharacterMovementComponent3D>();
        var animation = a.Owner.GetComponent<CharacterAnimationComponent3D>();
        Text("NpcDetails", $"{Name(a)} [{a.ActorId}]  ·  {a.RuntimeStatus}\n{a.IdleReason}\n\n"
            + $"当前目标：{Or(a.CurrentGoal, "无")}\n当前计划：{Or(a.CurrentPlan, "无")}\n当前行动：{Or(a.CurrentAction, "无")}"
            + (a.CurrentAction.Length > 0 ? $"  ·  {a.ActionElapsed:0.0} / {a.ActionTimeout:0.0} 秒超时" : "")
            + $"\n后续步骤：{Or(string.Join(" → ", a.QueuedActions), "无")}\n已完成行动：{a.CompletedActions}  ·  模拟：{(a.IsAbstract ? "远处抽象" : "近处详细")}\n\n"
            + $"导航：{(nav == null ? "缺少组件" : !nav.IsActive ? "组件停用" : !nav.MapReady ? "地图未就绪" : NavigationStatus(nav.Status))}  ·  目标：{Or(nav?.TargetId, "无")}\n"
            + $"世界坐标：{a.Owner.GlobalPosition}  ·  速度：{move?.Velocity.Length():0.00} m/s\n"
            + $"移动：{(move == null ? "缺少组件" : !move.IsActive ? "组件停用" : move.StartDisabled ? "已禁用" : move.IsOnFloor ? "在地面" : "在空中")}  ·  控制锁：{(move?.MovementLocked == true ? "已锁定" : "无")}  ·  当前动画：{animation?.ActiveAnimation}\n\n"
            + $"最近失败：{Failure(a.LastFailure)}\n导航最近失败：{Failure(nav?.Failure)}\n\n"
            + "计划绑定\n" + (a.PlanBindings.Count == 0 ? "无" : string.Join("\n", a.PlanBindings.Select(x => x.Key + " → " + x.Value)))
            + "\n\n需求摘要\n" + (a.State == null ? "未初始化" : string.Join("  ·  ", a.State.Needs.Values.Select(n => $"{NeedName(a, n.Key)} {n.Value:0.00}"))));
    }
    static string Or(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;
    static string TraceLine(string line)
    {
        var parts = line.Split(' ', 3);
        if (parts.Length < 2) return line;
        string kind = parts[1] switch { "Initialized" => "初始化", "Goal" => "选择目标", "Plan" => "生成计划", "Action" => "开始行动", "Completed" => "行动完成", "Cancelled" => "取消计划", _ => parts[1] };
        string detail = parts.Length < 3 ? "" : parts[2] switch
        {
            "GoalChanged" => "目标变更", "Replan" => "手动重规划", "Paused" => "暂停 AI", "Stopped" => "AI 停止",
            "LodChanged" => "切换详细/抽象模拟", "StateChanged" => "角色数据改变", "CharacterRestored" => "角色数据已恢复", "Restored" => "AI 存档已恢复", _ => Failure(parts[2])
        };
        return parts[0] + "s  " + kind + "  " + detail;
    }
    static string NeedName(AiAgentComponent3D a, string id) => a.Character?.Profile?.Needs?.Needs.FirstOrDefault(n => n.NeedId == id)?.DisplayName is { Length: > 0 } name ? name : id;
    static string NavigationStatus(NpcNavigationStatus status) => status switch
    {
        NpcNavigationStatus.Idle => "空闲", NpcNavigationStatus.Moving => "移动中", NpcNavigationStatus.Arrived => "已到达", _ => "失败"
    };
    static string Failure(string reason) => reason switch
    {
        null or "" => "无",
        "NoPlan" => "找不到合法计划（NoPlan）",
        "InputUnavailable" or "InputChanged" => "绑定对象已不可用或不符合选择条件（" + reason + "）",
        "ResourceBusy" => "交互点容量已满（ResourceBusy）",
        "PreconditionChanged" => "行动前提已改变（PreconditionChanged）",
        "PlanEligibilityChanged" => "计划适用条件已改变，如天气变化（PlanEligibilityChanged）",
        "NavigationUnavailable" => "导航地图不可用（NavigationUnavailable）",
        "TargetUnreachable" => "目标不可达（TargetUnreachable）",
        "NavigationStuck" => "移动卡住（NavigationStuck）",
        "Timeout" => "行动超时（Timeout）",
        _ => reason
    };
    static string Expression(AiAgentComponent3D a, CharacterExpression e, int depth = 0)
    {
        if (e == null) return "始终满足";
        if (depth > 12) return "…";
        if (e.Kind == CharacterExpressionKind.Literal) return e.Literal.ToString();
        if (e.Kind is CharacterExpressionKind.Fact or CharacterExpressionKind.Parameter)
        {
            string key = e.Key;
            foreach (var need in a.Program.Needs) key = key.Replace("needs." + need.Id + ".value", NeedName(a, need.Id), StringComparison.Ordinal);
            return $"{key}({(e.Kind == CharacterExpressionKind.Parameter ? a.Program.Parameter(e.Key) : a.Read(e.Key))})";
        }
        string op = e.Kind switch
        {
            CharacterExpressionKind.Greater => ">", CharacterExpressionKind.AtLeast => "≥", CharacterExpressionKind.Less => "<", CharacterExpressionKind.AtMost => "≤",
            CharacterExpressionKind.Equal => "=", CharacterExpressionKind.NotEqual => "≠", CharacterExpressionKind.Multiply => "×", CharacterExpressionKind.Add => "+",
            CharacterExpressionKind.Subtract => "−", CharacterExpressionKind.Divide => "÷", CharacterExpressionKind.All => "且", CharacterExpressionKind.Any => "或", _ => e.Kind.ToString()
        };
        var inputs = e.Inputs.Select(x => Expression(a, x, depth + 1));
        return e.Inputs.Count == 1 ? op + "(" + string.Join(", ", inputs) + ")" : "(" + string.Join(" " + op + " ", inputs) + ")";
    }
    static string Goals(AiAgentComponent3D a)
    {
        if (a.Program == null) return a.IdleReason;
        var text = new StringBuilder("评分最高的可选目标会被选中；承诺时间和切换差值可暂时保留当前目标。\n\n");
        foreach (var goal in a.Program.Goals)
        {
            bool active = goal.Activation?.Evaluate(a.Read, a.Program.Parameter).IsTrue ?? true;
            bool done = goal.Completion.Evaluate(a.Read, a.Program.Parameter).IsTrue;
            double cooldown = a.GoalCooldown(goal.Id);
            var utility = goal.Utility.Evaluate(a.Read, a.Program.Parameter);
            string status = done ? "已完成" : !active ? "条件不满足" : cooldown > 0 ? $"失败冷却 {cooldown:0.0}s" : utility.Type != CharacterValueType.Number ? "评分未知" : utility.Number <= 0 ? "评分为零" : "可选";
            text.AppendLine($"{goal.Name}  ·  {status}{(a.CurrentGoal == goal.Name ? "  ← 当前目标" : "")}  ·  原始评分 {utility}");
            text.AppendLine("  启动：" + Expression(a, goal.Activation));
            text.AppendLine("  完成：" + Expression(a, goal.Completion));
            foreach (var plan in goal.Plans)
            {
                bool eligible = plan.Eligibility?.Evaluate(a.Read, a.Program.Parameter).IsTrue ?? true;
                text.AppendLine($"  计划「{plan.Name}」 · {(eligible ? "条件满足" : "条件不满足")} · 优先级 {plan.Priority}");
                text.AppendLine("    条件：" + Expression(a, plan.Eligibility));
                text.AppendLine("    可用行动：" + string.Join(" / ", plan.ActionIds.Select(id => goal.Actions.FirstOrDefault(x => x.Id == id)?.Name ?? id)));
            }
            text.AppendLine();
        }
        return text.ToString();
    }
    static string State(AiAgentComponent3D a)
    {
        if (a.State == null || a.Program == null) return a.IdleReason;
        var text = new StringBuilder("需求随游戏时间增长；时钟暂停会停止增长。高数值不一定代表紧迫，方向和阈值由配置决定。\n\n");
        foreach (var n in a.State.Profile.Needs)
            text.AppendLine($"{NeedName(a, n.Id)} [{n.Id}]：{a.State.Needs.Values[n.Id]:0.000}  ·  范围 {n.Min:0.##}～{n.Max:0.##}\n  初值 {n.Initial:0.##}  ·  基础变化 {n.Rate:+0.###;-0.###;0}/游戏小时  ·  {(a.Read("needs." + n.Id + ".urgent").IsTrue ? "紧迫" : "不紧迫")}\n  {(n.Higher ? "越高越紧迫" : "越低越紧迫")}  ·  进入 {n.Enter:0.##} / 退出 {n.Exit:0.##}");
        text.AppendLine("\n当前位置：" + Or(a.State.Location, "未处于交互点"));
        string Feature(string id) => a.State.Profile.FeatureDefinitions.TryGetValue(id, out var d) ? d.Name + " [" + id + "]" : id;
        text.AppendLine("\n性格\n" + string.Join("\n", a.State.Profile.Personality.Select(x => Feature(x.Key) + " = " + x.Value)));
        text.AppendLine("\n偏好\n" + string.Join("\n", a.State.Profile.Preferences.Select(x => Feature(x.Key) + " = " + x.Value)));
        text.AppendLine("\n特质：" + Or(string.Join("、", a.State.Profile.Traits.Select(Feature)), "无"));
        text.AppendLine("\n情绪\n" + string.Join("\n", a.State.Affect.Values.Select(x => Feature(x.Key) + " = " + x.Value.ToString("0.###"))));
        text.AppendLine("\n业务事实\n" + string.Join("\n", a.State.PrivateFacts.Select(x => x.Key + " = " + x.Value)));
        text.AppendLine("\n参数\n" + string.Join("\n", a.Program.Parameters.Select(x => x.Key + " = " + x.Value)));
        text.AppendLine($"\n关系联系人：{a.State.Relations.Contacts.Count}");
        foreach (var contact in a.State.Relations.Contacts) text.AppendLine(contact.Key + "：" + string.Join("，", contact.Value.Select(x => Feature(x.Key) + " = " + x.Value.ToString("0.###"))));
        text.AppendLine($"\nAI 记忆：{a.Decision?.Memory.Entries.Count}");
        if (a.Decision != null) foreach (var entry in a.Decision.Memory.Entries.TakeLast(8)) text.AppendLine($"{entry.Hours:0.00}h · {entry.Category} · {entry.Subject} · {entry.Detail}");
        return text.ToString();
    }
    string Points(AiAgentComponent3D a)
    {
        if (World == null) return "无 NPC 世界。";
        var text = new StringBuilder("开放、容量、库存、归属和可达性均会影响计划；开放不代表该 NPC 有权使用。\n\n");
        foreach (var p in World.Points.Values.OrderBy(p => p.PointId, StringComparer.Ordinal))
        {
            double distance = 0;
            bool reachable = a.Navigation?.TryEstimateBetween(a.Owner.GlobalPosition, p.UsePosition, out distance) == true;
            text.AppendLine($"{(Point == p ? "▶ " : "")}{Or(p.DisplayName, p.PointId)} [{p.PointId}]\n  {(p.IsOpen ? "开放" : "关闭")} · {(p.ManualOpen.HasValue ? "手动覆盖" : "按日程")} · 容量 {p.Reservations}/{p.Capacity} · 库存 {p.Stock:0.##}");
            text.AppendLine($"  此 NPC：{(p.CanUse(a.ActorId) ? "有权使用" : "不可使用")} · {(p.CanReserve(a.ActorId) ? "可预订" : "不可预订")} · {(a.Navigation?.MapReady != true ? "导航未就绪" : reachable ? $"可达 {distance:0.0}m" : "不可达")}");
            text.AppendLine($"  {(a.Program?.KnownPoints.Contains(p.PointId) == true ? "已知" : "未知")} · {(a.PerceivedPoint(p.PointId) ? "感知到" : "未感知")} · 标签 {string.Join(", ", p.Tags)}\n");
        }
        return text.ToString();
    }
}
