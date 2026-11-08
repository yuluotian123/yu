<a id="coding-patterns"></a>
# 编码约定

<a id="gameplay-ec-structure"></a>
## 玩法的 EC 结构

添加玩法行为或修改场景宿主前，先阅读[实体与组件框架](ec-framework.md)。沿用已有 GameObject 宿主、Component 资源及原生子节点。

<a id="godot-c-conventions"></a>
## Godot C# 约定

- 继承 `Node`、`Resource` 或其他 Godot 类型的脚本类使用 `partial class`。
- 需要由 Godot 编辑或序列化的属性使用 `[Export]`。
- 除非任务包含迁移，否则保留 Godot 生命周期方法、信号连接和导出属性名称。
- 项目素材与资源优先使用 Godot 的 `res://` 路径。
- 参照附近代码选择 `ResourceLoader` 或项目资源模块。

<a id="project-boundaries"></a>
## 项目边界

- 可复用框架系统放在 `scripts/framework`，通常使用 `Framework` 命名空间。
- 游戏特有行为放在 `scripts/gamelogic`，通常使用 `GameLogic` 命名空间。
- 新增基础设施前，优先复用 `ModuleSystem`、资源模块 API、FSM API、UI 模块、任务系统、能力运行时和 AI 辅助类。
- 访问修饰符、空值处理、日志和注释应与附近文件风格一致。

<a id="generated-config"></a>
## 生成的配置代码

- `scripts/generated/config` 是生成产物目录。
- 不要为了修改行为而手工修改生成的配置类。
- 需要扩展行为时，依据现有模式选择独立的非生成类型、分部类、辅助类，或修改生成器。

<a id="verification"></a>
## 验证

- 修改代码后，在可行时运行 `dotnet build yu.csproj`。
- 验证玩法运行时行为时，优先使用 `scripts/tools/` 中现有的相关验证器；仅在任务确有需要时添加针对性的 Godot 检查。
