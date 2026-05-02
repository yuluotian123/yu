# GraphPlugin V2

GraphPlugin V2 是项目内的通用图编辑与运行框架。当前版本采用硬切 V2 架构：

- 图资源只使用 `GraphAsset.GraphJson` 一个字段保存完整图文档。
- 节点注册、图类型注册和类型解析统一由 `GraphTypeRegistry` 管理。
- 节点实例创建能力放在 `GraphNodeDefinition.CreateNode()`，不再保留旧 `GraphNodeFactory`。
- 编辑器窗口只负责装配 toolbar、GraphEdit 和信号转发，黑板、子图、连线编辑、保存、剪贴板、Explorer 都拆到独立服务或面板。
- FlowGraph、StateGraph、HFSM、MissionGraph 共享 core 的文档模型、黑板、验证和运行时索引。

## 文件夹结构

```text
addons/GraphPlugin/
├── docs/                  # GraphPlugin V2 中文文档
├── editor/
│   ├── controls/          # 通用编辑器控件，例如 SearchablePopup
│   ├── panels/            # 独立面板：Blackboard、Explorer
│   ├── services/          # 编辑器服务：Command、Save、Clipboard、SubGraph、Connection
│   └── windows/           # 编辑器窗口壳与 Godot Inspector 插件
├── runtime/
│   ├── blackboard/        # 黑板数据、运行时作用域和值类型
│   ├── core/              # 图文档、节点、连线、注册、验证、索引
│   ├── flow/              # FlowGraph 节点与运行时
│   └── state/             # StateGraph 节点、连线与运行时
└── samples/               # 示例节点
```

业务图类型位于项目脚本目录，例如：

- `scripts/gamelogic/hfsm/graph/`
- `scripts/gamelogic/skills/`
- `scripts/gamelogic/missions/mission_chains/`

## 文档入口

- [文档总览](docs/README.md)
- [整体架构](docs/architecture.md)
- [文件夹结构](docs/folder-structure.md)
- [创建图类型](docs/create-graph-type.md)
- [创建节点](docs/create-node.md)
- [编辑器架构](docs/editor-architecture.md)
- [序列化格式](docs/serialization.md)
- [运行时接入](docs/runtime.md)
- [编辑器工作流](docs/workflow.md)

