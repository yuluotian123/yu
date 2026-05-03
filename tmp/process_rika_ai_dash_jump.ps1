param(
    [string]$Root = (Resolve-Path ".").Path,
    [string]$JumpSource = "C:\Users\Administrator\.codex\generated_images\019dec3b-1ee5-7080-ab3f-87a20fe48a14\ig_098742d4d38023670169f6ece821e8819ab3de383f3919423b.png",
    [string]$DashSource = "C:\Users\Administrator\.codex\generated_images\019dec3b-1ee5-7080-ab3f-87a20fe48a14\ig_098742d4d38023670169f6ecaa5420819aa4f6f9d1625f407c.png"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$FrameSize = 256
$OutputDir = Join-Path $Root "assets\generated\rika"
$FrameDir = Join-Path $OutputDir "frames"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
New-Item -ItemType Directory -Force -Path $FrameDir | Out-Null

$GeneratedPngs = New-Object System.Collections.Generic.List[string]

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

function Save-Png {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Force
    }

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $script:GeneratedPngs.Add($Path)
}

function Is-ChromaGreen {
    param([System.Drawing.Color]$Pixel)

    return $Pixel.G -gt 110 `
        -and $Pixel.G -gt ($Pixel.R + 32) `
        -and $Pixel.G -gt ($Pixel.B + 24)
}

