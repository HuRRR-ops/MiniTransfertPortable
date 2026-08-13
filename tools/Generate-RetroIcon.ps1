Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class RetroIconNative {
    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr handle);
}
"@

$outputPath = Join-Path $PSScriptRoot '..\assets\MiniTransfert.ico'
$outputDirectory = Split-Path -Parent $outputPath
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

$small = New-Object System.Drawing.Bitmap 16, 16
$graphics = [System.Drawing.Graphics]::FromImage($small)
$graphics.Clear([System.Drawing.Color]::Fuchsia)
$dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(64, 64, 64))
$face = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(192, 192, 192))
$blue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0, 128, 192))
$navy = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0, 0, 128))
$yellow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::Yellow)
$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)

$graphics.FillRectangle($dark, 1, 2, 6, 5)
$graphics.FillRectangle($face, 2, 3, 4, 3)
$graphics.FillRectangle($blue, 3, 3, 2, 2)
$graphics.FillRectangle($dark, 2, 8, 5, 4)
$graphics.FillRectangle($face, 3, 9, 3, 2)
$graphics.FillRectangle($dark, 9, 2, 6, 5)
$graphics.FillRectangle($face, 10, 3, 4, 3)
$graphics.FillRectangle($navy, 11, 3, 2, 2)
$graphics.FillRectangle($dark, 9, 8, 5, 4)
$graphics.FillRectangle($face, 10, 9, 3, 2)
$graphics.FillRectangle($yellow, 6, 6, 4, 2)
$graphics.FillRectangle($yellow, 8, 5, 2, 4)
$graphics.FillRectangle($white, 7, 6, 1, 1)
$graphics.FillRectangle($dark, 5, 12, 6, 2)
$small.MakeTransparent([System.Drawing.Color]::Fuchsia)

$bitmap = New-Object System.Drawing.Bitmap 32, 32
$scaled = [System.Drawing.Graphics]::FromImage($bitmap)
$scaled.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$scaled.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$scaled.DrawImage($small, 0, 0, 32, 32)
$handle = $bitmap.GetHicon()
try {
    $icon = [System.Drawing.Icon]::FromHandle($handle)
    $stream = [System.IO.File]::Create($outputPath)
    try { $icon.Save($stream) } finally { $stream.Dispose() }
} finally {
    [RetroIconNative]::DestroyIcon($handle) | Out-Null
    $scaled.Dispose()
    $bitmap.Dispose()
    $graphics.Dispose()
    $small.Dispose()
    $dark.Dispose()
    $face.Dispose()
    $blue.Dispose()
    $navy.Dispose()
    $yellow.Dispose()
    $white.Dispose()
}
