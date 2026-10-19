#if TOOLS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace GameLogic.AI;

public sealed record NpcEditorSymbol(string Key, string Label, string Group, NpcValueType Type, bool Parameter = false, bool Writable = false);

/// <summary>Authoring symbols come from assembled modules, even while a goal is incomplete.</summary>
public static class NpcExpressionEditorCatalog
{
    public static IReadOnlyList<NpcEditorSymbol> Collect(NpcGraphAsset graph, bool actionContext = false, NpcActionDefinition action = null)
    {
        var symbols = new Dictionary<string, NpcEditorSymbol>(StringComparer.Ordinal);
        void Add(string key, string label, string group, NpcValueType type, bool parameter = false, bool writable = false)
        {
            if (!string.IsNullOrWhiteSpace(key) && type != NpcValueType.Unknown)
                symbols[(parameter ? "parameter:" : "fact:") + key] = new(key, label, group, type, parameter, writable);
        }
        foreach (var root in graph.Nodes.OfType<NpcRootNodeData>())
        foreach (var edge in graph.GetOutgoingConnections(root.Id, 0).Where(e => e.IsAvailable))
        {
            if (graph.FindNodeById(edge.ToNode) is not NpcDefinitionNodeData node ||
                graph.Resolve(node.DefinitionId) is not { Enabled: true } definition) continue;
            switch (definition)
            {
                case NpcIdentityDefinition identity:
                    foreach (var p in identity.Parameters.Where(p => p != null)) Add(p.Key, p.Key, "身份参数", p.ValueType, true);
                    foreach (var tag in identity.Tags) Add("tag." + tag, tag, "身份标签", NpcValueType.Boolean, true);
                    break;
                case NpcNeedsDefinition needs:
                    foreach (var n in needs.Needs.Where(n => n != null))
                    {
                        string label = string.IsNullOrWhiteSpace(n.DisplayName) ? n.NeedId : n.DisplayName;
                        Add($"needs.{n.NeedId}.value", label + " · 当前值", "需求", NpcValueType.Number, writable: true);
                        Add($"needs.{n.NeedId}.urgent", label + " · 是否紧急", "需求", NpcValueType.Boolean);
                        Add($"needs.{n.NeedId}.urgency", label + " · 紧迫度", "需求", NpcValueType.Number);
                    }
                    break;
                case NpcPersonalityDefinition personality:
                    foreach (var p in personality.Dimensions.Where(p => p != null)) Add("personality." + p.Key, p.Key, "性格", p.ValueType);
                    foreach (var p in personality.Preferences.Where(p => p != null)) Add("preference." + p.Key, p.Key, "偏好", p.ValueType);
                    foreach (var tag in personality.Traits) Add("trait." + tag, tag, "特质", NpcValueType.Boolean);
                    break;
                case NpcScheduleDefinition schedule:
                    foreach (var tag in schedule.Windows.Where(w => w != null).SelectMany(w => w.Tags)) Add("schedule." + tag, tag, "日程", NpcValueType.Boolean);
                    break;
                case NpcFactSchema schema:
                    foreach (var f in schema.Facts.Where(f => f != null)) Add(f.Key, f.Key, "私有事实", f.Type, writable: true);
                    break;
                case NpcAffectDefinition affect:
                    foreach (var p in affect.Dimensions.Where(p => p != null)) Add("affect." + p.Key, p.Key, "情绪", NpcValueType.Number);
                    break;
                case NpcRelationsDefinition relations:
                    foreach (var p in relations.Dimensions.Where(p => p != null)) Add("relations." + p.Key + ".max", p.Key + " · 最高值", "关系", NpcValueType.Number);
                    break;
                case NpcMemoryDefinition memory:
                    foreach (var category in memory.Categories) Add("memory." + category + ".count", category + " · 条数", "记忆", NpcValueType.Number);
                    break;
            }
        }
        Add("world.hour", "当前小时", "世界", NpcValueType.Number);
        Add("world.rain", "降雨量", "世界", NpcValueType.Number);
        Add("self.location", "当前位置 ID", "人物", NpcValueType.Symbol);
        if (actionContext)
        {
            Add("self.location", "当前位置（仅用于预测）", "人物", NpcValueType.Symbol, writable:true);
            Add("action.duration", "行动时长", "行动", NpcValueType.Number);
            foreach(var input in (action!=null?new[]{action}:graph.Definitions.OfType<NpcActionDefinition>()).SelectMany(a=>a.Inputs).Where(i=>i!=null))
            {
                string prefix="input."+input.Name;
                Add(prefix,input.Name,"行动输入",input.ValueType);
                if(NpcBindingAdapters.Find(input.Adapter) is {} adapter)foreach(var property in adapter.Properties)
                    Add(prefix+"."+property.Name,input.Name+" · "+property.Label,"行动输入",property.Type,writable:property.Writable);
            }
        }
        return symbols.Values.ToArray();
    }

