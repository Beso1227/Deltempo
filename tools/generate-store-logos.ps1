# Generates the Store logo set from the WPF head's app.ico.
# Writes exactly the sizes the Store flavor's Package.appxmanifest and the
# Partner Center submission reference (docs/PUBLISHING_MICROSOFT_STORE.md
# section 4): 44/50/71/150/256 square plus a 16 tile for file dialogs.
#
# The .ico's frames are all PNG-compressed (Vista+ convention). System.Drawing's
# Icon.ToBitmap() cannot decode PNG frames - it renders random noise (verified:
# the first generator revision produced static-filled PNGs). So the selector
# below parses the ICO directory itself, extracts the chosen frame's raw bytes
# - which ARE a standalone PNG file - and decodes them with Image.FromStream.
# Icon_256.png is embedded byte-for-byte (zero recompression); smaller sizes
# are bicubic downscales of the closest native frame.
param()

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$icoPath = Join-Path $repoRoot "src\Deltempo.App.Wpf\app.ico"
$outDir = Join-Path $repoRoot "src\Deltempo.App.WinUI\Assets\Store"

Add-Type -AssemblyName System.Drawing

# --- Parse the ICO directory: frame count, then (w, h, len, offset) per entry.
$icoBytes = [IO.File]::ReadAllBytes($icoPath)
if ($icoBytes.Length -lt 6) { throw "app.ico is not a valid ICO file." }
$frameCount = [BitConverter]::ToUInt16($icoBytes, 4)
$frames = @()
for ($i = 0; $i -lt $frameCount; $i++) {
    $o = 6 + $i * 16
    $w = $icoBytes[$o]; if ($w -eq 0) { $w = 256 }
    $len = [BitConverter]::ToUInt32($icoBytes, $o + 8)
    $off = [BitConverter]::ToUInt32($icoBytes, $o + 12)
    $frames += [pscustomobject]@{ Width = [int]$w; Offset = [int]$off; Length = [int]$len }
}

function Get-FrameBitmap([int]$target) {
    # Quality rule: take the smallest native frame at or above the target size
    # (downscale, never upscale); fall back to the largest frame (256).
    $frame = $frames | Where-Object { $_.Width -ge $target } |
        Sort-Object Width | Select-Object -First 1
    if ($null -eq $frame) {
        $frame = $frames | Sort-Object Width | Select-Object -Last 1
    }
    # PNG-compressed frame: the entry bytes are a standalone PNG (89 50 4E 47).
    $entry = New-Object byte[] $frame.Length
    [Array]::Copy($icoBytes, $frame.Offset, $entry, 0, $frame.Length)
    $ms = New-Object IO.MemoryStream(,$entry)
    $bmp = [System.Drawing.Image]::FromStream($ms)
    return $bmp
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

foreach ($size in 16, 44, 50, 71, 150, 256) {
    if ($size -eq 256) {
        # Byte-for-byte PNG passthrough: no decoder, no recompressed pixels.
        $frame = $frames | Where-Object { $_.Width -eq 256 } | Select-Object -First 1
        if ($null -eq $frame) { throw "app.ico has no 256px frame to passthrough." }
        $png = New-Object byte[] $frame.Length
        [Array]::Copy($icoBytes, $frame.Offset, $png, 0, $frame.Length)
        [IO.File]::WriteAllBytes((Join-Path $outDir "Icon_256.png"), $png)
        continue
    }

    $source = Get-FrameBitmap $size
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($source, 0, 0, $size, $size)
    $g.Dispose()
    $source.Dispose()
    $bmp.Save((Join-Path $outDir ("Icon_{0}.png" -f $size)), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

Get-ChildItem $outDir | ForEach-Object { "{0}  {1} bytes" -f $_.Name, $_.Length }
