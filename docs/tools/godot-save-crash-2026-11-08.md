# 2026-11-08 保存场景崩溃排查

用户确认触发操作为“保存场景修改”。本次检查 Windows Application 事件、三份 CrashDumps、匹配的引擎 PDB、.NET 调用栈及项目保存回调。引擎为当前项目使用的 Godot 4.8-beta .NET / `4d10419ca`。

## 已确认的证据

| 本地时间 | PID | 异常 | 故障点 |
| --- | --- | --- | --- |
| 19:21:45 | 42392 | 0x80000003，RVA 0xec6c22 | `CSharpInstance::mono_object_disposed_baseref`，`csharp_script.cpp:1916` |
| 19:22:25 | 40904 | 0xc0000005，RVA 0x1d3745d | `EditorFileSystemDirectory::get_file_count`，`editor_file_system.cpp:94` |
| 19:24:34 | 45592 | 0x80000003，RVA 0xec6c22 | 同第一项 |

前后两次由 .NET Finalizer 线程调用 `GodotObject.Finalize → Dispose → godotsharp_internal_refcounted_disposed`，触发引擎 `CRASH_COND(gchandle.is_released())`。不是 C# 可捕获异常，也不是普攻或架势输入的调用栈。

45592 的主线程同时位于 `_SaveExternalData → WorldAuthoringCompiler.CompileAll → Compile → GodotObject.Free`，直接对应用户描述的保存过程。42392 的主线程位于图编辑器 `GraphComponentPanel.Refresh → CollectHosts → ReferencesGraph → ContainsReference/ContainsVariant → GetPropertyList`。这证明了当时的活动，但不能仅据此认定被回收的具体资源类型；两份小转储未包含可解码的目标对象堆数据。

40904 的原生栈候选对应 `_regen_script_doc_thread → _reload_scripts_documentation → EditorFileSystemDirectory::get_file_count`。引擎把可被资源扫描更新的目录指针交给后台文档线程，存在并发访问风险。此项与 C# 句柄释放属于不同故障点；没有把静态栈扫描当作完整栈展开或确定的修复证明。

上游也记录了相同的 C# 句柄断言：[Godot #83762](https://github.com/godotengine/godot/issues/83762)。这支持其为已知引擎生命周期问题，但不证明本项目与该报告拥有完全相同的触发资源。

## 本次保存链路调整

- `WorldAuthoringPlugin` 将 `_SaveExternalData`、`SceneSaved` 和相关 `ResourceSaved` 合并到一次延后编译；构建和导出仍同步保证目录可用。插件退出后不执行已排队的编译。
- `WorldAuthoringCompiler` 检查磁盘目录时使用独立读取，不再对编辑器正在引用的 C# 资源执行 `CacheMode.Replace`。
- 目录保存成功后更新已有缓存对象的字段并发送 `Changed`，保留检查器持有的对象实例；首次生成仍发布新目录。

这是减少保存期间重复扫描和资源重置的项目级修正。**没有修改引擎断言或后台文档线程，也未宣称两类崩溃都已根治。**

## 验证与复查

`assets/scenes/tests/world_save.tscn` 使用真实森林场景的临时副本，只写入 `.godot/crash-save/stress/`。检查 12 次保存、24 次目录编译、强制托管回收后，缓存目录保持同一 C# 实例且内容更新。运行日志位于 `.godot/crash-save/save-stress.log`。该检查不等价于长时间 GUI 编辑和后台导入压力测试。

诊断产物位于 `.godot/crash-save/`；原始转储保留在 Windows CrashDumps 目录。后续如再次发生，应按新的异常码、PID 和 RVA 分别追踪，不把所有保存崩溃归到同一个问题。

## 独立玩法测试的退出异常

23:45 左右核验 ComfyUI v7 移动资源时，`weapon_stances.tscn` 的键鼠、手柄、移动、存档和实例隔离断言全部打印通过，但随后进程以 `0xC0000005`（退出码 `-1073741819`）终止。调用栈为 `Array.Finalize → Array.Dispose → godot_array.Dispose → godotsharp_array_destroy`，与上面保存场景时的两个故障点不同，尚未确认根因。

本次 52 帧图片导入检查正常退出；独立 Forward+ 动画录制也完成并正常退出，但仍报告资源未释放。不能将局部检查通过当成编辑器稳定性已经恢复。原临时日志已按后续清理要求删除，本节保留异常码、故障点和检查范围。

2026-11-09 的 v11 移动检查在断言通过后，又于退出阶段发生 0xC0000005，堆栈为 godotsharp_internal_object_get_associated_gchandle → GodotObject.Dispose → GodotObject.Finalize，伴随 Resource/SpriteFrames 引用泄漏；实际录制正常退出。随后 v12 完整武器/架势检查、130 帧导入核验及实际录制均以 0 退出。未修改引擎释放逻辑，不能把本次正常退出作为历史问题已修复的证据。最新结果见[视觉复核记录](../../assets/resources/sprites/amber_fox_style_v2/greatsword_stance_movement/visual-review.json)。
