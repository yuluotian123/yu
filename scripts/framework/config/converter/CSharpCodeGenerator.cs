using System;
using System.Collections.Generic;
using System.Text;

namespace Framework
{
    /// <summary>
    /// 根据 xlsx 表结构生成 C# 配置行类代码。
    /// <para>
    /// 生成规则：
    /// <list type="bullet">
    ///   <item>类名 = 表名首字母大写 + "Config"（如 "monster" → "MonsterConfig"）</item>
    ///   <item>继承 <see cref="ConfigRow"/>，标记 <see cref="ConfigTableAttribute"/></item>
    ///   <item>字段名首字母转大写作为属性名</item>
    ///   <item>list&lt;T&gt; 类型自动添加 using System.Collections.Generic</item>
    ///   <item>生成文件顶部注释说明"此文件为自动生成，禁止手动修改"</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class CSharpCodeGenerator
    {
        /// <summary>
        /// 生成 C# 配置类代码字符串。
        /// </summary>
        /// <param name="tableName">表名（不含扩展名），如 "monster"。</param>
        /// <param name="tableData">从 <see cref="XlsxReader"/> 解析得到的表格结构。</param>
        /// <param name="namespaceName">生成类所属的命名空间，默认 "Generated.Config"。</param>
        /// <returns>完整的 .cs 文件内容字符串。</returns>
        public string Generate(string tableName, XlsxTableData tableData, string namespaceName = "Generated.Config")
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("tableName 不能为空。");
            if (tableData == null)
                throw new ArgumentNullException(nameof(tableData));

            var className = ToPascalCase(tableName) + "Config";
            var needsCollections = NeedsCollectionsUsing(tableData.Fields);

            var sb = new StringBuilder();

            // 文件头注释
            sb.AppendLine($"// 【此文件由 XlsxConverter 自动生成，请勿手动修改】");
            sb.AppendLine($"// 源表名：{tableName}");
            sb.AppendLine($"// 生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // using
            if (needsCollections)
                sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Framework;");
            sb.AppendLine();

            // namespace & class
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
            sb.AppendLine($"    [ConfigTable(\"{tableName}\")]");
            sb.AppendLine($"    public class {className} : ConfigRow");
            sb.AppendLine("    {");

            // 属性
            foreach (var field in tableData.Fields)
            {
                // id 已在基类 ConfigRow 中定义，跳过
                if (field.FieldName.Equals("id", StringComparison.OrdinalIgnoreCase))
                    continue;

                var propName = ToPascalCase(field.FieldName);
                string csharpType;
                try
                {
                    csharpType = ConfigTypeRegistry.GetCSharpTypeName(field.TypeStr);
                }
                catch
                {
                    // 不支持的类型降级为 string，并添加警告注释
                    csharpType = "string";
                    sb.AppendLine($"        // [WARNING] 不支持的类型 '{field.TypeStr}'，已降级为 string");
                }

                if (!string.IsNullOrWhiteSpace(field.Comment))
                {
                    sb.AppendLine($"        /// <summary>{EscapeXml(field.Comment)}</summary>");
                }

                sb.AppendLine($"        public {csharpType} {propName} {{ get; set; }}");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // ── 私有辅助 ──────────────────────────────────────────────────────────

        /// <summary>将字符串首字母及下划线后字母转为大写（camelCase / snake_case → PascalCase）。</summary>
        private static string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var parts = name.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var part in parts)
                sb.Append(char.ToUpperInvariant(part[0]) + part.Substring(1));
            return sb.ToString();
        }

        /// <summary>检查字段列表中是否有需要 using System.Collections.Generic 的类型。</summary>
        private static bool NeedsCollectionsUsing(IReadOnlyList<XlsxFieldDef> fields)
        {
            foreach (var f in fields)
            {
                if (string.IsNullOrWhiteSpace(f.TypeStr)) continue;
                try
                {
                    if (ConfigTypeRegistry.NeedsCollectionsUsing(f.TypeStr))
                        return true;
                }
                catch { /* 忽略不支持的类型 */ }
            }
            return false;
        }

        /// <summary>转义 XML 注释中的特殊字符。</summary>
        private static string EscapeXml(string text)
            => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
