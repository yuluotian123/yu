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

### 生成与回滚

- 单表和编辑器批量转换先对全部输出目录加独占锁并恢复未完成事务，再读取、校验并生成整批内容。按固定顺序获取目录锁；占用时立即报错，进程退出会自动释放，不会无限等待。
- 发布前在每个输出目录的 `.config-converter/` 保存同一份版本化日志，暂存完整新文件和原文件快照并刷盘。全部准备完成后才逐个替换；出现异常时尝试逆序回滚。
- 强制结束进程后，下次转换自动恢复：没有提交完成标记的批次恢复整批旧内容；已提交的批次只清理日志。恢复本身被中断后也可重试。
- 不支持的类型、非法非空数字/布尔值、重复或空 ID、属性名冲突、目标文件冲突会中止整批生成，不再静默降级。空的可选数值仍使用 `0`，空布尔值使用 `false`。
- 输出不再包含生成时间戳。JSON 和 C# 内容未变化时都跳过写入，避免无意义的重新编译。
- Excel 临时文件被过滤；稀疏列、内联字符串及缺省的空注释行按实际单元格位置读取。
- 恢复检查日志副本、提交标记和 SHA-256 内容摘要。文件被外部修改、备份损坏或只选择了原批次的部分目录时，拒绝恢复并保留资料，不会直接覆盖冲突内容。

### 单独恢复

在转换窗口选回同一批次的 JSON/C# 输出目录，点击“恢复输出”。此操作不读取 xlsx，也不重新生成表。代码入口：

```csharp
int recovered = new XlsxConverter().RecoverOutputs(jsonOutputDir, csOutputDir);
```

路径使用文件系统路径，`res://` 需要先经 `ProjectSettings.GlobalizePath` 转换。提示目录不完整时，应包含日志列出的所有目录。发生冲突时先保留当前文件和 `.config-converter/`，人工核对后再重试；不要删除事务日志来绕过保护。目录里的 `lock` 文件会长期保留，文件存在不代表仍被占用。

这不是跨文件的原子读取协议：未参与锁协议的读取者在发布期间可能看到混合版本，编辑器仅在成功后主动扫描。锁只约束使用此转换器的写入者；不支持经符号链接或目录联接访问输出目录。刷盘和原子替换依赖文件系统及设备保证，已实测的是进程强制结束，未做真实断电、磁盘损坏或网络文件系统验证。

### 其他约束

- 转换会写入目标目录，提交前检查生成 diff，避免误覆盖手工文件。
- `.xlsx` 文件格式、字段行和类型字符串必须符合转换器约定。
- 生成 C# 后可能触发 Godot 域重载；插件窗口和工具栏必须保持热重载安全。
- 回归入口：`assets/scenes/graph_config_reliability_smoke.tscn`，文件样本仅创建在项目 `tmp/` 的独立目录。
- 独立进程故障测试：从项目根目录执行 `dotnet run --project tests/ConfigTransactionTests/ConfigTransactionTests.csproj`。测试仅终止自身子进程，样本位于项目 `tmp/` 独立目录，构建产物位于 `.godot/`。
- 插件版本、支持的表格式和生成 schema 目前没有独立版本号，后续格式升级需要显式兼容策略。

## 相关代码

- `ConfigPlugin.cs`：插件生命周期和工具栏入口。
- `ConfigConverterWindow.cs`：路径配置与转换窗口。
- `scripts/framework/config/converter/`：xlsx 读取、JSON 和 C# 生成。
- `scripts/framework/config/`：运行时配置模块。

