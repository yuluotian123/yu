[CmdletBinding()]
param(
    [string]$EnginePath = 'D:\DCC\Godot_4.8\bin\ddgi_scene\godot.windows.editor.x86_64.mono.ddgi_scene.console.exe',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$npcProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$npcOutput = Join-Path $npcProjectRoot '.godot/npc'
New-Item -ItemType Directory -Path $npcOutput -Force | Out-Null
if (-not (Test-Path -LiteralPath $EnginePath)) { throw "Godot executable not found: $EnginePath" }
$priorAppData = $env:APPDATA
$priorWorkloadResolver = $env:MSBuildEnableWorkloadResolver
Push-Location $npcProjectRoot
try {
    # Keep the test's MCP registry and engine logs inside the ignored workspace.
    $env:APPDATA = Join-Path $npcOutput 'appdata'
    $env:MSBuildEnableWorkloadResolver = 'false'
    & dotnet run --project 'scripts/tools/npc/GoapValidation.csproj' --nologo
    if ($LASTEXITCODE -ne 0) { throw 'GOAP domain validation failed.' }
    if (-not $SkipBuild) {
        & dotnet build yu.csproj --no-restore --nologo -v:q
        if ($LASTEXITCODE -ne 0) { throw 'yu.csproj build failed.' }
    }
    $npcLog=Join-Path $npcOutput 'framework.log'
    & $EnginePath --headless --path $npcProjectRoot --fixed-fps 60 --disable-render-loop --audio-driver Dummy --log-file (Join-Path $npcOutput 'framework_engine.log') 'res://assets/scenes/tests/npc_framework_validation.tscn' *> $npcLog
    if($LASTEXITCODE -ne 0 -or -not (Select-String -LiteralPath $npcLog -SimpleMatch 'NPC_FRAMEWORK_VALIDATION_PASS' -Quiet)) {
        Get-Content -LiteralPath $npcLog -Tail 65
        throw 'NPC framework validation failed.'
    }
    Select-String -LiteralPath $npcLog -SimpleMatch 'NPC_FRAMEWORK_VALIDATION_PASS' | ForEach-Object {$_.Line}
    Write-Host 'NPC_VALIDATION_PASS: pure GOAP and configurable NPC framework in SpaceLevel.'

}
finally {
    Pop-Location
    $env:APPDATA = $priorAppData
    $env:MSBuildEnableWorkloadResolver = $priorWorkloadResolver
}
