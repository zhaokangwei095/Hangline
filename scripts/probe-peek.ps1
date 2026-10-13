<#
.SYNOPSIS
  Reproduce "a copied image hangs but the line does not come down".

.DESCRIPTION
  Hangs one image through the same code path the clipboard uses, then samples
  the line window's visibility every 200 ms for a few seconds, so the reveal
  and the retract can both be seen. Peek only lasts 2.5 s, which is shorter
  than a human's reaction time — this makes the timing visible.
#>
param(
    [string] $Image = '',
    [double] $Seconds = 6
)

$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src/Hangline/bin/Debug/net8.0-windows10.0.19041.0/Hangline.exe'
if (-not (Test-Path $exe)) { Write-Error "no dev build at $exe — run scripts/dev.ps1 first"; exit 1 }

if (-not $Image) {
    $shots = Join-Path $env:APPDATA 'Hangline\Screenshots'
    $Image = (Get-ChildItem $shots -Filter *.png -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}
if (-not $Image -or -not (Test-Path $Image)) { Write-Error "no test image found"; exit 1 }

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'stop.ps1') | Out-Null
Start-Sleep -Milliseconds 500

$log = Join-Path $env:APPDATA 'Hangline\hangline.log'
$mark = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }

$proc = Start-Process $exe -ArgumentList '--hang', "`"$Image`"" -PassThru
Start-Sleep -Seconds 3

Write-Output "image: $Image"
Write-Output "line state before the hang:"
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'winrect.ps1') |
    Where-Object { $_ -match 'vis=True' } | ForEach-Object { Write-Output "  $_" }

Write-Output ""
Write-Output "sampling the line window for $Seconds s after the hang:"

$deadline = (Get-Date).AddSeconds($Seconds)
$prev = $null
while ((Get-Date) -lt $deadline) {
    # The line window is the one with a real rect; the message sink sits at
    # -32000,-32000 with a zero size. Match on the rect, not on the ex style.
    $vis = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'winrect.ps1') |
           Where-Object { $_ -match 'vis=True' -and $_ -notmatch 'rect=-32000,-32000 0x0' }
    $now = if ($vis) { 'DOWN' } else { 'up  ' }
    if ($now -ne $prev) {
        $t = [math]::Round(((Get-Date) - $proc.StartTime).TotalSeconds, 2)
        Write-Output ("  t={0,6}s  line={1}" -f $t, $now)
        $prev = $now
    }
    Start-Sleep -Milliseconds 150
}

Start-Sleep -Milliseconds 300
Write-Output ""
Write-Output "new log lines:"
if (Test-Path $log) {
    $fs = [System.IO.File]::Open($log, 'Open', 'Read', 'ReadWrite')
    $fs.Seek($mark, 'Begin') | Out-Null
    $sr = New-Object System.IO.StreamReader($fs)
    $sr.ReadToEnd().Split("`n") | Where-Object { $_.Trim() } | Select-Object -Last 12 | ForEach-Object { Write-Output "  $_" }
    $sr.Close()
}

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'stop.ps1') | Out-Null
