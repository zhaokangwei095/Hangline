<#
.SYNOPSIS
  List notification-area (tray) icon names, so an agent can confirm a tray icon
  appeared without hovering over the user's screen.

.DESCRIPTION
  Walks the overflow window and the visible tray with UI Automation and prints
  each button's Name, which is the tooltip text the app set.

.EXAMPLE
  powershell -File scripts/tray.ps1 -Contains Hangline
#>
param([string] $Contains = '')

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$root = [System.Windows.Automation.AutomationElement]::RootElement

function Find-ByClass([System.Windows.Automation.AutomationElement] $scope, [string] $class,
                      [System.Windows.Automation.TreeScope] $depth) {
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ClassNameProperty, $class)
    return $scope.FindFirst($depth, $cond)
}

$targets = @()

$overflow = Find-ByClass $root 'NotifyIconOverflowWindow' ([System.Windows.Automation.TreeScope]::Children)
if ($overflow) { $targets += $overflow }

$tray = Find-ByClass $root 'Shell_TrayWnd' ([System.Windows.Automation.TreeScope]::Children)
if ($tray) {
    $promoted = Find-ByClass $tray 'TrayNotifyWnd' ([System.Windows.Automation.TreeScope]::Descendants)
    if ($promoted) { $targets += $promoted }
}

$buttonType = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Button)

foreach ($t in $targets) {
    foreach ($e in $t.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttonType)) {
        $name = $e.Current.Name
        if ([string]::IsNullOrWhiteSpace($name)) { continue }
        if ($Contains -and $name -notlike "*$Contains*") { continue }
        Write-Output $name
    }
}
