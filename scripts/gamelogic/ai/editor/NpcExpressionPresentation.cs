#if TOOLS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace GameLogic.AI;

/// <summary>Readable authoring text and local validation; serialized expression resources stay unchanged.</summary>
public static class NpcExpressionPresentation
{
    public static string Operator(NpcExpressionKind kind)=>kind switch
    {
        NpcExpressionKind.Equal=>"=",NpcExpressionKind.NotEqual=>"≠",NpcExpressionKind.Greater=>">",NpcExpressionKind.AtLeast=>"≥",
        NpcExpressionKind.Less=>"<",NpcExpressionKind.AtMost=>"≤",NpcExpressionKind.Add=>"+",NpcExpressionKind.Subtract=>"−",
        NpcExpressionKind.Multiply=>"×",NpcExpressionKind.Divide=>"÷",NpcExpressionKind.Minimum=>"最小值",NpcExpressionKind.Maximum=>"最大值",
        NpcExpressionKind.Clamp01=>"限制到 0～1",NpcExpressionKind.All=>"且",NpcExpressionKind.Any=>"或",_=>kind.ToString()
    };
    public static string Format(NpcExpressionDefinition value,IReadOnlyList<NpcEditorSymbol> symbols)
    {
        var path=new HashSet<NpcExpressionDefinition>();int budget=256;
        string Visit(NpcExpressionDefinition e,int depth)
        {
            if(e==null)return "未设置";
            if(depth>32||--budget<0)return "…";
            if(!path.Add(e))return "循环引用";
            try
            {
                if(e.Kind==NpcExpressionKind.Literal)return e.ValueType switch
                {
                    NpcValueType.Boolean=>e.Boolean?"真":"假",NpcValueType.Number=>e.Number.ToString("G",CultureInfo.InvariantCulture),
                    NpcValueType.Symbol=>"“"+e.Symbol+"”",_=>"未知值"
                };
                if(e.Kind is NpcExpressionKind.Fact or NpcExpressionKind.Parameter)
                    return symbols.FirstOrDefault(s=>s.Key==e.Key&&s.Parameter==(e.Kind==NpcExpressionKind.Parameter))?.Label??"未注册："+e.Key;
                var inputs=e.Inputs.Select(x=>Visit(x,depth+1)).ToArray();
                if(e.Kind==NpcExpressionKind.Not)return "非("+string.Join(", ",inputs)+")";
                if(e.Kind==NpcExpressionKind.Known)return "已知("+string.Join(", ",inputs)+")";
                if(e.Kind is NpcExpressionKind.Minimum or NpcExpressionKind.Maximum or NpcExpressionKind.Clamp01)return Operator(e.Kind)+"("+string.Join(", ",inputs)+")";
                if(inputs.Length==0)return e.Kind==NpcExpressionKind.All?"始终满足":e.Kind==NpcExpressionKind.Any?"始终不满足":"空计算";
                string text=string.Join(" "+Operator(e.Kind)+" ",inputs);
                return depth>0&&inputs.Length>1?"("+text+")":text;
            }
            finally{path.Remove(e);}
        }
        return Visit(value,0);
    }
    public static string Validate(NpcExpressionDefinition value,NpcValueType expected,IReadOnlyList<NpcEditorSymbol> symbols)
    {
        if(value==null)return "";
        var path=new HashSet<NpcExpressionDefinition>();
        NpcValueType Visit(NpcExpressionDefinition e,int depth)
        {
            if(e==null||depth>32||!path.Add(e))throw new ArgumentException("公式为空、包含循环引用或嵌套过深。");
            try
            {
                if(e.Kind==NpcExpressionKind.Literal)
                {
                    if(e.ValueType is not (NpcValueType.Boolean or NpcValueType.Number or NpcValueType.Symbol)||!double.IsFinite(e.Number))throw new ArgumentException("常量类型或数值无效。");
                    return e.ValueType;
                }
                if(e.Kind is NpcExpressionKind.Fact or NpcExpressionKind.Parameter)
                    return symbols.FirstOrDefault(s=>s.Key==e.Key&&s.Parameter==(e.Kind==NpcExpressionKind.Parameter))?.Type??throw new ArgumentException("参数未注册："+e.Key);
                if(!Enum.IsDefined(e.Kind))throw new ArgumentException("运算符无效。");
                var types=e.Inputs.Select(x=>Visit(x,depth+1)).ToArray();
                bool comparison=e.Kind is NpcExpressionKind.Equal or NpcExpressionKind.NotEqual or NpcExpressionKind.Greater or NpcExpressionKind.AtLeast or NpcExpressionKind.Less or NpcExpressionKind.AtMost;
                bool logical=e.Kind is NpcExpressionKind.All or NpcExpressionKind.Any or NpcExpressionKind.Not;
                bool unary=e.Kind is NpcExpressionKind.Not or NpcExpressionKind.Known or NpcExpressionKind.Clamp01;
                if(unary&&types.Length!=1||(comparison||e.Kind is NpcExpressionKind.Subtract or NpcExpressionKind.Divide)&&types.Length!=2)
                    throw new ArgumentException("“"+Operator(e.Kind)+"”的参数数量不正确。");
                if(e.Kind==NpcExpressionKind.Known)return NpcValueType.Boolean;
                if(e.Kind is NpcExpressionKind.Equal or NpcExpressionKind.NotEqual)
                {
                    if(types[0]!=types[1])throw new ArgumentException("比较两侧的类型必须一致。");return NpcValueType.Boolean;
                }
                if(types.Any(t=>t!=(logical?NpcValueType.Boolean:NpcValueType.Number))||!logical&&types.Length==0)
                    throw new ArgumentException(logical?"条件组中的每一项必须是条件。":"数值运算只能使用数字或数值参数。");
                if(e.Kind==NpcExpressionKind.Divide&&e.Inputs[1] is {Kind:NpcExpressionKind.Literal,Number:0})throw new ArgumentException("除数不能是 0。");
                return comparison||logical?NpcValueType.Boolean:NpcValueType.Number;
            }
            finally{path.Remove(e);}
        }
        try{return Visit(value,0)==expected?"":"公式的结果类型不匹配。";}
        catch(ArgumentException error){return error.Message;}
    }
}
#endif
