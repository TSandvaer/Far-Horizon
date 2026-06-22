#requires -Version 5.1
<#
.SYNOPSIS
  Stop the Far Horizon self-hosted runner's per-step console windows from
  stealing focus while you work.

  When a CI job runs on the interactive runner, each `shell: cmd` / `shell: bash`
  step spawns a console window that pops to the foreground and grabs focus. This
  watcher minimizes those windows (no-activate) the instant they appear, so your
  focus stays where you put it.

  SAFETY: it only touches windows that are BOTH (a) class `ConsoleWindowClass`
  AND (b) descendants of a `Runner.Worker.exe` process. That deliberately EXCLUDES:
    - your `run.cmd` runner window (an ancestor of Worker, not a descendant),
    - your own cmd/PowerShell windows (not under Worker),
    - the capture-gate GAME window (FarHorizon.exe — a Unity window class, not
      ConsoleWindowClass) — minimizing it would break the capture gate's render.

.PARAMETER Mode
  on     - install Startup launcher (admin-free) + start the watcher now (default)
  off    - stop the watcher + remove the Startup launcher
  status - report whether the launcher is installed and the watcher is running
  loop   - (internal) the watcher loop itself; not called directly
#>
param([ValidateSet('on','off','status','loop','diag','catch')][string]$Mode = 'on')

$ErrorActionPreference = 'Stop'

$WorkerName  = 'Runner.Worker'
$StartupDir  = [Environment]::GetFolderPath('Startup')
$VbsPath     = Join-Path $StartupDir 'FarHorizonRunnerWindowTamer.vbs'
$LogFile     = Join-Path $env:TEMP 'fh-window-tamer.log'
$MutexName   = 'Global\FarHorizonRunnerWindowTamer'

function Get-TamerProcesses {
  Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -and $_.CommandLine -like '*runner-window-tamer.ps1*' -and $_.CommandLine -like '*-Mode loop*' }
}

function Show-Status {
  Write-Host '=== Far Horizon runner window-tamer status ==='
  if (Test-Path $VbsPath) { Write-Host "Startup launcher : INSTALLED ($VbsPath)" }
  else                    { Write-Host 'Startup launcher : NOT installed' }
  $running = @(Get-TamerProcesses)
  if ($running.Count -gt 0) { Write-Host ("Watcher          : RUNNING (PID {0})" -f ($running.ProcessId -join ',')) }
  else                      { Write-Host 'Watcher          : not running' }
  $w = @(Get-Process -Name $WorkerName -ErrorAction SilentlyContinue)
  if ($w.Count -gt 0) { Write-Host ("CI job now       : a Runner.Worker is active (PID {0}) - windows being tamed" -f ($w.Id -join ',')) }
  else                { Write-Host 'CI job now       : no Runner.Worker (runner idle)' }
}

