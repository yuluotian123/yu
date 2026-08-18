# Save Module

Save Module 把所有已注册的 `ISaveable` 对象写入同一个 JSON 存档槽，并在加载时按 `SaveKey` 把数据恢复到对应对象。

## 核心类型

- `ISaveModule`：注册、保存、加载、删除和查询存档槽。
- `ISaveable`：可存档对象协议。
- `SaveModule`：注册表和文件读写实现。
- `AutoStateSerializer`：对象状态序列化。
- `SerializationComponent2D/3D`：场景对象快照辅助。

## 注册可存档对象

```csharp
ISaveModule saves = ModuleSystem.GetModule<ISaveModule>();
saves.Register(playerSaveData);

saves.Save(slot1);
bool loaded = saves.Load(slot1);

saves.Unregister(playerSaveData);
```

每个 `ISaveable.SaveKey` 必须唯一。保存时先调用对象的 `Save()`，再序列化其状态；加载时先回写字段，再调用对象的 `Load()`。

## 文件格式

当前路径为：

```text
res://saves/{slot}.json
```

顶层 JSON 以 `SaveKey` 为键，每个注册对象保存一个独立数据块。

## 生命周期约定

- 长生命周期系统在初始化时注册，在关闭时取消注册。
- 场景对象应保证加载存档时已经完成注册。
- 删除存档前应停止可能同时进行的保存操作。
- 新版本增加字段时提供默认值；删除或改名字段时需要迁移。

## 当前注意事项

- **高优先级**：正式构建不应写入 `res://`，应迁移到 `user://saves`，并兼容读取旧路径。
- slot 名称未经严格校验，需阻止路径分隔符和非法文件名。
- 写入不是原子操作，进程中断可能损坏存档；建议临时文件写入后替换。
- 单个对象序列化失败可能影响整槽保存，需要错误隔离和结果对象。
- 缺少 schema version、备份、校验和与损坏存档恢复策略。

## Save V2

Save V2 writes to `user://saves/{slot}.json` and keeps `res://saves` as a read-only fallback for old slots. The root contains `meta`, `legacy`, and `sections` objects. Character state is registered through `ISaveSection` and stored as `sections.characters/{PersistentId}` with a section schema version.

Writes use a temporary file followed by a backup and replacement. Character loading is safe before a level scene exists: `SaveModule` keeps pending sections and applies them when `CharacterPersistenceComponent2D` registers during scene initialization.
