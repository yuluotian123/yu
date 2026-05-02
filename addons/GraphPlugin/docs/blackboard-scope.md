# Blackboard Scope

GraphPlugin 的黑板规则尽量保持一句话：谁使用，谁声明。

例如父级 HFSM 只负责 `Grounded / Airborne / Dash / Attack` 这种大状态，而 `IsOnFloor`、`MoveAxisX`、`VelocityY` 只被 locomotion 子图消费，那么这些输入就应该声明在 locomotion 子图黑板里。父图不需要为了转发数据而声明一堆自己不用的 key。

## 运行时写入

外部系统仍然只调用根运行时：

```csharp
_hfsm.SetValue("IsOnFloor", isOnFloor);
_hfsm.SetValue("MoveAxisX", moveAxisX);
```

`StateGraphRuntime` 和 `FlowGraphRuntime` 都接入了 `IGraphRuntimeScope`。写入时会走 `GraphRuntimeBlackboardWriter`：

1. 先查当前运行时的本地图黑板。
2. 再递归查正在运行的子图黑板。
3. 找到声明者就写入声明者。
4. 所有本地图都没声明时，才回到根黑板的普通写入规则，也就是写全局黑板或创建当前图本地 key。

这样调用方不需要知道 key 在父图、子图还是更深层子图。

## 接口含义

`IGraphRuntimeScope` 不是图节点接口，而是运行时作用域接口：

```csharp
public interface IGraphRuntimeScope
{
    GraphExecutionContext Context { get; }
    IEnumerable<IGraphRuntimeScope> ChildScopes { get; }
}
```

- `Context` 提供当前图、黑板和业务对象。
- `ChildScopes` 提供当前正在运行的子图 runtime。

业务层通常不直接实现它；只有新的图运行时类型需要实现。

## 推荐做法

- 节点读写自己图里实际消费的 key。
- 父图只声明父图条件会用到的 key。
- 子图只声明子图条件和节点会用到的 key。
- 不要为了方便输入而把所有 key 都堆到根图。
- 如果确实是跨图共享状态，再放到场景级 `GraphBlackboardNode`。
