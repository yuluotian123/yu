param(
    [string]$Root = (Resolve-Path ".").Path,
    [string]$SourcePath = "assets\generated\rika\idle.png",
    [string]$Output = "tmp\rika_idle_hd_locked.png",
    [switch]$Crisp,
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$FrameSize = 256
$FrameCount = 4

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
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
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

function Resize-Bitmap {
    param(
        [System.Drawing.Bitmap]$Source,
        [int]$Width,
        [int]$Height,
        [System.Drawing.Drawing2D.InterpolationMode]$InterpolationMode
    )

    $result = New-BitmapArgb -Width $Width -Height $Height
    $graphics = [System.Drawing.Graphics]::FromImage($result)
    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = $InterpolationMode
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
        $graphics.DrawImage($Source, 0, 0, $Width, $Height)
    }
    finally {
        $graphics.Dispose()
    }

    return $result
}

function Copy-Frame {
    param(
        [System.Drawing.Bitmap]$Sheet,
        [int]$Index
    )

    $frame = New-BitmapArgb -Width $FrameSize -Height $FrameSize
    $graphics = [System.Drawing.Graphics]::FromImage($frame)
    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $sourceRect = New-Object System.Drawing.Rectangle -ArgumentList ($Index * $FrameSize), 0, $FrameSize, $FrameSize
        $destRect = New-Object System.Drawing.Rectangle -ArgumentList 0, 0, $FrameSize, $FrameSize
        $graphics.DrawImage($Sheet, $destRect, $sourceRect, [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally {
        $graphics.Dispose()
    }

    return $frame
}

function Get-Luminance {
    param([System.Drawing.Color]$Color)

    return (0.2126 * $Color.R) + (0.7152 * $Color.G) + (0.0722 * $Color.B)
}

function ConvertTo-Hsl {
    param([System.Drawing.Color]$Color)

    $r = $Color.R / 255.0
    $g = $Color.G / 255.0
    $b = $Color.B / 255.0

    $max = [Math]::Max($r, [Math]::Max($g, $b))
    $min = [Math]::Min($r, [Math]::Min($g, $b))
    $h = 0.0
    $s = 0.0
    $l = ($max + $min) / 2.0

    if ($max -ne $min) {
        $d = $max - $min
        if ($l -gt 0.5) {
            $s = $d / (2.0 - $max - $min)
        }
        else {
            $s = $d / ($max + $min)
        }

        if ($max -eq $r) {
            $h = (($g - $b) / $d)
            if ($g -lt $b) {
                $h += 6.0
            }
        }
        elseif ($max -eq $g) {
            $h = (($b - $r) / $d) + 2.0
        }
        else {
            $h = (($r - $g) / $d) + 4.0
        }

        $h /= 6.0
    }

    return [pscustomobject]@{ H = $h; S = $s; L = $l }
}

function Get-HueToRgb {
    param(
        [double]$P,
        [double]$Q,
        [double]$T
    )

    if ($T -lt 0.0) { $T += 1.0 }
    if ($T -gt 1.0) { $T -= 1.0 }
    if ($T -lt (1.0 / 6.0)) { return $P + (($Q - $P) * 6.0 * $T) }
    if ($T -lt 0.5) { return $Q }
    if ($T -lt (2.0 / 3.0)) { return $P + (($Q - $P) * ((2.0 / 3.0) - $T) * 6.0) }
    return $P
}

function ConvertFrom-Hsl {
    param(
        [int]$Alpha,
        [double]$Hue,
        [double]$Saturation,
        [double]$Lightness
    )

    if ($Saturation -eq 0.0) {
        $value = [int][Math]::Round($Lightness * 255.0)
        return [System.Drawing.Color]::FromArgb($Alpha, $value, $value, $value)
    }

    if ($Lightness -lt 0.5) {
        $q = $Lightness * (1.0 + $Saturation)
    }
    else {
        $q = $Lightness + $Saturation - ($Lightness * $Saturation)
    }

    $p = (2.0 * $Lightness) - $q
    $r = Get-HueToRgb -P $p -Q $q -T ($Hue + (1.0 / 3.0))
    $g = Get-HueToRgb -P $p -Q $q -T $Hue
    $b = Get-HueToRgb -P $p -Q $q -T ($Hue - (1.0 / 3.0))

    return [System.Drawing.Color]::FromArgb(
        $Alpha,
        [Math]::Max(0, [Math]::Min(255, [int][Math]::Round($r * 255.0))),
        [Math]::Max(0, [Math]::Min(255, [int][Math]::Round($g * 255.0))),
        [Math]::Max(0, [Math]::Min(255, [int][Math]::Round($b * 255.0)))
    )
}

function Test-BackgroundResidue {
    param([System.Drawing.Color]$Color)

    if ($Color.A -eq 0) {
        return $false
    }

    $greenResidue = $Color.G -gt ($Color.R + 52) -and $Color.G -gt ($Color.B + 36) -and $Color.G -gt 80
    $blueResidue = $Color.B -gt ($Color.R + 18) -and $Color.G -gt ($Color.R + 12) -and $Color.B -gt 70 -and $Color.G -gt 70 -and $Color.R -lt 128
    return $greenResidue -or $blueResidue
}

function Blend-Channel {
    param(
        [int]$A,
        [int]$B,
        [double]$T
    )

    return [Math]::Max(0, [Math]::Min(255, [int][Math]::Round(($A * (1.0 - $T)) + ($B * $T))))
}

function Enhance-Frame {
    param(
        [System.Drawing.Bitmap]$Original,
        [bool]$UseCrisp
    )

    if ($UseCrisp) {
        $downsampleMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $outlineBlend = 0.78
        $saturationScale = 1.12
        $contrastScale = 1.10
    }
    else {
        $downsampleMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $outlineBlend = 0.55
        $saturationScale = 1.08
        $contrastScale = 1.06
    }

    $small = Resize-Bitmap `
        -Source $Original `
        -Width 128 `
        -Height 128 `
        -InterpolationMode $downsampleMode
    try {
        $smooth = Resize-Bitmap `
            -Source $small `
            -Width $FrameSize `
            -Height $FrameSize `
            -InterpolationMode ([System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic)
        try {
            $result = New-BitmapArgb -Width $FrameSize -Height $FrameSize

            for ($y = 0; $y -lt $FrameSize; $y++) {
                for ($x = 0; $x -lt $FrameSize; $x++) {
                    $o = $Original.GetPixel($x, $y)
                    $s = $smooth.GetPixel($x, $y)

                    if ((Test-BackgroundResidue -Color $o) -or (Test-BackgroundResidue -Color $s)) {
                        $result.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                        continue
                    }

                    $alpha = [Math]::Max($s.A, [int]($o.A * 0.88))
                    if ($alpha -lt 8) {
                        $result.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                        continue
                    }

                    $r = $s.R
                    $g = $s.G
                    $b = $s.B

                    # Keep dark ink and sword outlines anchored to the source silhouette.
                    if ($o.A -gt 150 -and (Get-Luminance -Color $o) -lt 78) {
                        $r = Blend-Channel -A $s.R -B $o.R -T $outlineBlend
                        $g = Blend-Channel -A $s.G -B $o.G -T $outlineBlend
                        $b = Blend-Channel -A $s.B -B $o.B -T $outlineBlend
                    }

                    $color = [System.Drawing.Color]::FromArgb($alpha, $r, $g, $b)
                    $hsl = ConvertTo-Hsl -Color $color
                    $sat = [Math]::Max(0.0, [Math]::Min(1.0, $hsl.S * $saturationScale))
                    $light = $hsl.L

                    if ($light -gt 0.18 -and $light -lt 0.88) {
                        $light = (($light - 0.5) * $contrastScale) + 0.5
                    }

                    $light = [Math]::Max(0.0, [Math]::Min(1.0, $light))
                    $color = ConvertFrom-Hsl -Alpha $alpha -Hue $hsl.H -Saturation $sat -Lightness $light
                    $result.SetPixel($x, $y, $color)
                }
            }

            return $result
        }
        finally {
            $smooth.Dispose()
        }
    }
    finally {
        $small.Dispose()
    }
}

function Save-Png {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )

    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Force
    }

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

$inputPath = if ([System.IO.Path]::IsPathRooted($SourcePath)) { $SourcePath } else { Join-Path $Root $SourcePath }
$previewOutputPath = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $Root $Output }
$finalPath = Join-Path $Root "assets\generated\rika\idle.png"
$backupPath = Join-Path $Root "tmp\rika_idle_before_hd.png"

$sheet = Open-BitmapClone -Path $inputPath
try {
    if ($sheet.Width -ne ($FrameSize * $FrameCount) -or $sheet.Height -ne $FrameSize) {
        throw "Expected idle sheet $($FrameSize * $FrameCount)x$FrameSize, got $($sheet.Width)x$($sheet.Height)."
    }

    $outSheet = New-BitmapArgb -Width $sheet.Width -Height $sheet.Height
    try {
        for ($i = 0; $i -lt $FrameCount; $i++) {
            $frame = Copy-Frame -Sheet $sheet -Index $i
            try {
                $enhanced = Enhance-Frame -Original $frame -UseCrisp ([bool]$Crisp)
                try {
                    $graphics = [System.Drawing.Graphics]::FromImage($outSheet)
                    try {
                        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                        $graphics.DrawImage($enhanced, $i * $FrameSize, 0, $FrameSize, $FrameSize)
                    }
                    finally {
                        $graphics.Dispose()
                    }
                }
                finally {
                    $enhanced.Dispose()
                }
            }
            finally {
                $frame.Dispose()
            }
        }

        Save-Png -Bitmap $outSheet -Path $previewOutputPath
        Write-Host ("preview: {0}" -f $previewOutputPath)

        if ($Apply) {
            if (-not (Test-Path -LiteralPath $backupPath)) {
                Copy-Item -LiteralPath $finalPath -Destination $backupPath
                Write-Host ("backup: {0}" -f $backupPath)
            }
            Save-Png -Bitmap $outSheet -Path $finalPath
            Write-Host ("applied: {0}" -f $finalPath)
        }
    }
    finally {
        $outSheet.Dispose()
    }
}
finally {
    $sheet.Dispose()
}
