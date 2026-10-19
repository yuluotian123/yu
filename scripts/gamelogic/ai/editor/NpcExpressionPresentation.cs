#if TOOLS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace GameLogic.AI;

/// <summary>Readable authoring text and local validation; serialized expression resources stay unchanged.</summary>
public static class NpcExpressionPresentation
{
    public static string Operator(CharacterExpressionKind kind)=>kind switch
    {
        CharacterExpressionKind.Equal=>"=",CharacterExpressionKind.NotEqual=>"≠",CharacterExpressionKind.Greater=>">",CharacterExpressionKind.AtLeast=>"≥",
        CharacterExpressionKind.Less=>"<",CharacterExpressionKind.AtMost=>"≤",CharacterExpressionKind.Add=>"+",CharacterExpressionKind.Subtract=>"−",
        CharacterExpressionKind.Multiply=>"×",CharacterExpressionKind.Divide=>"÷",CharacterExpressionKind.Minimum=>"最小值",CharacterExpressionKind.Maximum=>"最大值",
        CharacterExpressionKind.Clamp01=>"限制到 0～1",CharacterExpressionKind.All=>"且",CharacterExpressionKind.Any=>"或",_=>kind.ToString()
    };
    public static string Format(CharacterExpressionDefinition value,IReadOnlyList<NpcEditorSymbol> symbols)
    {
        var path=new HashSet<CharacterExpressionDefinition>();int budget=256;
        string Visit(CharacterExpressionDefinition e,int depth)
        {
            if(e==null)return "未设置";
            if(depth>32||--budget<0)return "…";
            if(!path.Add(e))return "循环引用";
            try
            {
                if(e.Kind==CharacterExpressionKind.Literal)return e.ValueType switch
                {
                    CharacterValueType.Boolean=>e.Boolean?"真":"假",CharacterValueType.Number=>e.Number.ToString("G",CultureInfo.InvariantCulture),
                    CharacterValueType.Symbol=>"“"+e.Symbol+"”",_=>"未知值"
                };
                if(e.Kind is CharacterExpressionKind.Fact or CharacterExpressionKind.Parameter)
                    return symbols.FirstOrDefault(s=>s.Key==e.Key&&s.Parameter==(e.Kind==CharacterExpressionKind.Parameter))?.Label??"未注册："+e.Key;
                var inputs=e.Inputs.Select(x=>Visit(x,depth+1)).ToArray();
                if(e.Kind==CharacterExpressionKind.Not)return "非("+string.Join(", ",inputs)+")";
                if(e.Kind==CharacterExpressionKind.Known)return "已知("+string.Join(", ",inputs)+")";
                if(e.Kind is CharacterExpressionKind.Minimum or CharacterExpressionKind.Maximum or CharacterExpressionKind.Clamp01)return Operator(e.Kind)+"("+string.Join(", ",inputs)+")";
                if(inputs.Length==0)return e.Kind==CharacterExpressionKind.All?"始终满足":e.Kind==CharacterExpressionKind.Any?"始终不满足":"空计算";
                string text=string.Join(" "+Operator(e.Kind)+" ",inputs);
                return depth>0&&inputs.Length>1?"("+text+")":text;
            }
            finally{path.Remove(e);}
        }
        return Visit(value,0);
    }
    public static string Validate(CharacterExpressionDefinition value,CharacterValueType expected,IReadOnlyList<NpcEditorSymbol> symbols)
    {
        if(value==null)return "";
        var path=new HashSet<CharacterExpressionDefinition>();
        CharacterValueType Visit(CharacterExpressionDefinition e,int depth)
        {
            if(e==null||depth>32||!path.Add(e))throw new ArgumentException("公式为空、包含循环引用或嵌套过深。");
            try
            {
                if(e.Kind==CharacterExpressionKind.Literal)
                {
                    if(e.ValueType is not (CharacterValueType.Boolean or CharacterValueType.Number or CharacterValueType.Symbol)||!double.IsFinite(e.Number))throw new ArgumentException("常量类型或数值无效。");
                    return e.ValueType;
                }
                if(e.Kind is CharacterExpressionKind.Fact or CharacterExpressionKind.Parameter)
                    return symbols.FirstOrDefault(s=>s.Key==e.Key&&s.Parameter==(e.Kind==CharacterExpressionKind.Parameter))?.Type??throw new ArgumentException("参数未注册："+e.Key);
                if(!Enum.IsDefined(e.Kind))throw new ArgumentException("运算符无效。");
                var types=e.Inputs.Select(x=>Visit(x,depth+1)).ToArray();
                bool comparison=e.Kind is CharacterExpressionKind.Equal or CharacterExpressionKind.NotEqual or CharacterExpressionKind.Greater or CharacterExpressionKind.AtLeast or CharacterExpressionKind.Less or CharacterExpressionKind.AtMost;
                bool logical=e.Kind is CharacterExpressionKind.All or CharacterExpressionKind.Any or CharacterExpressionKind.Not;
                bool unary=e.Kind is CharacterExpressionKind.Not or CharacterExpressionKind.Known or CharacterExpressionKind.Clamp01;
                if(unary&&types.Length!=1||(comparison||e.Kind is CharacterExpressionKind.Subtract or CharacterExpressionKind.Divide)&&types.Length!=2)
                    throw new ArgumentException("“"+Operator(e.Kind)+"”的参数数量不正确。");
                if(e.Kind==CharacterExpressionKind.Known)return CharacterValueType.Boolean;
                if(e.Kind is CharacterExpressionKind.Equal or CharacterExpressionKind.NotEqual)
                {
                    if(types[0]!=types[1])throw new ArgumentException("比较两侧的类型必须一致。");return CharacterValueType.Boolean;
                }
                if(types.Any(t=>t!=(logical?CharacterValueType.Boolean:CharacterValueType.Number))||!logical&&types.Length==0)
                    throw new ArgumentException(logical?"条件组中的每一项必须是条件。":"数值运算只能使用数字或数值参数。");
                if(e.Kind==CharacterExpressionKind.Divide&&e.Inputs[1] is {Kind:CharacterExpressionKind.Literal,Number:0})throw new ArgumentException("除数不能是 0。");
                return comparison||logical?CharacterValueType.Boolean:CharacterValueType.Number;
            }
            finally{path.Remove(e);}
        }
        try{return Visit(value,0)==expected?"":"公式的结果类型不匹配。";}
        catch(ArgumentException error){return error.Message;}
    }
}
#endif
