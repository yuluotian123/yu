using System;
using System.Collections.Generic;

namespace Framework
{
    /// <summary>
    /// 配置字段类型处理器注册表。
    /// <para>
    /// 内置处理器（按优先级从高到低）：
    /// <list type="number">
    ///   <item><see cref="ListTypeHandler"/> — list&lt;T&gt;</item>
    ///   <item><see cref="RefTypeHandler"/> — ref&lt;T&gt;</item>
    ///   <item><see cref="PrimitiveTypeHandler"/> — int / long / float / double / bool / string</item>
    /// </list>
    /// </para>
    /// <para>
    /// 通过 <see cref="Register"/> 可在框架初始化时注入自定义处理器，
    /// 新注册的处理器优先级最高（排在最前）。
    /// </para>
    /// </summary>
    public static class ConfigTypeRegistry
    {
        // 有序列表，CanHandle 按顺序匹配，靠前的优先级更高
        private static readonly List<IConfigTypeHandler> _handlers = new();

        static ConfigTypeRegistry()
        {
            // 内置处理器，越后 Register 的优先级越高
            // 此处按"优先级从低到高"顺序调用，最终列表头部为优先级最高的
            RegisterInternal(new PrimitiveTypeHandler());
            RegisterInternal(new RefTypeHandler());
            RegisterInternal(new ListTypeHandler());
        }

        // ── 公开 API ──────────────────────────────────────────────────────────

        /// <summary>
        /// 注册自定义处理器。新注册的处理器优先级最高（排在内置处理器之前）。
        /// </summary>
        /// <param name="handler">自定义类型处理器。</param>
        public static void Register(IConfigTypeHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Insert(0, handler);
        }

        /// <summary>
        /// 根据类型字符串查找匹配的处理器。找不到时抛出 <see cref="NotSupportedException"/>。
        /// </summary>
        /// <param name="typeStr">类型字符串（原始大小写，内部会自动规范化）。</param>
        public static IConfigTypeHandler Get(string typeStr)
        {
            var normalized = Normalize(typeStr);
            foreach (var h in _handlers)
                if (h.CanHandle(normalized)) return h;
            throw new NotSupportedException($"[ConfigTypeRegistry] 不支持的配置类型：'{typeStr}'");
        }

        // ── 委托方法（供 ConfigTypeParser 调用）──────────────────────────────

        /// <summary>返回运行时 C# Type。</summary>
        public static Type GetRuntimeType(string typeStr)
            => Get(typeStr).GetRuntimeType(Normalize(typeStr));

        /// <summary>返回代码生成用的 C# 类型名称字符串。</summary>
        public static string GetCSharpTypeName(string typeStr)
            => Get(typeStr).GetCSharpTypeName(Normalize(typeStr));

        /// <summary>将单元格字符串解析为运行时值。</summary>
        public static object ParseCell(string cellValue, string typeStr)
            => Get(typeStr).ParseCell(cellValue, Normalize(typeStr));

        /// <summary>将运行时值转换为 JSON 兼容对象。</summary>
        public static object ToJsonValue(object value, string typeStr)
            => Get(typeStr).ToJsonValue(value, Normalize(typeStr));

        /// <summary>判断该类型是否需要 using System.Collections.Generic。</summary>
        public static bool NeedsCollectionsUsing(string typeStr)
            => Get(typeStr).NeedsCollectionsUsing(Normalize(typeStr));

        // ── 私有辅助 ──────────────────────────────────────────────────────────

        /// <summary>规范化类型字符串：Trim + ToLowerInvariant。</summary>
        private static string Normalize(string typeStr)
        {
            if (string.IsNullOrWhiteSpace(typeStr))
                throw new ArgumentException("[ConfigTypeRegistry] 类型字符串不能为空。");
            return typeStr.Trim().ToLowerInvariant();
        }

        /// <summary>内部注册，Insert(0) 使后注册的优先级更高。</summary>
        private static void RegisterInternal(IConfigTypeHandler handler)
            => _handlers.Insert(0, handler);
    }
}