# ----------------------------------------------------------------------------
#  loop: the actual watcher
# ----------------------------------------------------------------------------
if ($Mode -eq 'loop') {
  # single-instance guard
  $mutex = New-Object System.Threading.Mutex($false, $MutexName)
  if (-not $mutex.WaitOne(0)) { exit }   # another watcher already running

  Add-Type @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class Win32Win {
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr p);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll", CharSet=CharSet.Auto)] static extern int GetClassName(IntPtr h, StringBuilder s, int max);
  delegate bool EnumWindowsProc(IntPtr h, IntPtr p);
  const int SW_SHOWMINNOACTIVE = 7;
  public static List<IntPtr> Consoles() {
    var list = new List<IntPtr>();
    EnumWindows((h,p) => {
      if (IsWindowVisible(h) && !IsIconic(h)) {
        var sb = new StringBuilder(64);
        GetClassName(h, sb, 64);
        if (sb.ToString() == "ConsoleWindowClass") list.Add(h);
      }
      return true;
    }, IntPtr.Zero);
    return list;
  }
  public static uint Pid(IntPtr h){ uint p; GetWindowThreadProcessId(h, out p); return p; }
  public static void Minimize(IntPtr h){ ShowWindow(h, SW_SHOWMINNOACTIVE); }
}
'@

  while ($true) {
    try {
      $workers = @(Get-Process -Name $WorkerName -ErrorAction SilentlyContinue)
      if ($workers.Count -eq 0) { Start-Sleep -Milliseconds 1500; continue }  # idle: cheap

      # pid -> ppid map (only built while a job is running)
      $procs = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue
      $ppid = @{}
      foreach ($p in $procs) { $ppid[[uint32]$p.ProcessId] = [uint32]$p.ParentProcessId }
      $roots = New-Object 'System.Collections.Generic.HashSet[uint32]'
      foreach ($w in $workers) { [void]$roots.Add([uint32]$w.Id) }

      foreach ($h in [Win32Win]::Consoles()) {
        $wpid = [Win32Win]::Pid($h)
        if ($wpid -eq 0) { continue }
        # walk up the parent chain: is this console a descendant of a Worker?
        $cur = [uint32]$wpid; $depth = 0; $hit = $false
        while ($cur -ne 0 -and $depth -lt 12) {
          if ($roots.Contains($cur)) { $hit = $true; break }
          if (-not $ppid.ContainsKey($cur)) { break }
          $cur = $ppid[$cur]; $depth++
        }
        if ($hit) {
          [Win32Win]::Minimize($h)
          try { "$(Get-Date -Format o) minimized console pid=$wpid" | Add-Content -Path $LogFile -ErrorAction SilentlyContinue } catch {}
        }
      }
    } catch {
      try { "$(Get-Date -Format o) ERROR $($_.Exception.Message)" | Add-Content -Path $LogFile -ErrorAction SilentlyContinue } catch {}
    }
    Start-Sleep -Milliseconds 250
  }
}

# ----------------------------------------------------------------------------
#  status
# ----------------------------------------------------------------------------
if ($Mode -eq 'status') { Show-Status; return }

# ----------------------------------------------------------------------------
#  diag: log every NEW window + every foreground-steal, with class + process
#  tree, so we can see EXACTLY what pops during a CI job. Read-only (no
#  minimizing). Run it, leave it through a full CI run, then send the output.
# ----------------------------------------------------------------------------
if ($Mode -eq 'diag') {
  $DiagLog = Join-Path $env:TEMP 'fh-window-diag.log'
  Add-Type @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class WDiag {
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc c, IntPtr p);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet=CharSet.Auto)] static extern int GetClassName(IntPtr h, StringBuilder s, int m);
  [DllImport("user32.dll", CharSet=CharSet.Auto)] static extern int GetWindowText(IntPtr h, StringBuilder s, int m);
  delegate bool EnumWindowsProc(IntPtr h, IntPtr p);
  public static string Info(IntPtr h) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    var c = new StringBuilder(160); GetClassName(h, c, 160);
    var t = new StringBuilder(160); GetWindowText(h, t, 160);
    return ((long)h) + "|" + pid + "|" + c.ToString() + "|" + t.ToString();
  }
  public static List<string> Dump() {
    var r = new List<string>();
    EnumWindows((h,p) => { if (IsWindowVisible(h) && !IsIconic(h)) r.Add(Info(h)); return true; }, IntPtr.Zero);
    return r;
  }
}
'@
  function Get-Tree($p) {
    $chain = @(); $cur = [uint32]$p; $d = 0
    while ($cur -ne 0 -and $d -lt 14) {
      $pr = Get-CimInstance Win32_Process -Filter "ProcessId=$cur" -ErrorAction SilentlyContinue
      if (-not $pr) { break }
      $chain += ("{0}:{1}" -f $cur, $pr.Name); $cur = [uint32]$pr.ParentProcessId; $d++
    }
    ($chain -join " <- ")
  }
  function Emit($s) { Write-Host $s; Add-Content -Path $DiagLog -Value $s -ErrorAction SilentlyContinue }
  $seen = @{}; foreach ($l in [WDiag]::Dump()) { $seen[$l.Split("|")[0]] = $true }
  $lastFg = [int64]0
  Emit ("=== diag start " + (Get-Date -Format o) + " (baseline " + $seen.Count + " windows suppressed) ===")
  Emit "Leave this running through a full CI job, then send me the output above (or the file: $DiagLog). Ctrl+C to stop. Auto-stops after 20 min."
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ($sw.Elapsed.TotalMinutes -lt 20) {
    try {
      $fg = [WDiag]::GetForegroundWindow(); $fgv = $fg.ToInt64()
      if ($fgv -ne $lastFg) {
        $lastFg = $fgv; $ts = (Get-Date).ToString("HH:mm:ss.fff"); $i = [WDiag]::Info($fg).Split("|")
        Emit ("FG  $ts | CLASS={0} | TITLE={1} | TREE={2}" -f $i[2], $i[3], (Get-Tree $i[1]))
      }
      foreach ($l in [WDiag]::Dump()) {
        $f = $l.Split("|")
        if (-not $seen.ContainsKey($f[0])) {
          $seen[$f[0]] = $true; $ts = (Get-Date).ToString("HH:mm:ss.fff")
          Emit ("NEW $ts | CLASS={0} | TITLE={1} | TREE={2}" -f $f[2], $f[3], (Get-Tree $f[1]))
        }
      }
    } catch {}
    Start-Sleep -Milliseconds 40
  }
  Emit "=== diag stopped (20 min elapsed) ==="
  return
}

