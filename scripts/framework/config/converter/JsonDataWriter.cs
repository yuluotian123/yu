using System;
using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Framework
{
    /// <summary>
    /// 将 xlsx 表格数据转换并写出为 JSON 数组字符串。
    /// <para>
    /// 不复用 <see cref="JsonHelper"/>，原因：JsonHelper 会写入 "$type" 多态标记，
    /// 而配置表 JSON 只需要干净的数据数组，不需要多态信息。
    /// 序列化逻辑通过 <see cref="ConfigTypeRegistry.ParseCell"/> 将单元格字符串
    /// 解析为运行时值，再用 <see cref="System.Text.Json.Nodes"/> 直接构建 JSON 树。
    /// </para>
    /// <para>
    /// 输出格式示例：
    /// <code>
    /// [
    ///   { "Id": 1001, "Name": "史莱姆", "Hp": 100, "DropItems": [1, 2] },
    ///   { "Id": 1002, "Name": "哥布林", "Hp": 200, "DropItems": [3] }
    /// ]
    /// </code>
    /// </para>
    /// </summary>
    public sealed class JsonDataWriter
    {
        private static readonly JsonSerializerOptions _opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// 将表格数据序列化为 JSON 字符串。
        /// </summary>
        /// <param name="tableData">从 <see cref="XlsxReader"/> 解析得到的表格数据。</param>
        /// <returns>格式化的 JSON 数组字符串。</returns>
        public string Generate(XlsxTableData tableData)
        {
            if (tableData == null) throw new ArgumentNullException(nameof(tableData));

            var array = new JsonArray();

            foreach (var dataRow in tableData.DataRows)
            {
                var rowObj = new JsonObject();

                foreach (var field in tableData.Fields)
                {
                    // 字段名转 PascalCase 作为 JSON key（与 C# 属性名保持一致）
                    var jsonKey = ToPascalCase(field.FieldName);
                    dataRow.TryGetValue(field.FieldName, out var cellStr);
                    cellStr ??= string.Empty;

                    // 由 ConfigTypeRegistry 负责将字符串解析为运行时值，再转为 JSON 兼容对象
                    object jsonValue;
                    try
                    {
                        var parsed = ConfigTypeRegistry.ParseCell(cellStr, field.TypeStr);
                        jsonValue = ConfigTypeRegistry.ToJsonValue(parsed, field.TypeStr);
                    }
                    catch
                    {
                        // 不支持的类型降级为原始字符串
                        jsonValue = cellStr;
                    }

                    rowObj[jsonKey] = ValueToJsonNode(jsonValue);
                }

                array.Add(rowObj);
            }

            return array.ToJsonString(_opts);
        }

        // ── 私有辅助 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 将运行时值转为 JsonNode。
        /// 只处理配置表支持的类型（基础类型 + List），不写 $type。
        /// </summary>
        private static JsonNode ValueToJsonNode(object value)
        {
            if (value == null) return null;

            return value switch
            {
                bool   b => JsonValue.Create(b),
                int    i => JsonValue.Create(i),
                long   l => JsonValue.Create(l),
                float  f => JsonValue.Create(f),
                double d => JsonValue.Create(d),
                string s => JsonValue.Create(s),
                IList list => ListToJsonArray(list),
                _ => JsonValue.Create(value.ToString())
            };
        }

        private static JsonArray ListToJsonArray(IList list)
        {
            var arr = new JsonArray();
            foreach (var item in list)
                arr.Add(ValueToJsonNode(item));
            return arr;
        }

        /// <summary>camelCase / snake_case → PascalCase。</summary>
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
}
