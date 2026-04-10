using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Framework
{
    /// <summary>
    /// xlsx 文件读取器，解析表格结构和数据。
    /// <para>
    /// xlsx 格式约定：
    /// <list type="number">
    ///   <item>第 1 行：字段名（驼峰 / snake_case，第一列必须为 "id"）</item>
    ///   <item>第 2 行：字段类型（int / long / float / double / bool / string / list&lt;T&gt; / ref&lt;T&gt;）</item>
    ///   <item>第 3 行：注释（生成到 C# 的 &lt;summary&gt;）</item>
    ///   <item>第 4 行起：数据（全空行自动跳过）</item>
    /// </list>
    /// </para>
    /// <para>
    /// 使用 DocumentFormat.OpenXml（Open XML SDK）而非 MiniExcel，
    /// 避免静态类型缓存导致 Godot 程序集热重载时卸载失败。
    /// </para>
    /// </summary>
    public sealed class XlsxReader
    {
        /// <summary>
        /// 读取 xlsx 文件，返回解析后的表格数据。
        /// </summary>
        /// <param name="xlsxPath">xlsx 文件的绝对路径。</param>
        /// <param name="sheetName">Sheet 名；为 null 时读取第一个 Sheet。</param>
        public XlsxTableData Read(string xlsxPath, string sheetName = null)
        {
            if (string.IsNullOrWhiteSpace(xlsxPath))
                throw new ArgumentException("xlsxPath 不能为空。");

            // 将所有行读取为 string[]（按列索引）
            var allRows = ReadAllRows(xlsxPath, sheetName);

            if (allRows.Count < 3)
                throw new InvalidOperationException(
                    $"xlsx 行数不足（至少需要 3 行表头），文件：{xlsxPath}");

            var headerRow  = allRows[0];
            var typeRow    = allRows[1];
            var commentRow = allRows[2];

            // ── 解析字段定义（第 1~3 行）──────────────────────────────────────
            var fields = new List<XlsxFieldDef>();
            for (int col = 0; col < headerRow.Length; col++)
            {
                var fieldName = headerRow[col]?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(fieldName)) continue;

                var typeStr = col < typeRow.Length    ? typeRow[col]?.Trim()    ?? "" : "";
                var comment = col < commentRow.Length ? commentRow[col]?.Trim() ?? "" : "";
                fields.Add(new XlsxFieldDef(fieldName, typeStr, comment));
            }

            if (fields.Count == 0)
                throw new InvalidOperationException($"xlsx 未找到任何字段定义：{xlsxPath}");

            if (!fields[0].FieldName.Equals("id", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"xlsx 第一列必须为 'id'，当前为 '{fields[0].FieldName}'，文件：{xlsxPath}");

            // ── 解析数据行（第 4 行起）────────────────────────────────────────
            var dataRows = new List<IReadOnlyDictionary<string, string>>();
            for (int rowIdx = 3; rowIdx < allRows.Count; rowIdx++)
            {
                var rawRow  = allRows[rowIdx];
                var dataRow = new Dictionary<string, string>();
                bool hasData = false;

                for (int col = 0; col < fields.Count; col++)
                {
                    var cellStr = col < rawRow.Length ? rawRow[col] ?? "" : "";
                    dataRow[fields[col].FieldName] = cellStr;
                    if (!string.IsNullOrWhiteSpace(cellStr)) hasData = true;
                }

                if (hasData) dataRows.Add(dataRow);
            }

            return new XlsxTableData(fields, dataRows);
        }

        // ── 内部：用 Open XML SDK 读取所有行 ─────────────────────────────────

        private static List<string[]> ReadAllRows(string xlsxPath, string sheetName)
        {
            var result = new List<string[]>();

            using var doc = SpreadsheetDocument.Open(xlsxPath, isEditable: false);
            var workbook  = doc.WorkbookPart ?? throw new InvalidOperationException("无法读取 Workbook。");

            // 找目标 Sheet
            Sheet sheet;
            if (sheetName == null)
            {
                sheet = workbook.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault()
                        ?? throw new InvalidOperationException("xlsx 中没有任何 Sheet。");
            }
            else
            {
                sheet = workbook.Workbook.Sheets?.Elements<Sheet>()
                            .FirstOrDefault(s => s.Name?.Value == sheetName)
                        ?? throw new InvalidOperationException($"找不到 Sheet：'{sheetName}'");
            }

            var sheetPart = (WorksheetPart)workbook.GetPartById(sheet.Id!.Value!);
            var worksheet = sheetPart.Worksheet;
            var sheetData = worksheet.GetFirstChild<SheetData>()
                            ?? throw new InvalidOperationException("Sheet 中没有数据。");

            // 共享字符串表（xlsx 中字符串类型的单元格值存在这里）
            var sst = workbook.SharedStringTablePart?.SharedStringTable;

            // 确定最大列数（用于按列索引对齐）
            int maxCol = 0;
            var rows   = sheetData.Elements<Row>().ToList();
            foreach (var row in rows)
            {
                foreach (var cell in row.Elements<Cell>())
                {
                    int col = ColIndexFromRef(cell.CellReference?.Value);
                    if (col + 1 > maxCol) maxCol = col + 1;
                }
            }

            foreach (var row in rows)
            {
                var cells = new string[maxCol];
                foreach (var cell in row.Elements<Cell>())
                {
                    int colIdx = ColIndexFromRef(cell.CellReference?.Value);
                    cells[colIdx] = GetCellValue(cell, sst);
                }
                result.Add(cells);
            }

            return result;
        }

        /// <summary>从单元格引用（如 "B3"）解析列索引（0-based）。</summary>
        private static int ColIndexFromRef(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef)) return 0;
            int idx = 0;
            foreach (char c in cellRef)
            {
                if (!char.IsLetter(c)) break;
                idx = idx * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
            }
            return idx - 1;
        }

        /// <summary>读取单元格的字符串值（处理共享字符串、数字、布尔等）。</summary>
        private static string GetCellValue(Cell cell, SharedStringTable sst)
        {
            var raw = cell.CellValue?.Text ?? "";
            if (cell.DataType?.Value == CellValues.SharedString && sst != null)
            {
                if (int.TryParse(raw, out int idx))
                    return sst.ElementAt(idx).InnerText;
            }
            if (cell.DataType?.Value == CellValues.Boolean)
                return raw == "1" ? "true" : "false";
            return raw;
        }
    }

    // ── 数据结构 ──────────────────────────────────────────────────────────────

    /// <summary>xlsx 字段定义（一列的元数据）。</summary>
    public sealed class XlsxFieldDef
    {
        /// <summary>字段名（第 1 行），代码生成时首字母自动大写为属性名。</summary>
        public string FieldName { get; }
        /// <summary>类型字符串（第 2 行），如 "int"、"list&lt;float&gt;"。</summary>
        public string TypeStr { get; }
        /// <summary>注释文本（第 3 行），生成到 C# 的 &lt;summary&gt;。</summary>
        public string Comment { get; }

        public XlsxFieldDef(string fieldName, string typeStr, string comment)
        {
            FieldName = fieldName;
            TypeStr   = typeStr;
            Comment   = comment;
        }
    }

    /// <summary>xlsx 表格完整数据（字段定义 + 所有数据行）。</summary>
    public sealed class XlsxTableData
    {
        /// <summary>字段定义列表（按列顺序）。</summary>
        public IReadOnlyList<XlsxFieldDef> Fields { get; }
        /// <summary>数据行列表（全空行已跳过）。key 为字段名，value 为单元格字符串。</summary>
        public IReadOnlyList<IReadOnlyDictionary<string, string>> DataRows { get; }

        public XlsxTableData(
            IList<XlsxFieldDef> fields,
            IList<IReadOnlyDictionary<string, string>> dataRows)
        {
            Fields   = new ReadOnlyCollection<XlsxFieldDef>(fields);
            DataRows = new ReadOnlyCollection<IReadOnlyDictionary<string, string>>(dataRows);
        }
    }
}
