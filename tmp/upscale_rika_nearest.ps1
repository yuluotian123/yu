param(
    [string]$Root = (Resolve-Path ".").Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$Targets = @(
    "assets\generated\rika\idle.png",
    "assets\generated\rika\image.png",
    "assets\generated\rika\run.png"
)

function New-BitmapArgb {
    param([int]$Width, [int]$Height)

    return New-Object System.Drawing.Bitmap -ArgumentList @(
        $Width,
        $Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
    )
}

function Open-BitmapClone {
    param([string]$Path)

    $image = [System.Drawing.Image]::FromFile($Path)
    try {
        $bitmap = New-BitmapArgb -Width $image.Width -Height $image.Height
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $graphics.DrawImage($image, 0, 0, $image.Width, $image.Height)
        $graphics.Dispose()
        return $bitmap
    }
    finally {
        $image.Dispose()
    }
}

function Expand-Nearest2x {
    param([System.Drawing.Bitmap]$Source)

    $result = New-BitmapArgb -Width ($Source.Width * 2) -Height ($Source.Height * 2)
    for ($y = 0; $y -lt $Source.Height; $y++) {
        for ($x = 0; $x -lt $Source.Width; $x++) {
            $pixel = $Source.GetPixel($x, $y)
            $dx = $x * 2
            $dy = $y * 2
            $result.SetPixel($dx, $dy, $pixel)
            $result.SetPixel($dx + 1, $dy, $pixel)
            $result.SetPixel($dx, $dy + 1, $pixel)
            $result.SetPixel($dx + 1, $dy + 1, $pixel)
        }
    }

    return $result
}

foreach ($target in $Targets) {
    $path = if ([System.IO.Path]::IsPathRooted($target)) { $target } else { Join-Path $Root $target }
    $source = Open-BitmapClone -Path $path
    try {
        $expanded = Expand-Nearest2x -Source $source
        try {
            if (Test-Path -LiteralPath $path) {
                Remove-Item -LiteralPath $path -Force
            }
            $expanded.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
            Write-Host ("{0}: {1}x{2} -> {3}x{4}" -f `
                ([System.IO.Path]::GetFileName($path)),
                $source.Width,
                $source.Height,
                $expanded.Width,
                $expanded.Height)
        }
        finally {
            $expanded.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}
