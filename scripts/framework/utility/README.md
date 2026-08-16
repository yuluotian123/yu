# Framework Utility

`scripts/framework/utility` 保存不属于具体模块、但被多个框架系统复用的轻量工具。

## 当前工具

- `GameTime`：缩放时间、非缩放时间、物理时间、平滑 delta 和实时钟。
- `JsonHelper`：基于 `System.Text.Json` 的通用序列化辅助。
- `Debugger`：对 Godot 日志的简单开关封装。
- `TypeNamePair`：以 `Type + Name` 作为字典键，供 FSM 和对象池使用。
- `ViewportInputUtility`：Viewport/Input 相关辅助逻辑。

## GameTime

项目入口应分别转发普通帧和物理帧：

```csharp
gameTime.OnProcess(delta);
gameTime.OnPhysicsProcess(delta);
```

修改 `TimeScale` 会同步 Godot `Engine.TimeScale`。需要不受暂停或缩放影响的逻辑时使用 `UnscaledDeltaTime` 或实时钟。

## JsonHelper

```csharp
string json = JsonHelper.Serialize(data);
SaveData loaded = JsonHelper.Deserialize<SaveData>(json);
```

该工具适合普通 DTO，不负责 GraphPlugin 的多态图序列化；图数据必须使用 `GraphJsonHelper`。

## 当前注意事项

- `Debugger` 名称容易与 `System.Diagnostics.Debugger` 混淆，建议后续统一为项目日志服务。
- `JsonHelper` 需要明确异常、未知字段、枚举和版本迁移策略。
- Utility 应保持无业务依赖；功能增长后应迁移到职责明确的模块。
- 时间、JSON 和输入辅助需要独立测试，避免成为无验证的全局依赖。
