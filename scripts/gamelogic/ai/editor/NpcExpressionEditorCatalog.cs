#if TOOLS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace GameLogic.AI;

public sealed record NpcEditorSymbol(string Key, string Label, string Group, CharacterValueType Type, bool Parameter = false, bool Writable = false);

/// <summary>Authoring symbols come from assembled modules, even while a goal is incomplete.</summary>
public static class NpcExpressionEditorCatalog
{
    public static IReadOnlyList<NpcEditorSymbol> Collect(NpcGraphAsset graph, bool actionContext = false, NpcActionDefinition action = null)
    {
        var symbols = new Dictionary<string, NpcEditorSymbol>(StringComparer.Ordinal);
        void Add(string key, string label, string group, CharacterValueType type, bool parameter = false, bool writable = false)
        {
            if (!string.IsNullOrWhiteSpace(key) && type != CharacterValueType.Unknown)
                symbols[(parameter ? "parameter:" : "fact:") + key] = new(key, label, group, type, parameter, writable);
        }
        var definitions=new List<CharacterDefinitionResource>();
        var features=graph.CharacterContext?.ResolveFeatureLibrary()?.Definitions.Where(d=>d is {Enabled:true}).ToArray()??Array.Empty<CharacterFeatureDefinition>();
        string FeatureName(CharacterFeatureDefinition definition)=>string.IsNullOrWhiteSpace(definition.DisplayName)?definition.Category:definition.DisplayName;
        if(graph.CharacterContext is {} profile)definitions.AddRange(new CharacterDefinitionResource[]{profile.Identity,profile.Needs,profile.Personality,profile.Relations,profile.Affect,profile.Facts}.Where(d=>d is {Enabled:true}));
        foreach(var root in graph.Nodes.OfType<NpcRootNodeData>())foreach(var edge in graph.GetOutgoingConnections(root.Id,0).Where(e=>e.IsAvailable))
            if(graph.FindNodeById(edge.ToNode) is NpcDefinitionNodeData node&&graph.Resolve(node.DefinitionId) is {Enabled:true} d)definitions.Add(d);
        foreach(var definition in definitions)
        {
            switch (definition)
            {
                case CharacterIdentityDefinition identity:
                    foreach (var p in identity.Parameters.Where(p => p != null)) Add(p.Key, p.Key, "角色参数", p.ValueType, true);
                    foreach (var tag in identity.Tags) Add("tag." + tag, tag, "角色标签", CharacterValueType.Boolean, true);
                    break;
                case CharacterNeedsDefinition needs:
                    foreach (var n in needs.Needs.Where(n => n != null))
                    {
                        string label = string.IsNullOrWhiteSpace(n.DisplayName) ? n.NeedId : n.DisplayName;
                        Add($"needs.{n.NeedId}.value", label + " · 当前值", "需求", CharacterValueType.Number, writable: true);
                        if(graph.IncludeDecisionSymbols)Add($"needs.{n.NeedId}.urgent", label + " · 是否紧急", "需求", CharacterValueType.Boolean);
                        if(graph.IncludeDecisionSymbols)Add($"needs.{n.NeedId}.urgency", label + " · 紧迫度", "需求", CharacterValueType.Number);
                    }
                    break;
                case CharacterPersonalityDefinition personality:
                    foreach (var p in features.Where(d=>d.Kind==CharacterFeatureKind.Personality)) Add("personality." + p.DefinitionId, FeatureName(p), "性格", CharacterValueType.Number);
                    foreach (var p in features.Where(d=>d.Kind==CharacterFeatureKind.Preference)) Add("preference." + p.DefinitionId, FeatureName(p), "偏好", CharacterValueType.Number);
                    foreach (var p in features.Where(d=>d.Kind==CharacterFeatureKind.Trait)) Add("trait." + p.DefinitionId, FeatureName(p), "特质", CharacterValueType.Boolean);
                    break;
                case NpcScheduleDefinition schedule:
                    foreach (var tag in schedule.Windows.Where(w => w != null).SelectMany(w => w.Tags)) Add("schedule." + tag, tag, "日程", CharacterValueType.Boolean);
                    break;
                case CharacterFactSchema schema:
                    foreach (var f in schema.Facts.Where(f => f != null)) Add(f.Key, f.Key, "私有事实", f.Type, writable: true);
                    break;
                case CharacterAffectDefinition affect:
                    foreach (var p in features.Where(d=>d.Kind==CharacterFeatureKind.Affect)) Add("affect." + p.DefinitionId, FeatureName(p), "情绪", CharacterValueType.Number);
                    break;
                case CharacterRelationsDefinition relations:
                    foreach (var p in features.Where(d=>d.Kind==CharacterFeatureKind.Relation)) Add("relations." + p.DefinitionId + ".max", FeatureName(p) + " · 最高值", "关系", CharacterValueType.Number);
                    break;
                case NpcMemoryDefinition memory:
                    foreach (var category in memory.Categories) Add("memory." + category + ".count", category + " · 条数", "记忆", CharacterValueType.Number);
                    break;
            }
        }
        Add("world.hour", "当前小时", "世界", CharacterValueType.Number);
        Add("world.rain", "降雨量", "世界", CharacterValueType.Number);
        Add("self.location", "当前位置 ID", "人物", CharacterValueType.Symbol);
        if (actionContext)
        {
            Add("self.location", "当前位置（仅用于预测）", "人物", CharacterValueType.Symbol, writable:true);
            Add("action.duration", "行动时长", "行动", CharacterValueType.Number);
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
        owner is NpcActionInputDefinition?Collect(graph):Collect(graph,ActionOwner(graph,owner)!=null,ActionOwner(graph,owner));
    public static IReadOnlyList<NpcEditorSymbol> WritableTargets(NpcGraphAsset graph,GodotObject owner=null)=>
        (owner==null?Collect(graph,true):For(graph,owner)).Where(s=>s.Writable&&(s.Key!="self.location"||owner==null||owner is NpcEffectDefinition effect&&ActionOwner(graph,effect)?.Predictions.Contains(effect)==true)).ToArray();

    public static CharacterValueType? ExpectedType(NpcGraphAsset graph, GodotObject target, string property) => (target, property) switch
    {
        (NpcGoalDefinition, nameof(NpcGoalDefinition.Activation) or nameof(NpcGoalDefinition.Completion)) => CharacterValueType.Boolean,
        (NpcGoalDefinition, nameof(NpcGoalDefinition.Utility)) => CharacterValueType.Number,
        (NpcActionDefinition, nameof(NpcActionDefinition.Preconditions) or nameof(NpcActionDefinition.SuccessConditions)) => CharacterValueType.Boolean,
        (NpcActionDefinition, nameof(NpcActionDefinition.Cost)) => CharacterValueType.Number,
        (NpcTargetSelectorDefinition, nameof(NpcTargetSelectorDefinition.Filter)) => CharacterValueType.Boolean,
        (NpcTargetSelectorDefinition, nameof(NpcTargetSelectorDefinition.Score)) => CharacterValueType.Number,
        (NpcPlanTemplateDefinition, nameof(NpcPlanTemplateDefinition.Eligibility)) => CharacterValueType.Boolean,
        (CharacterNeedDefinition, nameof(CharacterNeedDefinition.RateMultiplier)) => CharacterValueType.Number,
        (NpcEffectDefinition effect, nameof(NpcEffectDefinition.Value)) => effect.Mode == NpcEffectMode.Add ? CharacterValueType.Number :
            WritableTargets(graph,effect).FirstOrDefault(s => s.Key == effect.Key)?.Type ?? CharacterValueType.Number,
        (NpcActionInputDefinition input, nameof(NpcActionInputDefinition.Value)) => input.IsTarget?null:input.ValueType,
        _ => null
    };

    public static bool Owns(NpcGraphAsset graph, GodotObject target)
    {
        if (target is not (CharacterDefinitionResource or CharacterNeedDefinition or NpcEffectDefinition or NpcActionInputDefinition)) return false;
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

    public static CharacterValueType TypeOf(CharacterExpressionDefinition expression, IReadOnlyList<NpcEditorSymbol> symbols)
    {
        if (expression == null) return CharacterValueType.Unknown;
        if (expression.Kind == CharacterExpressionKind.Literal) return expression.ValueType;
        if (expression.Kind is CharacterExpressionKind.Fact or CharacterExpressionKind.Parameter)
            return symbols.FirstOrDefault(s => s.Key == expression.Key && s.Parameter == (expression.Kind == CharacterExpressionKind.Parameter))?.Type ?? CharacterValueType.Unknown;
        return expression.Kind is CharacterExpressionKind.Equal or CharacterExpressionKind.NotEqual or CharacterExpressionKind.Greater or CharacterExpressionKind.AtLeast or
            CharacterExpressionKind.Less or CharacterExpressionKind.AtMost or CharacterExpressionKind.All or CharacterExpressionKind.Any or CharacterExpressionKind.Not or CharacterExpressionKind.Known
            ? CharacterValueType.Boolean : CharacterValueType.Number;
    }
}
#endif
