param(
    [string]$Root = (Resolve-Path ".").Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$FrameSize = 256
$SheetPath = Join-Path $Root "assets\generated\rika\idle.png"
$OutputDir = Join-Path $Root "tmp\rika_idle_pose_refs"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$sheet = [System.Drawing.Bitmap]::FromFile($SheetPath)
try {
    for ($i = 0; $i -lt 4; $i++) {
        $frame = New-Object System.Drawing.Bitmap -ArgumentList @(
            $FrameSize,
            $FrameSize,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
        )
        $graphics = [System.Drawing.Graphics]::FromImage($frame)
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

        $sourceX = [int]($i * $FrameSize)
        $srcRect = New-Object System.Drawing.Rectangle -ArgumentList @($sourceX, 0, $FrameSize, $FrameSize)
        $dstRect = New-Object System.Drawing.Rectangle -ArgumentList @(0, 0, $FrameSize, $FrameSize)
        $graphics.DrawImage($sheet, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
        $graphics.Dispose()

        $path = Join-Path $OutputDir ("idle_{0:D2}.png" -f $i)
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
        }
        $frame.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $frame.Dispose()
        Write-Host $path
    }
}
finally {
    $sheet.Dispose()
}
