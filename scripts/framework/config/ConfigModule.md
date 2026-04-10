# 配置表模块（ConfigModule）

> 参考 [Luban](https://github.com/focus-creative-games/luban) 和 [Tengine](https://github.com/Tencent/Tars) 设计，支持 xlsx 读取、JSON 序列化和 C# 代码生成的配置表模块。

---

## 目录

- [整体架构](#整体架构)
- [xlsx 格式约定](#xlsx-格式约定)
- [支持的类型](#支持的类型)
- [编辑器插件使用（推荐）](#编辑器插件使用推荐)
- [运行时接口使用](#运行时接口使用)
- [目录结构](#目录结构)
- [扩展 C# 配置类](#扩展-c-配置类)
- [热重载](#热重载)
- [注意事项](#注意事项)

---

## 整体架构

```
xlsx 文件
   │
   ▼
XlsxReader          ← 读取表格结构和数据
   │
   ├──▶ CSharpCodeGenerator  → 生成 MonsterConfig.cs（编辑期）
   │
   └──▶ JsonDataWriter        → 生成 monster.json（编辑期）
                                       │
                                       ▼
                              JsonConfigLoader      ← 运行时加载 JSON
                                       │
                                       ▼
                              ConfigModule<T>       ← 缓存 / 查询 / 热重载
```

---

## xlsx 格式约定

每张配置表对应一个 Sheet，行的含义如下：

| 行号 | 含义 | 示例 |
|------|------|------|
| 第 1 行 | **字段名**（驼峰 / snake_case，首列必须为 `id`） | `id` / `name` / `drop_items` |
| 第 2 行 | **字段类型** | `int` / `string` / `list<int>` |
| 第 3 行 | **注释**（生成到 C# `<summary>`） | `怪物ID` / `掉落物列表` |
| 第 4 行起 | **数据**（全空行自动跳过） | `1001` / `史莱姆` / `1;2;3` |

### 示例

| id | name | hp | drop_items |
|----|------|----|------------|
| int | string | int | list\<int\> |
| 怪物ID | 名称 | 血量 | 掉落物品ID列表 |
| 1001 | 史莱姆 | 100 | 1;2 |
| 1002 | 哥布林 | 200 | 3 |

---

## 支持的类型

| 类型字符串 | C# 类型 | 说明 |
|-----------|---------|------|
| `int` | `int` | 32 位整数 |
| `long` | `long` | 64 位整数 |
| `float` | `float` | 单精度浮点 |
| `double` | `double` | 双精度浮点 |
| `bool` | `bool` | `true` / `false` / `1` / `0` |
| `string` | `string` | 字符串 |
| `list<T>` | `List<T>` | 列表，单元格内用 `;` 分隔，如 `1;2;3` |
| `ref<T>` | `int` | 外键引用（存储 id，运行时按需查询） |

---

## 编辑器插件使用（推荐）

### 启用插件

1. 在 Godot 编辑器菜单中选择 **项目 → 项目设置 → 插件**
2. 找到 `ConfigPlugin`，勾选 **启用**

### 使用步骤

1. 点击编辑器顶部菜单 **工具 → 转换配置表...**
2. 在弹出的窗口中配置以下路径：

| 输入项 | 说明 | 默认值 |
|--------|------|--------|
| xlsx 源文件夹 | 存放 xlsx 文件的目录 | `res://assets/config/xlsx/` |
| JSON 输出目录 | 运行时 JSON 数据的输出目录 | `res://assets/config/tables/` |
| C# 代码输出目录 | 生成的 C# 类文件目录 | `res://scripts/generated/config/` |
| C# 命名空间 | 生成类的命名空间 | `Generated.Config` |

3. 点击 **浏览...** 按钮选择文件夹，或直接填写 `res://` 路径
4. 点击 **开始转换**，窗口下方的日志区会显示每张表的结果
5. 转换完成后编辑器文件系统自动刷新

### 转换结果示例

```
[成功] [monster] 50 行 / 8 字段 → JSON: D:\project\assets\config\tables\monster.json → CS: D:\project\scripts\generated\config\MonsterConfig.cs
[成功] [item] 200 行 / 12 字段 → JSON: ...
转换完成：成功 3 个，失败 0 个。
```

---

## 运行时接口使用

### 预加载（推荐在场景加载时调用）

```csharp
var cfg = ModuleSystem.GetModule<IConfigModule>();

// 显式指定要预加载的表（推荐，完全可控）
cfg.PreloadTables(typeof(MonsterConfig), typeof(ItemConfig), typeof(SkillConfig));
```

### 按 ID 查询

```csharp
var cfg = ModuleSystem.GetModule<IConfigModule>();

// 首次调用时懒加载，之后从缓存返回
var monster = cfg.GetById<MonsterConfig>(1001);
GD.Print(monster.Name);   // 输出：史莱姆
```

### 获取全部数据

```csharp
var allMonsters = cfg.GetAll<MonsterConfig>();
foreach (var m in allMonsters)
    GD.Print($"{m.Id} - {m.Name} HP:{m.Hp}");
```

### 获取表对象

```csharp
var table = cfg.GetTable<MonsterConfig>();
GD.Print(table.Count);   // 行数
```

### 卸载

```csharp
cfg.UnloadTable<MonsterConfig>();   // 卸载单张表
cfg.UnloadAll();                    // 卸载全部
```

---

## 目录结构

```
scripts/framework/config/
├── IConfigModule.cs                  # 对外接口
├── ConfigModule.cs                   # 模块实现
├── ConfigSetting.cs                  # 编辑器配置（TablePath）
├── core/
│   ├── ConfigRow.cs                  # 配置行基类（含 Id 属性）
│   ├── ConfigTable.cs                # 泛型表容器
│   ├── ConfigTableAttribute.cs       # [ConfigTable("tableName")] 特性
│   └── typehandler/
│       ├── IConfigTypeHandler.cs     # 类型处理器接口
│       ├── ConfigTypeRegistry.cs     # 类型注册中心
│       ├── PrimitiveTypeHandler.cs   # int/float/bool/string 等
│       ├── ListTypeHandler.cs        # list<T>
│       └── RefTypeHandler.cs         # ref<T>（外键）
├── loader/
│   ├── IConfigLoader.cs              # 加载器接口
│   └── JsonConfigLoader.cs           # JSON 文件加载器
└── converter/                        # 编辑期工具（不参与运行时）
    ├── XlsxReader.cs                 # xlsx 读取/解析
    ├── CSharpCodeGenerator.cs        # C# 代码生成
    ├── JsonDataWriter.cs             # JSON 数据生成
    └── XlsxConverter.cs             # 转换入口（单文件/批量目录）

addons/ConfigPlugin/
├── plugin.cfg                        # Godot 插件声明
├── ConfigPlugin.cs                   # 插件入口（注册工具菜单项）
└── ConfigConverterWindow.cs          # 转换窗口 UI
```

---

## 扩展 C# 配置类

> ⚠️ **生成的文件禁止手动修改**，再次转换会覆盖。

如需为配置类添加额外方法或属性，请新建一个同名的 `partial class`：

```csharp
// 文件：MonsterConfig.Extension.cs（手动创建，不受生成影响）
namespace Generated.Config
{
    public partial class MonsterConfig
    {
        /// <summary>是否为精英怪（血量 > 500）</summary>
        public bool IsElite => Hp > 500;

        /// <summary>获取该怪物所有掉落物配置。</summary>
        public IEnumerable<ItemConfig> GetDropItems(IConfigModule cfg)
        {
            foreach (var id in DropItems)
                yield return cfg.GetById<ItemConfig>(id);
        }
    }
}
```

---

## 热重载

开发期修改 xlsx 后重新转换，可在不重启游戏的情况下热重载数据：

```csharp
var cfg = ModuleSystem.GetModule<IConfigModule>();

cfg.ReloadTable<MonsterConfig>();   // 重载单张表
cfg.ReloadAll();                    // 重载已加载的全部表
```

---

## 注意事项

1. **第一列必须是 `id`**（类型为 `int`），且每行的 id 必须唯一。
2. **Excel 临时文件**（以 `~` 开头）会被自动跳过。
3. **list\<T\> 分隔符**默认为 `;`，单元格填写示例：`1;2;3`。
4. **ref\<T\>** 类型在 C# 中生成为 `int`（存储目标表的 id），运行时需手动调用 `cfg.GetById<T>(id)` 查询。
5. `ConfigSetting.TablePath` 默认为 `res://assets/config/tables/`，可在编辑器的 **Settings** 资源中修改。
6. 预加载推荐使用 `cfg.PreloadTables(typeof(X), ...)` 显式指定，不要依赖反射扫描。
