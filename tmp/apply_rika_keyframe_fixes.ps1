param(
    [string]$Root = (Resolve-Path ".").Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$FrameSize = 256

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

function Replace-SheetFrame {
    param(
        [string]$SheetPath,
        [int]$Index,
        [System.Drawing.Bitmap]$Frame
    )

    $sheet = Open-BitmapClone -Path $SheetPath
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($sheet)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

        $dstRect = New-Object System.Drawing.Rectangle -ArgumentList @(
            (($Index % 4) * $FrameSize),
            ([Math]::Floor($Index / 4) * $FrameSize),
            $FrameSize,
            $FrameSize
        )
        $srcRect = New-Object System.Drawing.Rectangle -ArgumentList @(0, 0, $FrameSize, $FrameSize)
        $graphics.DrawImage($Frame, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
        $graphics.Dispose()
        Save-Png -Bitmap $sheet -Path $SheetPath
    }
    finally {
        $sheet.Dispose()
    }
}

function Replace-LinearSheetFrame {
    param(
        [string]$SheetPath,
        [int]$Index,
        [System.Drawing.Bitmap]$Frame
    )

    $sheet = Open-BitmapClone -Path $SheetPath
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($sheet)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

        $dstRect = New-Object System.Drawing.Rectangle -ArgumentList @(
            ($Index * $FrameSize),
            0,
            $FrameSize,
            $FrameSize
        )
        $srcRect = New-Object System.Drawing.Rectangle -ArgumentList @(0, 0, $FrameSize, $FrameSize)
        $graphics.DrawImage($Frame, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
        $graphics.Dispose()
        Save-Png -Bitmap $sheet -Path $SheetPath
    }
    finally {
        $sheet.Dispose()
    }
}

$dashFrame = Open-BitmapClone -Path (Join-Path $Root "tmp\rika_dash_frame_07_corrected.png")
try {
    Replace-SheetFrame -SheetPath (Join-Path $Root "assets\generated\rika\dash.png") -Index 6 -Frame $dashFrame
    Save-Png -Bitmap $dashFrame -Path (Join-Path $Root "assets\generated\rika\frames\dash_06.png")
}
finally {
    $dashFrame.Dispose()
}

$jumpFrame = Open-BitmapClone -Path (Join-Path $Root "tmp\rika_jump_frame_05_corrected.png")
try {
    Replace-SheetFrame -SheetPath (Join-Path $Root "assets\generated\rika\jump.png") -Index 4 -Frame $jumpFrame
    Replace-LinearSheetFrame -SheetPath (Join-Path $Root "assets\generated\rika\isfalling.png") -Index 0 -Frame $jumpFrame
    Save-Png -Bitmap $jumpFrame -Path (Join-Path $Root "assets\generated\rika\frames\jump_04.png")
}
finally {
    $jumpFrame.Dispose()
}

Write-Host "Applied dash frame 7 and jump frame 5 fixes."
