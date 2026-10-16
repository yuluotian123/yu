# NVIDIA Godot 源码位置与构建

NVIDIA 源码与编译产物使用独立目录：

```text
D:\DCC\Godot_NVIDIA_RTX\
  .git\
  core\
  modules\
  servers\
  SConstruct
  bin\
	godot.windows.editor.x86_64.mono.exe
	godot.windows.editor.x86_64.mono.console.exe
	GodotSharp\
	sl.interposer.dll
```

源码来自 `https://github.com/NVIDIA-RTX/godot.git` 的 `nvidia-pt-dlss` 分支，
当前提交为 `135dff3887a3d2ccf45cafbd9bdffa7b718e1e76`，本地修改分支为
`codex/yu-rtx-engine`，引擎版本为 **4.8-dev .NET**。
源码拥有独立 Git 仓库，不随游戏仓库提交。

原版 4.6.1 安装目录为 `D:\DCC\Godot_v4.6.1-stable_mono_win64`，
只保留原版编辑器、原版 `GodotSharp` 和原有快捷方式。
NVIDIA 源码、Git 仓库、运行库和编译产物已全部移出此目录。
原项目内的 `tools/godot/src` 已删除。
原 4.6.1 源码基线保存在同一 Git 仓库的 `codex/yu-engine` 分支。

## 构建 NVIDIA 引擎

在游戏项目根目录运行：

```powershell
.\scripts\tools\engine\build_godot.ps1

# 需要代理时。
.\scripts\tools\engine\build_godot.ps1 -HttpProxy 'http://127.0.0.1:7890'

# 指定其他源码位置。
.\scripts\tools\engine\build_godot.ps1 -SourceDirectory 'D:\other\godot'
```

脚本继续使用项目内 `tools/godot/.venv` 的 Python 与 SCons 4.10.0。
构建目标为 Windows x64 .NET 编辑器，启用 Streamline、Vulkan/OpenGL、调试符号，
并生成 `compile_commands.json`。默认关闭 D3D12；此 NVIDIA 分支的光追后端使用 Vulkan。
AccessKit 和 ANGLE 未启用。原生编译后，脚本会生成 C# 绑定并构建编辑器的 .NET 工具。

DLSS 与光线重建需要 Streamline 2.10.0 运行库。以下脚本校验官方压缩包的 SHA256，
并将生产版 DLL 及许可证部署到同一 `bin` 目录：

```powershell
.\scripts\tools\engine\install_streamline.ps1 -HttpProxy 'http://127.0.0.1:7890'
```

引擎的 C# NuGet 包生成在 `bin/GodotSharp/Tools/nupkgs`。
项目使用 `Godot.NET.Sdk/4.8.0-dev`；根目录 `NuGet.Config` 将 Godot 包绑定到此本地源，
并将包缓存放在 `.godot/nuget-packages`。重新生成同版本的绑定后，如需刷新缓存，
只清理该缓存中的 Godot 包，再运行 `dotnet restore yu.csproj`。

## 启动当前项目

默认使用 `bin/godot.windows.editor.x86_64.mono.console.exe`，通过 Vulkan 打开项目：

```powershell
dotnet build yu.csproj
.\scripts\tools\engine\launch_godot.ps1
.\scripts\tools\engine\launch_godot.ps1 -RunGame
```

`-SourceBuild` 保留为默认行为的显式写法。`-StockBuild` 可运行保留的原版编辑器，
但回退 yu 前还需要恢复 4.6.1 的 SDK 和项目设置，并重新构建 C# 工程。

VS Code 的启动/调试已指向新编译的 NVIDIA 编辑器。
`Godot: Build Engine` 任务编译新位置的 NVIDIA 源码；C++ IntelliSense 的数据库路径
也已指向安装目录。构建与验证日志保留在 `.godot/engine-build/`。
项目保持 Forward+ 渲染；具体场景的路径追踪/DLSS 选项需要在新引擎中按需配置。

已验证引擎原生与 .NET 构建成功、yu C# 工程构建成功，并使用 Vulkan/Streamline 在
RTX 5080 上打开编辑器。首次打开 SpaceLevel 时记录过一条 C# 句柄异常，以及自定义
材质的光追着色器编译警告；完整的路径追踪材质兼容性仍需单独验证。
