param(
    [string]$Root = (Resolve-Path ".").Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$OutputDir = Join-Path $Root "assets\generated\rika"
$Targets = @(
    "dash.png",
    "jump.png",
    "jumpup.png",
    "inair.png",
    "isfalling.png",
    "land.png"
)

function Open-BitmapClone {
    param([string]$Path)

    $image = [System.Drawing.Image]::FromFile($Path)
    try {
        $bitmap = New-Object System.Drawing.Bitmap -ArgumentList @(
            $image.Width,
            $image.Height,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
        )
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

function Is-GreenResidue {
    param([System.Drawing.Color]$Pixel)

    return $Pixel.A -gt 0 `
        -and $Pixel.G -gt 95 `
        -and $Pixel.G -gt ($Pixel.R + 22) `
        -and $Pixel.G -gt ($Pixel.B + 16)
}

function Despill-Green {
    param([System.Drawing.Color]$Pixel)

    if ($Pixel.A -eq 0) {
        return $Pixel
    }

    if ($Pixel.G -gt ($Pixel.R + 8) -and $Pixel.G -gt ($Pixel.B + 8)) {
        $newG = [Math]::Min([int]$Pixel.G, [Math]::Max([int]$Pixel.R, [int]$Pixel.B) + 4)
        return [System.Drawing.Color]::FromArgb($Pixel.A, $Pixel.R, $newG, $Pixel.B)
    }

    return $Pixel
}

foreach ($target in $Targets) {
    $path = Join-Path $OutputDir $target
    $bitmap = Open-BitmapClone -Path $path
    try {
        $removed = 0
        $despilled = 0
        for ($y = 0; $y -lt $bitmap.Height; $y++) {
            for ($x = 0; $x -lt $bitmap.Width; $x++) {
                $pixel = $bitmap.GetPixel($x, $y)
                if (Is-GreenResidue -Pixel $pixel) {
                    $bitmap.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                    $removed += 1
                    continue
                }

                $fixed = Despill-Green -Pixel $pixel
                if ($fixed.ToArgb() -ne $pixel.ToArgb()) {
                    $bitmap.SetPixel($x, $y, $fixed)
                    $despilled += 1
                }
            }
        }

        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
        }
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host ("{0}: removed {1}, despilled {2}" -f $target, $removed, $despilled)
    }
    finally {
        $bitmap.Dispose()
    }
}
