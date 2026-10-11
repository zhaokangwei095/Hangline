<#
.SYNOPSIS
  Build and run Hangline from source, the way it will actually ship.

.DESCRIPTION
  Always launches Hangline.exe rather than `dotnet Hangline.dll`: a process's
  DPI awareness comes from the executable's manifest, so running through the
  dotnet host gives a different (and wrong) monitor geometry on a scaled screen.
#>
param([string[]] $Extra = @())

$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'src/Hangline/Hangline.csproj'

# The SDK was installed per-user, so a shell that started before that has no
# PATH entry for it. Find it rather than assume.
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) {
    $local = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
    if (Test-Path $local) { $dotnet = $local }
}
if (-not $dotnet) { Write-Error 'dotnet SDK not found; run scripts/install-sdk.ps1'; exit 1 }

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'stop.ps1')
# Never set ErrorActionPreference='Stop' around a native command: MSBuild writes
# warnings to stderr, and Windows PowerShell turns that into a terminating error.
& $dotnet build $proj -v q --nologo
if ($LASTEXITCODE -ne 0) { Write-Error "build failed ($LASTEXITCODE)"; exit $LASTEXITCODE }

$exe = Join-Path $root 'src/Hangline/bin/Debug/net8.0-windows10.0.19041.0/Hangline.exe'
if ($Extra.Count -gt 0) { Start-Process $exe -ArgumentList $Extra }
else { Start-Process $exe }
Write-Output "started $exe"
