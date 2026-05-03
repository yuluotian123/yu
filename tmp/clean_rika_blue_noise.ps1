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

$BlueKey = [System.Drawing.Color]::FromArgb(255, 0x5A, 0x7E, 0x87)

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

function Get-ColorDistance {
    param(
        [System.Drawing.Color]$A,
        [System.Drawing.Color]$B
    )

    $dr = [int]$A.R - [int]$B.R
    $dg = [int]$A.G - [int]$B.G
    $db = [int]$A.B - [int]$B.B
    return [Math]::Sqrt(($dr * $dr) + ($dg * $dg) + ($db * $db))
}

function Is-BlueBackgroundNoise {
    param([System.Drawing.Color]$Pixel)

    if ($Pixel.A -eq 0) {
        return $false
    }

    $distance = Get-ColorDistance -A $Pixel -B $BlueKey
    $blueGreenDominant = $Pixel.B -gt ($Pixel.R + 16) -and $Pixel.G -gt ($Pixel.R + 10)
    $closeToOriginalBlueBg = $distance -le 82
    $knownScaledResidue = (
        ($Pixel.R -eq 0x72 -and $Pixel.G -eq 0x88 -and $Pixel.B -eq 0x8F) -or
        ($Pixel.R -eq 0x4A -and $Pixel.G -eq 0x5C -and $Pixel.B -eq 0x65) -or
        ($Pixel.R -eq 0x4E -and $Pixel.G -eq 0x5F -and $Pixel.B -eq 0x69) -or
        ($Pixel.R -eq 0x70 -and $Pixel.G -eq 0x88 -and $Pixel.B -eq 0x92) -or
        ($Pixel.R -eq 0x78 -and $Pixel.G -eq 0x8A -and $Pixel.B -eq 0x94)
    )
    $lowBlueGrayResidue = $Pixel.B -gt 70 `
        -and $Pixel.G -gt 70 `
        -and $Pixel.R -lt 120 `
        -and $Pixel.B -gt ($Pixel.R + 18) `
        -and $Pixel.G -gt ($Pixel.R + 12)

    return ($blueGreenDominant -and $closeToOriginalBlueBg) -or $knownScaledResidue -or $lowBlueGrayResidue
}

foreach ($target in $Targets) {
    $path = Join-Path $Root $target
    $bitmap = Open-BitmapClone -Path $path
    try {
        $removed = 0
        for ($y = 0; $y -lt $bitmap.Height; $y++) {
            for ($x = 0; $x -lt $bitmap.Width; $x++) {
                if (Is-BlueBackgroundNoise -Pixel $bitmap.GetPixel($x, $y)) {
                    $bitmap.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                    $removed += 1
                }
            }
        }

        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
        }
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host ("{0}: removed {1} blue/cyan background pixels" -f ([System.IO.Path]::GetFileName($path)), $removed)
    }
    finally {
        $bitmap.Dispose()
    }
}
