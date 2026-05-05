param(
    [string]$Root = (Resolve-Path ".").Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

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
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.DrawImage($image, 0, 0, $image.Width, $image.Height)
        }
        finally {
            $graphics.Dispose()
        }
        return $bitmap
    }
    finally {
        $image.Dispose()
    }
}

function Test-GreenResidue {
    param([System.Drawing.Color]$Pixel)

    if ($Pixel.A -eq 0) {
        return $false
    }

    return $Pixel.G -gt 45 `
        -and $Pixel.G -gt ($Pixel.R + 14) `
        -and $Pixel.G -gt ($Pixel.B + 10) `
        -and (($Pixel.R -lt 190) -or ($Pixel.B -lt 190))
}

function Clean-Png {
    param([string]$Path)

    $bitmap = Open-BitmapClone -Path $Path
    try {
        $removed = 0
        $despilled = 0
        for ($y = 0; $y -lt $bitmap.Height; $y++) {
            for ($x = 0; $x -lt $bitmap.Width; $x++) {
                $p = $bitmap.GetPixel($x, $y)
                if (-not (Test-GreenResidue -Pixel $p)) {
                    continue
                }

                if ($p.A -lt 96 -or $p.G -gt ($p.R + 40) -or $p.G -gt ($p.B + 34)) {
                    $bitmap.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                    $removed++
                }
                else {
                    $target = [Math]::Max($p.R, $p.B)
                    $newG = [int][Math]::Round(($p.G * 0.20) + ($target * 0.80))
                    $bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($p.A, $p.R, $newG, $p.B))
                    $despilled++
                }
            }
        }

        if (Test-Path -LiteralPath $Path) {
            Remove-Item -LiteralPath $Path -Force
        }
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host ("{0}: removed={1}, despilled={2}" -f $Path, $removed, $despilled)
    }
    finally {
        $bitmap.Dispose()
    }
}

$targets = @(
    "assets\sprites\rika_jump.png",
    "assets\sprites\rika_jumpup.png",
    "assets\sprites\rika_inair.png",
    "assets\sprites\rika_isfalling.png",
    "assets\sprites\rika_land.png"
)

for ($i = 0; $i -lt 8; $i++) {
    $targets += ("assets\sprites\frames\rika_jump_{0:00}.png" -f $i)
}

foreach ($target in $targets) {
    $path = Join-Path $Root $target
    if (Test-Path -LiteralPath $path) {
        Clean-Png -Path $path
    }
}
