[CmdletBinding()]
param(
    [string]$EnginePath = 'D:\DCC\Godot_4.8\bin\ddgi_scene\godot.windows.editor.x86_64.mono.ddgi_scene.console.exe'
)

$ErrorActionPreference = 'Stop'
$ddgiProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$ddgiOutput = Join-Path $ddgiProjectRoot '.godot/ddgi-validation'
New-Item -ItemType Directory -Force -Path $ddgiOutput | Out-Null
if (-not (Test-Path -LiteralPath $EnginePath)) {
    throw "DDGI engine not found: $EnginePath. Run scripts/tools/engine/build_godot.ps1."
}
$ddgiArguments = @('--path', ('"' + $ddgiProjectRoot + '"'),
    '--rendering-method', 'forward_plus', '--rendering-driver', 'vulkan',
    '--audio-driver', 'Dummy', '--windowed', '--resolution', '640x360', '--quit-after', '1200',
    '--log-file', ('"' + (Join-Path $ddgiOutput 'engine.log') + '"'),
    '--script', 'res://scripts/tools/rendering/validate_ddgi.gd')
$ddgiProcess = Start-Process -FilePath $EnginePath -ArgumentList $ddgiArguments -WindowStyle Hidden -PassThru -Wait `
    -RedirectStandardOutput (Join-Path $ddgiOutput 'stdout.log') -RedirectStandardError (Join-Path $ddgiOutput 'stderr.log')
$ddgiStdout = Get-Content -LiteralPath (Join-Path $ddgiOutput 'stdout.log') -Raw
$ddgiStderr = Get-Content -LiteralPath (Join-Path $ddgiOutput 'stderr.log') -Raw
if ($ddgiProcess.ExitCode -ne 0 -or $ddgiStdout -notmatch 'DDGI_VALIDATION_COMPLETE failures=0' -or
    $ddgiStderr -match '(?m)^(ERROR:|SCRIPT ERROR:|SHADER ERROR:)' ) {
    throw "DDGI validation failed (exit $($ddgiProcess.ExitCode)). See $ddgiOutput/engine.log and stderr.log.`n$ddgiStderr"
}
Write-Host "DDGI validation passed. Report and captures: $ddgiOutput"
