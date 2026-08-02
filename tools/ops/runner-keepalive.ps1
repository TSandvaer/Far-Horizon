<#
.SYNOPSIS
    Restarts the far-horizon-local self-hosted runner if its process is DEAD.

.DESCRIPTION
    Covers the failure mode neither existing tool covered:

      FarHorizonRunnerAutostart          - at-logon trigger, NO repetition.
                                           Fires once per logon. If the runner
                                           dies mid-session, nothing re-runs it.
      FarHorizon-RunnerDisconnectWatchdog - handles CONNECTION dead (process
                                           alive, GitHub reports offline). It
                                           deliberately does nothing when the
                                           process itself is gone.

    Measured gap, 2026-08-02: autostart fired at 09:47:41 (result 0), the runner
    died after 09:56, NextRunTime was empty, and CI was starved for ~4.5 hours.
    Every self-hosted job queued behind a 'playmode' job that could never start,
    and because ci.yml uses concurrency ci-<ref>, later runs sat pending with
    ZERO jobs created - presenting as a GitHub dispatch outage when it was
    simply a dead runner.

    This script is the process-dead half. It is deliberately dumb and safe:
      - if a Runner.Listener launched from RunnerDir is alive -> do nothing
      - otherwise -> relaunch run.cmd in the INTERACTIVE user session

.NOTES
    Interactive, never a service: the runner licenses Unity through the
    logged-in Unity Hub session. A service-context relaunch loses it and Unity
    exits 198.

    Scoped to ONE runner by path. It never touches far-horizon-local-2
    (C:\actions-runner-2), which Far Horizon keeps intentionally OFFLINE - a
    second online runner breaks windowed captures (A/B-CONFIRMED 2026-06-29).

    Saved UTF-8 WITH BOM and pure ASCII so Windows PowerShell 5.1 parses it.
#>
[CmdletBinding()]
param(
    [string] $RunnerDir           = 'C:\actions-runner-farhorizon',
    [string] $LogDir              = 'C:\actions-runner-farhorizon\_watchdog',
    [int]    $CooldownSeconds     = 300
)

$ErrorActionPreference = 'Stop'

$LogFile   = Join-Path $LogDir 'runner-keepalive.log'
$StampFile = Join-Path $LogDir 'last-keepalive-launch.txt'
$RunCmd    = Join-Path $RunnerDir 'run.cmd'

function Write-Log([string] $Level, [string] $Message) {
    $line = '[{0}] [{1}] {2}' -f (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'), $Level, $Message
    Write-Output $line
    try {
        if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }
        Add-Content -Path $LogFile -Value $line -Encoding UTF8
    } catch { }
}

if (-not (Test-Path $RunCmd)) {
    Write-Log 'ERROR' ("run.cmd not found at '{0}'. Nothing to do." -f $RunCmd)
    exit 1
}

# Is a listener for THIS runner alive? Match by executable path, not by name -
# matching on name alone would also see far-horizon-local-2 and wrongly suppress.
$alive = @(Get-Process -Name 'Runner.Listener' -ErrorAction SilentlyContinue |
           Where-Object { $_.Path -and $_.Path.StartsWith($RunnerDir, [System.StringComparison]::OrdinalIgnoreCase) })

if ($alive.Count -gt 0) {
    Write-Log 'OK' ("Listener alive (pid {0}). No action." -f ($alive[0].Id))
    exit 0
}

# Cooldown: a relaunch takes ~10-30s to register. Without this, a 5-minute
# schedule could stack launches while the previous one is still coming up.
if (Test-Path $StampFile) {
    try {
        $last = [datetime]::Parse((Get-Content $StampFile -Raw).Trim())
        $age  = ((Get-Date).ToUniversalTime() - $last).TotalSeconds
        if ($age -lt $CooldownSeconds) {
            Write-Log 'SKIP' ("Listener dead but last launch was {0:N0}s ago (cooldown {1}s). Waiting." -f $age, $CooldownSeconds)
            exit 0
        }
    } catch {
        Write-Log 'WARN' 'Could not read cooldown stamp; proceeding.'
    }
}

Write-Log 'DEAD' 'No Runner.Listener for this runner. Relaunching run.cmd interactively.'

try {
    Start-Process -FilePath $RunCmd -WorkingDirectory $RunnerDir | Out-Null
    (Get-Date).ToUniversalTime().ToString('o') | Set-Content -Path $StampFile -Encoding ASCII
    Start-Sleep -Seconds 10
    $now = @(Get-Process -Name 'Runner.Listener' -ErrorAction SilentlyContinue |
             Where-Object { $_.Path -and $_.Path.StartsWith($RunnerDir, [System.StringComparison]::OrdinalIgnoreCase) })
    if ($now.Count -gt 0) {
        Write-Log 'RELAUNCHED' ("Listener is up (pid {0})." -f ($now[0].Id))
        exit 0
    }
    Write-Log 'ERROR' 'Launch issued but no listener appeared within 10s. Check the runner window.'
    exit 1
} catch {
    Write-Log 'ERROR' ("Relaunch failed: {0}" -f $_.Exception.Message)
    exit 1
}
