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

function Is-GreenNoise {
    param([System.Drawing.Color]$Pixel)

    return $Pixel.A -gt 0 `
        -and $Pixel.G -gt 135 `
        -and $Pixel.G -gt ($Pixel.R + 18) `
        -and $Pixel.G -gt ($Pixel.B + 14)
}

function Clean-GreenNoise {
    param([System.Drawing.Bitmap]$Bitmap)

    $removed = 0
    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            $pixel = $Bitmap.GetPixel($x, $y)
            if (Is-GreenNoise -Pixel $pixel) {
                $Bitmap.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                $removed += 1
            }
        }
    }

    return $removed
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

function Remove-TinyComponents {
    param([System.Drawing.Bitmap]$Bitmap)

    $visited = New-Object 'bool[,]' $Bitmap.Width, $Bitmap.Height
    $removed = 0

    for ($startY = 0; $startY -lt $Bitmap.Height; $startY++) {
        for ($startX = 0; $startX -lt $Bitmap.Width; $startX++) {
            if ($visited[$startX, $startY]) {
                continue
            }

            $visited[$startX, $startY] = $true
            if ($Bitmap.GetPixel($startX, $startY).A -eq 0) {
                continue
            }

            $queue = New-Object System.Collections.Generic.Queue[System.Drawing.Point]
            $pixels = New-Object System.Collections.Generic.List[System.Drawing.Point]
            $queue.Enqueue((New-Object System.Drawing.Point -ArgumentList @($startX, $startY)))

            while ($queue.Count -gt 0) {
                $point = $queue.Dequeue()
                $pixels.Add($point)

                for ($dy = -1; $dy -le 1; $dy++) {
                    for ($dx = -1; $dx -le 1; $dx++) {
                        if ($dx -eq 0 -and $dy -eq 0) {
                            continue
                        }

                        $nx = $point.X + $dx
                        $ny = $point.Y + $dy
                        if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $Bitmap.Width -or $ny -ge $Bitmap.Height) {
                            continue
                        }
                        if ($visited[$nx, $ny]) {
                            continue
                        }

                        $visited[$nx, $ny] = $true
                        if ($Bitmap.GetPixel($nx, $ny).A -gt 0) {
                            $queue.Enqueue((New-Object System.Drawing.Point -ArgumentList @($nx, $ny)))
                        }
                    }
                }
            }

            if ($pixels.Count -le 16) {
                foreach ($pixelPoint in $pixels) {
                    $Bitmap.SetPixel($pixelPoint.X, $pixelPoint.Y, [System.Drawing.Color]::Transparent)
                    $removed += 1
                }
            }
        }
    }

    return $removed
}

foreach ($target in $Targets) {
    $path = Join-Path $Root $target
    $bitmap = Open-BitmapClone -Path $path
    try {
        $greenRemoved = Clean-GreenNoise -Bitmap $bitmap
        $tinyRemoved = Remove-TinyComponents -Bitmap $bitmap

        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
        }
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)

        $bounds = Get-AlphaBounds -Bitmap $bitmap
        Write-Host ("{0}: green {1}, tiny {2}, bounds ({3},{4})-({5},{6})" -f `
            ([System.IO.Path]::GetFileName($path)),
            $greenRemoved,
            $tinyRemoved,
            $bounds.MinX,
            $bounds.MinY,
            $bounds.MaxX,
            $bounds.MaxY)
    }
    finally {
        $bitmap.Dispose()
    }
}
