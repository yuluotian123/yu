using System;

namespace Framework
{
    /// <summary>
    /// 标记配置表类与表名（JSON文件名）的映射关系。
    /// <para>
    /// 用法示例：
    /// <code>
    /// [ConfigTable("monster")]
    /// public class MonsterConfig : ConfigRow { ... }
    /// </code>
    /// 对应 JSON 文件路径：{ConfigSetting.TablePath}/monster.json
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ConfigTableAttribute : Attribute
    {
        /// <summary>
        /// 表名（不含扩展名），对应 JSON 数据文件名以及 xlsx 文件名。
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// 创建配置表特性。
        /// </summary>
        /// <param name="tableName">表名（不含扩展名）。</param>
        public ConfigTableAttribute(string tableName)
        {
            TableName = tableName;
        }
    }
}
