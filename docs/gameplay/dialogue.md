# NPC 对话

对话使用现有 GraphPlugin 编辑器，业务代码在 `GameLogic` 中。`DialogueGraphAsset.GraphJson` 是唯一图数据源；每次播放创建独立的 `DialogueSession` 快照。台词、选项和历史不写回资源。

## 试玩

- 在林间镇靠近灰绿居民，看到 `[ E ] … · 交谈` 后按 E。
- 鼠标左键、空格、Enter 或手柄确认：先补全当前文字，再推进。方向键/手柄方向选择选项，确认提交。选项不会被自动模式跳过。
- 「自动」在文字完整显示后等待再继续；「回看」显示本次完整对话和已选选项，打开时暂停自动播放。
- 「结束交谈」或 Esc 取消；`AllowCancel = false` 时仅能按剧情走到结束。对话对象被移除、切换关卡、界面加载失败时仍会清理输入锁。

全屏阅读区使用透明的 NVL 排版：直接在实时场景上叠加文字，台词和回看没有暗幕、底板或边框；选项保留各自的底板和选中高亮。用字形描边和阴影保持可读性；选择选项或打开回看时暂时隐藏正文，避免文字叠在一起。旁白和台词逐段累积，溢出自动换页。单条长台词会按实际字体和阅读区宽高分成多页；显式换页使用台词的「开始新页」。中文阅读字体通过 SystemFont 优先选择宋体/思源宋体，按窗口尺寸缩放。

## 在 NPC 图中接入

1. 创建 `DialogueGraphAsset` 资源，在 Inspector 中打开图编辑器。
2. 建立「开始对话 → 台词/旁白 → 选项分支 → … → 结束对话」。每个输出只能连接一个节点；多条剧情可以汇入同一台词。
3. 打开 NPC 图，创建「对话入口」，将 NPC 根节点的「对话」输出连到入口。
4. 在入口绑定刚创建的对话图，通过「进入子图」编辑。入口标识在同一 NPC 内必须唯一；交互名称用于 E 键提示。
5. 在 NPC 图已连接的「记忆规则」资源中配置 `Entries`（记忆条目）：键、显示名、初始值和遗忘时长。进入对话子图后，从下拉列表选择这些条目作为写入目标和条件。

玩家通过既有 `PlayerWorldInteractionComponent3D` 寻找交谈对象，默认范围 2.4 米，要求视线畅通。对象交互和交谈共用最近目标选择。E 键使用 NPC 图里第一个已连接的对话入口；额外入口可用代码按 `EntryId` 触发。未连接的入口不生效。

森林关卡的居民、居民预制体和世界人口目录实际引用 `gray_green_resident/daily_goals.tres`；对话入口已接在这张图上，使用 `res://assets/resources/dialogue/resident_conversation.tres`。问候后，居民记住已经向当前玩家介绍过自己（`introduced`）；询问小路后记住已经告知路线（`knows_path`）。居民会按照原有日程前往工坊；森林里的赭色信使也已连接独立的 `courier_conversation.tres`，即使条目键相同，也保存在信使自己的记忆里。

## 节点

| 节点 | 配置与行为 |
| --- | --- |
| 开始对话 | 每张图恰好一个，连到第一段剧情 |
| 台词 / 旁白 | 说话人留空为旁白，正文为纯文本；支持 `{npc}`、`{player}` |
| 选项分支 | 1–8 个选项，各自连到独立输出；可选择 NPC 记忆作为显示条件，删除选项会同步删除并重排对应连线 |
| 记忆条件 | 读取当前 NPC 关于当前玩家的指定条目，按真/假选择输出；未写入时使用定义的初始值 |
| 写入记忆 | 设置当前 NPC 对当前玩家的记忆值，然后继续 |
| 结束对话 | 返回结果字符串，默认 `Completed` |

记忆归 `AiAgentComponent3D.Decision.Memory` 持有，配置和运行时统一使用 `Entries`。每条记录由记忆键与对象身份共同确定，保存布尔值和记录时间；重复写入会更新同一条记录。每个 NPC、每个交谈对象分别记录。条目的 `LifetimeHours = 0` 表示永久保留，正数表示对应的游戏小时数之后遗忘并恢复到初始值。示例中的介绍和路线条目默认永久保留。

条目随 NPC 原有存档流程保存，字段为 `memory_entries`；旧版 `memory_facts` 数据在读档时自动读取，新存档只写统一字段。世界居民随世界人口状态保存和切换关卡，开始新游戏时随新的 NPC 状态重建。旧版 `dialogue/story/flags` 的 `resident.introduced` 等已知键会在第一次交谈时迁移到相应 NPC 的记忆，然后移除旧键。当前播放位置和回看记录不存档；取消前已执行的记忆写入保留。

运行时可在 GM 的 NPC 状态页查看「记忆」，包括条目、布尔值、交谈对象和记录时间；未写入的条目显示其初始值。旧分类白名单、事件列表、全局容量与有效期、分类计数表达式及「记录记忆」事件动作已移除。

目前条件是布尔记忆判断，不直接读取任务或好感数值。其他系统可通过代码写入已配置的记忆条目。缺少可显示选项时结束并报告 `NoAvailableChoices`，建议至少保留一个无条件选项。

## 代码触发

```csharp
var dialogue = ModuleSystem.GetModule<IDialogueModule>();
dialogue.TryStart(npc, player, "talk");
// 或使用指定资源，参与者仍是现有 GameObject3D。
dialogue.TryStart(dialogueGraph, npc, player);
dialogue.Finished += session => GD.Print(session.Result);
bool exists = dialogue.TryReadMemory(npc, player, "knows_path", out bool knowsPath);
dialogue.WriteMemory(npc, player, "introduced", true);
```

订阅 `Finished` 的调用方应在自身销毁时解绑。播放期间暂停参与者的 AI 和导航组件，用具名移动锁锁住双方移动，并保存/恢复玩家输入层、原有 GameplayBlocked 状态与 UI 焦点。界面由 `UIModule` 加载与回收，没有额外的 gameplay Node 更新循环。

## 运行检查

先运行 `dotnet build yu.csproj`，再启动主场景并进入林间镇。靠近居民或信使按 E，检查分支选项、透明正文、选项底板、回看和退出；保存并继续游戏后检查 NPC 仍记得此前交谈。独立测试与预览场景已移除。
