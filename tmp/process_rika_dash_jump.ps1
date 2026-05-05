param(
    [string]$Root = (Resolve-Path ".").Path,
    [string]$DashSource,
    [string]$JumpSource
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$FrameSize = 128
$Columns = 4
$Rows = 2
$Key = [System.Drawing.Color]::FromArgb(0, 255, 0)

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

function Test-KeyPixel {
    param([System.Drawing.Color]$Pixel)

    $strongGreen = $Pixel.G -gt 120 -and $Pixel.G -gt ($Pixel.R + 45) -and $Pixel.G -gt ($Pixel.B + 35)
    $closeToKey = (Get-ColorDistance -A $Pixel -B $Key) -lt 130
    return $strongGreen -or $closeToKey
}

function Remove-Chroma {
    param([System.Drawing.Bitmap]$Source)

    $result = New-BitmapArgb -Width $Source.Width -Height $Source.Height
    for ($y = 0; $y -lt $Source.Height; $y++) {
        for ($x = 0; $x -lt $Source.Width; $x++) {
            $p = $Source.GetPixel($x, $y)
            if (Test-KeyPixel -Pixel $p) {
                $result.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
            }
            else {
                $result.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $p.R, $p.G, $p.B))
            }
        }
    }

    # Contract tiny green fringe pixels near the matte.
    $clean = New-BitmapArgb -Width $Source.Width -Height $Source.Height
    for ($y = 0; $y -lt $Source.Height; $y++) {
        for ($x = 0; $x -lt $Source.Width; $x++) {
            $p = $result.GetPixel($x, $y)
            if ($p.A -eq 0) {
                $clean.SetPixel($x, $y, $p)
                continue
            }

            $transparentNeighbors = 0
            for ($ny = [Math]::Max(0, $y - 1); $ny -le [Math]::Min($Source.Height - 1, $y + 1); $ny++) {
                for ($nx = [Math]::Max(0, $x - 1); $nx -le [Math]::Min($Source.Width - 1, $x + 1); $nx++) {
                    if ($result.GetPixel($nx, $ny).A -eq 0) {
                        $transparentNeighbors++
                    }
                }
            }

            if ($transparentNeighbors -ge 5 -and ($p.G -gt ($p.R + 20)) -and ($p.G -gt ($p.B + 16))) {
                $clean.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
            }
            else {
                $clean.SetPixel($x, $y, $p)
            }
        }
    }

    $result.Dispose()
    return $clean
}

