param(
    [string]$InputPath = (Join-Path $PSScriptRoot '..\assets\brand\Mediance-selected-reference.png')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.Drawing

$brandDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\assets\brand'))
$appAssetsDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\prototypes\Mediance.AcrylicProbe\Assets'))
$input = [System.IO.Path]::GetFullPath($InputPath)

if (-not (Test-Path -LiteralPath $input)) {
    throw "Brand reference was not found: $input"
}

[System.IO.Directory]::CreateDirectory($brandDirectory) | Out-Null
[System.IO.Directory]::CreateDirectory($appAssetsDirectory) | Out-Null

function New-TransparentBitmap([int]$width, [int]$height) {
    return [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
}

function Get-RoundedRectanglePath([System.Drawing.RectangleF]$rectangle, [float]$radius) {
    $diameter = $radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($rectangle.Left, $rectangle.Top, $diameter, $diameter, 180, 90)
    $path.AddArc($rectangle.Right - $diameter, $rectangle.Top, $diameter, $diameter, 270, 90)
    $path.AddArc($rectangle.Right - $diameter, $rectangle.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($rectangle.Left, $rectangle.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Convert-ReferenceToMask([string]$path) {
    $source = [System.Drawing.Bitmap]::new($path)
    try {
        $mask = New-TransparentBitmap $source.Width $source.Height
        $left = $source.Width
        $top = $source.Height
        $right = -1
        $bottom = -1

        for ($y = 0; $y -lt $source.Height; $y++) {
            for ($x = 0; $x -lt $source.Width; $x++) {
                $pixel = $source.GetPixel($x, $y)
                $luminance = (0.2126 * $pixel.R) + (0.7152 * $pixel.G) + (0.0722 * $pixel.B)
                $alpha = if ($luminance -ge 250) { 0 } else { [Math]::Min(255, [Math]::Round((250 - $luminance) * 255 / 250)) }
                if ($alpha -gt 0) {
                    $mask.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, 0, 0, 0))
                    if ($alpha -gt 8) {
                        $left = [Math]::Min($left, $x)
                        $top = [Math]::Min($top, $y)
                        $right = [Math]::Max($right, $x)
                        $bottom = [Math]::Max($bottom, $y)
                    }
                }
            }
        }

        if ($right -lt $left -or $bottom -lt $top) {
            $mask.Dispose()
            throw 'The selected reference contains no visible dark mark.'
        }

        return [pscustomobject]@{
            Bitmap = $mask
            Bounds = [System.Drawing.Rectangle]::FromLTRB($left, $top, $right + 1, $bottom + 1)
        }
    }
    finally {
        $source.Dispose()
    }
}

function New-MarkBitmap(
    [System.Drawing.Bitmap]$mask,
    [System.Drawing.Rectangle]$bounds,
    [int]$size,
    [int]$padding,
    [System.Drawing.Color]$color
) {
    $result = New-TransparentBitmap $size $size
    $available = $size - (2 * $padding)
    $scale = [Math]::Min($available / $bounds.Width, $available / $bounds.Height)
    $width = [int][Math]::Round($bounds.Width * $scale)
    $height = [int][Math]::Round($bounds.Height * $scale)
    $destination = [System.Drawing.Rectangle]::new(
        [int](($size - $width) / 2),
        [int](($size - $height) / 2),
        $width,
        $height)

    $graphics = [System.Drawing.Graphics]::FromImage($result)
    $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

        $red = $color.R / 255.0
        $green = $color.G / 255.0
        $blue = $color.B / 255.0
        $matrix = [System.Drawing.Imaging.ColorMatrix]::new([single[][]]@(
            [single[]]@(0, 0, 0, 0, 0),
            [single[]]@(0, 0, 0, 0, 0),
            [single[]]@(0, 0, 0, 0, 0),
            [single[]]@(0, 0, 0, 1, 0),
            [single[]]@($red, $green, $blue, 0, 1)
        ))
        $attributes.SetColorMatrix($matrix)
        $graphics.DrawImage($mask, $destination, $bounds.X, $bounds.Y, $bounds.Width, $bounds.Height,
            [System.Drawing.GraphicsUnit]::Pixel, $attributes)
    }
    finally {
        $attributes.Dispose()
        $graphics.Dispose()
    }

    return $result
}

function New-AppIconBitmap([System.Drawing.Bitmap]$whiteMark, [int]$size) {
    $icon = New-TransparentBitmap $size $size
    $graphics = [System.Drawing.Graphics]::FromImage($icon)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $tile = [System.Drawing.RectangleF]::new(24, 24, $size - 48, $size - 48)
        $path = Get-RoundedRectanglePath $tile 220
        try {
            $brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 5, 5, 7))
            try { $graphics.FillPath($brush, $path) } finally { $brush.Dispose() }
        }
        finally { $path.Dispose() }

        $markSize = [int]($size * 0.68)
        $markPosition = [int](($size - $markSize) / 2)
        $graphics.DrawImage($whiteMark, [System.Drawing.Rectangle]::new($markPosition, $markPosition, $markSize, $markSize))
    }
    finally {
        $graphics.Dispose()
    }
    return $icon
}

