<#
.SYNOPSIS
  Install the .NET 8 SDK into the user profile — no admin rights, no system PATH edit.

.DESCRIPTION
  Uses Microsoft's official dotnet-install script with -InstallDir under
  %LOCALAPPDATA%, then adds that directory to the *user* PATH. Hangline is built
  against it but does not need it at run time: the published folder carries its
  own runtime.
#>
$dir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$script = Join-Path $PSScriptRoot 'dotnet-install.ps1'

if (-not (Test-Path $script)) {
    Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1' -OutFile $script
}

& $script -Channel LTS -Version 8.0.410 -InstallDir $dir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$path = [Environment]::GetEnvironmentVariable('Path', 'User')
if ($path -notlike "*$dir*") {
    [Environment]::SetEnvironmentVariable('Path', ($path.TrimEnd(';') + ';' + $dir), 'User')
    Write-Output "added $dir to the user PATH (open a new terminal to see it)"
}
[Environment]::SetEnvironmentVariable('DOTNET_ROOT', $dir, 'User')
Write-Output "installed: $((& (Join-Path $dir 'dotnet.exe') --version))"
