using System;
using System.Collections.Generic;
using System.Linq;
namespace GameLogic;

public sealed record CharacterNeedSpec(string Id,double Min,double Max,double Initial,double Rate,CharacterExpression Multiplier);
public sealed record CharacterFactSpec(string Key,CharacterValueType Type,double Minimum,double Maximum,double Step,CharacterValue Initial,bool Writable);

/// <summary>Frozen character schema, independent of graphs, controllers and the planner.</summary>
public sealed class CharacterProfileSpec
{
    public readonly Dictionary<string,CharacterValue> Parameters=new(StringComparer.Ordinal),Personality=new(StringComparer.Ordinal),Preferences=new(StringComparer.Ordinal),Affect=new(StringComparer.Ordinal),RelationDefaults=new(StringComparer.Ordinal);
    public readonly Dictionary<string,CharacterFactSpec> Facts=new(StringComparer.Ordinal);
    public readonly List<CharacterNeedSpec> Needs=new();
    public readonly HashSet<string> Traits=new(StringComparer.Ordinal),KnownKeys=new(StringComparer.Ordinal);
    public double AffectRecovery {get;private set;}=.1;
    public int MaximumContacts {get;private set;}=64;
    public double RelationMin {get;private set;}=-1;
    public double RelationMax {get;private set;}=1;
    public static void Require(bool condition,string message){if(!condition)throw new ArgumentException(message);}
    public CharacterValue Parameter(string key)=>Parameters.GetValueOrDefault(key);
    public static CharacterProfileSpec Compile(CharacterProfile profile)
    {
        Require(profile!=null,"缺少通用角色配置。");var p=new CharacterProfileSpec();
        static void Fill(Dictionary<string,CharacterValue> target,IEnumerable<CharacterScalarDefinition> source)
        {
            foreach(var x in source){Require(x!=null&&!string.IsNullOrWhiteSpace(x.Key)&&!target.ContainsKey(x.Key)&&x.Value.Type!=CharacterValueType.Unknown&&(x.Value.Type!=CharacterValueType.Number||double.IsFinite(x.Number)),"角色参数无效或重复。");target.Add(x.Key,x.Value);}
        }
        if(profile.Identity is {Enabled:true} identity){Fill(p.Parameters,identity.Parameters);foreach(string tag in identity.Tags)Require(p.Parameters.TryAdd("tag."+tag,CharacterValue.Bool(true)),"角色标签重复。");}
        if(profile.Personality is {Enabled:true} personality){Fill(p.Personality,personality.Dimensions);Fill(p.Preferences,personality.Preferences);p.Traits.UnionWith(personality.Traits);}
        if(profile.Needs is {Enabled:true} needs)foreach(var n in needs.Needs)
        {
            Require(n!=null&&!string.IsNullOrWhiteSpace(n.NeedId)&&!p.Needs.Any(v=>v.Id==n.NeedId)&&double.IsFinite(n.Minimum)&&double.IsFinite(n.Maximum)&&n.Minimum<n.Maximum&&double.IsFinite(n.Initial)&&n.Initial>=n.Minimum&&n.Initial<=n.Maximum&&double.IsFinite(n.ChangePerHour),"角色需求范围、ID 或变化率无效。");
            p.Needs.Add(new(n.NeedId,n.Minimum,n.Maximum,n.Initial,n.ChangePerHour,n.RateMultiplier?.Freeze()));
            p.AddFact(new("needs."+n.NeedId+".value",CharacterValueType.Number,n.Minimum,n.Maximum,(n.Maximum-n.Minimum)/20,CharacterValue.Numeric(n.Initial),true));
        }
        if(profile.Facts is {Enabled:true} facts)foreach(var f in facts.Facts)
        {Require(f!=null&&f.Key.StartsWith("private.",StringComparison.Ordinal),"角色自定义事实必须以 private. 开头。");p.AddFact(new(f.Key,f.Type,f.Minimum,f.Maximum,f.Step,f.Initial?.Value??default,true));}
        if(profile.Relations is {Enabled:true} relations)
        {
            Fill(p.RelationDefaults,relations.Dimensions);p.MaximumContacts=relations.MaximumContacts;p.RelationMin=relations.Minimum;p.RelationMax=relations.Maximum;
            Require(p.MaximumContacts>0&&double.IsFinite(p.RelationMin)&&double.IsFinite(p.RelationMax)&&p.RelationMin<p.RelationMax&&p.RelationDefaults.Values.All(v=>v.Type==CharacterValueType.Number&&v.Number>=p.RelationMin&&v.Number<=p.RelationMax),"角色关系配置无效。");
        }
        if(profile.Affect is {Enabled:true} affect){Fill(p.Affect,affect.Dimensions);p.AffectRecovery=affect.RecoveryPerHour;Require(double.IsFinite(p.AffectRecovery)&&p.AffectRecovery>=0&&p.Affect.Values.All(v=>v.Type==CharacterValueType.Number),"角色情绪配置无效。");}
        foreach(string key in p.Personality.Keys)p.KnownKeys.Add("personality."+key);
        foreach(string key in p.Preferences.Keys)p.KnownKeys.Add("preference."+key);
        foreach(string key in p.Traits)p.KnownKeys.Add("trait."+key);
        foreach(string key in p.Affect.Keys)p.KnownKeys.Add("affect."+key);
        foreach(string key in p.RelationDefaults.Keys)p.KnownKeys.Add("relations."+key+".max");
        foreach(string key in new[]{"world.hour","world.rain","self.location"})p.KnownKeys.Add(key);
        foreach(var n in p.Needs)if(n.Multiplier!=null)
            Require(p.Infer(n.Multiplier)==CharacterValueType.Number,"角色需求变化倍率必须返回数值。");
        return p;
    }
    void AddFact(CharacterFactSpec spec)
    {
        Require(!Facts.ContainsKey(spec.Key)&&Enum.IsDefined(spec.Type)&&double.IsFinite(spec.Minimum)&&double.IsFinite(spec.Maximum)&&double.IsFinite(spec.Step)&&spec.Step>0&&spec.Minimum<=spec.Maximum&&Math.Abs(spec.Minimum/spec.Step)<long.MaxValue/2d&&Math.Abs(spec.Maximum/spec.Step)<long.MaxValue/2d,"角色事实范围无效或重复："+spec.Key);
        Require(spec.Initial.Type==CharacterValueType.Unknown||spec.Initial.Type==spec.Type&&(spec.Type!=CharacterValueType.Number||double.IsFinite(spec.Initial.Number)&&spec.Initial.Number>=spec.Minimum&&spec.Initial.Number<=spec.Maximum),"角色事实初值类型或范围无效。");
        Facts.Add(spec.Key,spec);KnownKeys.Add(spec.Key);
    }
    CharacterValueType Infer(CharacterExpression e)
    {
        if(e.Kind==CharacterExpressionKind.Literal)return e.Literal.Type;
        if(e.Kind==CharacterExpressionKind.Parameter){Require(Parameters.ContainsKey(e.Key),"角色变化倍率引用未知参数："+e.Key);return Parameters[e.Key].Type;}
        if(e.Kind==CharacterExpressionKind.Fact)
        {
            Require(KnownKeys.Contains(e.Key),"角色变化倍率只能读取角色与世界数据："+e.Key);
            if(Facts.TryGetValue(e.Key,out var f))return f.Type;
            if(e.Key.StartsWith("personality."))return Personality[e.Key[12..]].Type;
            if(e.Key.StartsWith("preference."))return Preferences[e.Key[11..]].Type;
            return e.Key=="self.location"?CharacterValueType.Symbol:e.Key.StartsWith("trait.")?CharacterValueType.Boolean:CharacterValueType.Number;
        }
        var types=e.Inputs.Select(Infer).ToArray();if(e.Kind==CharacterExpressionKind.Known)return CharacterValueType.Boolean;
        if(e.Kind is CharacterExpressionKind.Equal or CharacterExpressionKind.NotEqual){Require(types.Length==2&&types[0]==types[1],"角色变化倍率比较类型不一致。");return CharacterValueType.Boolean;}
        bool logical=e.Kind is CharacterExpressionKind.All or CharacterExpressionKind.Any or CharacterExpressionKind.Not;
        Require(types.All(t=>t==(logical?CharacterValueType.Boolean:CharacterValueType.Number))&&(logical||types.Length>0),"角色变化倍率运算类型不匹配。");
        return logical||e.Kind is CharacterExpressionKind.Greater or CharacterExpressionKind.AtLeast or CharacterExpressionKind.Less or CharacterExpressionKind.AtMost?CharacterValueType.Boolean:CharacterValueType.Number;
    }
}
