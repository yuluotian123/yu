using System;
using System.Collections.Generic;

namespace Framework
{
    /// <summary>
    /// 配置表容器，持有某张表的全部数据行，支持按主键 ID 快速查询。
    /// </summary>
    /// <typeparam name="T">配置行类型，必须继承 <see cref="ConfigRow"/>。</typeparam>
    public sealed class ConfigTable<T> where T : ConfigRow
    {
        private readonly Dictionary<int, T> _dict;
        private readonly List<T> _list;

        /// <summary>
        /// 以主键为键的只读字典。
        /// </summary>
        public IReadOnlyDictionary<int, T> Dict => _dict;

        /// <summary>
        /// 保持原始顺序的只读列表。
        /// </summary>
        public IReadOnlyList<T> List => _list;

        /// <summary>
        /// 数据行总数。
        /// </summary>
        public int Count => _list.Count;

        /// <summary>
        /// 使用数据行列表构造配置表容器。
        /// </summary>
        /// <param name="rows">从加载器获得的数据行集合。</param>
        public ConfigTable(IList<T> rows)
        {
            _list = new List<T>(rows.Count);
            _dict = new Dictionary<int, T>(rows.Count);

            foreach (var row in rows)
            {
                if (row == null) continue;
                _list.Add(row);
                if (_dict.ContainsKey(row.Id))
                {
                    Debugger.Warn($"[ConfigTable<{typeof(T).Name}>] 重复的主键 Id={row.Id}，后者将覆盖前者。");
                }
                _dict[row.Id] = row;
            }
        }

        /// <summary>
        /// 按主键 ID 查询数据行。找不到时抛出异常。
        /// </summary>
        /// <param name="id">主键 ID。</param>
        /// <returns>对应数据行。</returns>
        /// <exception cref="KeyNotFoundException">当 ID 不存在时抛出。</exception>
        public T GetById(int id)
        {
            if (_dict.TryGetValue(id, out var row))
                return row;
            throw new KeyNotFoundException($"[ConfigTable<{typeof(T).Name}>] 找不到 Id={id} 的配置行。");
        }

        /// <summary>
        /// 尝试按主键 ID 查询数据行。
        /// </summary>
        /// <param name="id">主键 ID。</param>
        /// <param name="row">查询结果，找不到时为 null。</param>
        /// <returns>是否找到。</returns>
        public bool TryGetById(int id, out T row)
        {
            return _dict.TryGetValue(id, out row);
        }

        /// <summary>
        /// 判断指定 ID 是否存在。
        /// </summary>
        public bool Contains(int id) => _dict.ContainsKey(id);
    }
}
