using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Framework
{
    /// <summary>
    /// xlsx 配置表转换入口。
    /// <para>
    /// 读取一个 xlsx 文件，同时输出：
    /// <list type="bullet">
    ///   <item>运行时 JSON 数据文件（供 <see cref="JsonConfigLoader"/> 加载）</item>
    ///   <item>C# 配置行类代码文件（供游戏代码直接引用）</item>
    /// </list>
    /// </para>
    /// <para>注意：生成的 C# 文件禁止手动修改。如需扩展行为，请新建同名 partial class。</para>
    /// </summary>
    public sealed class XlsxConverter
    {
        private readonly XlsxReader         _reader  = new XlsxReader();
        private readonly CSharpCodeGenerator _csGen   = new CSharpCodeGenerator();
        private readonly JsonDataWriter      _jsonGen = new JsonDataWriter();

        /// <summary>转换单个 xlsx 文件。</summary>
        /// <param name="options">转换参数。</param>
        public XlsxConvertResult Convert(XlsxConvertOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            using var transaction = ConfigOutputTransaction.Begin(new[] { options.JsonOutputDir, options.CsOutputDir });
            var outputs = new List<ConfigOutput>();
            XlsxConvertResult result = Prepare(options, outputs);
            transaction.Publish(outputs, options.Overwrite);
            return result;
        }

        /// <summary>Recover an interrupted batch without regenerating its source workbooks.</summary>
        public int RecoverOutputs(params string[] outputDirectories)
        {
            using var transaction = ConfigOutputTransaction.Begin(outputDirectories);
            return transaction.RecoveredTransactions;
        }

        private XlsxConvertResult Prepare(XlsxConvertOptions options, List<ConfigOutput> outputs)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.Validate();


            var tableData = _reader.Read(options.XlsxPath, options.SheetName);

            var tableName = !string.IsNullOrWhiteSpace(options.TableName)
                ? options.TableName
                : Path.GetFileNameWithoutExtension(options.XlsxPath).ToLowerInvariant();

            if (tableName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                tableName.Contains('/') || tableName.Contains('\\') || tableName is "." or "..")
                throw new ArgumentException($"Invalid config table name: '{tableName}'.");

            // Validate both representations even when only one output was requested.
            string jsonContent;
            string csContent;
            try
            {
                csContent = _csGen.Generate(tableName, tableData, options.Namespace);
                jsonContent = _jsonGen.Generate(tableData);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Config '{options.XlsxPath}': {exception.Message}", exception);
            }

            string jsonPath = null;
            if (options.JsonOutputDir != null)
            {
                jsonPath = Path.Combine(options.JsonOutputDir, tableName + ".json");
                outputs.Add(new ConfigOutput(jsonPath, jsonContent));
            }

            string csPath = null;
            if (options.CsOutputDir != null)
            {
                var className = ToPascalCase(tableName) + "Config";
                csPath = Path.Combine(options.CsOutputDir, className + ".cs");
                outputs.Add(new ConfigOutput(csPath, csContent));
            }

            return new XlsxConvertResult
            {
                TableName  = tableName,
                RowCount   = tableData.DataRows.Count,
                FieldCount = tableData.Fields.Count,
                JsonPath   = jsonPath,
                CsPath     = csPath
            };
        }

        /// <summary>
        /// 批量转换指定目录下所有 xlsx 文件（不递归子目录）。
        /// </summary>
        /// <param name="xlsxDir">xlsx 文件所在目录。</param>
        /// <param name="jsonOutputDir">JSON 输出目录；null 则不输出 JSON。</param>
        /// <param name="csOutputDir">C# 输出目录；null 则不输出 C# 代码。</param>
        /// <param name="namespaceName">C# 命名空间，默认 "Generated.Config"。</param>
        /// <param name="overwrite">是否覆盖已有文件，默认 true。</param>
        /// <returns>每个文件的转换结果列表。</returns>
        public XlsxConvertResult[] ConvertDirectory(
            string xlsxDir,
            string jsonOutputDir  = null,
            string csOutputDir    = null,
            string namespaceName  = "Generated.Config",
            bool   overwrite      = true)
        {
            using var transaction = ConfigOutputTransaction.Begin(new[] { jsonOutputDir, csOutputDir });
            if (!Directory.Exists(xlsxDir))
                throw new DirectoryNotFoundException($"找不到目录：'{xlsxDir}'");
            if (jsonOutputDir == null && csOutputDir == null)
                throw new ArgumentException("jsonOutputDir 和 csOutputDir 不能同时为 null。");

            var files = Directory.GetFiles(xlsxDir, "*.xlsx", SearchOption.TopDirectoryOnly)
                .Where(path => !Path.GetFileName(path).StartsWith("~", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var results = new XlsxConvertResult[files.Length];
            var outputs = new List<ConfigOutput>();

            for (int i = 0; i < files.Length; i++)
            {
                results[i] = Prepare(new XlsxConvertOptions
                {
                    XlsxPath      = files[i],
                    JsonOutputDir = jsonOutputDir,
                    CsOutputDir   = csOutputDir,
                    Namespace     = namespaceName,
                    Overwrite     = overwrite
                }, outputs);
            }
            transaction.Publish(outputs, overwrite);
            return results;
        }

        private static string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var parts = name.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var part in parts)
                sb.Append(char.ToUpperInvariant(part[0]) + part[1..]);
            return sb.ToString();
        }
    }

    // ── 参数 & 结果 ───────────────────────────────────────────────────────────

    /// <summary>单次转换参数。</summary>
    public sealed class XlsxConvertOptions
    {
        /// <summary>xlsx 文件绝对路径（必填）。</summary>
        public string XlsxPath { get; set; }
        /// <summary>Sheet 名；null 时读取第一个 Sheet。</summary>
        public string SheetName { get; set; }
        /// <summary>表名（不含扩展名）；null 时从文件名推断。</summary>
        public string TableName { get; set; }
        /// <summary>JSON 输出目录；null 则不输出。</summary>
        public string JsonOutputDir { get; set; }
        /// <summary>C# 代码输出目录；null 则不输出。</summary>
        public string CsOutputDir { get; set; }
        /// <summary>生成的 C# 命名空间，默认 "Generated.Config"。</summary>
        public string Namespace { get; set; } = "Generated.Config";
        /// <summary>文件已存在时是否覆盖，默认 true。</summary>
        public bool Overwrite { get; set; } = true;

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(XlsxPath))
                throw new ArgumentException("XlsxPath 不能为空。");
            if (!File.Exists(XlsxPath))
                throw new FileNotFoundException($"找不到 xlsx 文件：'{XlsxPath}'");
            if (JsonOutputDir == null && CsOutputDir == null)
                throw new ArgumentException("JsonOutputDir 和 CsOutputDir 不能同时为 null。");
        }
    }

    /// <summary>单次转换结果。</summary>
    public sealed class XlsxConvertResult
    {
        public string TableName  { get; init; }
        public int    RowCount   { get; init; }
        public int    FieldCount { get; init; }
        public string JsonPath   { get; init; }
        public string CsPath     { get; init; }

        public override string ToString()
            => $"[{TableName}] {RowCount} 行 / {FieldCount} 字段" +
               (JsonPath != null ? $" → JSON: {JsonPath}" : "") +
               (CsPath   != null ? $" → CS: {CsPath}"   : "");
    }
}
