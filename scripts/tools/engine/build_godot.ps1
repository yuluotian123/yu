[CmdletBinding()]
param(
    [ValidateSet('editor', 'template_debug', 'template_release')]
    [string]$Target = 'editor',
    [ValidateRange(1, 64)]
    [int]$Jobs = [Math]::Min(12, [Environment]::ProcessorCount),
    [string]$PythonExecutable = $env:GODOT_BUILD_PYTHON,
    [string]$SourceDirectory = 'D:\DCC\Godot_4.8',
    [string]$HttpProxy,
    [ValidatePattern('^[A-Za-z0-9_-]*$')]
    [string]$ProgramSuffix = 'ddgi_scene',
    [string]$RuntimeDirectory = 'bin/ddgi_scene',
    [switch]$WithDirect3D12,
    [switch]$SkipManaged,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$sourceRoot = [IO.Path]::GetFullPath($SourceDirectory)
$runtimeRoot = if ([IO.Path]::IsPathRooted($RuntimeDirectory)) { [IO.Path]::GetFullPath($RuntimeDirectory) } else { [IO.Path]::GetFullPath((Join-Path $sourceRoot $RuntimeDirectory)) }
$python = Join-Path $sourceRoot '.venv/Scripts/python.exe'
$sourceRef = 'master'
$sourceRepository = 'https://github.com/godotengine/godot.git'
$sconsVersion = '4.10.0'

function Invoke-Checked {
    param([string]$Executable, [string[]]$Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Executable failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $sourceRoot)) {
    $gitArguments = @()
    if ($HttpProxy) { $gitArguments += @('-c', "http.proxy=$HttpProxy") }
    $gitArguments += @('clone', '--depth', '1', '--single-branch', '--branch', $sourceRef,
        $sourceRepository, $sourceRoot)
    Invoke-Checked 'git' $gitArguments
}
if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot 'SConstruct'))) {
    throw "Godot source is incomplete at $sourceRoot. Preserve any changes and finish the checkout first."
}

$versionSource = Get-Content -LiteralPath (Join-Path $sourceRoot 'version.py') -Raw
if ($versionSource -notmatch '(?m)^major = 4\s*$' -or $versionSource -notmatch '(?m)^minor = 8\s*$') {
    throw "Expected Godot 4.8 source at $sourceRoot. Check out a 4.8 revision before building."
}

$tempDirectory = Join-Path $projectRoot '.godot/engine-build/tmp'
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
        Invoke-Checked $PythonExecutable @('-m', 'venv', (Join-Path $sourceRoot '.venv'))
    }

    & $python -m pip --version | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Invoke-Checked $python @('-m', 'ensurepip', '--upgrade')
    }
    $installedScons = & $python -c 'import importlib.metadata, importlib.util; print(importlib.metadata.version("scons") if importlib.util.find_spec("SCons") else "")'
    $sconsReady = $LASTEXITCODE -eq 0 -and $installedScons -eq $sconsVersion
    & $python -c 'import importlib.util, sys; sys.exit(0 if importlib.util.find_spec("zstandard") else 1)'
    if (-not $sconsReady -or $LASTEXITCODE -ne 0) {
        $pipArguments = @('-m', 'pip', 'install', '--disable-pip-version-check', "scons==$sconsVersion", 'zstandard==0.25.0')
        if ($HttpProxy) { $pipArguments += @('--proxy', $HttpProxy) }
        Invoke-Checked $python $pipArguments
    }

    Push-Location $sourceRoot
    try {
        $d3d12 = if ($WithDirect3D12) { 'yes' } else { 'no' }
        $sconsOptions = @('platform=windows', "target=$Target",
            'arch=x86_64', 'module_mono_enabled=yes',
            "d3d12=$d3d12", 'accesskit=no', 'angle=no', 'debug_symbols=yes', 'compiledb=yes')
        if ($ProgramSuffix) { $sconsOptions += "program_suffix=$ProgramSuffix" }
        if ($Clean) { Invoke-Checked $python (@('-m', 'SCons', '-c') + $sconsOptions) }
        Invoke-Checked $python (@('-m', 'SCons', '-j', "$Jobs") + $sconsOptions)

        New-Item -ItemType Directory -Force -Path $runtimeRoot | Out-Null
        $binaryName = "godot.windows.$Target.x86_64.mono"
        if ($ProgramSuffix) { $binaryName += ".$ProgramSuffix" }
        foreach ($binaryFile in @("$binaryName.exe", "$binaryName.console.exe", "$binaryName.pdb")) {
            $builtBinary = Join-Path $sourceRoot "bin/$binaryFile"
            $runtimeBinary = Join-Path $runtimeRoot $binaryFile
            if ((Test-Path -LiteralPath $builtBinary) -and $builtBinary -ne $runtimeBinary) {
                Copy-Item -LiteralPath $builtBinary -Destination $runtimeBinary -Force
            }
        }

        if ($Target -eq 'editor' -and -not $SkipManaged) {
            $editorName = if ($ProgramSuffix) { "godot.windows.editor.x86_64.mono.$ProgramSuffix.console.exe" } else { 'godot.windows.editor.x86_64.mono.console.exe' }
            $editor = Join-Path $sourceRoot "bin/$editorName"
            Invoke-Checked $editor @('--headless', '--generate-mono-glue', 'modules/mono/glue')
            foreach ($api in @('GodotSharp', 'GodotSharpEditor')) {
                $generatedIncludes = Join-Path $sourceRoot "modules/mono/glue/GodotSharp/$api/Generated/GeneratedIncludes.props"
                if (-not (Test-Path -LiteralPath $generatedIncludes)) {
                    throw "C# binding generation did not produce $generatedIncludes."
                }
            }
            Invoke-Checked $python @('modules/mono/build_scripts/build_assemblies.py',
                '--godot-output-dir', $runtimeRoot, '--godot-platform', 'windows')
            # GodotTools and SDK packaging currently use the source tree's bin
            # directory even when build_assemblies redirects the API output.
            $builtTools = Join-Path $sourceRoot 'bin/GodotSharp/Tools'
            $runtimeManagedRoot = Join-Path $runtimeRoot 'GodotSharp'
            if ($runtimeRoot -ne (Join-Path $sourceRoot 'bin')) {
                Copy-Item -LiteralPath $builtTools -Destination $runtimeManagedRoot -Recurse -Force
            }
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

Write-Host "Godot $Target build completed: $runtimeRoot"