# ----------------------------------------------------------------------------
#  catch: SetWinEventHook on EVENT_OBJECT_SHOW — fires the instant ANY window is
#  shown, so it CANNOT miss even a 1ms console flash that polling misses. Prints
#  suspected console/terminal popups LIVE + logs every window-show event.
# ----------------------------------------------------------------------------
if ($Mode -eq 'catch') {
  $CatchLog = Join-Path $env:TEMP 'fh-window-catch.log'
  $Seconds = 180
  Add-Type @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
public static class WHook {
  delegate void WinEventDelegate(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thr, uint time);
  [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint a, uint b, IntPtr m, WinEventDelegate cb, uint pid, uint tid, uint flags);
  [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr h);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Auto)] static extern int GetClassName(IntPtr h, StringBuilder s, int m);
  [DllImport("user32.dll", CharSet=CharSet.Auto)] static extern int GetWindowText(IntPtr h, StringBuilder s, int m);
  [DllImport("user32.dll")] static extern bool PeekMessage(out MSG msg, IntPtr h, uint a, uint b, uint r);
  [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG m);
  [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG m);
  [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint message; public IntPtr w; public IntPtr l; public uint time; public int x; public int y; }
  const uint SHOW = 0x8002;
  static List<string> _ev; static WinEventDelegate _cb;
  static void Proc(IntPtr hook, uint ev, IntPtr hwnd, int idObj, int idChild, uint thr, uint t) {
    if (idObj != 0 || idChild != 0 || hwnd == IntPtr.Zero) return;
    uint pid; GetWindowThreadProcessId(hwnd, out pid);
    var c = new StringBuilder(160); GetClassName(hwnd, c, 160);
    var w = new StringBuilder(200); GetWindowText(hwnd, w, 200);
    string pn = "?"; try { pn = Process.GetProcessById((int)pid).ProcessName; } catch {}
    string cls = c.ToString(); string title = w.ToString();
    string line = DateTime.Now.ToString("HH:mm:ss.fff") + " | pid=" + pid + " | PROC=" + pn + " | CLASS=" + cls + " | TITLE=" + title;
    lock (_ev) { _ev.Add(line); }
    bool susp = cls.Contains("Console") || cls.Contains("CASCADIA") || cls.Contains("OpenConsole")
      || pn == "cmd" || pn == "conhost" || pn == "pwsh" || pn == "powershell" || pn == "git" || pn == "bash" || pn == "sh"
      || pn == "node" || pn.StartsWith("Unity") || pn.StartsWith("Runner") || pn == "WindowsTerminal" || pn == "OpenConsole"
      || title.Contains("actions-runner") || title.Contains("Far-Horizon") || title.Contains("-wt");
    if (susp) Console.WriteLine("POPUP> " + line);
  }
  public static List<string> Capture(int seconds) {
    _ev = new List<string>(); _cb = new WinEventDelegate(Proc);
    IntPtr h = SetWinEventHook(SHOW, SHOW, IntPtr.Zero, _cb, 0, 0, 0);
    var sw = Stopwatch.StartNew(); MSG m;
    while (sw.ElapsedMilliseconds < (long)seconds * 1000) {
      while (PeekMessage(out m, IntPtr.Zero, 0, 0, 1)) { TranslateMessage(ref m); DispatchMessage(ref m); }
      Thread.Sleep(3);
    }
    UnhookWinEvent(h); return _ev;
  }
}
'@
  Write-Host "Hooking EVERY window-show event for $Seconds s (cannot miss flashes). Let the popups happen."
  Write-Host "Suspected console/terminal popups print LIVE below as 'POPUP>'. Full list logs to: $CatchLog"
  Write-Host "----------------------------------------------------------------------"
  $events = [WHook]::Capture($Seconds)
  "=== catch $(Get-Date -Format o) : $($events.Count) window-show events ===" | Set-Content $CatchLog
  $events | Add-Content $CatchLog
  Write-Host "----------------------------------------------------------------------"
  Write-Host "Done. $($events.Count) total window-show events captured; full list in $CatchLog"
  Write-Host "Paste me the POPUP> lines above (or the whole log file)."
  return
}

