[CmdletBinding()]
param(
    [string]$SourceDirectory = 'D:\DCC\Godot_NVIDIA_RTX',
    [string]$SdkArchive,
    [string]$HttpProxy
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
if (-not $SdkArchive) {
    $SdkArchive = Join-Path $projectRoot 'tools/godot/streamline-sdk-v2.10.0.zip'
}
if (-not (Test-Path -LiteralPath $SdkArchive)) {
    New-Item -ItemType Directory -Force -Path (Split-Path $SdkArchive -Parent) | Out-Null
    $downloadArguments = @('--location', '--fail', '--retry', '2', '--output', $SdkArchive,
        'https://github.com/NVIDIA-RTX/Streamline/releases/download/v2.10.0/streamline-sdk-v2.10.0.zip')
    if ($HttpProxy) { $downloadArguments += @('--proxy', $HttpProxy) }
    & curl.exe @downloadArguments
    if ($LASTEXITCODE -ne 0) { throw 'Streamline download failed.' }
}

$expectedHash = 'B0CC810C86B0335D4FD76A02D35FFF53E528D2FB66C29860565130F76C9383B0'
if ((Get-FileHash -LiteralPath $SdkArchive -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Streamline 2.10.0 archive does not match the official release SHA256.'
}

$outputDirectory = Join-Path ([IO.Path]::GetFullPath($SourceDirectory)) 'bin'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($SdkArchive))
try {
    foreach ($entry in $archive.Entries) {
        $fileName = $null
        if ($entry.FullName -match '^bin/x64/[^/]+\.(dll|license\.txt)$') {
            $fileName = [IO.Path]::GetFileName($entry.FullName)
        }
        elseif ($entry.FullName -eq 'license.txt') {
            $fileName = 'Streamline.LICENSE.txt'
        }
        elseif ($entry.FullName -eq '3rd-party-licenses.md') {
            $fileName = 'Streamline.3rd-party-licenses.md'
        }
        elseif ($entry.FullName -eq 'NVIDIA Nsight Perf SDK License (28Sept2022).pdf') {
            $fileName = $entry.FullName
        }
        if ($fileName) {
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $outputDirectory $fileName), $true)
        }
    }
}
finally {
    $archive.Dispose()
}

foreach ($requiredFile in @('sl.interposer.dll', 'sl.common.dll', 'sl.dlss.dll', 'sl.dlss_d.dll',
        'nvngx_dlss.dll', 'nvngx_dlssd.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $outputDirectory $requiredFile))) {
        throw "Missing Streamline runtime: $requiredFile"
    }
}
Write-Host "Streamline 2.10.0 production runtimes installed: $outputDirectory"
