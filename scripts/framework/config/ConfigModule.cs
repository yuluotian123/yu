using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Framework
{
    /// <summary>
    /// 配置表管理模块实现。
    /// </summary>
    internal sealed class ConfigModule : Module, IConfigModule
    {
        private readonly Dictionary<Type, IConfigTableBox> _tables = new();
        private readonly Dictionary<Type, SemaphoreSlim> _tableGates = new();
        private readonly object _stateLock = new();

        private IConfigLoader _loader;
        private ConfigSetting _setting;

        public override int Priority => 0;

        public int LoadedTableCount
        {
            get
            {
                lock (_stateLock)
                {
                    return _tables.Count;
                }
            }
        }

        public override void OnInit()
        {
            _setting = RootModule.Instance?.settings?.configSetting ?? new ConfigSetting();
            _loader ??= new JsonConfigLoader(_setting.TablePath);

            Debugger.Info($"[ConfigModule] 初始化完成。TablePath={_setting.TablePath}");
        }

        public override void Shutdown()
        {
            lock (_stateLock)
            {
                _tables.Clear();
            }

            Debugger.Info("[ConfigModule] 已关闭。");
        }

        public void LoadTable<T>() where T : ConfigRow
        {
            EnsureTableLoadedAsync<T>().GetAwaiter().GetResult();
        }

        public Task LoadTableAsync<T>() where T : ConfigRow
        {
            return EnsureTableLoadedAsync<T>();
        }

        public void PreloadTables(params Type[] tableTypes)
        {
            PreloadTablesAsync(tableTypes).GetAwaiter().GetResult();
        }

        public Task PreloadTablesAsync(params Type[] tableTypes)
        {
            return PreloadTablesAsync(maxConcurrency: 4, tableTypes);
        }

        public async Task PreloadTablesAsync(int maxConcurrency, params Type[] tableTypes)
        {
            if (tableTypes == null || tableTypes.Length == 0)
                return;

            if (maxConcurrency <= 0)
                maxConcurrency = 1;

            var uniqueTypes = new HashSet<Type>();
            var validTypes = new List<Type>(tableTypes.Length);

            foreach (var tableType in tableTypes)
            {
                if (tableType == null || !typeof(ConfigRow).IsAssignableFrom(tableType))
                    continue;

                if (!uniqueTypes.Add(tableType))
                    continue;

                validTypes.Add(tableType);
            }

            using var limiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var tasks = new List<Task>(validTypes.Count);

            foreach (var tableType in validTypes)
            {
                tasks.Add(PreloadSingleTableWithLimitAsync(tableType, limiter));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task PreloadSingleTableWithLimitAsync(Type tableType, SemaphoreSlim limiter)
        {
            await limiter.WaitAsync().ConfigureAwait(false);
            try
            {
                await InvokeEnsureTableLoadedAsync(tableType, forceReload: false).ConfigureAwait(false);
            }
            finally
            {
                limiter.Release();
            }
        }

        public T GetById<T>(int id) where T : ConfigRow
        {
            var table = GetOrLoadTable<T>();
            table.TryGetById(id, out var row);
            return row;
        }

        public IReadOnlyList<T> GetAll<T>() where T : ConfigRow
        {
            return GetOrLoadTable<T>().List;
        }

        public ConfigTable<T> GetTable<T>() where T : ConfigRow
        {
            return GetOrLoadTable<T>();
        }

        public void ReloadTable<T>() where T : ConfigRow
        {
            ReloadTableAsync<T>().GetAwaiter().GetResult();
        }

        public async Task ReloadTableAsync<T>() where T : ConfigRow
        {
            await EnsureTableLoadedAsync<T>(forceReload: true).ConfigureAwait(false);
            Debugger.Info($"[ConfigModule] 热重载完成：{typeof(T).Name}");
        }

        public void ReloadAll()
        {
            ReloadAllAsync().GetAwaiter().GetResult();
        }

        public async Task ReloadAllAsync()
        {
            List<Type> types;
            lock (_stateLock)
            {
                types = [.. _tables.Keys];
            }

            foreach (var type in types)
            {
                await InvokeEnsureTableLoadedAsync(type, forceReload: true).ConfigureAwait(false);
            }

            Debugger.Info($"[ConfigModule] 热重载全部 {types.Count} 张表完成。");
        }

        public void UnloadTable<T>() where T : ConfigRow
        {
            var type = typeof(T);
            var gate = GetTableGate(type);
            gate.Wait();
            try
            {
                lock (_stateLock)
                {
                    if (_tables.Remove(type))
                        Debugger.Info($"[ConfigModule] 已卸载：{type.Name}");
                }
            }
            finally
            {
                gate.Release();
            }
        }

        public void UnloadAll()
        {
            var gates = SnapshotTableGates();
            foreach (var gate in gates)
                gate.Wait();

            try
            {
                lock (_stateLock)
                {
                    _tables.Clear();
                }
            }
            finally
            {
                foreach (var gate in gates)
                    gate.Release();
            }

            Debugger.Info("[ConfigModule] 已卸载所有配置表。");
        }

        public void SetLoader(IConfigLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        private ConfigTable<T> GetOrLoadTable<T>() where T : ConfigRow
        {
            return EnsureTableLoadedAsync<T>().GetAwaiter().GetResult();
        }

        private async Task<ConfigTable<T>> EnsureTableLoadedAsync<T>(bool forceReload = false) where T : ConfigRow
        {
            var type = typeof(T);
            if (!forceReload && TryGetLoadedTable(type, out ConfigTable<T> cachedTable))
                return cachedTable;

            var gate = GetTableGate(type);
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!forceReload && TryGetLoadedTable(type, out cachedTable))
                    return cachedTable;

                return await LoadTableInternalAsync<T>().ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<ConfigTable<T>> LoadTableInternalAsync<T>() where T : ConfigRow
        {
            var type = typeof(T);
            var tableName = GetTableName(type);
            var rows = await _loader.LoadAsync<T>(tableName).ConfigureAwait(false);
            var table = new ConfigTable<T>(rows);

            lock (_stateLock)
            {
                _tables[type] = new ConfigTableBox<T>(table);
            }

            Debugger.Info($"[ConfigModule] 加载完成：{type.Name}（表名={tableName}，{table.Count} 行）");
            return table;
        }

        private Task InvokeEnsureTableLoadedAsync(Type type, bool forceReload)
        {
            var method = typeof(ConfigModule)
                .GetMethod(nameof(EnsureTableLoadedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (Task)method.MakeGenericMethod(type).Invoke(this, new object[] { forceReload })!;
        }

        private bool TryGetLoadedTable<T>(Type type, out ConfigTable<T> table) where T : ConfigRow
        {
            lock (_stateLock)
            {
                if (_tables.TryGetValue(type, out var box))
                {
                    table = ((ConfigTableBox<T>)box).Table;
                    return true;
                }
            }

            table = null;
            return false;
        }

        private SemaphoreSlim GetTableGate(Type type)
        {
            lock (_stateLock)
            {
                if (_tableGates.TryGetValue(type, out var gate))
                    return gate;

                gate = new SemaphoreSlim(1, 1);
                _tableGates[type] = gate;
                return gate;
            }
        }

        private List<SemaphoreSlim> SnapshotTableGates()
        {
            lock (_stateLock)
            {
                return new List<SemaphoreSlim>(_tableGates.Values);
            }
        }

        private static string GetTableName(Type type)
        {
            var attr = type.GetCustomAttribute<ConfigTableAttribute>();
            if (attr != null && !string.IsNullOrWhiteSpace(attr.TableName))
                return attr.TableName;

            var name = type.Name;
            return name.EndsWith("Config", StringComparison.Ordinal)
                ? name[..^6].ToLowerInvariant()
                : name.ToLowerInvariant();
        }

        private interface IConfigTableBox
        {
        }

        private sealed class ConfigTableBox<T> : IConfigTableBox where T : ConfigRow
        {
            public ConfigTable<T> Table { get; }

            public ConfigTableBox(ConfigTable<T> table)
            {
                Table = table;
            }
        }
    }
}
