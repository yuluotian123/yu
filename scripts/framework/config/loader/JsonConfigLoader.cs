using System.Collections.Generic;
using Godot;

namespace Framework
{
    /// <summary>
    /// 从 Godot 资源路径读取 JSON 文件并反序列化的配置加载器（运行时默认实现）。
    /// <para>
    /// 使用 Godot <see cref="FileAccess"/> 读取文件内容（兼容 res:// 和打包后的 PCK），
    /// 通过 <see cref="JsonHelper.DeserializeList{T}"/> 反序列化为强类型列表。
    /// </para>
    /// <para>
    /// JSON 文件格式为数组，每个元素对应一条配置行，字段名与 C# 属性名一致：
    /// <code>
    /// [
    ///   { "Id": 1001, "Name": "史莱姆", "Hp": 100, "DropItems": [1, 2, 3] },
    ///   { "Id": 1002, "Name": "哥布林", "Hp": 200, "DropItems": [4] }
    /// ]
    /// </code>
    /// </para>
    /// </summary>
    public sealed class JsonConfigLoader : IConfigLoader
    {
        private readonly string _tablePath;

        /// <summary>
        /// 创建加载器。
        /// </summary>
        /// <param name="tablePath">JSON 数据文件所在目录（res:// 格式，末尾有无 / 均可）。</param>
        public JsonConfigLoader(string tablePath)
        {
            _tablePath = (tablePath ?? "res://assets/config/tables/").TrimEnd('/') + "/";
        }

        /// <inheritdoc/>
        public List<T> Load<T>(string tableName) where T : ConfigRow
        {
            var path = _tablePath + tableName + ".json";

            if (!FileAccess.FileExists(path))
            {
                Debugger.Error($"[JsonConfigLoader] 找不到配置文件：'{path}'");
                return new List<T>();
            }

            string json;
            using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Read))
            {
                if (file == null)
                {
                    Debugger.Error($"[JsonConfigLoader] 无法打开文件：'{path}'，错误：{FileAccess.GetOpenError()}");
                    return new List<T>();
                }
                json = file.GetAsText();
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                Debugger.Warn($"[JsonConfigLoader] 文件内容为空：'{path}'");
                return new List<T>();
            }

            var result = JsonHelper.DeserializeList<T>(json);
            Debugger.Info($"[JsonConfigLoader] 加载 '{tableName}' 完成，共 {result.Count} 条。");
            return result;
        }
    }
}