function Resize-Bitmap([System.Drawing.Bitmap]$source, [int]$size) {
    $result = New-TransparentBitmap $size $size
    $graphics = [System.Drawing.Graphics]::FromImage($result)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.DrawImage($source, 0, 0, $size, $size)
    }
    finally { $graphics.Dispose() }
    return $result
}

function Save-MultiSizeIcon([System.Drawing.Bitmap]$source, [string]$path) {
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $images = [System.Collections.Generic.List[byte[]]]::new()
    foreach ($size in $sizes) {
        $bitmap = Resize-Bitmap $source $size
        $stream = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $images.Add($stream.ToArray())
        }
        finally {
            $stream.Dispose()
            $bitmap.Dispose()
        }
    }

    $file = [System.IO.File]::Create($path)
    $writer = [System.IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$sizes.Count)
        $offset = 6 + (16 * $sizes.Count)
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $size = $sizes[$index]
            $encodedSize = if ($size -eq 256) { 0 } else { $size }
            $writer.Write([byte]$encodedSize)
            $writer.Write([byte]$encodedSize)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$images[$index].Length)
            $writer.Write([uint32]$offset)
            $offset += $images[$index].Length
        }
        foreach ($image in $images) { $writer.Write($image) }
    }
    finally {
        $writer.Dispose()
        $file.Dispose()
    }
}

$converted = Convert-ReferenceToMask $input
try {
    $blackMaster = New-MarkBitmap $converted.Bitmap $converted.Bounds 1024 100 ([System.Drawing.Color]::Black)
    $whiteMaster = New-MarkBitmap $converted.Bitmap $converted.Bounds 1024 100 ([System.Drawing.Color]::White)
    $runtimeMark = New-MarkBitmap $converted.Bitmap $converted.Bounds 512 42 ([System.Drawing.Color]::White)
    $appIcon = New-AppIconBitmap $whiteMaster 1024
    try {
        $blackMaster.Save((Join-Path $brandDirectory 'Mediance-mark-master.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        $whiteMaster.Save((Join-Path $brandDirectory 'Mediance-mark-white-master.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        $appIcon.Save((Join-Path $brandDirectory 'Mediance-app-icon-master.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        $runtimeMark.Save((Join-Path $appAssetsDirectory 'Mediance-mark.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        $appIcon.Save((Join-Path $appAssetsDirectory 'Mediance-app-icon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        Save-MultiSizeIcon $appIcon (Join-Path $appAssetsDirectory 'Mediance.ico')
    }
    finally {
        $blackMaster.Dispose()
        $whiteMaster.Dispose()
        $runtimeMark.Dispose()
        $appIcon.Dispose()
    }
}
finally {
    $converted.Bitmap.Dispose()
}

Write-Host "Mediance brand assets rebuilt from $input"
