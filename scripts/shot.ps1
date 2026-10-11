<#
.SYNOPSIS
  Capture the screen so an agent can look at the overlay without a human.

.DESCRIPTION
  Grabs the whole virtual desktop (all monitors) or a crop, and writes a PNG.
  The capturing process is made PerMonitorV2-aware first, otherwise Windows
  hands back a scaled-down bitmap on a high-DPI screen and every pixel
  measurement taken from it is wrong.

.EXAMPLE
  powershell -File scripts/shot.ps1                       # whole desktop
  powershell -File scripts/shot.ps1 -Top 300              # top 300 px, full width
  powershell -File scripts/shot.ps1 -Rect 1200,0,1280,300 # a crop
#>
param(
    [string] $Out = "$env:TEMP\hangline-shot.png",
    [int] $Top = 0,
    [string] $Rect = ''
)

$source = @"
using System;
using System.Runtime.InteropServices;
public static class Shhh {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    public static void Aware() {
        // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
    }
}
"@
Add-Type -TypeDefinition $source
[Shhh]::Aware() | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
$x = $vs.X; $y = $vs.Y; $w = $vs.Width; $h = $vs.Height

if ($Rect -ne '') {
    $p = $Rect.Split(',')
    $x = [int]$p[0]; $y = [int]$p[1]; $w = [int]$p[2]; $h = [int]$p[3]
} elseif ($Top -gt 0) {
    $h = $Top
}

$bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size $w, $h))
$g.Dispose()

$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Output "saved $Out ($w x $h at $x,$y)"