function Extract-TransparentCell {
    param(
        [System.Drawing.Bitmap]$Source,
        [int]$Index
    )

    $columns = 4
    $rows = 2
    $cellW = [int][Math]::Floor($Source.Width / $columns)
    $cellH = [int][Math]::Floor($Source.Height / $rows)
    $sourceX = ($Index % $columns) * $cellW
    $sourceY = [Math]::Floor($Index / $columns) * $cellH

    $cell = New-BitmapArgb -Width $cellW -Height $cellH
    $minX = $cellW
    $minY = $cellH
    $maxX = -1
    $maxY = -1

    for ($y = 0; $y -lt $cellH; $y++) {
        for ($x = 0; $x -lt $cellW; $x++) {
            $pixel = $Source.GetPixel($sourceX + $x, $sourceY + $y)
            if (Is-ChromaGreen -Pixel $pixel) {
                $cell.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                continue
            }

            $cell.SetPixel($x, $y, $pixel)
            if ($pixel.A -gt 0) {
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    if ($maxX -lt 0) {
        return New-BitmapArgb -Width $FrameSize -Height $FrameSize
    }

    $boundsW = $maxX - $minX + 1
    $boundsH = $maxY - $minY + 1
    $targetMax = 236.0
    $scale = [Math]::Min($targetMax / $boundsW, $targetMax / $boundsH)
    if ($scale -gt 1.0) {
        $scale = 1.0
    }

    $drawW = [int][Math]::Max(1, [Math]::Round($boundsW * $scale))
    $drawH = [int][Math]::Max(1, [Math]::Round($boundsH * $scale))
    $destX = [int][Math]::Round(($FrameSize - $drawW) / 2)
    $destY = [int][Math]::Round(($FrameSize - $drawH) / 2)

    $frame = New-BitmapArgb -Width $FrameSize -Height $FrameSize
    $graphics = [System.Drawing.Graphics]::FromImage($frame)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

    $srcRect = New-Object System.Drawing.Rectangle -ArgumentList @($minX, $minY, $boundsW, $boundsH)
    $dstRect = New-Object System.Drawing.Rectangle -ArgumentList @($destX, $destY, $drawW, $drawH)
    $graphics.DrawImage($cell, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()
    $cell.Dispose()

    return $frame
}

function Write-Sheet {
    param(
        [string]$SourcePath,
        [string]$OutputName
    )

    $source = Open-BitmapClone -Path $SourcePath
    try {
        $sheet = New-BitmapArgb -Width ($FrameSize * 4) -Height ($FrameSize * 2)
        $graphics = [System.Drawing.Graphics]::FromImage($sheet)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

        for ($i = 0; $i -lt 8; $i++) {
            $frame = Extract-TransparentCell -Source $source -Index $i
            $graphics.DrawImage(
                $frame,
                (($i % 4) * $FrameSize),
                ([Math]::Floor($i / 4) * $FrameSize),
                $FrameSize,
                $FrameSize
            )
            Save-Png -Bitmap $frame -Path (Join-Path $FrameDir ("{0}_{1:D2}.png" -f $OutputName, $i))
            $frame.Dispose()
        }

        $graphics.Dispose()
        Save-Png -Bitmap $sheet -Path (Join-Path $OutputDir "$OutputName.png")
        $sheet.Dispose()
    }
    finally {
        $source.Dispose()
    }
}

function Copy-FrameFromSheet {
    param(
        [System.Drawing.Bitmap]$SourceSheet,
        [int]$Index,
        [System.Drawing.Graphics]$Graphics,
        [int]$DestIndex
    )

    $srcRect = New-Object System.Drawing.Rectangle -ArgumentList @(
        (($Index % 4) * $FrameSize),
        ([Math]::Floor($Index / 4) * $FrameSize),
        $FrameSize,
        $FrameSize
    )
    $dstRect = New-Object System.Drawing.Rectangle -ArgumentList @(
        ($DestIndex * $FrameSize),
        0,
        $FrameSize,
        $FrameSize
    )
    $Graphics.DrawImage($SourceSheet, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
}

function Write-JumpParts {
    $jumpPath = Join-Path $OutputDir "jump.png"
    $jumpSheet = Open-BitmapClone -Path $jumpPath
    try {
        $parts = @(
            [PSCustomObject]@{ Name = "jumpup"; Frames = @(0, 1, 2) },
            [PSCustomObject]@{ Name = "inair"; Frames = @(3) },
            [PSCustomObject]@{ Name = "isfalling"; Frames = @(4, 5) },
            [PSCustomObject]@{ Name = "land"; Frames = @(6, 7) }
        )

        foreach ($part in $parts) {
            $partSheet = New-BitmapArgb -Width ($FrameSize * $part.Frames.Count) -Height $FrameSize
            $graphics = [System.Drawing.Graphics]::FromImage($partSheet)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

            for ($i = 0; $i -lt $part.Frames.Count; $i++) {
                Copy-FrameFromSheet -SourceSheet $jumpSheet -Index $part.Frames[$i] -Graphics $graphics -DestIndex $i
            }

            $graphics.Dispose()
            Save-Png -Bitmap $partSheet -Path (Join-Path $OutputDir ($part.Name + ".png"))
            $partSheet.Dispose()
        }
    }
    finally {
        $jumpSheet.Dispose()
    }
}

function Get-PngUid {
    param([string]$RelativeName)

    $resPath = "res://assets/generated/rika/$RelativeName"
    $md5 = [System.Security.Cryptography.MD5]::Create()
    try {
        $hashBytes = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($resPath))
    }
    finally {
        $md5.Dispose()
    }
    $hash = -join ($hashBytes | ForEach-Object { $_.ToString("x2") })
    return "uid://rk" + $hash.Substring(0, 11)
}

function Get-TextureImportText {
    param([string]$PngPath)

    $rootFull = [System.IO.Path]::GetFullPath($Root)
    $pngFull = [System.IO.Path]::GetFullPath($PngPath)
    $relativePath = $pngFull.Substring($rootFull.Length + 1)
    $resPath = "res://" + ($relativePath -replace "\\", "/")
    $md5 = [System.Security.Cryptography.MD5]::Create()
    try {
        $hashBytes = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($resPath))
    }
    finally {
        $md5.Dispose()
    }
    $hash = -join ($hashBytes | ForEach-Object { $_.ToString("x2") })
    $uid = "uid://rk" + $hash.Substring(0, 11)
    $fileName = [System.IO.Path]::GetFileName($PngPath)

    return @"
[remap]

importer="texture"
type="CompressedTexture2D"
uid="$uid"
path="res://.godot/imported/$fileName-$hash.ctex"
metadata={
"vram_texture": false
}

[deps]

source_file="$resPath"
dest_files=["res://.godot/imported/$fileName-$hash.ctex"]

[params]

compress/mode=0
compress/high_quality=false
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map=0
compress/channel_pack=0
mipmaps/generate=false
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/channel_remap/red=0
process/channel_remap/green=1
process/channel_remap/blue=2
process/channel_remap/alpha=3
process/fix_alpha_border=true
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=1
"@
}

function Write-ImportFiles {
    $mainPngs = @(
        "dash.png",
        "jump.png",
        "jumpup.png",
        "inair.png",
        "isfalling.png",
        "land.png"
    )

    foreach ($name in $mainPngs) {
        $path = Join-Path $OutputDir $name
        Set-Content -Path "$path.import" -Value (Get-TextureImportText -PngPath $path) -Encoding ASCII
    }

    $framePngs = @()
    $framePngs += Get-ChildItem -Path $FrameDir -Filter "dash_*.png"
    $framePngs += Get-ChildItem -Path $FrameDir -Filter "jump_*.png"
    foreach ($framePng in $framePngs) {
        if ($null -eq $framePng) {
            continue
        }
        Set-Content -Path "$($framePng.FullName).import" -Value (Get-TextureImportText -PngPath $framePng.FullName) -Encoding ASCII
    }
}

function Add-AtlasResources {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$AtlasId,
        [string]$Prefix,
        [int]$FrameCount,
        [int]$Columns
    )

    for ($i = 0; $i -lt $FrameCount; $i++) {
        $x = ($i % $Columns) * $FrameSize
        $y = [Math]::Floor($i / $Columns) * $FrameSize
        $Lines.Add("[sub_resource type=`"AtlasTexture`" id=`"$Prefix`_$i`"]")
        $Lines.Add("atlas = ExtResource(`"$AtlasId`")")
        $Lines.Add(("region = Rect2({0}, {1}, 256, 256)" -f $x, $y))
        $Lines.Add("")
    }
}

function Add-Animation {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Name,
        [string]$Prefix,
        [int]$FrameCount,
        [bool]$Loop,
        [double]$Speed,
        [bool]$Close
    )

    $Lines.Add('"frames": [{')
    for ($i = 0; $i -lt $FrameCount; $i++) {
        $Lines.Add('"duration": 1.0,')
        $Lines.Add(('"texture": SubResource("{0}_{1}")' -f $Prefix, $i))
        if ($i -lt ($FrameCount - 1)) {
            $Lines.Add('}, {')
        }
    }
    $Lines.Add('}],')
    $Lines.Add(('"loop": {0},' -f ($(if ($Loop) { "true" } else { "false" }))))
    $Lines.Add(('"name": &"{0}",' -f $Name))
    $Lines.Add(('"speed": {0}' -f $Speed.ToString("0.0", [System.Globalization.CultureInfo]::InvariantCulture)))
    if ($Close) {
        $Lines.Add('}]')
    }
    else {
        $Lines.Add('}, {')
    }
}

function Write-SpriteFrames {
    $path = Join-Path $OutputDir "rika_sprite_frames.tres"
    $lines = New-Object System.Collections.Generic.List[string]

    $lines.Add('[gd_resource type="SpriteFrames" format=3]')
    $lines.Add('')
    $lines.Add("[ext_resource type=`"Texture2D`" uid=`"$(Get-PngUid "idle.png")`" path=`"res://assets/generated/rika/idle.png`" id=`"1_idle`"]")
    $lines.Add("[ext_resource type=`"Texture2D`" uid=`"$(Get-PngUid "image.png")`" path=`"res://assets/generated/rika/image.png`" id=`"2_image`"]")
    $lines.Add("[ext_resource type=`"Texture2D`" uid=`"$(Get-PngUid "run.png")`" path=`"res://assets/generated/rika/run.png`" id=`"3_run`"]")
    $lines.Add("[ext_resource type=`"Texture2D`" uid=`"$(Get-PngUid "dash.png")`" path=`"res://assets/generated/rika/dash.png`" id=`"4_dash`"]")
    $lines.Add("[ext_resource type=`"Texture2D`" uid=`"$(Get-PngUid "jumpup.png")`" path=`"res://assets/generated/rika/jumpup.png`" id=`"5_jumpup`"]")
    $lines.Add("[ext_resource type=`"Texture2D`" uid=`"$(Get-PngUid "inair.png")`" path=`"res://assets/generated/rika/inair.png`" id=`"6_inair`"]")
    $lines.Add("[ext_resource type=`"Texture2D`" uid=`"$(Get-PngUid "isfalling.png")`" path=`"res://assets/generated/rika/isfalling.png`" id=`"7_isfalling`"]")
    $lines.Add("[ext_resource type=`"Texture2D`" uid=`"$(Get-PngUid "land.png")`" path=`"res://assets/generated/rika/land.png`" id=`"8_land`"]")
    $lines.Add('')

    Add-AtlasResources -Lines $lines -AtlasId "1_idle" -Prefix "idle" -FrameCount 4 -Columns 4
    Add-AtlasResources -Lines $lines -AtlasId "2_image" -Prefix "image" -FrameCount 1 -Columns 1
    Add-AtlasResources -Lines $lines -AtlasId "3_run" -Prefix "run" -FrameCount 12 -Columns 4
    Add-AtlasResources -Lines $lines -AtlasId "4_dash" -Prefix "dash" -FrameCount 8 -Columns 4
    Add-AtlasResources -Lines $lines -AtlasId "5_jumpup" -Prefix "jumpup" -FrameCount 3 -Columns 3
    Add-AtlasResources -Lines $lines -AtlasId "6_inair" -Prefix "inair" -FrameCount 1 -Columns 1
    Add-AtlasResources -Lines $lines -AtlasId "7_isfalling" -Prefix "isfalling" -FrameCount 2 -Columns 2
    Add-AtlasResources -Lines $lines -AtlasId "8_land" -Prefix "land" -FrameCount 2 -Columns 2

    $lines.Add('[resource]')
    $lines.Add('animations = [{')
    Add-Animation -Lines $lines -Name "dash" -Prefix "dash" -FrameCount 8 -Loop $false -Speed 16.0 -Close $false
    Add-Animation -Lines $lines -Name "idle" -Prefix "idle" -FrameCount 4 -Loop $true -Speed 6.0 -Close $false
    Add-Animation -Lines $lines -Name "image" -Prefix "image" -FrameCount 1 -Loop $true -Speed 1.0 -Close $false
    Add-Animation -Lines $lines -Name "inair" -Prefix "inair" -FrameCount 1 -Loop $true -Speed 1.0 -Close $false
    Add-Animation -Lines $lines -Name "isfalling" -Prefix "isfalling" -FrameCount 2 -Loop $true -Speed 8.0 -Close $false
    Add-Animation -Lines $lines -Name "jumpup" -Prefix "jumpup" -FrameCount 3 -Loop $false -Speed 12.0 -Close $false
    Add-Animation -Lines $lines -Name "land" -Prefix "land" -FrameCount 2 -Loop $false -Speed 12.0 -Close $false
    Add-Animation -Lines $lines -Name "run" -Prefix "run" -FrameCount 12 -Loop $true -Speed 12.0 -Close $true

    Set-Content -Path $path -Value $lines -Encoding ASCII
}

Write-Sheet -SourcePath $DashSource -OutputName "dash"
Write-Sheet -SourcePath $JumpSource -OutputName "jump"
Write-JumpParts
Write-ImportFiles
Write-SpriteFrames

Write-Host "Processed AI dash and jump assets into $OutputDir"
