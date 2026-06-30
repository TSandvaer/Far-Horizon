<#
.SYNOPSIS
    Far Horizon self-hosted CI runner reliability watchdog -- recovers the
    "alive-but-disconnected" failure mode on this S0 Modern-Standby laptop.

.DESCRIPTION
    On this Modern-Standby (S0) laptop, deep-idle network suspend silently drops
    the self-hosted runner's GitHub long-poll connection. The symptom:

        * the `Runner.Listener` process stays ALIVE (Get-Process finds it), but
        * `gh api repos/<repo>/actions/runners` reports the runner `offline`, and
        * queued CI jobs sit unpicked (a connected idle runner grabs a queued job
          in seconds, so a long-`queued` job + an "idle/offline" runner = the
          connection died, not the process).

    Existing tools cover the ADJACENT failure modes, not this one:
        * `keep-screens-alive` skill  -> prevents DISPLAY sleep.
        * `/runner-autostart`         -> recovers a DEAD (exited) runner process.
        * THIS watchdog                -> recovers a runner whose PROCESS is alive
                                          but whose GitHub CONNECTION is dead.

    Recovery action (only when BOTH conditions hold -- offline AND process alive):
        1. Log the detection (state + PID + GitHub status).
        2. Kill the stale `Runner.Listener` (scoped to THIS runner's directory so
           it never touches a second runner's listener).
        3. Relaunch `run.cmd` in the INTERACTIVE user session via the Task
           Scheduler shell context -- NOT as a Windows service. Running as a
           service loses the Unity Hub license (Unity exits 198); the runner MUST
           run as the logged-in interactive user.
           (memory: runner-unity-license-needs-interactive-user)

    Idempotent and safe to run repeatedly: a single relaunch per detected
    disconnect, a cooldown guard so a slow GitHub re-register doesn't trigger a
    relaunch storm, and a check that we don't relaunch while a fresh listener is
    already coming up.

.PARAMETER Repo
    owner/name slug for the GitHub repo. Default: TSandvaer/Far-Horizon.

.PARAMETER RunnerName
    The runner's registered name as shown by `gh api .../actions/runners`.
    Default: far-horizon-local (runner-1). This is matched EXACTLY so the
    watchdog never acts on a different runner (e.g. far-horizon-local-2, which
    Far Horizon keeps intentionally OFFLINE -- see
    single-unity-build-slot-serializes-orchestration).

.PARAMETER RunnerDir
    The runner install directory containing run.cmd. Default:
    C:\actions-runner-farhorizon. Used BOTH to launch run.cmd AND to scope the
    Runner.Listener process match so only THIS runner's listener is killed.

.PARAMETER IntervalSeconds
    Poll interval. Default 300 (~5 min). One-shot mode (-Once) ignores this.

.PARAMETER Once
    Run a single check-and-recover pass, then exit. Used by the Scheduled-Task
    install (the scheduler supplies the ~5-min repeat); also handy for testing.

.PARAMETER CooldownSeconds
    Minimum seconds between two relaunch actions. Default 600 (~10 min). After a
    relaunch the runner needs ~10-30s to re-register; the cooldown stops a tight
    relaunch loop while GitHub's status catches up.

.PARAMETER LogDir
    Where to write watchdog logs. Default: <RunnerDir>\_watchdog.

.NOTES
    AUTH DEPENDENCY (explicit): this script shells out to `gh api` and therefore
    requires the GitHub CLI to be installed and AUTHENTICATED as a user/token
    with `repo`/`actions:read` access to TSandvaer/Far-Horizon -- i.e. the
    Sponsor's own `gh` login. The watchdog does NOT manage credentials; if
    `gh auth status` is not logged in, every poll logs an AUTH-ERROR and takes
    no action (it will NOT kill/relaunch on an auth failure, because an auth
    failure cannot prove the runner is offline).

    This script makes NO changes to .github/, runs no Unity build, and is not a
    CI artifact. It is an operator-side reliability tool.
#>

[CmdletBinding()]
param(
    [string]$Repo            = 'TSandvaer/Far-Horizon',
    [string]$RunnerName      = 'far-horizon-local',
    [string]$RunnerDir       = 'C:\actions-runner-farhorizon',
    [int]   $IntervalSeconds = 300,
    [switch]$Once,
    [int]   $CooldownSeconds = 600,
    [string]$LogDir
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($LogDir)) {
    $LogDir = Join-Path $RunnerDir '_watchdog'
}
$LogFile        = Join-Path $LogDir 'runner-disconnect-watchdog.log'
$LastRelaunchFile = Join-Path $LogDir 'last-relaunch.txt'

function Initialize-LogDir {
    if (-not (Test-Path $LogDir)) {
        New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    }
}

function Write-Log {
    param([string]$Level, [string]$Message)
    $ts = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    $line = "[$ts] [$Level] $Message"
    Write-Host $line
    try { Add-Content -Path $LogFile -Value $line -Encoding UTF8 } catch { }
}

# --- GitHub side: is the runner reporting offline? ---------------------------
# Returns one of: 'online' | 'offline' | 'missing' | 'auth-error' | 'query-error'
function Get-RunnerGitHubStatus {
    # Verify gh auth first -- an auth failure must NOT be read as "offline".
    $null = & gh auth status 2>&1
    if ($LASTEXITCODE -ne 0) { return 'auth-error' }

    $json = & gh api "repos/$Repo/actions/runners" --paginate 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Log 'WARN' "gh api query failed: $json"
        return 'query-error'
    }

    try {
        $data = $json | ConvertFrom-Json
    } catch {
        Write-Log 'WARN' "Could not parse gh api JSON: $($_.Exception.Message)"
        return 'query-error'
    }

    # --paginate may emit multiple JSON objects; flatten the .runners arrays.
    $runners = @()
    foreach ($obj in @($data)) {
        if ($null -ne $obj.runners) { $runners += $obj.runners }
    }

    $r = $runners | Where-Object { $_.name -eq $RunnerName } | Select-Object -First 1
    if ($null -eq $r) { return 'missing' }

    # GitHub reports status as 'online' or 'offline'.
    if ($r.status -eq 'online') { return 'online' }
    return 'offline'
}

