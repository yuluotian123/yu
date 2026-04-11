using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Framework
{
    /// <summary>
    /// 配置表管理模块接口。
    /// 通过 <see cref="ModuleSystem.GetModule{T}"/> 以此接口获取实例。
    /// <para>
    /// 功能概览：
    /// <list type="bullet">
    ///   <item>按需（懒加载）或预加载配置表数据。</item>
    ///   <item>按主键 ID 快速查询，或遍历全表。</item>
    ///   <item>支持热重载（开发环境修改数据后无需重启）。</item>
    ///   <item>可替换加载器策略（注入 <see cref="IConfigLoader"/> 自定义实现）。</item>
    /// </list>
    /// </para>
    /// <example>
    /// <code>
    /// var cfg = ModuleSystem.GetModule&lt;IConfigModule&gt;();
    ///
    /// // 按 ID 查询（首次访问时自动加载表）
    /// var monster = cfg.GetById&lt;MonsterConfig&gt;(1001);
    ///
    /// // 遍历全表
    /// foreach (var item in cfg.GetAll&lt;ItemConfig&gt;())
    ///     GD.Print(item.Name);
    /// </code>
    /// </example>
    /// </summary>
    public interface IConfigModule
    {
        /// <summary>
        /// 预加载指定类型对应的配置表。
        /// 若已加载则不重复加载。
        /// </summary>
        /// <typeparam name="T">配置行类型，必须标记 <see cref="ConfigTableAttribute"/>。</typeparam>
        void LoadTable<T>() where T : ConfigRow;

        Task LoadTableAsync<T>() where T : ConfigRow;

        /// <summary>
        /// 按类型数组批量预加载多张配置表。
        /// </summary>
        /// <param name="tableTypes">要预加载的配置行类型列表。</param>
        void PreloadTables(params Type[] tableTypes);

        Task PreloadTablesAsync(params Type[] tableTypes);
        Task PreloadTablesAsync(int maxConcurrency, params Type[] tableTypes);

        /// <summary>
        /// 按主键 ID 查询指定表中的数据行。
        /// 若表尚未加载，则自动加载。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <param name="id">主键 ID。</param>
        /// <returns>对应数据行；找不到时返回 null。</returns>
        T GetById<T>(int id) where T : ConfigRow;

        /// <summary>
        /// 获取指定表的全部数据行（保持原始顺序）。
        /// 若表尚未加载，则自动加载。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        IReadOnlyList<T> GetAll<T>() where T : ConfigRow;

        /// <summary>
        /// 获取指定类型对应的配置表容器（高级用法）。
        /// 若表尚未加载，则自动加载。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        ConfigTable<T> GetTable<T>() where T : ConfigRow;

        /// <summary>
        /// 热重载指定类型对应的配置表（重新从数据源读取）。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        void ReloadTable<T>() where T : ConfigRow;

        Task ReloadTableAsync<T>() where T : ConfigRow;

        /// <summary>
        /// 热重载所有已加载的配置表。
        /// </summary>
        void ReloadAll();

        Task ReloadAllAsync();

        /// <summary>
        /// 卸载指定类型对应的配置表，释放内存。
        /// 下次访问时将重新加载。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        void UnloadTable<T>() where T : ConfigRow;

        /// <summary>
        /// 卸载所有已加载的配置表。
        /// </summary>
        void UnloadAll();

        /// <summary>
        /// 替换数据加载器（必须在首次加载表之前调用）。
        /// </summary>
        /// <param name="loader">自定义加载器实现。</param>
        void SetLoader(IConfigLoader loader);

        /// <summary>
        /// 当前已加载的表数量。
        /// </summary>
        int LoadedTableCount { get; }
    }
}
