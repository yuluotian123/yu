using System;
using System.Globalization;

namespace Framework
{
    /// <summary>
    /// 外键引用类型处理器，处理 ref&lt;XxxConfig&gt; 格式的类型字符串。
    /// <para>
    /// ref&lt;T&gt; 在运行时和代码生成中均映射为 <see cref="int"/>（存储目标表的主键 ID）。
    /// 外键的实际对象解析由业务层在需要时调用 <see cref="IConfigModule.GetById{T}"/> 完成。
    /// </para>
    /// </summary>
    internal sealed class RefTypeHandler : IConfigTypeHandler
    {
        public bool CanHandle(string typeStr)
            => typeStr.StartsWith("ref<") && typeStr.EndsWith(">");

        public Type GetRuntimeType(string typeStr) => typeof(int);

        public string GetCSharpTypeName(string typeStr) => "int";

        public object ParseCell(string cellValue, string typeStr)
        {
            if (string.IsNullOrWhiteSpace(cellValue)) return 0;
            return int.TryParse(cellValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                ? id : 0;
        }

        public object ToJsonValue(object value, string typeStr) => value; // int 可直接写 JSON

        public bool NeedsCollectionsUsing(string typeStr) => false;
    }
}