# ----------------------------------------------------------------------------
#  on: install Startup launcher (admin-free) + start now
# ----------------------------------------------------------------------------
if ($Mode -eq 'on') {
  # VBScript launches PowerShell fully hidden (no console flash) at logon
  $cmd = 'powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""' + $PSCommandPath + '"" -Mode loop'
  $vbs = 'CreateObject("WScript.Shell").Run "' + $cmd + '", 0, False'
  Set-Content -Path $VbsPath -Value $vbs -Encoding ASCII
  Write-Host "Startup launcher installed: $VbsPath"

  $already = @(Get-TamerProcesses)
  if ($already.Count -gt 0) {
    Write-Host "Watcher already running (PID $($already.ProcessId -join ',')) - left as is."
  } else {
    Start-Process -FilePath 'wscript.exe' -ArgumentList ('"{0}"' -f $VbsPath) | Out-Null
    Write-Host 'Watcher started now (hidden). It minimizes runner console windows on each CI step.'
  }
  Write-Host "DONE. Survives reboots via the Startup launcher (fires on your logon). Log: $LogFile"
  Write-Host ''
  Show-Status
  return
}

# ----------------------------------------------------------------------------
#  off: stop watcher + remove Startup launcher
# ----------------------------------------------------------------------------
if ($Mode -eq 'off') {
  if (Test-Path $VbsPath) { Remove-Item $VbsPath -Force; Write-Host "Removed Startup launcher: $VbsPath" }
  else                    { Write-Host 'No Startup launcher to remove.' }
  $running = @(Get-TamerProcesses)
  if ($running.Count -gt 0) {
    foreach ($r in $running) { Stop-Process -Id $r.ProcessId -Force -ErrorAction SilentlyContinue }
    Write-Host "Stopped watcher (PID $($running.ProcessId -join ',') )."
  } else {
    Write-Host 'No running watcher to stop.'
  }
  Write-Host ''
  Show-Status
  return
}
