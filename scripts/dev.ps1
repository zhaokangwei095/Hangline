<#
.SYNOPSIS
  Build and run Hangline from source, the way it will actually ship.

.DESCRIPTION
  Always launches Hangline.exe rather than `dotnet Hangline.dll`: a process's
  DPI awareness comes from the executable's manifest, so running through the
  dotnet host gives a different (and wrong) monitor geometry on a scaled screen.
#>
param([string[]] $Extra = @())

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'src/Hangline/Hangline.csproj'

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'stop.ps1')
dotnet build $proj -v q --nologo
if ($LASTEXITCODE) { exit $LASTEXITCODE }

$exe = Join-Path $root 'src/Hangline/bin/Debug/net8.0-windows10.0.19041.0/Hangline.exe'
if ($Extra.Count -gt 0) { Start-Process $exe -ArgumentList $Extra }
else { Start-Process $exe }
Write-Output "started $exe"
