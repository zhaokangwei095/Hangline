<#
.SYNOPSIS
  Stop every running Hangline, whichever host started it.

.DESCRIPTION
  A second instance exits on the single-instance mutex, so a stale one makes a
  new launch look like it silently did nothing. Both the app host and the
  `dotnet Hangline.dll` form are covered.
#>
$stopped = 0
foreach ($name in 'Hangline', 'dotnet') {
    foreach ($p in Get-Process -Name $name -ErrorAction SilentlyContinue) {
        try {
            if ($name -eq 'dotnet') {
                # Only kill dotnet hosts running our dll, never an unrelated build server.
                if ($p.MainModule.FileName -notmatch 'dotnet$' -or $p.Path -notmatch 'dotnet') { continue }
                $cmd = (Get-CimInstance Win32_Process -Filter "ProcessId=$($p.Id)").CommandLine
                if ($cmd -notmatch 'Hangline\.dll') { continue }
            }
            $p.Kill(); $p.WaitForExit(2000) | Out-Null; $stopped++
        } catch { }
    }
}
Write-Output "stopped $stopped"
