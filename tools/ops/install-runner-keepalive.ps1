<#
.SYNOPSIS
    Installs the runner keep-alive task and re-enables the disconnect watchdog.
    MUST run elevated - every Task Scheduler write on this machine is denied
    to a non-elevated user (verified 2026-08-02: both Register-ScheduledTask
    and Enable-ScheduledTask returned Access Denied).

.DESCRIPTION
    Closes the gap that starved CI for ~4.5 hours on 2026-08-02.

    Before this, two tasks existed and NEITHER covered a runner dying
    mid-session:

      FarHorizonRunnerAutostart           at-logon trigger, NO repetition.
                                          Fired 09:47:41 (result 0), runner
                                          died after 09:56, NextRunTime empty.
                                          Nothing re-ran until next logon.

      FarHorizon-RunnerDisconnectWatchdog Disabled since 2026-07-23. Handles
                                          CONNECTION dead (process alive,
                                          GitHub offline) - it deliberately
                                          does nothing when the process is gone.

    This installs the missing PROCESS-dead half on a 5-minute repeat, and
    re-enables the connection-dead watchdog. Together they cover both modes.
#>
[CmdletBinding()]
param(
    [string] $RepoRoot = 'C:\Trunk\PRIVATE\Far-Horizon'
)

$ErrorActionPreference = 'Stop'

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
           ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host ''
    Write-Host '  NOT ELEVATED. Right-click install-runner-keepalive.cmd and pick' -ForegroundColor Red
    Write-Host '  "Run as administrator", or run this file from an elevated prompt.' -ForegroundColor Red
    Write-Host ''
    exit 1
}

$Script   = Join-Path $RepoRoot 'tools\ops\runner-keepalive.ps1'
$TaskName = 'FarHorizon-RunnerKeepAlive'
$Watchdog = 'FarHorizon-RunnerDisconnectWatchdog'

if (-not (Test-Path $Script)) { throw "Keep-alive script not found: $Script" }

Write-Host ''
Write-Host '=== 1/3  Registering the PROCESS-dead keep-alive task ===' -ForegroundColor Cyan

$action  = New-ScheduledTaskAction -Execute 'powershell.exe' `
             -Argument ('-NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $Script)

# At logon, then repeat every 5 minutes. Do NOT use [TimeSpan]::MaxValue -
# it serializes to a duration Task Scheduler rejects outright.
$trigger = New-ScheduledTaskTrigger -AtLogOn
$trigger.Repetition = (New-ScheduledTaskTrigger -Once -At (Get-Date) `
             -RepetitionInterval (New-TimeSpan -Minutes 5) `
             -RepetitionDuration (New-TimeSpan -Days 3650)).Repetition

# Interactive, Limited: the runner licenses Unity via the logged-in Unity Hub
# session. A service-context relaunch loses it and Unity exits 198.
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries `
             -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Minutes 10) `
             -MultipleInstances IgnoreNew

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
             -Principal $principal -Settings $settings -Force | Out-Null
Write-Host ("  OK  registered '{0}' (every 5 min)" -f $TaskName) -ForegroundColor Green

Write-Host ''
Write-Host '=== 2/3  Re-enabling the CONNECTION-dead watchdog ===' -ForegroundColor Cyan
try {
    Enable-ScheduledTask -TaskName $Watchdog -ErrorAction Stop | Out-Null
    Write-Host ("  OK  enabled '{0}'" -f $Watchdog) -ForegroundColor Green
} catch {
    Write-Host ("  SKIP  could not enable '{0}': {1}" -f $Watchdog, $_.Exception.Message) -ForegroundColor Yellow
}

Write-Host ''
Write-Host '=== 3/3  Verifying ===' -ForegroundColor Cyan
Get-ScheduledTask | Where-Object { $_.TaskName -in @($TaskName, $Watchdog, 'FarHorizonRunnerAutostart') } |
    Select-Object TaskName, State | Format-Table -AutoSize

Write-Host '  Firing the keep-alive once to prove it runs...' -ForegroundColor Cyan
Start-ScheduledTask -TaskName $TaskName
Start-Sleep -Seconds 12
Get-ScheduledTaskInfo -TaskName $TaskName | Select-Object LastRunTime, LastTaskResult | Format-List

$log = 'C:\actions-runner-farhorizon\_watchdog\runner-keepalive.log'
if (Test-Path $log) {
    Write-Host '  --- keep-alive log tail ---' -ForegroundColor Cyan
    Get-Content $log -Tail 4
} else {
    Write-Host '  WARNING: no keep-alive log was written. LastTaskResult 0x80070002' -ForegroundColor Yellow
    Write-Host '  means the action path did not resolve - check the -File path above.' -ForegroundColor Yellow
}

Write-Host ''
Write-Host '  Done. Expect "[OK] Listener alive" when the runner is healthy.' -ForegroundColor Green
Write-Host ''
