# Godot 4.8 源码与构建

main 使用官方仓库 https://github.com/godotengine/godot.git 加项目 DDGI 先决、原生接收与天空桥接补丁，源码与产物位于 D:\DCC\Godot_4.8，当前默认编辑器为 bin/ddgi_scene/godot.windows.editor.x86_64.mono.ddgi_scene.exe。

源码基线为 4d10419cacd6d3009976e8f9fd3abd970fd58018，版本为 4.8-beta .NET。构建使用源码目录内独立的 .venv、SCons 4.10.0、zstandard 0.25.0、MSVC 和 .NET 10 SDK，目标为 Windows x64 编辑器，保留调试符号及 compile_commands.json。默认启用 Vulkan/OpenGL，关闭 D3D12、ANGLE 和 AccessKit。yu 的目标框架为 net10.0。

## 首次准备源码

在干净的对应上游提交上，按顺序检查并应用以下补丁。当前本机目录已经应用，不要重复应用或重置现有改动。构建脚本不会自动应用 DDGI 补丁；新目录应先准备好基线与补丁，再构建。

```powershell
git -C D:/DCC/Godot_4.8 apply --check D:/Project/GitHub/yu/scripts/tools/engine/patches/godot48_ddgi_prerequisites.patch
git -C D:/DCC/Godot_4.8 apply D:/Project/GitHub/yu/scripts/tools/engine/patches/godot48_ddgi_prerequisites.patch
git -C D:/DCC/Godot_4.8 apply --check D:/Project/GitHub/yu/scripts/tools/engine/patches/godot48_ddgi_native_reception.patch
git -C D:/DCC/Godot_4.8 apply D:/Project/GitHub/yu/scripts/tools/engine/patches/godot48_ddgi_native_reception.patch
git -C D:/DCC/Godot_4.8 apply --check D:/Project/GitHub/yu/scripts/tools/engine/patches/godot48_ddgi_scene_integration.patch
git -C D:/DCC/Godot_4.8 apply D:/Project/GitHub/yu/scripts/tools/engine/patches/godot48_ddgi_scene_integration.patch
```

| 补丁 | 内容 |
| --- | --- |
| godot48_ddgi_prerequisites.patch | Ray Query 的零 SBT、TLAS 上传、Vulkan SPIR-V 兼容与 program_suffix |
| godot48_ddgi_native_reception.patch | 当帧 DDGI 发布、Forward+ 不透明 / 透明原生接收、反射图集退出回收 |
| godot48_ddgi_scene_integration.patch | 当前视图 HDR 天空及曝光 / 旋转 / 版本桥接 |

## 构建

从项目根目录运行：

```powershell
# 清理当前目标并全量重新编译，包括 C# 绑定与编辑器工具。
.\scripts\tools\engine\build_godot.ps1 -Clean

# 首次构建可指定用于建立独立 .venv 的 Python。
.\scripts\tools\engine\build_godot.ps1 -Clean -PythonExecutable 'C:\path\to\python.exe'

# 如需代理。
.\scripts\tools\engine\build_godot.ps1 -Clean -HttpProxy 'http://127.0.0.1:7890'
```

不传 -Clean 时，脚本允许增量构建。源码不存在时会克隆官方 master；编译前检查版本必须为 4.8，避免未来 master 升级后误用其他版本。

DDGI 不依赖 NVIDIA 分支。构建与启动脚本默认 `-ProgramSuffix ddgi_scene -RuntimeDirectory bin/ddgi_scene`：原生链接产物仍由 SCons 生成在 bin，再复制到包含配套 GodotSharp 的独立运行目录，复用已有对象文件。明确需要无后缀、原 bin 目录构建时传 `-ProgramSuffix '' -RuntimeDirectory bin`。场景使用与支持范围见 [DDGI 指南](../../../docs/art/ddgi_implementation.md)。

原生接收新增了 RenderSceneBuffersRD API，首次构建必须包含托管生成，不能传 `-SkipManaged`。脚本将生成的 API 与编辑器 Tools / nupkgs 一并打包到 bin/ddgi_scene/GodotSharp。NuGet.Config 将 Godot 包绑定到该运行目录的 Tools/nupkgs，缓存位于 .godot/nuget-packages-ddgi-scene，避免相同版本号的旧 API 包被复用。yu.csproj 使用 Godot.NET.Sdk/4.8.0-beta；引擎 ABI 改变后需重新生成托管包并刷新项目的对应本地缓存。

## 打开项目

```powershell
dotnet restore yu.csproj
dotnet build yu.csproj --no-restore
.\scripts\tools\engine\launch_godot.ps1

# 运行主菜单，点击开始进入已接入 DDGI 的 ForestTownLevel。
.\scripts\tools\engine\launch_godot.ps1 -RunGame

# 直接运行当前关卡。
.\scripts\tools\engine\launch_godot.ps1 -RunGame -GodotArguments @('res://assets/scenes/levels/forest_town_level.tscn')
```

切换默认路径不能改变已经运行的进程。先保存并退出旧编辑器 / 游戏，再用上述入口重开；启动时会打印实际引擎路径。旧 `.mono.exe` 不含 Ray Query 修复，旧 `.mono.ddgi.exe` 不含原生接收 API，旧 `.mono.ddgi_native.exe` 不含天空 API，均不能替代当前默认版本。

VS Code 的编辑器与运行/调试配置均指向上述 ddgi_scene 运行目录。维护时先编译 C#、导入资源，再运行 [DDGI 指南](../../../docs/art/ddgi_implementation.md#验证)中的验证入口并检查实际 ForestTownLevel。性能和硬件覆盖边界见同一指南。

## RTX 保留版本

原有源码、项目资源、路径追踪配置和构建工具已提交至 codex/rtx-version 分支，快照提交为 a95497b。该版本使用 D:\DCC\Godot_NVIDIA_RTX 的 NVIDIA 4.8-dev 引擎，保留 Streamline/DLSS、引擎补丁与路径追踪验证流程。原引擎目录继续保留。

main 使用官方 Forward+ 接口，已移除 NVIDIA 专有材质分支、Streamline 安装工具及路径追踪验证脚本。需要恢复该路线时使用保留分支，避免混用两套引擎和托管 API。
