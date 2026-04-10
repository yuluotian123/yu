using System;
using System.Globalization;

namespace Framework
{
    /// <summary>
    /// 基础类型处理器，处理 int / long / float / double / bool / string。
    /// </summary>
    internal sealed class PrimitiveTypeHandler : IConfigTypeHandler
    {
        public bool CanHandle(string typeStr)
        {
            return typeStr switch
            {
                "int" or "long" or "float" or "double" or "bool" or "string" => true,
                _ => false
            };
        }

        public Type GetRuntimeType(string typeStr)
        {
            return typeStr switch
            {
                "int"    => typeof(int),
                "long"   => typeof(long),
                "float"  => typeof(float),
                "double" => typeof(double),
                "bool"   => typeof(bool),
                "string" => typeof(string),
                _ => throw new NotSupportedException($"PrimitiveTypeHandler 不支持类型：'{typeStr}'")
            };
        }

        public string GetCSharpTypeName(string typeStr) => typeStr; // 基础类型名称与 C# 关键字一致

        public object ParseCell(string cellValue, string typeStr)
        {
            cellValue ??= string.Empty;
            return typeStr switch
            {
                "int"    => int.TryParse(cellValue, out var i) ? i : 0,
                "long"   => long.TryParse(cellValue, out var l) ? l : 0L,
                "float"  => float.TryParse(cellValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f,
                "double" => double.TryParse(cellValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0.0,
                "bool"   => cellValue.ToLowerInvariant() is "true" or "1" or "yes",
                "string" => cellValue,
                _ => throw new NotSupportedException($"PrimitiveTypeHandler 不支持类型：'{typeStr}'")
            };
        }

        public object ToJsonValue(object value, string typeStr) => value; // 基础类型可直接写 JSON

        public bool NeedsCollectionsUsing(string typeStr) => false;
    }
}
