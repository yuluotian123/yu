[CmdletBinding()]
param(
    [string]$EnginePath = 'D:\DCC\Godot_4.8\bin\ddgi_scene\godot.windows.editor.x86_64.mono.ddgi_scene.console.exe'
)

$ErrorActionPreference = 'Stop'
$npcCaptureRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$npcCaptureOutput = Join-Path $npcCaptureRoot '.godot/npc/art'
New-Item -ItemType Directory -Force -Path $npcCaptureOutput | Out-Null
$priorNpcCaptureAppData = $env:APPDATA
try {
    $env:APPDATA = Join-Path $npcCaptureRoot '.godot/npc/appdata'
    $npcCaptureArguments = @('--path', ('"' + $npcCaptureRoot + '"'),
        '--rendering-method', 'forward_plus', '--rendering-driver', 'vulkan',
        '--audio-driver', 'Dummy', '--windowed', '--resolution', '960x720', '--quit-after', '600',
        '--log-file', ('"' + (Join-Path $npcCaptureOutput 'visual.log') + '"'),
        'res://assets/scenes/npc/visual_capture.tscn')
    $npcCaptureProcess = Start-Process -FilePath $EnginePath -ArgumentList $npcCaptureArguments -WindowStyle Hidden -PassThru -Wait `
        -RedirectStandardOutput (Join-Path $npcCaptureOutput 'visual_stdout.log') -RedirectStandardError (Join-Path $npcCaptureOutput 'visual_stderr.log')
    $npcCaptureLog = Get-Content -LiteralPath (Join-Path $npcCaptureOutput 'visual_stdout.log') -Raw
    if ($npcCaptureProcess.ExitCode -ne 0 -or $npcCaptureLog -notmatch 'NPC_VISUAL_CAPTURE_PASS') {
        throw "NPC GPU capture failed. See $npcCaptureOutput/visual.log and visual_stderr.log."
    }
    Write-Host "NPC_VISUAL_CAPTURE_PASS: $npcCaptureOutput/spacelevel_idle.png, spacelevel_walk.png and spacelevel_population.png"
}
finally { $env:APPDATA = $priorNpcCaptureAppData }
