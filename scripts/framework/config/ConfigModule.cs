using System;
using System.Collections.Generic;
using System.Reflection;

namespace Framework
{
    /// <summary>
    /// 配置表管理模块实现。
    /// <para>
    /// 通过 <see cref="ModuleSystem.GetModule{T}"/> 以 <see cref="IConfigModule"/> 接口获取实例。
    /// 命名遵循框架约定：接口名去掉 'I' 前缀即为实现类名。
    /// </para>
    /// <para>
    /// 职责：维护已加载配置表的缓存字典，提供按类型的懒加载、查询、热重载和卸载。
    /// IO 和反序列化完全委托给 <see cref="IConfigLoader"/>（默认为 <see cref="JsonConfigLoader"/>）。
    /// </para>
    /// </summary>
    internal sealed class ConfigModule : Module, IConfigModule
    {
        // Type → 非泛型 IConfigTableBox 接口，用于统一存储不同泛型的 ConfigTable<T>
        private readonly Dictionary<Type, IConfigTableBox> _tables = new();
        private IConfigLoader _loader;
        private ConfigSetting _setting;

        public override int Priority => 0;

        public int LoadedTableCount => _tables.Count;

        // ── Module 生命周期 ──────────────────────────────────────────────────

        public override void OnInit()
        {
            _setting = RootModule.Instance?.settings?.configSetting ?? new ConfigSetting();
            _loader ??= new JsonConfigLoader(_setting.TablePath);

            Debugger.Info($"[ConfigModule] 初始化完成。TablePath={_setting.TablePath}");
        }

        public override void Shutdown()
        {
            _tables.Clear();
            Debugger.Info("[ConfigModule] 已关闭。");
        }

        // ── IConfigModule ────────────────────────────────────────────────────

        public void LoadTable<T>() where T : ConfigRow
        {
            var type = typeof(T);
            if (_tables.ContainsKey(type)) return;
            LoadTableInternal<T>();
        }

        public void PreloadTables(params Type[] tableTypes)
        {
            foreach (var t in tableTypes)
            {
                if (t == null || !typeof(ConfigRow).IsAssignableFrom(t)) continue;
                if (_tables.ContainsKey(t)) continue;

                // 通过反射调用泛型方法 LoadTableInternal<T>
                var method = typeof(ConfigModule)
                    .GetMethod(nameof(LoadTableInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(t);
                method.Invoke(this, null);
            }
        }

        public T GetById<T>(int id) where T : ConfigRow
        {
            var table = GetOrLoadTable<T>();
            table.TryGetById(id, out var row);
            return row;
        }

        public IReadOnlyList<T> GetAll<T>() where T : ConfigRow
            => GetOrLoadTable<T>().List;

        public ConfigTable<T> GetTable<T>() where T : ConfigRow
            => GetOrLoadTable<T>();

        public void ReloadTable<T>() where T : ConfigRow
        {
            _tables.Remove(typeof(T));
            LoadTableInternal<T>();
            Debugger.Info($"[ConfigModule] 热重载完成：{typeof(T).Name}");
        }

        public void ReloadAll()
        {
            var types = new List<Type>(_tables.Keys);
            _tables.Clear();
            foreach (var t in types)
            {
                var method = typeof(ConfigModule)
                    .GetMethod(nameof(LoadTableInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(t);
                method.Invoke(this, null);
            }
            Debugger.Info($"[ConfigModule] 热重载全部 {types.Count} 张表完成。");
        }

        public void UnloadTable<T>() where T : ConfigRow
        {
            if (_tables.Remove(typeof(T)))
                Debugger.Info($"[ConfigModule] 已卸载：{typeof(T).Name}");
        }

        public void UnloadAll()
        {
            _tables.Clear();
            Debugger.Info("[ConfigModule] 已卸载所有配置表。");
        }

        public void SetLoader(IConfigLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        // ── 私有辅助 ──────────────────────────────────────────────────────────

        /// <summary>获取已加载的表容器，若不存在则先加载。</summary>
        private ConfigTable<T> GetOrLoadTable<T>() where T : ConfigRow
        {
            var type = typeof(T);
            if (_tables.TryGetValue(type, out var box))
                return ((ConfigTableBox<T>)box).Table;
            return LoadTableInternal<T>();
        }

        /// <summary>执行实际加载：获取表名 → 调加载器 → 构建 ConfigTable → 缓存。</summary>
        private ConfigTable<T> LoadTableInternal<T>() where T : ConfigRow
        {
            var type = typeof(T);
            var tableName = GetTableName(type);

            var rows = _loader.Load<T>(tableName);
            var table = new ConfigTable<T>(rows);
            _tables[type] = new ConfigTableBox<T>(table);

            Debugger.Info($"[ConfigModule] 加载完成：{type.Name}（表名={tableName}，{table.Count} 行）");
            return table;
        }

        /// <summary>从 <see cref="ConfigTableAttribute"/> 获取表名；无特性则用类名。</summary>
        private static string GetTableName(Type type)
        {
            var attr = type.GetCustomAttribute<ConfigTableAttribute>();
            if (attr != null && !string.IsNullOrWhiteSpace(attr.TableName))
                return attr.TableName;

            // 回退：用类名（去掉尾部 "Config"）
            var name = type.Name;
            return name.EndsWith("Config") ? name[..^6].ToLowerInvariant() : name.ToLowerInvariant();
        }

        // ── 内部类型擦除接口 ──────────────────────────────────────────────────

        /// <summary>用于在非泛型字典中存储任意 ConfigTable&lt;T&gt; 的类型擦除接口。</summary>
        private interface IConfigTableBox { }

        private sealed class ConfigTableBox<T> : IConfigTableBox where T : ConfigRow
        {
            public ConfigTable<T> Table { get; }
            public ConfigTableBox(ConfigTable<T> table) => Table = table;
        }
    }
}
