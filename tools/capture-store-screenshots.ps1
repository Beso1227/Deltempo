# ==============================================================================
# Deltempo Store listing screenshots - capture from the Store flavor
# ==============================================================================
# Partner Center wants at least one screenshot, 1366x768 or larger, of the app
# as it actually looks. This script launches the Store-flavor publish output,
# sizes the window to fill the display, walks the left rail, and writes PNGs.
#
#   .\tools\capture-store-screenshots.ps1
#   .\tools\capture-store-screenshots.ps1 -OutputDir .\docs\images\store
#
# Requires an interactive desktop session: it captures the real window with
# CopyFromScreen, so the app must be able to come to the foreground. It does
# not run on CI and is never part of the gate.
#
# The Store flavor is used on purpose: it compiles out the About screen's
# "not yet code-signed" note (DELTEMPO_MSIX), which would be false in a
# listing where Microsoft re-signs the package. Publish first if the folder is
# missing:
#
#   dotnet publish src\Deltempo.App.WinUI\Deltempo.App.WinUI.csproj -c Release `
#       -r win-x64 --self-contained true -p:DeltempoMSIX=true
#
# Views captured: Cleaner (landing), Repair, Settings, About. Nothing in them
# shows the local machine's installed software or window titles, which is why
# Processes / Apps / Large Files are deliberately not in the set.
# ==============================================================================

[CmdletBinding()]
param(
    # Where the PNGs land. Defaults to a gitignored scratch folder.
    [string]$OutputDir = "",

    # Architecture of the publish folder to launch.
    [ValidateSet("x64", "arm64")]
    [string]$Architecture = "x64"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class DeltempoCapture {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
}
"@

[void][DeltempoCapture]::SetProcessDPIAware()

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not $OutputDir) { $OutputDir = Join-Path $repoRoot ".internal\store-listing" }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$exe = Join-Path $repoRoot (
    "src\Deltempo.App.WinUI\bin\Release\net10.0-windows10.0.19041.0\win-{0}\publish\Deltempo.WinUI.exe" -f $Architecture)
if (-not (Test-Path $exe)) {
    throw "No Store-flavor publish output at $exe. Publish with -p:DeltempoMSIX=true first (see the header)."
}

function Save-Window {
    param([IntPtr]$Handle, [string]$Path)

    $rect = New-Object DeltempoCapture+RECT
    [void][DeltempoCapture]::GetWindowRect($Handle, [ref]$rect)
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top

    # The Store rejects anything smaller than 1366x768, and a scaled-down
    # capture would look soft, so refuse rather than ship a bad asset.
    if ($width -lt 1366 -or $height -lt 768) {
        throw "Captured window is ${width}x${height}; the Store needs at least 1366x768."
    }

    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($width, $height)))
    $graphics.Dispose()
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()

    Write-Host ("captured {0} ({1} x {2})" -f (Split-Path $Path -Leaf), $width, $height)
}

function Invoke-Click {
    param([int]$X, [int]$Y)

    [void][DeltempoCapture]::SetCursorPos($X, $Y)
    Start-Sleep -Milliseconds 250
    [DeltempoCapture]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)   # LEFTDOWN
    Start-Sleep -Milliseconds 80
    [DeltempoCapture]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)   # LEFTUP
}

$process = Start-Process -FilePath $exe -PassThru
$handle = [IntPtr]::Zero
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Milliseconds 500
    $process.Refresh()
    if ($process.HasExited) { throw "The shell exited during startup (code $($process.ExitCode))." }
    if ($process.MainWindowHandle -ne [IntPtr]::Zero) { $handle = $process.MainWindowHandle; break }
}
if ($handle -eq [IntPtr]::Zero) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    throw "No main window appeared within 30 s - this needs an interactive desktop session."
}

try {
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    [void][DeltempoCapture]::ShowWindow($handle, 9)   # SW_RESTORE
    [void][DeltempoCapture]::SetWindowPos(
        $handle, [IntPtr]::Zero, 4, 4, ($screen.Width - 20), ($screen.Height - 90), 0x0040)
    [void][DeltempoCapture]::SetForegroundWindow($handle)
    Start-Sleep -Seconds 4

    $rect = New-Object DeltempoCapture+RECT
    [void][DeltempoCapture]::GetWindowRect($handle, [ref]$rect)
    $originX = $rect.Left
    $originY = $rect.Top

    # Park the pointer on neutral space inside the window between captures so
    # no rail tooltip survives into a shot.
    $parkX = $originX + 1700
    $parkY = $originY + 760

    # Left-rail item centres, measured from a full-size window. Y = -1 means
    # "stay on the landing view" (Cleaner).
    $shots = @(
        @{ Name = "01-cleaner"; RailY = -1 },
        @{ Name = "02-repair"; RailY = 405 },
        @{ Name = "03-settings"; RailY = 845 },
        @{ Name = "04-about"; RailY = 905 }
    )

    foreach ($shot in $shots) {
        if ($shot.RailY -ge 0) {
            Invoke-Click -X ($originX + 43) -Y ($originY + $shot.RailY)
            Start-Sleep -Milliseconds 1200
            # A click on neutral header space dismisses the lingering tooltip.
            Invoke-Click -X ($originX + 1250) -Y ($originY + 100)
        }
        [void][DeltempoCapture]::SetCursorPos($parkX, $parkY)
        Start-Sleep -Seconds 4
        Save-Window -Handle $handle -Path (Join-Path $OutputDir ($shot.Name + ".png"))
    }
}
finally {
    if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null; Start-Sleep -Seconds 2 }
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}

Write-Host ""
Write-Host ("Screenshots in {0}" -f $OutputDir)
Write-Host "Upload at least one; four is the recommended listing set."