function Copy-SourceCell {
    param(
        [System.Drawing.Bitmap]$Source,
        [int]$Index
    )

    $cellWidth = [int]($Source.Width / $Columns)
    $cellHeight = [int]($Source.Height / $Rows)
    $sourceX = ($Index % $Columns) * $cellWidth
    $sourceY = [Math]::Floor($Index / $Columns) * $cellHeight

    $cell = New-BitmapArgb -Width $cellWidth -Height $cellHeight
    $graphics = [System.Drawing.Graphics]::FromImage($cell)
    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $destRect = New-Object System.Drawing.Rectangle -ArgumentList 0, 0, $cellWidth, $cellHeight
        $srcRect = New-Object System.Drawing.Rectangle -ArgumentList $sourceX, $sourceY, $cellWidth, $cellHeight
        $graphics.DrawImage($Source, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally {
        $graphics.Dispose()
    }

    return $cell
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

function Render-Frame128 {
    param(
        [System.Drawing.Bitmap]$Cell,
        [double]$TargetScale = 1.0
    )

    $bounds = Get-OpaqueBounds -Bitmap $Cell
    $frame = New-BitmapArgb -Width $FrameSize -Height $FrameSize
    $graphics = [System.Drawing.Graphics]::FromImage($frame)
    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None

        $maxContentWidth = 116.0
        $maxContentHeight = 114.0
        $scale = [Math]::Min($maxContentWidth / $bounds.Width, $maxContentHeight / $bounds.Height) * $TargetScale
        $destWidth = [Math]::Max(1, [int][Math]::Round($bounds.Width * $scale))
        $destHeight = [Math]::Max(1, [int][Math]::Round($bounds.Height * $scale))
        $destX = [int][Math]::Round(($FrameSize - $destWidth) / 2.0)
        $destY = [int][Math]::Round(($FrameSize - $destHeight) / 2.0) + 3
        if ($destY + $destHeight -gt ($FrameSize - 2)) {
            $destY = $FrameSize - 2 - $destHeight
        }
        if ($destY -lt 2) {
            $destY = 2
        }

        $destRect = New-Object System.Drawing.Rectangle -ArgumentList $destX, $destY, $destWidth, $destHeight
        $graphics.DrawImage($Cell, $destRect, $bounds, [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally {
        $graphics.Dispose()
    }

    return $frame
}

function Clean-GreenResidue {
    param([System.Drawing.Bitmap]$Bitmap)

    $clean = New-BitmapArgb -Width $Bitmap.Width -Height $Bitmap.Height
    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            $p = $Bitmap.GetPixel($x, $y)
            if ($p.A -eq 0) {
                $clean.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                continue
            }

            $isGreenFringe = $p.G -gt 45 `
                -and $p.G -gt ($p.R + 14) `
                -and $p.G -gt ($p.B + 10) `
                -and (($p.R -lt 190) -or ($p.B -lt 190))

            if ($isGreenFringe) {
                $clean.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                continue
            }

            $r = $p.R
            $g = $p.G
            $b = $p.B
            if ($g -gt $r -and $g -gt $b) {
                $target = [Math]::Max($r, $b)
                $g = [Math]::Max(0, [Math]::Min(255, [int][Math]::Round(($g * 0.35) + ($target * 0.65))))
            }

            $clean.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($p.A, $r, $g, $b))
        }
    }

    return $clean
}

function Remove-SmallAlphaComponents {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [int]$MinimumPixels = 20
    )

    $width = $Bitmap.Width
    $height = $Bitmap.Height
    $visited = New-Object 'bool[,]' $width, $height
    $result = New-BitmapArgb -Width $width -Height $height

    for ($y = 0; $y -lt $height; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            $p = $Bitmap.GetPixel($x, $y)
            $result.SetPixel($x, $y, $p)
        }
    }

    for ($startY = 0; $startY -lt $height; $startY++) {
        for ($startX = 0; $startX -lt $width; $startX++) {
            if ($visited[$startX, $startY]) {
                continue
            }

            $visited[$startX, $startY] = $true
            if ($Bitmap.GetPixel($startX, $startY).A -eq 0) {
                continue
            }

            $queue = New-Object 'System.Collections.Generic.Queue[System.Drawing.Point]'
            $component = New-Object 'System.Collections.Generic.List[System.Drawing.Point]'
            $queue.Enqueue((New-Object System.Drawing.Point -ArgumentList $startX, $startY))

            while ($queue.Count -gt 0) {
                $point = $queue.Dequeue()
                $component.Add($point)

                foreach ($offset in @(
                    (New-Object System.Drawing.Point -ArgumentList -1, 0),
                    (New-Object System.Drawing.Point -ArgumentList 1, 0),
                    (New-Object System.Drawing.Point -ArgumentList 0, -1),
                    (New-Object System.Drawing.Point -ArgumentList 0, 1)
                )) {
                    $nx = $point.X + $offset.X
                    $ny = $point.Y + $offset.Y
                    if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $width -or $ny -ge $height) {
                        continue
                    }
                    if ($visited[$nx, $ny]) {
                        continue
                    }
                    $visited[$nx, $ny] = $true
                    if ($Bitmap.GetPixel($nx, $ny).A -gt 0) {
                        $queue.Enqueue((New-Object System.Drawing.Point -ArgumentList $nx, $ny))
                    }
                }
            }

            if ($component.Count -lt $MinimumPixels) {
                foreach ($point in $component) {
                    $result.SetPixel($point.X, $point.Y, [System.Drawing.Color]::Transparent)
                }
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
        [int]$SheetColumns,
        [string]$OutPath
    )

    $sheetRows = [int][Math]::Ceiling($Frames.Count / [double]$SheetColumns)
    $sheet = New-BitmapArgb -Width ($FrameSize * $SheetColumns) -Height ($FrameSize * $sheetRows)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($sheet)
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.Clear([System.Drawing.Color]::Transparent)
            for ($i = 0; $i -lt $Frames.Count; $i++) {
                $x = ($i % $SheetColumns) * $FrameSize
                $y = [Math]::Floor($i / $SheetColumns) * $FrameSize
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

function Process-GeneratedSheet {
    param(
        [string]$SourcePath,
        [string]$OutPath,
        [string]$FramePrefix
    )

    $source = Open-BitmapClone -Path $SourcePath
    try {
        $transparent = Remove-Chroma -Source $source
        try {
            $frames = New-Object 'System.Collections.Generic.List[System.Drawing.Bitmap]'
            for ($i = 0; $i -lt 8; $i++) {
                $cell = Copy-SourceCell -Source $transparent -Index $i
                try {
                    $frame = Render-Frame128 -Cell $cell
                    $cleanFrame = Clean-GreenResidue -Bitmap $frame
                    $frame.Dispose()
                    $finalFrame = Remove-SmallAlphaComponents -Bitmap $cleanFrame
                    $cleanFrame.Dispose()
                    $frames.Add($finalFrame)
                    Save-Png -Bitmap $finalFrame -Path (Join-Path $Root ("assets\sprites\frames\{0}_{1:00}.png" -f $FramePrefix, $i))
                }
                finally {
                    $cell.Dispose()
                }
            }

            try {
                Build-Sheet -Frames $frames.ToArray() -SheetColumns 4 -OutPath $OutPath
            }
            finally {
                foreach ($frame in $frames) {
                    $frame.Dispose()
                }
            }
        }
        finally {
            $transparent.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}

if (-not $DashSource) { throw "DashSource is required." }
if (-not $JumpSource) { throw "JumpSource is required." }

$dashPath = if ([System.IO.Path]::IsPathRooted($DashSource)) { $DashSource } else { Join-Path $Root $DashSource }
$jumpPath = if ([System.IO.Path]::IsPathRooted($JumpSource)) { $JumpSource } else { Join-Path $Root $JumpSource }

$spritesDir = Join-Path $Root "assets\sprites"
$framesDir = Join-Path $spritesDir "frames"
if (-not (Test-Path -LiteralPath $framesDir)) {
    New-Item -ItemType Directory -Path $framesDir | Out-Null
}

Process-GeneratedSheet -SourcePath $dashPath -OutPath (Join-Path $spritesDir "rika_dash.png") -FramePrefix "rika_dash"
Process-GeneratedSheet -SourcePath $jumpPath -OutPath (Join-Path $spritesDir "rika_jump.png") -FramePrefix "rika_jump"

$jumpFrames = @()
for ($i = 0; $i -lt 8; $i++) {
    $jumpFrames += Open-BitmapClone -Path (Join-Path $framesDir ("rika_jump_{0:00}.png" -f $i))
}

try {
    Build-Sheet -Frames @($jumpFrames[0], $jumpFrames[1], $jumpFrames[2]) -SheetColumns 3 -OutPath (Join-Path $spritesDir "rika_jumpup.png")
    Build-Sheet -Frames @($jumpFrames[3]) -SheetColumns 1 -OutPath (Join-Path $spritesDir "rika_inair.png")
    Build-Sheet -Frames @($jumpFrames[4], $jumpFrames[5]) -SheetColumns 2 -OutPath (Join-Path $spritesDir "rika_isfalling.png")
    Build-Sheet -Frames @($jumpFrames[6], $jumpFrames[7]) -SheetColumns 2 -OutPath (Join-Path $spritesDir "rika_land.png")
}
finally {
    foreach ($frame in $jumpFrames) {
        $frame.Dispose()
    }
}

Copy-Item -LiteralPath $dashPath -Destination (Join-Path $Root "tmp\rika_dash_source_chromakey.png") -Force
Copy-Item -LiteralPath $jumpPath -Destination (Join-Path $Root "tmp\rika_jump_source_chromakey.png") -Force

Write-Host "Generated rika dash/jump sheets under assets/sprites."
