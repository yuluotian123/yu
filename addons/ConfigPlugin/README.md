# ConfigPlugin

ConfigPlugin 是项目内的 Godot C# 编辑器插件，用于把 `.xlsx` 配置表批量转换为运行时 JSON 数据和 C# 配置行类型。转换核心复用 `scripts/framework/config/converter/`，插件本身只负责编辑器入口、路径设置、进度展示和错误反馈。

## 主要能力

- 在 Godot 顶部工具栏显示配置目录按钮。
- 选择 xlsx 输入目录、JSON 输出目录和 C# 输出目录。
- 单表或批量转换 `.xlsx` 文件。
- 生成供 `JsonConfigLoader` 使用的 JSON。
- 生成继承 `ConfigRow` 的 C# 类型。
- 支持编辑器扩展热重载后重建按钮和窗口引用。

## 使用方式

1. 确认 `project.godot` 已启用 `addons/ConfigPlugin/plugin.cfg`。
2. 点击编辑器顶部的文件夹按钮打开转换窗口。
3. 设置 xlsx 目录、JSON 输出目录和 C# 输出目录。
4. 执行转换并检查窗口中的成功或失败信息。
5. 等待 Godot 完成 C# 重新编译，再通过 `IConfigModule` 加载生成表。

推荐输出位置：

- JSON：`res://assets/config/tables/`
- C#：`res://scripts/generated/config/`

`scripts/generated/config/` 是生成目录，不应手工编辑；需要改变字段或类型时应修改源表或转换器。

## 数据流

```text
xlsx
  -> XlsxReader
  -> XlsxTableData
  -> JsonDataWriter -> *.json
  -> CSharpCodeGenerator -> *.cs
  -> JsonConfigLoader / ConfigModule
```

## 与 ConfigModule 的关系

ConfigPlugin 负责编辑期生成，`scripts/framework/config` 负责运行时加载与查询。插件不应依赖具体业务表类型，运行时模块也不应依赖编辑器窗口。

```csharp
IConfigModule config = ModuleSystem.GetModule<IConfigModule>();
config.LoadTable<ItemConfig>();

ItemConfig item = config.GetById<ItemConfig>(1001);
```

## 当前注意事项

- 转换会写入目标目录，提交前检查生成 diff，避免误覆盖手工文件。
- `.xlsx` 文件格式、字段行和类型字符串必须符合转换器约定。
- 生成 C# 后可能触发 Godot 域重载；插件窗口和工具栏必须保持热重载安全。
- 转换失败应保留原文件，不能留下半写入的 JSON/C# 结果。
- 插件版本、支持的表格式和生成 schema 目前没有独立版本号，后续格式升级需要显式兼容策略。

## 相关代码

- `ConfigPlugin.cs`：插件生命周期和工具栏入口。
- `ConfigConverterWindow.cs`：路径配置与转换窗口。
- `scripts/framework/config/converter/`：xlsx 读取、JSON 和 C# 生成。
- `scripts/framework/config/`：运行时配置模块。

