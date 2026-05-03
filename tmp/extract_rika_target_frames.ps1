param(
    [string]$Root = (Resolve-Path ".").Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$FrameSize = 256
$Items = @(
    [PSCustomObject]@{ Sheet = "assets\generated\rika\dash.png"; Index = 6; Out = "tmp\rika_dash_frame_07_current.png" },
    [PSCustomObject]@{ Sheet = "assets\generated\rika\jump.png"; Index = 4; Out = "tmp\rika_jump_frame_05_current.png" }
)

foreach ($item in $Items) {
    $sourcePath = Join-Path $Root $item.Sheet
    $outPath = Join-Path $Root $item.Out

    $source = [System.Drawing.Bitmap]::FromFile($sourcePath)
    try {
        $frame = New-Object System.Drawing.Bitmap -ArgumentList @(
            $FrameSize,
            $FrameSize,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
        )
        $graphics = [System.Drawing.Graphics]::FromImage($frame)
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

        $srcRect = New-Object System.Drawing.Rectangle -ArgumentList @(
            (($item.Index % 4) * $FrameSize),
            ([Math]::Floor($item.Index / 4) * $FrameSize),
            $FrameSize,
            $FrameSize
        )
        $dstRect = New-Object System.Drawing.Rectangle -ArgumentList @(0, 0, $FrameSize, $FrameSize)
        $graphics.DrawImage($source, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
        $graphics.Dispose()

        if (Test-Path -LiteralPath $outPath) {
            Remove-Item -LiteralPath $outPath -Force
        }
        $frame.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $frame.Dispose()
        Write-Host $outPath
    }
    finally {
        $source.Dispose()
    }
}
