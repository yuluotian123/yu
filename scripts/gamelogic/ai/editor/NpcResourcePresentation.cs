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
        CharacterNeedDefinition need=>string.IsNullOrWhiteSpace(need.DisplayName)?need.NeedId:need.DisplayName,
        CharacterFeatureDefinition feature=>string.IsNullOrWhiteSpace(feature.DisplayName)?feature.Category:feature.DisplayName,
        CharacterFeatureValue value=>graph.CharacterContext?.ResolveFeatureLibrary()?.Find(value.DefinitionId)?.DisplayName??"特征引用已失效",
        NpcActionInputDefinition input=>input.Name+" · 行动输入",
        NpcEffectDefinition effect=>"修改 · "+(NpcExpressionEditorCatalog.WritableTargets(graph,effect).FirstOrDefault(s=>s.Key==effect.Key)?.Label??effect.Key),
        CharacterScalarDefinition scalar=>scalar.Key,
        NpcScheduleWindow window=>window.WindowId+" · 日程时段",
        CharacterFactDefinition fact=>fact.Key,
        CharacterExpressionDefinition=>"公式",
        _=>string.IsNullOrWhiteSpace(resource.ResourceName)?"配置项":resource.ResourceName
    };
    public static string Description(NpcGraphAsset graph,Resource resource)=>resource switch
    {
        CharacterIdentityDefinition identity=>$"身份与表现 · {identity.Parameters.Count} 个参数 · {identity.Tags.Length} 个标签",
        CharacterNeedsDefinition needs=>$"需求 · {needs.Needs.Count} 项 · 点击条目编辑基础数值与紧迫度规则",
        CharacterPersonalityDefinition personality=>$"性格 · {personality.Dimensions.Count} 项覆盖 · {personality.Traits.Length} 项特质 · {personality.Preferences.Count} 项偏好覆盖",
        NpcGoalDefinition=>"目标 · 启用条件、评分、完成条件；通过连线配置候选行动与计划",
        NpcActionDefinition action=>$"行动 · {action.Inputs.Count} 个输入 · {action.Predictions.Count} 个预测效果 · {action.Results.Count} 个结果效果",
        NpcPlanTemplateDefinition plan=>$"计划 · {plan.ActionIds.Length} 个候选行动 · 适用条件与优先级",
        NpcTargetSelectorDefinition=>"目标选择器 · 按世界对象类型、能力、操作及条件筛选；同一节点在本次计划中共享选定目标",
        CharacterNeedDefinition need=>$"需求 · 每小时变化 {need.ChangePerHour:G} · 进入 {(need.HigherIsUrgent?"≥":"≤")} {need.UrgencyEnter:G} · 解除 {(need.HigherIsUrgent?"≤":"≥")} {need.UrgencyExit:G} · 曲线指数 {need.UrgencyExponent:G}",
        NpcActionInputDefinition input=>$"行动输入 · {TypeName(input)} · {(input.IsTarget?"选择器连线":"表达式或常量")} · {input.ResourceRequirements.Length} 项资源要求",
        NpcEffectDefinition effect=>(effect.Mode==NpcEffectMode.Add?"增加/减少：":"设为：")+NpcExpressionPresentation.Format(effect.Value,NpcExpressionEditorCatalog.For(graph,effect)),
        NpcDefinitionResource definition=>definition.Category+" · 修改随人物图保存",
        _=>"配置项 · 修改随人物图保存"
    };
    static string TypeName(NpcActionInputDefinition binding)=>NpcBindingAdapters.Find(binding.Adapter)?.Label??binding.ValueType switch
    {CharacterValueType.Boolean=>"布尔",CharacterValueType.Number=>"数字",CharacterValueType.Symbol=>"文本",_=>"未设置类型"};
}
#endif
