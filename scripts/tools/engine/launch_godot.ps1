[CmdletBinding()]
param(
    [switch]$SourceBuild,
    [switch]$StockBuild,
    [string]$EngineDirectory = 'D:\DCC\Godot_4.8',
    [ValidatePattern('^[A-Za-z0-9_-]*$')]
    [string]$ProgramSuffix = 'ddgi_scene',
    [string]$RuntimeDirectory = 'bin/ddgi_scene',
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
    $editorName = if ($ProgramSuffix) { "godot.windows.editor.x86_64.mono.$ProgramSuffix.console.exe" } else { 'godot.windows.editor.x86_64.mono.console.exe' }
    $runtimeRoot = if ([IO.Path]::IsPathRooted($RuntimeDirectory)) { $RuntimeDirectory } else { Join-Path $EngineDirectory $RuntimeDirectory }
    Join-Path $runtimeRoot $editorName
}
else {
    Join-Path $StockEngineDirectory 'Godot_v4.6.1-stable_mono_win64_console.exe'
}
if (-not (Test-Path -LiteralPath $editor)) {
    throw "Editor not found at $editor. Build the patched Godot 4.8 with scripts/tools/engine/build_godot.ps1 (default suffix: ddgi_scene)."
}

Write-Host "Godot executable: $editor"
$arguments = @('--path', $projectRoot)
if (-not $StockBuild) { $arguments += @('--rendering-driver', 'vulkan') }
if (-not $RunGame) { $arguments += '--editor' }
if ($Headless) { $arguments += '--headless' }
if ($QuitAfter -gt 0) { $arguments += @('--quit-after', "$QuitAfter") }
$arguments += $GodotArguments
& $editor @arguments
exit $LASTEXITCODE
