[CmdletBinding()]
param(
    [ValidateSet('editor', 'template_debug', 'template_release')]
    [string]$Target = 'editor',
    [ValidateRange(1, 64)]
    [int]$Jobs = [Math]::Min(12, [Environment]::ProcessorCount),
    [string]$PythonExecutable = $env:GODOT_BUILD_PYTHON,
    [string]$SourceDirectory = 'D:\DCC\Godot_NVIDIA_RTX',
    [string]$HttpProxy,
    [switch]$WithDirect3D12,
    [switch]$SkipManaged
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$engineRoot = Join-Path $projectRoot 'tools/godot'
$sourceRoot = [IO.Path]::GetFullPath($SourceDirectory)
$python = Join-Path $engineRoot '.venv/Scripts/python.exe'
$sourceRef = 'nvidia-pt-dlss'
$sourceRepository = 'https://github.com/NVIDIA-RTX/godot.git'
$sconsVersion = '4.10.0'

function Invoke-Checked {
    param([string]$Executable, [string[]]$Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Executable failed with exit code $LASTEXITCODE."
    }
}

New-Item -ItemType Directory -Force -Path $engineRoot | Out-Null
if (-not (Test-Path -LiteralPath $sourceRoot)) {
    $gitArguments = @()
    if ($HttpProxy) { $gitArguments += @('-c', "http.proxy=$HttpProxy") }
    $gitArguments += @('clone', '--depth', '1', '--single-branch', '--branch', $sourceRef,
        $sourceRepository, $sourceRoot)
    Invoke-Checked 'git' $gitArguments
    Invoke-Checked 'git' @('-C', $sourceRoot, 'switch', '-c', 'codex/yu-rtx-engine')
}
if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot 'SConstruct'))) {
    throw "Godot source is incomplete at $sourceRoot. Preserve any changes and finish the checkout first."
}

# Keep fixes to the external engine reproducible from this game repository.
foreach ($enginePatch in (Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'patches') -Filter '*.patch' | Sort-Object Name)) {
    & git -C $sourceRoot apply --reverse --check $enginePatch.FullName *> $null
    if ($LASTEXITCODE -eq 0) { continue }
    Invoke-Checked 'git' @('-C', $sourceRoot, 'apply', '--check', $enginePatch.FullName)
    Invoke-Checked 'git' @('-C', $sourceRoot, 'apply', $enginePatch.FullName)
}

$tempDirectory = Join-Path $engineRoot 'tmp'
New-Item -ItemType Directory -Force -Path $tempDirectory | Out-Null
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
$previousHttpProxy = $env:HTTP_PROXY
$previousHttpsProxy = $env:HTTPS_PROXY
$env:TEMP = $tempDirectory
$env:TMP = $tempDirectory
if ($HttpProxy) {
    $env:HTTP_PROXY = $HttpProxy
    $env:HTTPS_PROXY = $HttpProxy
}

try {
    if (-not (Test-Path -LiteralPath $python)) {
        if (-not $PythonExecutable) {
            $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
            if ($pythonCommand -and $pythonCommand.Source -notmatch 'WindowsApps') {
                $PythonExecutable = $pythonCommand.Source
            }
        }
        if (-not $PythonExecutable) {
            throw 'Pass -PythonExecutable with a Python 3.8+ executable to create the local build environment.'
        }
        Invoke-Checked $PythonExecutable @('-m', 'venv', (Join-Path $engineRoot '.venv'))
    }

    & $python -m pip --version | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Invoke-Checked $python @('-m', 'ensurepip', '--upgrade')
    }
    $installedScons = & $python -c 'import importlib.metadata; print(importlib.metadata.version("scons"))' 2>$null
    if ($LASTEXITCODE -ne 0 -or $installedScons -ne $sconsVersion) {
        $pipArguments = @('-m', 'pip', 'install', '--disable-pip-version-check', "scons==$sconsVersion")
        if ($HttpProxy) { $pipArguments += @('--proxy', $HttpProxy) }
        Invoke-Checked $python $pipArguments
    }

    Push-Location $sourceRoot
    try {
        $d3d12 = if ($WithDirect3D12) { 'yes' } else { 'no' }
        $sconsArguments = @('-m', 'SCons', '-j', "$Jobs", 'platform=windows', "target=$Target",
            'arch=x86_64', 'module_mono_enabled=yes', 'use_streamline=yes',
            "d3d12=$d3d12", 'accesskit=no', 'angle=no', 'debug_symbols=yes', 'compiledb=yes')
        Invoke-Checked $python $sconsArguments

        if ($Target -eq 'editor' -and -not $SkipManaged) {
            $editor = Join-Path $sourceRoot 'bin/godot.windows.editor.x86_64.mono.console.exe'
            Invoke-Checked $editor @('--headless', '--generate-mono-glue', 'modules/mono/glue')
            foreach ($api in @('GodotSharp', 'GodotSharpEditor')) {
                $generatedIncludes = Join-Path $sourceRoot "modules/mono/glue/GodotSharp/$api/Generated/GeneratedIncludes.props"
                if (-not (Test-Path -LiteralPath $generatedIncludes)) {
                    throw "C# binding generation did not produce $generatedIncludes."
                }
            }
            Invoke-Checked $python @('modules/mono/build_scripts/build_assemblies.py',
                '--godot-output-dir', 'bin', '--godot-platform', 'windows')
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
    $env:HTTP_PROXY = $previousHttpProxy
    $env:HTTPS_PROXY = $previousHttpsProxy
}

Write-Host "Godot $Target build completed: $sourceRoot/bin"
