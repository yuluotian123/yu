# Config Module

Config Module 负责运行时配置表加载、缓存、查询、重载和卸载。编辑期数据由 `addons/ConfigPlugin` 生成，运行时默认通过 `JsonConfigLoader` 从 `ConfigSetting.TablePath` 读取 JSON。

## 核心类型

- `IConfigModule`：业务侧使用的配置查询入口。
- `ConfigModule`：表加载、缓存和重载实现。
- `ConfigRow`：所有生成配置行的基类。
- `ConfigTable<T>`：按 ID 索引的只读配置表。
- `ConfigTableAttribute`：声明表名等元数据。
- `IConfigLoader` / `JsonConfigLoader`：配置数据来源。
- `ConfigTypeRegistry`：转换器字段类型处理注册表。

## 快速开始

```csharp
IConfigModule config = ModuleSystem.GetModule<IConfigModule>();

config.LoadTable<ItemConfig>();
ItemConfig item = config.GetById<ItemConfig>(1001);
IReadOnlyList<ItemConfig> allItems = config.GetAll<ItemConfig>();
```

异步预加载：

```csharp
await config.PreloadTablesAsync(
    maxConcurrency: 4,
    typeof(ItemConfig),
    typeof(CharacterConfig));
```

## 生命周期与缓存

- 表第一次加载后缓存在模块内。
- `GetById<T>()`、`GetAll<T>()` 和 `GetTable<T>()` 要求对应表已经加载。
- `ReloadTable<T>()` 用于开发期刷新单表。
- `UnloadTable<T>()` 和 `UnloadAll()` 清理表缓存。
- `SetLoader()` 可替换 JSON、本地数据库或远端配置实现。

## 编辑期生成

配置生成链位于 `converter/`：

```text
XlsxReader -> XlsxTableData -> JsonDataWriter / CSharpCodeGenerator
```

生成类型放在 `scripts/generated/config/`，不要直接修改生成文件。

## 当前注意事项

- 表加载失败、重复 ID、字段类型转换失败需要明确区分并提供表名/行号上下文。
- 异步加载和同步加载共享缓存，必须保证同表并发请求不会重复提交或覆盖。
- `SetLoader()` 后已加载表不会自动切换数据源，需要显式重载。
- 表之间的引用当前主要依赖 ID，建议在加载后增加引用完整性验证。
- 转换器和运行时 loader 应共享 schema/version 约定，避免生成与读取规则漂移。

## 相关文档

- [`addons/ConfigPlugin/README.md`](../../../addons/ConfigPlugin/README.md)
- [`scripts/generated/config/`](../../generated/config/)

