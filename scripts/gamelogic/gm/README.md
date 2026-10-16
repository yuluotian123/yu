# Runtime GM

Spacelevel 场景内实例化 `assets/scenes/ui/runtime_gm.tscn`，F8 或右上角按钮开关。
所有控件都在场景中，可直接调整布局。`RuntimeGmComponent3D` 管理外壳和输入隔离，
`TimeOfDayGmComponent3D` 通过 Embedded 模式提供时间页，不再创建第二个入口。

- 时间与天空：日期、时分、时间倍率、暂停、黎明/正午/黄昏/午夜、月相预设和连续月相。
- 天气：自动天气开关、晴/阴/雨/雪手动覆盖、雨雪强度与下次变化倒计时，见 [天气系统](../weather/README.md)。
- 资源监控：资源缓存、异步加载任务、引用和句柄；输出快照日志、GC 回收。
- 存档：选择/输入槽位、读取原始 JSON、校验、写入修改、保存当前运行状态、确认删除。
- 渲染与 FPS：当前帧率、帧间隔、Process/Physics 近一秒处理耗时峰值、场景/阴影/UI Draw Calls、显存分配、渲染器和 GPU、视口与抗锯齿、当前相机/环境/曝光、GI 与雾、Relink 各阶段与 LUT 参数；提示相机合成器覆盖和原生后处理叠加冲突。

渲染页由 `RenderingGmComponent3D` 绑定场景控件，提供 FPS、视口、显存、原生效果和后处理诊断，可在没有 Relink 合成器的关卡复用。打开该页时每 0.5 秒刷新，FPS 由 Godot 每秒更新。帧间隔包含等待，不作为 GPU 耗时；帧处理时间、Draw Calls 和显存采用 [Godot Performance Monitor](https://docs.godotengine.org/en/4.6/classes/class_performance.html)，监控值可能延迟一秒，部分值在发行构建中不可用。统计包含 GM 控件自身的开销。

修改和删除只影响磁盘，下次进入关卡生效，不传送角色或修改当前运行状态。
写入检查原始文本以防外部修改冲突；有效原文件自动备份为 `.bak`，损坏原文件不会覆盖有效备份。
删除清理主文件、备份和待恢复状态，通过 `.deleted` 标记阻止只读旧存档回退；明确保存可重建该槽。
读取另一槽位前会确认放弃未写入内容，关闭面板则保留编辑区。
不再使用 `camera_down` 保存，避免调整镜头时意外覆盖存档。

新增调试页：在 Tabs 下添加场景控件，并在宿主的 Components 中配置对应业务组件。
输入开关仅由外壳管理。独立测试场景的任务图测试快捷键仍属于测试入口。

