#requires -Version 5.1
<#
.SYNOPSIS
  Reboot-survival for the Far Horizon GitHub Actions self-hosted Unity runner.

  Installs a logon-triggered Scheduled Task that launches run.cmd in the user's
  INTERACTIVE session (so Unity's per-user license is present), and disables the
  leftover NETWORK SERVICE runner service so it can never grab the runner
  registration and conflict.

  Attended-reboot only: NO auto-login, NO stored password. You log in after a
  reboot; the task then starts the runner automatically.

.PARAMETER Mode
  on     - create the logon task + disable the runner service (default)
  off    - remove the logon task (leaves the service disabled)
  status - report task state, service state, and whether a runner is running
#>
param([ValidateSet('on','off','status')][string]$Mode = 'on')

$ErrorActionPreference = 'Stop'

$RunnerDir  = 'C:\actions-runner-farhorizon'
$RunCmd     = Join-Path $RunnerDir 'run.cmd'
$TaskName   = 'FarHorizonRunnerAutostart'
$ResultFile = Join-Path $env:TEMP 'fh-runner-autostart-result.txt'

function Get-RunnerService {
  Get-CimInstance Win32_Service -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like 'actions.runner.*far-horizon*' } |
    Select-Object -First 1
}

function Test-Admin {
  $id = [Security.Principal.WindowsIdentity]::GetCurrent()
  (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Show-Status {
  Write-Host '=== Far Horizon runner auto-start status ==='
  $t = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
  if ($t) {
    $info = Get-ScheduledTaskInfo -TaskName $TaskName -ErrorAction SilentlyContinue
    Write-Host ("Logon task : INSTALLED (State={0}, LastRun={1}, LastResult={2})" -f `
      $t.State, $info.LastRunTime, $info.LastTaskResult)
  } else {
    Write-Host 'Logon task : NOT installed'
  }
  $svc = Get-RunnerService
  if ($svc) {
    Write-Host ("Runner svc : {0} (StartMode={1}, State={2})" -f $svc.Name, $svc.StartMode, $svc.State)
  } else {
    Write-Host 'Runner svc : not present'
  }
  $proc = Get-Process -Name 'Runner.Listener' -ErrorAction SilentlyContinue
  if ($proc) {
    Write-Host ("Runner now : RUNNING (Runner.Listener PID {0})" -f ($proc.Id -join ','))
  } else {
    Write-Host 'Runner now : not currently running (start run.cmd, or log off/on once the task is installed)'
  }
}

# --- status: read-only, no elevation ---
if ($Mode -eq 'status') { Show-Status; return }

# --- on/off need admin (service change + task in a specific principal) ---
if (-not (Test-Admin)) {
  Write-Host "Requesting administrator elevation (UAC) for the '$Mode' operation..."
  if (Test-Path $ResultFile) { Remove-Item $ResultFile -Force -ErrorAction SilentlyContinue }
  $p = Start-Process powershell.exe -Verb RunAs -PassThru -Wait -ArgumentList @(
    '-NoProfile','-ExecutionPolicy','Bypass','-File',('"{0}"' -f $PSCommandPath),'-Mode',$Mode
  )
  Write-Host ''
  if (Test-Path $ResultFile) {
    Get-Content $ResultFile | ForEach-Object { Write-Host $_ }
  } else {
    Write-Host 'No result captured - the UAC elevation prompt was likely declined/cancelled. Nothing changed.'
  }
  Write-Host ''
  Show-Status
  return
}

# ============================================================
#  Elevated from here (admin). Log to $ResultFile for the parent.
# ============================================================
$log = New-Object System.Collections.Generic.List[string]
function W([string]$m) { $log.Add($m) | Out-Null; Write-Host $m }

try {
  if ($Mode -eq 'on') {
    if (-not (Test-Path $RunCmd)) {
      W "ERROR: $RunCmd not found - is the runner installed at $RunnerDir ? Aborting."
    } else {
      # --- logon Scheduled Task: visible console, current user, no time limit ---
      $who      = "$env:USERDOMAIN\$env:USERNAME"
      $action   = New-ScheduledTaskAction -Execute "$env:SystemRoot\System32\cmd.exe" `
                    -Argument ('/c "{0}"' -f $RunCmd) -WorkingDirectory $RunnerDir
      $trigger  = New-ScheduledTaskTrigger -AtLogOn -User $who
      $principal= New-ScheduledTaskPrincipal -UserId $who -LogonType Interactive -RunLevel Limited
      $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
                    -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew `
                    -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
      Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
        -Principal $principal -Settings $settings -Force | Out-Null
      W "Scheduled Task '$TaskName' installed - At logon of $who, visible console, no time limit, single-instance."

      # --- disable the leftover runner service so it can't conflict ---
      $svc = Get-RunnerService
      if ($svc) {
        if ($svc.State -eq 'Running') {
          Stop-Service -Name $svc.Name -Force -ErrorAction SilentlyContinue
          W "Stopped running service '$($svc.Name)'."
        }
        Set-Service -Name $svc.Name -StartupType Disabled
        W "Service '$($svc.Name)' set to Disabled (was StartMode=$($svc.StartMode))."
      } else {
        W 'No runner service found to disable (already removed?).'
      }

      W 'DONE: reboot-survival is set up.'
      W 'Your currently-running run.cmd was NOT touched. The task takes effect on your NEXT logon (after a reboot or sign-out/in).'
    }
  }
  elseif ($Mode -eq 'off') {
    $t = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($t) {
      Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
      W "Scheduled Task '$TaskName' removed."
    } else {
      W "No task '$TaskName' found (nothing to remove)."
    }
    W 'Note: the runner service is left DISABLED (service mode is broken for Unity).'
    W 'To restore service mode later: Set-Service -Name <actions.runner...far-horizon-local> -StartupType Manual'
  }
}
catch {
  W "ERROR during '$Mode': $($_.Exception.Message)"
}

$log | Set-Content -Path $ResultFile -Encoding UTF8
