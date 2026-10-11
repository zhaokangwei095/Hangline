<#
.SYNOPSIS
  Build the portable folder you can double-click, on a machine with nothing installed.

.DESCRIPTION
  Self-contained: the .NET runtime travels inside the folder, so the target
  machine needs no runtime, no admin rights and no installer. -SingleFile also
  produces the one-exe variant, which unpacks native libraries to %TEMP% on each
  launch — kept as a comparison, not the default, because a folder that can be
  copied to a USB stick is the safer "开箱可用".

.EXAMPLE
  powershell -File scripts/publish.ps1
  powershell -File scripts/publish.ps1 -SingleFile
#>
param([switch] $SingleFile)

$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'src/Hangline/Hangline.csproj'

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) {
    $local = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
    if (Test-Path $local) { $dotnet = $local }
}
if (-not $dotnet) { Write-Error 'dotnet SDK not found; run scripts/dotnet-install.ps1 first'; exit 1 }

$out = if ($SingleFile) { Join-Path $root 'publish-singlefile' } else { Join-Path $root 'publish/Hangline' }

& $dotnet publish $proj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=$SingleFile `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -o $out --nologo -v q

if ($LASTEXITCODE -ne 0) { Write-Error "publish failed ($LASTEXITCODE)"; exit $LASTEXITCODE }

$exe = Join-Path $out 'Hangline.exe'
if (-not (Test-Path $exe)) { Write-Error "no exe at $exe"; exit 1 }

$files = Get-ChildItem $out -File
$size = [math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Output "portable: $out"
Write-Output ("  {0} files, {1} MB, {2}" -f $files.Count, $size, (Get-Item $exe).LastWriteTime)
