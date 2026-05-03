param(
    [string]$Root = (Resolve-Path ".").Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$FrameSize = 256

function New-BitmapArgb {
    param([int]$Width, [int]$Height)

    $bitmap = New-Object System.Drawing.Bitmap -ArgumentList @(
        $Width,
        $Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
    )
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.Dispose()
    return $bitmap
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

function Extract-Frame {
    param(
        [string]$SheetPath,
        [int]$Index
    )

    $sheet = Open-BitmapClone -Path $SheetPath
    try {
        $frame = New-BitmapArgb -Width $FrameSize -Height $FrameSize
        $graphics = [System.Drawing.Graphics]::FromImage($frame)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

        $srcRect = New-Object System.Drawing.Rectangle -ArgumentList @(
            (($Index % 4) * $FrameSize),
            ([Math]::Floor($Index / 4) * $FrameSize),
            $FrameSize,
            $FrameSize
        )
        $dstRect = New-Object System.Drawing.Rectangle -ArgumentList @(0, 0, $FrameSize, $FrameSize)
        $graphics.DrawImage($sheet, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
        $graphics.Dispose()
        return $frame
    }
    finally {
        $sheet.Dispose()
    }
}

function Get-AlphaBounds {
    param([System.Drawing.Bitmap]$Bitmap)

    $minX = $Bitmap.Width
    $minY = $Bitmap.Height
    $maxX = -1
    $maxY = -1

    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            if ($Bitmap.GetPixel($x, $y).A -gt 0) {
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    return [PSCustomObject]@{
        MinX = $minX
        MinY = $minY
        MaxX = $maxX
        MaxY = $maxY
    }
}

function Transform-Shear {
    param(
        [System.Drawing.Bitmap]$Source,
        [double]$Shear,
        [double]$PivotY,
        [int]$OffsetX,
        [int]$OffsetY
    )

    $result = New-BitmapArgb -Width $Source.Width -Height $Source.Height

    for ($ny = 0; $ny -lt $Source.Height; $ny++) {
        for ($nx = 0; $nx -lt $Source.Width; $nx++) {
            $sourceY = $ny - $OffsetY
            if ($sourceY -lt 0 -or $sourceY -ge $Source.Height) {
                continue
            }

            $sourceX = [int][Math]::Round($nx - ($Shear * ($sourceY - $PivotY)) - $OffsetX)
            if ($sourceX -lt 0 -or $sourceX -ge $Source.Width) {
                continue
            }

            $pixel = $Source.GetPixel($sourceX, $sourceY)
            if ($pixel.A -gt 0) {
                $result.SetPixel($nx, $ny, $pixel)
            }
        }
    }

    return $result
}

function Transform-Rotate {
    param(
        [System.Drawing.Bitmap]$Source,
        [double]$Degrees,
        [int]$OffsetX,
        [int]$OffsetY
    )

    $bounds = Get-AlphaBounds -Bitmap $Source
    $cx = ($bounds.MinX + $bounds.MaxX) / 2.0
    $cy = ($bounds.MinY + $bounds.MaxY) / 2.0
    $radians = $Degrees * [Math]::PI / 180.0
    $cos = [Math]::Cos($radians)
    $sin = [Math]::Sin($radians)
    $result = New-BitmapArgb -Width $Source.Width -Height $Source.Height

    for ($ny = 0; $ny -lt $Source.Height; $ny++) {
        for ($nx = 0; $nx -lt $Source.Width; $nx++) {
            $dx = $nx - $OffsetX - $cx
            $dy = $ny - $OffsetY - $cy
            $sourceX = [int][Math]::Round(($dx * $cos) + ($dy * $sin) + $cx)
            $sourceY = [int][Math]::Round((-1.0 * $dx * $sin) + ($dy * $cos) + $cy)
            if ($sourceX -lt 0 -or $sourceY -lt 0 -or $sourceX -ge $Source.Width -or $sourceY -ge $Source.Height) {
                continue
            }

            $pixel = $Source.GetPixel($sourceX, $sourceY)
            if ($pixel.A -gt 0) {
                $result.SetPixel($nx, $ny, $pixel)
            }
        }
    }

    return $result
}

function Save-Png {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Force
    }
    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

$dashBase = Extract-Frame -SheetPath (Join-Path $Root "assets\generated\rika\dash.png") -Index 6
try {
    $dashBrake = Transform-Shear -Source $dashBase -Shear 0.14 -PivotY 222 -OffsetX 3 -OffsetY 0
    try {
        Save-Png -Bitmap $dashBrake -Path (Join-Path $Root "tmp\rika_dash_frame_07_corrected.png")
    }
    finally {
        $dashBrake.Dispose()
    }
}
finally {
    $dashBase.Dispose()
}

$jumpBase = Extract-Frame -SheetPath (Join-Path $Root "assets\generated\rika\jump.png") -Index 4
try {
    $jumpBackLean = Transform-Rotate -Source $jumpBase -Degrees -13 -OffsetX 0 -OffsetY 0
    try {
        Save-Png -Bitmap $jumpBackLean -Path (Join-Path $Root "tmp\rika_jump_frame_05_corrected.png")
    }
    finally {
        $jumpBackLean.Dispose()
    }
}
finally {
    $jumpBase.Dispose()
}

Write-Host "Corrected keyframe previews written."
