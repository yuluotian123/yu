using System;
using System.Collections;
using System.Collections.Generic;

namespace Framework
{
    /// <summary>
    /// List 类型处理器，处理 list&lt;T&gt; 格式的类型字符串。
    /// <para>
    /// Excel 单元格中用英文逗号分隔元素，例如 "1,2,3"。
    /// 内部元素类型 T 必须是基础类型（委托给 <see cref="ConfigTypeRegistry"/> 处理）。
    /// </para>
    /// </summary>
    internal sealed class ListTypeHandler : IConfigTypeHandler
    {
        public bool CanHandle(string typeStr)
            => typeStr.StartsWith("list<") && typeStr.EndsWith(">");

        public Type GetRuntimeType(string typeStr)
        {
            var inner = ExtractInner(typeStr);
            var elemType = ConfigTypeRegistry.GetRuntimeType(inner);
            return typeof(List<>).MakeGenericType(elemType);
        }

        public string GetCSharpTypeName(string typeStr)
        {
            var inner = ExtractInner(typeStr);
            return $"List<{ConfigTypeRegistry.GetCSharpTypeName(inner)}>";
        }

        public object ParseCell(string cellValue, string typeStr)
        {
            var inner = ExtractInner(typeStr);
            var elemType = ConfigTypeRegistry.GetRuntimeType(inner);

            // 构造 List<elemType> 实例
            var listType = typeof(List<>).MakeGenericType(elemType);
            var list = (IList)Activator.CreateInstance(listType);

            if (string.IsNullOrWhiteSpace(cellValue))
                return list;

            foreach (var part in cellValue.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var elem = ConfigTypeRegistry.ParseCell(part.Trim(), inner);
                list.Add(elem);
            }

            return list;
        }

        public object ToJsonValue(object value, string typeStr)
        {
            if (value is not IList list) return null;

            var inner = ExtractInner(typeStr);
            var result = new List<object>(list.Count);
            foreach (var item in list)
                result.Add(ConfigTypeRegistry.ToJsonValue(item, inner));

            return result;
        }

        public bool NeedsCollectionsUsing(string typeStr) => true;

        private static string ExtractInner(string typeStr)
            => typeStr.Substring(5, typeStr.Length - 6).Trim(); // "list<...>" → "..."
    }
}
