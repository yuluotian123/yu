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
            if (typeStr == "string")
                return cellValue;
            cellValue = cellValue.Trim();
            if (cellValue.Length == 0)
                cellValue = typeStr == "bool" ? "false" : "0";
            return typeStr switch
            {
                "int"    => int.Parse(cellValue, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "long"   => long.Parse(cellValue, NumberStyles.Integer, CultureInfo.InvariantCulture),
                "float"  => float.TryParse(cellValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && float.IsFinite(f)
                    ? f : throw new FormatException($"Invalid float '{cellValue}'."),
                "double" => double.TryParse(cellValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d)
                    ? d : throw new FormatException($"Invalid double '{cellValue}'."),
                "bool"   => cellValue.ToLowerInvariant() switch
                {
                    "true" or "1" or "yes" => true,
                    "false" or "0" or "no" => false,
                    _ => throw new FormatException($"Invalid bool '{cellValue}'.")
                },
                _ => throw new NotSupportedException($"PrimitiveTypeHandler 不支持类型：'{typeStr}'")
            };
        }

        public object ToJsonValue(object value, string typeStr) => value; // 基础类型可直接写 JSON

        public bool NeedsCollectionsUsing(string typeStr) => false;
    }
}
