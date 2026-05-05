param(
    [string]$Root = (Resolve-Path ".").Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$FrameSize = 128
$TargetCenterY = 67
$GroundBottomY = 117
$MaxScale = 0.90
$MaxContentWidth = 104.0
$MaxContentHeight = 98.0

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

function Get-OpaqueBounds {
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

    if ($maxX -lt 0) {
        return New-Object System.Drawing.Rectangle -ArgumentList 0, 0, $Bitmap.Width, $Bitmap.Height
    }

    return New-Object System.Drawing.Rectangle -ArgumentList $minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1)
}

function Resize-Frame {
    param(
        [System.Drawing.Bitmap]$Frame,
        [bool]$Grounded
    )

    $bounds = Get-OpaqueBounds -Bitmap $Frame
    $scale = [Math]::Min($MaxScale, [Math]::Min($MaxContentWidth / $bounds.Width, $MaxContentHeight / $bounds.Height))
    $destWidth = [Math]::Max(1, [int][Math]::Round($bounds.Width * $scale))
    $destHeight = [Math]::Max(1, [int][Math]::Round($bounds.Height * $scale))
    $destX = [int][Math]::Round(($FrameSize - $destWidth) / 2.0)

    if ($Grounded) {
        $destY = $GroundBottomY - $destHeight + 1
    }
    else {
        $destY = [int][Math]::Round($TargetCenterY - ($destHeight / 2.0))
    }

    if ($destY -lt 2) {
        $destY = 2
    }
    if (($destY + $destHeight) -gt ($FrameSize - 2)) {
        $destY = $FrameSize - 2 - $destHeight
    }

    $result = New-BitmapArgb -Width $FrameSize -Height $FrameSize
    $graphics = [System.Drawing.Graphics]::FromImage($result)
    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
        $destRect = New-Object System.Drawing.Rectangle -ArgumentList $destX, $destY, $destWidth, $destHeight
        $graphics.DrawImage($Frame, $destRect, $bounds, [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally {
        $graphics.Dispose()
    }

    return $result
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

function Build-Sheet {
    param(
        [System.Drawing.Bitmap[]]$Frames,
        [int]$Columns,
        [string]$OutPath
    )

    $rows = [int][Math]::Ceiling($Frames.Count / [double]$Columns)
    $sheet = New-BitmapArgb -Width ($FrameSize * $Columns) -Height ($FrameSize * $rows)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($sheet)
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.Clear([System.Drawing.Color]::Transparent)
            for ($i = 0; $i -lt $Frames.Count; $i++) {
                $x = ($i % $Columns) * $FrameSize
                $y = [Math]::Floor($i / $Columns) * $FrameSize
                $graphics.DrawImage($Frames[$i], $x, $y, $FrameSize, $FrameSize)
            }
        }
        finally {
            $graphics.Dispose()
        }

        Save-Png -Bitmap $sheet -Path $OutPath
    }
    finally {
        $sheet.Dispose()
    }
}

$framesDir = Join-Path $Root "assets\sprites\frames"
$spritesDir = Join-Path $Root "assets\sprites"
$backupDir = Join-Path $Root "tmp\rika_jump_before_scale_fix"
if (-not (Test-Path -LiteralPath $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir | Out-Null
    foreach ($name in @("rika_jump.png", "rika_jumpup.png", "rika_inair.png", "rika_isfalling.png", "rika_land.png")) {
        $source = Join-Path $spritesDir $name
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $backupDir $name)
        }
    }
}

$resizedFrames = New-Object 'System.Collections.Generic.List[System.Drawing.Bitmap]'
for ($i = 0; $i -lt 8; $i++) {
    $path = Join-Path $framesDir ("rika_jump_{0:00}.png" -f $i)
    $frame = Open-BitmapClone -Path $path
    try {
        $grounded = $i -eq 0 -or $i -eq 6 -or $i -eq 7
        $resized = Resize-Frame -Frame $frame -Grounded $grounded
        $resizedFrames.Add($resized)
        Save-Png -Bitmap $resized -Path $path
    }
    finally {
        $frame.Dispose()
    }
}

try {
    Build-Sheet -Frames $resizedFrames.ToArray() -Columns 4 -OutPath (Join-Path $spritesDir "rika_jump.png")
    Build-Sheet -Frames @($resizedFrames[0], $resizedFrames[1], $resizedFrames[2]) -Columns 3 -OutPath (Join-Path $spritesDir "rika_jumpup.png")
    Build-Sheet -Frames @($resizedFrames[3]) -Columns 1 -OutPath (Join-Path $spritesDir "rika_inair.png")
    Build-Sheet -Frames @($resizedFrames[4], $resizedFrames[5]) -Columns 2 -OutPath (Join-Path $spritesDir "rika_isfalling.png")
    Build-Sheet -Frames @($resizedFrames[6], $resizedFrames[7]) -Columns 2 -OutPath (Join-Path $spritesDir "rika_land.png")
}
finally {
    foreach ($frame in $resizedFrames) {
        $frame.Dispose()
    }
}

Write-Host "Scaled jump-related Rika frames to match idle/run size."