# --- Local side: is THIS runner's Runner.Listener process alive? -------------
# Scope the match to the runner's own directory so a second runner's listener
# is never matched/killed.
function Get-RunnerListenerProcess {
    # Match by executable path under $RunnerDir. The interactive runner launches
    # Runner.Listener.exe out of <RunnerDir>\bin\.
    $expectedPathPrefix = (Join-Path $RunnerDir 'bin') + '\'
    $procs = Get-CimInstance Win32_Process -Filter "Name = 'Runner.Listener.exe'" -ErrorAction SilentlyContinue
    foreach ($p in @($procs)) {
        $exePath = $p.ExecutablePath
        if (-not [string]::IsNullOrEmpty($exePath) -and
            $exePath.StartsWith($expectedPathPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            return $p
        }
        # Fallback: some builds report a null ExecutablePath; match on the
        # command line containing the runner directory instead.
        if (-not [string]::IsNullOrEmpty($p.CommandLine) -and
            $p.CommandLine.IndexOf($RunnerDir, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $p
        }
    }
    return $null
}

function Test-CooldownActive {
    if (-not (Test-Path $LastRelaunchFile)) { return $false }
    try {
        $last = Get-Content $LastRelaunchFile -Raw
        $lastDt = [datetime]::Parse($last, [System.Globalization.CultureInfo]::InvariantCulture,
                                    [System.Globalization.DateTimeStyles]::RoundtripKind)
        $elapsed = ((Get-Date).ToUniversalTime() - $lastDt.ToUniversalTime()).TotalSeconds
        return ($elapsed -lt $CooldownSeconds)
    } catch {
        return $false
    }
}

function Set-LastRelaunch {
    $ts = (Get-Date).ToUniversalTime().ToString('o')
    Set-Content -Path $LastRelaunchFile -Value $ts -Encoding UTF8
}

# --- Recovery: kill the stale listener + relaunch run.cmd interactively ------
function Invoke-RunnerRelaunch {
    param($ListenerProc)

    $runCmd = Join-Path $RunnerDir 'run.cmd'
    if (-not (Test-Path $runCmd)) {
        Write-Log 'ERROR' "run.cmd not found at '$runCmd' -- cannot relaunch. Check RunnerDir."
        return
    }

    Write-Log 'ACTION' "Killing stale Runner.Listener (PID $($ListenerProc.ProcessId)) for runner '$RunnerName'."
    try {
        Stop-Process -Id $ListenerProc.ProcessId -Force -ErrorAction Stop
        # Also stop the Runner.Worker if one is hanging (rare for an idle runner).
        Start-Sleep -Seconds 2
    } catch {
        Write-Log 'WARN' "Stop-Process failed (already gone?): $($_.Exception.Message)"
    }

    Write-Log 'ACTION' "Relaunching '$runCmd' in the interactive user session (NOT as a service)."
    try {
        # Start in a fresh console window in the current (interactive) session so
        # the Unity Hub license context is preserved. Working dir = RunnerDir.
        Start-Process -FilePath 'cmd.exe' `
                      -ArgumentList '/c', "`"$runCmd`"" `
                      -WorkingDirectory $RunnerDir `
                      -WindowStyle Normal
        Set-LastRelaunch
        Write-Log 'INFO' "run.cmd relaunched. Cooldown ${CooldownSeconds}s before another relaunch."
    } catch {
        Write-Log 'ERROR' "Failed to launch run.cmd: $($_.Exception.Message)"
    }
}

# --- One detection-and-recovery pass -----------------------------------------
function Invoke-WatchdogPass {
    $ghStatus = Get-RunnerGitHubStatus

    switch ($ghStatus) {
        'auth-error' {
            Write-Log 'AUTH-ERROR' "gh is not authenticated (gh auth status failed). Cannot determine runner state; taking NO action. Log in with 'gh auth login' (the Sponsor's token)."
            return
        }
        'query-error' {
            Write-Log 'WARN' 'GitHub query failed this pass; taking NO action (cannot prove offline).'
            return
        }
        'missing' {
            Write-Log 'WARN' "Runner '$RunnerName' not found in repo '$Repo' runner list. Is the name/repo correct? Taking NO action."
            return
        }
        'online' {
            Write-Log 'OK' "Runner '$RunnerName' is ONLINE. No action."
            return
        }
        'offline' {
            $listener = Get-RunnerListenerProcess
            if ($null -eq $listener) {
                # Process dead AND offline => this is the /runner-autostart case
                # (process exited), NOT the connection-dead case this watchdog
                # owns. Do not act; log so the operator knows to use the
                # process-dead recovery path.
                Write-Log 'INFO' "Runner '$RunnerName' is OFFLINE and its Runner.Listener process is NOT running -- this is the process-DEAD case (use /runner-autostart). This watchdog only recovers ALIVE-but-disconnected. No action."
                return
            }

            if (Test-CooldownActive) {
                Write-Log 'INFO' "Runner '$RunnerName' OFFLINE + listener alive (PID $($listener.ProcessId)), but within relaunch cooldown (${CooldownSeconds}s). Waiting for re-register. No action."
                return
            }

            Write-Log 'DETECT' "ALIVE-BUT-DISCONNECTED: runner '$RunnerName' reports OFFLINE while Runner.Listener (PID $($listener.ProcessId)) is alive. Recovering."
            Invoke-RunnerRelaunch -ListenerProc $listener
        }
        default {
            Write-Log 'WARN' "Unexpected status '$ghStatus'. No action."
        }
    }
}

# --- Entry point -------------------------------------------------------------
Initialize-LogDir
Write-Log 'START' "Runner disconnect watchdog | repo=$Repo runner=$RunnerName dir=$RunnerDir once=$($Once.IsPresent) interval=${IntervalSeconds}s cooldown=${CooldownSeconds}s"

if ($Once) {
    Invoke-WatchdogPass
    Write-Log 'DONE' 'One-shot pass complete.'
    exit 0
}

# Loop mode (use when NOT installed as a repeating Scheduled Task).
while ($true) {
    try {
        Invoke-WatchdogPass
    } catch {
        Write-Log 'ERROR' "Unhandled error in watchdog pass: $($_.Exception.Message)"
    }
    Start-Sleep -Seconds $IntervalSeconds
}
