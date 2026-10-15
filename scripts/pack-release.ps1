<#
.SYNOPSIS
  Package the two distributable forms so they can be attached to a GitHub release.

.DESCRIPTION
  Writes into the repository root. That path is covered by
  `Hangline-*-win-x64-*.zip` / `*.exe` in .gitignore, so the artifacts are never
  committed by accident — they go up as GitHub release assets. The portable
  folder is zipped with its contents at the archive root, so unzipping yields
  Hangline.exe next to the runtime rather than one folder deeper.

.EXAMPLE
  powershell -File scripts/pack-release.ps1
#>
param(
    # Reads the version from the csproj unless given.
    [string] $Version = '',
    [string] $Out = (Split-Path -Parent $PSScriptRoot)
)

$proj = Join-Path $Out 'src/Hangline/Hangline.csproj'
if (-not $Version) {
    $m = Select-String -Path $proj -Pattern '<Version>(.*?)</Version>'
    if (-not $m) { Write-Error "no <Version> in $proj"; exit 1 }
    $Version = $m.Matches[0].Groups[1].Value
}

$portable = Join-Path $Out 'publish/Hangline/Hangline.exe'
$single = Join-Path $Out 'publish-singlefile/Hangline.exe'
foreach ($e in @($portable, $single)) {
    if (-not (Test-Path $e)) { Write-Error "missing $e — run scripts/publish.ps1 first"; exit 1 }
}

$zipName = "Hangline-$Version-win-x64-portable.zip"
$zip = Join-Path $Out $zipName
if (Test-Path $zip) { Remove-Item $zip -Force }

$sw = [System.Diagnostics.Stopwatch]::StartNew()
Compress-Archive -Path (Join-Path $Out 'publish/Hangline/*') -DestinationPath $zip -CompressionLevel Optimal
$sw.Stop()

$exeName = "Hangline-$Version-win-x64-singlefile.exe"
$exe = Join-Path $Out $exeName
# Copying a file onto itself throws IOException; the publish output is already
# named Hangline.exe, so a same-name target would be a no-op anyway.
if ((Resolve-Path $exe -ErrorAction SilentlyContinue) -ne (Resolve-Path $single)) {
    Copy-Item $single $exe -Force
}

function MB($p) { [math]::Round((Get-Item $p).Length / 1MB, 1) }

Write-Output "portable zip : $zipName  $(MB $zip) MB  ($($sw.Elapsed.TotalSeconds.ToString('0.0')) s)"
Write-Output "single file  : $exeName  $(MB $exe) MB"
Write-Output ""
Write-Output "These two live in the repository root and are gitignored — attach them to the release, do not commit them."
Write-Output ""
Write-Output "Attach them with an EMPTY label, so the release page shows the real file name:"
Write-Output "  gh release create v<ver> `"$zipName`" `"$exeName`""
Write-Output "Labeling an asset overrides its name in the assets list, which hides which file"
Write-Output "is which — the name already says win-x64 / portable / singlefile, so nothing is lost."
