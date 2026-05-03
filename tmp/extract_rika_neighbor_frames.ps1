param(
    [string]$Root = (Resolve-Path ".").Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$FrameSize = 256
$Items = @(
    [PSCustomObject]@{ Sheet = "assets\generated\rika\dash.png"; Prefix = "dash"; Indices = @(4, 5, 6, 7) },
    [PSCustomObject]@{ Sheet = "assets\generated\rika\jump.png"; Prefix = "jump"; Indices = @(3, 4, 5, 6) }
)

foreach ($item in $Items) {
    $sourcePath = Join-Path $Root $item.Sheet
    $source = [System.Drawing.Bitmap]::FromFile($sourcePath)
    try {
        foreach ($index in $item.Indices) {
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
                (($index % 4) * $FrameSize),
                ([Math]::Floor($index / 4) * $FrameSize),
                $FrameSize,
                $FrameSize
            )
            $dstRect = New-Object System.Drawing.Rectangle -ArgumentList @(0, 0, $FrameSize, $FrameSize)
            $graphics.DrawImage($source, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
            $graphics.Dispose()

            $outPath = Join-Path $Root ("tmp\rika_{0}_{1:D2}_current.png" -f $item.Prefix, $index)
            if (Test-Path -LiteralPath $outPath) {
                Remove-Item -LiteralPath $outPath -Force
            }
            $frame.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
            $frame.Dispose()
            Write-Host $outPath
        }
    }
    finally {
        $source.Dispose()
    }
}