    public static NpcActionDefinition ActionOwner(NpcGraphAsset graph,GodotObject target)=>graph.Definitions.OfType<NpcActionDefinition>().FirstOrDefault(a=>a==target||Contains(a,target));
    static bool Contains(Resource resource,GodotObject target)
    {
        var seen=new HashSet<Resource>();
        bool Visit(Resource current)
        {
            if(current==null||!seen.Add(current)||current is GraphAsset)return false;
            if(current==target)return true;
            foreach(var property in current.GetType().GetProperties().Where(p=>p.CanRead&&p.GetCustomAttribute<ExportAttribute>()!=null))
            {
                var value=property.GetValue(current);
                if(value is Resource child&&Visit(child)||value is IEnumerable items&&items.Cast<object>().OfType<Resource>().Any(Visit))return true;
            }
            return false;
        }
        return Visit(resource);
    }
    public static IReadOnlyList<NpcEditorSymbol> For(NpcGraphAsset graph,GodotObject owner)=>owner is NpcTargetSelectorDefinition selector?
        Collect(graph).Concat(NpcBindingAdapters.Find(selector.Adapter)?.Properties.Select(p=>new NpcEditorSymbol("target."+p.Name,"候选 · "+p.Label,"候选对象",p.Type))??Array.Empty<NpcEditorSymbol>()).ToArray():
        owner is NpcBindingDefinition?Collect(graph):Collect(graph,ActionOwner(graph,owner)!=null,ActionOwner(graph,owner));
    public static IReadOnlyList<NpcEditorSymbol> WritableTargets(NpcGraphAsset graph,GodotObject owner=null)=>
        (owner==null?Collect(graph,true):For(graph,owner)).Where(s=>s.Writable&&(s.Key!="self.location"||owner==null||owner is NpcEffectDefinition effect&&ActionOwner(graph,effect)?.Predictions.Contains(effect)==true)).ToArray();

    public static NpcValueType? ExpectedType(NpcGraphAsset graph, GodotObject target, string property) => (target, property) switch
    {
        (NpcGoalDefinition, nameof(NpcGoalDefinition.Activation) or nameof(NpcGoalDefinition.Completion)) => NpcValueType.Boolean,
        (NpcGoalDefinition, nameof(NpcGoalDefinition.Utility)) => NpcValueType.Number,
        (NpcActionDefinition, nameof(NpcActionDefinition.Preconditions) or nameof(NpcActionDefinition.SuccessConditions)) => NpcValueType.Boolean,
        (NpcActionDefinition, nameof(NpcActionDefinition.Cost)) => NpcValueType.Number,
        (NpcTargetSelectorDefinition, nameof(NpcTargetSelectorDefinition.Filter)) => NpcValueType.Boolean,
        (NpcTargetSelectorDefinition, nameof(NpcTargetSelectorDefinition.Score)) => NpcValueType.Number,
        (NpcPlanTemplateDefinition, nameof(NpcPlanTemplateDefinition.Eligibility)) => NpcValueType.Boolean,
        (NpcNeedDefinition, nameof(NpcNeedDefinition.RateMultiplier)) => NpcValueType.Number,
        (NpcEffectDefinition effect, nameof(NpcEffectDefinition.Value)) => effect.Mode == NpcEffectMode.Add ? NpcValueType.Number :
            WritableTargets(graph,effect).FirstOrDefault(s => s.Key == effect.Key)?.Type ?? NpcValueType.Number,
        (NpcBindingDefinition binding, nameof(NpcBindingDefinition.Value)) => binding.ValueType,
        _ => null
    };

    public static bool Owns(NpcGraphAsset graph, GodotObject target)
    {
        if (target is not (NpcDefinitionResource or NpcNeedDefinition or NpcEffectDefinition or NpcBindingDefinition or NpcResourceClaimDefinition)) return false;
        var visited = new HashSet<Resource>();
        bool Visit(Resource resource)
        {
            if (resource == null || !visited.Add(resource)) return false;
            if (resource == target) return true;
            if (resource is GraphAsset) return false;
            foreach (var p in resource.GetType().GetProperties().Where(p => p.CanRead && p.GetCustomAttribute<ExportAttribute>() != null))
            {
                var value = p.GetValue(resource);
                if (value is Resource child && Visit(child)) return true;
                if (value is IEnumerable items && items.Cast<object>().OfType<Resource>().Any(Visit)) return true;
            }
            return false;
        }
        return graph.Definitions.Any(Visit);
    }

    public static NpcValueType TypeOf(NpcExpressionDefinition expression, IReadOnlyList<NpcEditorSymbol> symbols)
    {
        if (expression == null) return NpcValueType.Unknown;
        if (expression.Kind == NpcExpressionKind.Literal) return expression.ValueType;
        if (expression.Kind is NpcExpressionKind.Fact or NpcExpressionKind.Parameter)
            return symbols.FirstOrDefault(s => s.Key == expression.Key && s.Parameter == (expression.Kind == NpcExpressionKind.Parameter))?.Type ?? NpcValueType.Unknown;
        return expression.Kind is NpcExpressionKind.Equal or NpcExpressionKind.NotEqual or NpcExpressionKind.Greater or NpcExpressionKind.AtLeast or
            NpcExpressionKind.Less or NpcExpressionKind.AtMost or NpcExpressionKind.All or NpcExpressionKind.Any or NpcExpressionKind.Not or NpcExpressionKind.Known
            ? NpcValueType.Boolean : NpcValueType.Number;
    }
}
#endif
