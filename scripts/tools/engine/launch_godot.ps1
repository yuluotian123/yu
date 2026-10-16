[CmdletBinding()]
param(
    [switch]$SourceBuild,
    [switch]$StockBuild,
    [string]$EngineDirectory = 'D:\DCC\Godot_NVIDIA_RTX',
    [string]$StockEngineDirectory = 'D:\DCC\Godot_v4.6.1-stable_mono_win64',
    [switch]$RunGame,
    [switch]$Headless,
    [ValidateRange(0, 2147483647)]
    [int]$QuitAfter = 0,
    [string[]]$GodotArguments = @()
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
if ($SourceBuild -and $StockBuild) {
    throw 'Choose either -SourceBuild or -StockBuild.'
}
$editor = if (-not $StockBuild) {
    Join-Path $EngineDirectory 'bin/godot.windows.editor.x86_64.mono.console.exe'
}
else {
    Join-Path $StockEngineDirectory 'Godot_v4.6.1-stable_mono_win64_console.exe'
}
if (-not (Test-Path -LiteralPath $editor)) {
    throw "Editor not found at $editor. Build the NVIDIA engine with scripts/tools/engine/build_godot.ps1, or select an installed editor with -StockBuild."
}

$arguments = @('--path', $projectRoot)
if (-not $StockBuild) { $arguments += @('--rendering-driver', 'vulkan') }
if (-not $RunGame) { $arguments += '--editor' }
if ($Headless) { $arguments += '--headless' }
if ($QuitAfter -gt 0) { $arguments += @('--quit-after', "$QuitAfter") }
$arguments += $GodotArguments
& $editor @arguments
exit $LASTEXITCODE
