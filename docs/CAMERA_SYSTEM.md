# 场景 3D 相机

`spacelevel/CameraRig/Camera3D` 已直接保存在场景里，可在编辑器中选中、预览和调整。`CameraRig` 是 GameObject3D，唯一的 `SceneCameraComponent3D` 负责自动绑定、跟随、前瞻和平滑震动。`CameraModule` 只路由当前相机的控制接口，不创建相机或宿主。

## 编辑构图

选中 `CameraRig/Camera3D`，使用 Inspector 中的 Transform、Projection、Size（正交）或 FOV（透视）、Near/Far 调整；开启 Camera3D 的 Preview 查看实际构图。默认正交 Size=5.4，俯角 25°。透视取景大小由 FOV 和相机距离决定，Size 不参与透视。

相机启动时记住已编辑的旋转、与目标的相对位移和 Size，跟随时只在该构图基础上平移。代码不强制改为正交，也不重新计算俯角。运行中的 SetViewHeight 只改变正交 Size。

`CameraRig → Components → SceneCameraComponent3D`：

- CameraPath：原生相机子节点，默认 Camera3D。
- TargetPath：跟随目标，默认 ../Player。等目标准备完成后自动绑定，不依赖节点 Ready 顺序。
- Profile：跟随参数，默认使用 hd2d_side_view_3d.tres；构图参数以原生 Camera3D 为准。

Profile 保留 DeadZone、FollowSmooth、LookSmooth、LookAheadDistance、VerticalVelocityLookDistance、TeleportDistance、PixelSnap 和手动上下观察参数。W/S 观察沿用 Camera 输入层；跟随 Free3D 角色时自动让出 W/S。GM 面板打开期间会隔离输入，收起后恢复。

## 生命周期

相机宿主以 ProcessPriority=100 在角色之后更新，在渲染帧读取目标的 GetGlobalTransformInterpolated()，相机本身关闭物理插值以避免双重插值。跟随目标离树时保留当前位置。关卡或相机宿主退出时释放模块引用并停止震动，不留下全局相机；切换关卡不会让旧场景释放新镜头。

旧 LevelCameraComponent3D / GlobalCameraComponent3D 已合并移除，原相机脚本 UID 保留给 SceneCameraComponent3D。自定义场景需按当前 CameraRig 结构添加组件、Camera3D 和 TargetPath。

## 天空与投影

正交视角的天空使用环境 Profile 的 OrthographicSkyFov 和 OrthographicSkyPitchDegrees。透视/视锥视角自动关闭天空 FOV 和俯仰覆盖，遵循当前相机；即使时钟暂停也会更新。Scene Sky Preview 编辑器插件让同样的切换在 Camera3D Preview 中实时生效。日月大小位于 EnvironmentRig 的 TimeOfDayProfile，使用独立的 SunDiameterDegrees / MoonDiameterDegrees。

## API

```csharp
var camera = ModuleSystem.GetModule<ICameraModule>();
camera.Follow(npc);
camera.Follow(player, snap: true);
camera.Focus(new Vector3(5, -7, 0));
camera.SetViewHeight(7f); // 正交缩放
camera.SnapToTarget();
camera.Shake(shakeProfile);
camera.StopShake();
```

场景组件自动调用 Activate(rigOwner, rig, target, profile)，通常不需要手动调用。Release(owner) 只解除控制引用，不销毁场景节点。技能 AbilityCameraShakeAction 继续调用同一全局接口。

## 验证

`dotnet build yu.csproj` 后运行 `res://assets/scenes/time_of_day_smoke.tscn`，检查相机使用场景节点、保留原生构图、节点顺序无关的自动绑定、常规帧移动跟随、正交/透视天空切换和退出释放。
