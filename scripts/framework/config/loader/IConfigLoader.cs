using System.Collections.Generic;
using System.Threading.Tasks;

namespace Framework
{
    /// <summary>
    /// 配置表数据加载器接口。
    /// <para>
    /// 负责从数据源（JSON 文件、xlsx、网络等）读取并反序列化指定表的全部数据行。
    /// 反序列化结果直接为强类型 <see cref="ConfigRow"/> 子类列表，
    /// 使 <see cref="ConfigModule"/> 只需专注于表的存取管理。
    /// </para>
    /// <para>
    /// 框架内置 <see cref="JsonConfigLoader"/>（从 res:// JSON 文件加载）。
    /// 可通过 <see cref="IConfigModule.SetLoader"/> 替换为自定义实现。
    /// </para>
    /// </summary>
    public interface IConfigLoader
    {
        /// <summary>
        /// 加载指定表名对应的全部数据行并反序列化为强类型列表。
        /// </summary>
        /// <typeparam name="T">配置行类型，继承自 <see cref="ConfigRow"/>。</typeparam>
        /// <param name="tableName">表名（不含扩展名），对应数据文件名。</param>
        /// <returns>数据行列表；读取或解析失败时返回空列表。</returns>
        List<T> Load<T>(string tableName) where T : ConfigRow;

        Task<List<T>> LoadAsync<T>(string tableName) where T : ConfigRow
            => Task.FromResult(Load<T>(tableName));
    }
}
