using System;

namespace Framework
{
    /// <summary>
    /// 配置字段类型处理器接口。
    /// <para>
    /// 每种类型（或类型模板，如 list&lt;T&gt;、ref&lt;T&gt;）对应一个实现类，
    /// 封装该类型在配置系统中的所有行为：
    /// <list type="bullet">
    ///   <item>运行时 C# Type 映射</item>
    ///   <item>代码生成时的类型名称字符串</item>
    ///   <item>Excel 单元格字符串 → 运行时值的解析</item>
    ///   <item>运行时值 → JSON 可序列化对象的转换</item>
    /// </list>
    /// </para>
    /// <para>
    /// 通过 <see cref="ConfigTypeRegistry.Register"/> 注册自定义处理器，
    /// 可在不修改框架代码的情况下支持新的字段类型。
    /// </para>
    /// </summary>
    public interface IConfigTypeHandler
    {
        /// <summary>
        /// 判断该处理器是否能处理指定的类型字符串。
        /// 类型字符串已被调用方 Trim 和 ToLowerInvariant 处理。
        /// </summary>
        /// <param name="typeStr">小写、已 Trim 的类型字符串。</param>
        bool CanHandle(string typeStr);

        /// <summary>
        /// 返回该类型字符串对应的 C# 运行时 <see cref="Type"/>。
        /// </summary>
        /// <param name="typeStr">小写、已 Trim 的类型字符串。</param>
        Type GetRuntimeType(string typeStr);

        /// <summary>
        /// 返回代码生成时使用的 C# 类型名称字符串。
        /// 例如："list&lt;int&gt;" → "List&lt;int&gt;"，"ref&lt;ItemConfig&gt;" → "int"。
        /// </summary>
        /// <param name="typeStr">小写、已 Trim 的类型字符串。</param>
        string GetCSharpTypeName(string typeStr);

        /// <summary>
        /// 将 Excel 单元格的字符串值解析为运行时值对象。
        /// </summary>
        /// <param name="cellValue">单元格原始字符串（可能为 null 或空）。</param>
        /// <param name="typeStr">小写、已 Trim 的类型字符串。</param>
        object ParseCell(string cellValue, string typeStr);

        /// <summary>
        /// 将运行时值转换为适合写入 JSON 的对象。
        /// 基础类型直接返回原值，list 类型返回元素已转换后的列表。
        /// </summary>
        /// <param name="value">运行时值（由 <see cref="ParseCell"/> 产生）。</param>
        /// <param name="typeStr">小写、已 Trim 的类型字符串。</param>
        object ToJsonValue(object value, string typeStr);

        /// <summary>
        /// 判断该类型在代码生成时是否需要 using System.Collections.Generic。
        /// 例如 list&lt;T&gt; 需要，其他基础类型不需要。
        /// </summary>
        /// <param name="typeStr">小写、已 Trim 的类型字符串。</param>
        bool NeedsCollectionsUsing(string typeStr);
    }
}
