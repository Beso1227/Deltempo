# Generates the Store logo set from the WPF head's app.ico.
# Writes exactly the sizes the Store flavor's Package.appxmanifest and the
# Partner Center submission reference (docs/PUBLISHING_MICROSOFT_STORE.md
# section 4): 44/50/71/150/256 square plus a 16 tile for file dialogs.
# Highest-quality bicubic downscale from the largest (256) frame; never
# upscale a small frame.
param()

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$icoPath = Join-Path $repoRoot "src\Deltempo.App.Wpf\app.ico"
$outDir = Join-Path $repoRoot "src\Deltempo.App.WinUI\Assets\Store"

Add-Type -AssemblyName System.Drawing

$source = New-Object System.Drawing.Icon($icoPath, 256, 256)
$sourceBitmap = $source.ToBitmap()   # 256px frame as a bitmap we can reliably scale from

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

foreach ($size in 16, 44, 50, 71, 150, 256) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($sourceBitmap, 0, 0, $size, $size)
    $g.Dispose()
    $bmp.Save((Join-Path $outDir ("Icon_{0}.png" -f $size)), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

$sourceBitmap.Dispose()
$source.Dispose()
Get-ChildItem $outDir | ForEach-Object { "{0}  {1} bytes" -f $_.Name, $_.Length }
