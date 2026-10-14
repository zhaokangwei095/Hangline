<#
.SYNOPSIS
  Send WM_CLOSE to a window, the way the taskbar's close command does.

.EXAMPLE
  powershell -File scripts/closewnd.ps1 0x53d1506
#>
param([Parameter(Position = 0, Mandatory = $true)] [string] $Hwnd)

$code = @'
using System;
using System.Runtime.InteropServices;
public static class Closer
{
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    public const uint WM_CLOSE = 0x0010;
}
'@

Add-Type -TypeDefinition $code
$handle = [IntPtr]([Convert]::ToInt64($Hwnd, 16))

if (-not [Closer]::IsWindow($handle)) { Write-Output "no such window $Hwnd"; exit 1 }
$posted = [Closer]::PostMessage($handle, [Closer]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero)
Write-Output "WM_CLOSE posted=$posted to $Hwnd"

Start-Sleep -Milliseconds 700
Write-Output ("still a window: " + [Closer]::IsWindow($handle))
