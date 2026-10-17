[CmdletBinding()]
param(
    [string]$EnginePath = 'D:\DCC\Godot_4.8\bin\ddgi_scene\godot.windows.editor.x86_64.mono.ddgi_scene.console.exe'
)

$ErrorActionPreference = 'Stop'
$nativeDdgiProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$nativeDdgiOutput = Join-Path $nativeDdgiProjectRoot '.godot/ddgi-native-validation'
New-Item -ItemType Directory -Force -Path $nativeDdgiOutput | Out-Null
if (-not (Test-Path -LiteralPath $EnginePath)) { throw "Native DDGI engine not found: $EnginePath. Run scripts/tools/engine/build_godot.ps1." }
$nativeDdgiArguments = @('--path', ('"' + $nativeDdgiProjectRoot + '"'),
    '--rendering-method', 'forward_plus', '--rendering-driver', 'vulkan',
    '--audio-driver', 'Dummy', '--windowed', '--resolution', '640x360', '--quit-after', '1200',
    '--log-file', ('"' + (Join-Path $nativeDdgiOutput 'engine.log') + '"'),
    '--script', 'res://scripts/tools/rendering/validate_ddgi_native.gd')
$nativeDdgiProcess = Start-Process -FilePath $EnginePath -ArgumentList $nativeDdgiArguments -WindowStyle Hidden -PassThru -Wait `
    -RedirectStandardOutput (Join-Path $nativeDdgiOutput 'stdout.log') -RedirectStandardError (Join-Path $nativeDdgiOutput 'stderr.log')
$nativeDdgiStdout = Get-Content -LiteralPath (Join-Path $nativeDdgiOutput 'stdout.log') -Raw
$nativeDdgiStderr = Get-Content -LiteralPath (Join-Path $nativeDdgiOutput 'stderr.log') -Raw
if ($nativeDdgiProcess.ExitCode -ne 0 -or $nativeDdgiStdout -notmatch 'DDGI_NATIVE_VALIDATION_COMPLETE failures=0' -or
    $nativeDdgiStderr -match '(?m)^(ERROR:|SCRIPT ERROR:|SHADER ERROR:)') {
    throw "Native DDGI validation failed (exit $($nativeDdgiProcess.ExitCode)). See $nativeDdgiOutput/engine.log and stderr.log.`n$nativeDdgiStderr"
}
Write-Host "Native DDGI validation passed. Report and captures: $nativeDdgiOutput"
