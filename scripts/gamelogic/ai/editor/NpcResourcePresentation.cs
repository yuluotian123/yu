#if TOOLS
using System;
using System.Linq;
using Godot;
namespace GameLogic.AI;

/// <summary>Human-facing labels for native NPC resource navigation, without changing resource names or IDs.</summary>
public static class NpcResourcePresentation
{
    public static string Title(NpcGraphAsset graph,Resource resource)=>resource switch
    {
        NpcDefinitionResource definition=>string.IsNullOrWhiteSpace(definition.DisplayName)?definition.Category:definition.DisplayName,
        NpcNeedDefinition need=>string.IsNullOrWhiteSpace(need.DisplayName)?need.NeedId:need.DisplayName,
        NpcActionInputDefinition input=>input.Name+" · 行动输入",
        NpcBindingDefinition binding=>binding.Name+" · 计划变量",
        NpcEffectDefinition effect=>"修改 · "+(NpcExpressionEditorCatalog.WritableTargets(graph,effect).FirstOrDefault(s=>s.Key==effect.Key)?.Label??effect.Key),
        NpcScalarDefinition scalar=>scalar.Key,
        NpcScheduleWindow window=>window.WindowId+" · 日程时段",
        NpcFactDefinition fact=>fact.Key,
        NpcExpressionDefinition=>"公式",
        _=>string.IsNullOrWhiteSpace(resource.ResourceName)?"配置项":resource.ResourceName
    };
    public static string Description(NpcGraphAsset graph,Resource resource)=>resource switch
    {
        NpcIdentityDefinition identity=>$"身份与表现 · {identity.Parameters.Count} 个参数 · {identity.Tags.Length} 个标签",
        NpcNeedsDefinition needs=>$"需求 · {needs.Needs.Count} 项 · 点击条目编辑初值、变化率与阈值",
        NpcPersonalityDefinition personality=>$"性格 · {personality.Dimensions.Count} 个维度 · {personality.Preferences.Count} 个偏好",
        NpcGoalDefinition=>"目标 · 启用条件、评分、完成条件；通过连线配置候选行动与计划",
        NpcActionDefinition action=>$"行动 · {action.Inputs.Count} 个输入 · {action.Predictions.Count} 个预测效果 · {action.Results.Count} 个结果效果",
        NpcPlanTemplateDefinition plan=>$"计划 · {plan.ActionIds.Length} 个候选行动 · {plan.Variables.Count} 个计划变量",
        NpcTargetSelectorDefinition=>"目标选择器 · 候选来源、筛选条件与选择策略",
        NpcNeedDefinition need=>$"需求 · 范围 {need.Minimum:G}～{need.Maximum:G} · 初值 {need.Initial:G} · 每小时变化 {need.ChangePerHour:G}",
        NpcActionInputDefinition input=>$"行动输入 · {TypeName(input)} · {Source(input)} · {input.ResourceRequirements.Length} 项资源要求",
        NpcBindingDefinition binding=>$"计划变量 · {TypeName(binding)} · {Source(binding)}",
        NpcEffectDefinition effect=>(effect.Mode==NpcEffectMode.Add?"增加/减少：":"设为：")+NpcExpressionPresentation.Format(effect.Value,NpcExpressionEditorCatalog.For(graph,effect)),
        NpcDefinitionResource definition=>definition.Category+" · 修改随人物图保存",
        _=>"配置项 · 修改随人物图保存"
    };
    static string TypeName(NpcBindingDefinition binding)=>NpcBindingAdapters.Find(binding.Adapter)?.Label??binding.ValueType switch
    {NpcValueType.Boolean=>"布尔",NpcValueType.Number=>"数字",NpcValueType.Symbol=>"文本",_=>"未设置类型"};
    static string Source(NpcBindingDefinition binding)=>binding.Source switch
    {NpcBindingSource.Expression=>"表达式或常量",NpcBindingSource.Selector=>"选择器候选",_=>"引用计划变量“"+binding.Variable+"”"};
}
#endif
