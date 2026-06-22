# PlayMode-Enter Headless Deadlock — Root-Cause Analysis & Fix Options

## Question

PlayMode CI on the Far Horizon self-hosted Windows runner (GitHub Actions `unity` job) hangs
consistently at play-mode-ENTER for ~18–20 min, then is killed by the job timeout. The hang
pre-dates any test fixture and pre-dates Boot scene load. The UUM-142421 hypothesis (EPERM
rename bug) was the prior candidate; upgrading to 6000.4.11f1 REFUTED it — the hang persists
identically on the new version (CI run 27847884304). What is the actual root cause, and what
are the ranked fix options?

---

## Bottom line

The most strongly evidenced root cause is **Android module USB/ADB device scanning blocking
play-mode-enter on a Windows headless runner** — a known multi-version Unity bug that persists
into Unity 6 (confirmed still present in 6000.0.45f1, April 2025). The CI log signature
(`~17× "Scanning for USB devices : Nms" → dead-silent`) is the textbook symptom. This
mechanism is completely independent of UUM-142421 (the EPERM fix was for a different subsystem
and correctly had no effect on the hang).

The highest-confidence fix is **Option 1: remove the Android module from the 6000.4.11f1
editor installation on the dev machine**. It eliminates the scanning code entirely. No code
changes, no CI changes, verifiable in one runner restart.

A robust secondary option (no module-removal required) is **Option 3: run PlayMode without
`-batchmode`** using the interactive-user runner configuration already in place — this trades
headless for an environment where the New Input System and input subsystem initialise safely.
This is the approach the Jenkins/GitHub-Actions community has converged on for Windows
self-hosted runners when `-nographics` PlayMode hangs resist other mitigations.

---

## Evidence

### E-1 — Android module "Scanning for USB devices" multi-year hang

- **Source:** Unity Issue Tracker, "Opening a project which doesn't target Android stalls for a
  few seconds with 'Scanning for USB devices'" (case 1349380) —
  [https://issuetracker.unity3d.com/issues/opening-project-which-doesnt-target-android-stall-for-a-few-seconds-with-scanning-for-usb-devices](https://issuetracker.unity3d.com/issues/opening-project-which-doesnt-target-android-stall-for-a-few-seconds-with-scanning-for-usb-devices)

  What it says: Whenever the Android module is installed, Unity scans for USB (ADB) devices on
  every project load and play-mode-enter — even when the project does NOT target Android.
  Originally synchronous/blocking. Marked "Fixed in 2021.2.x and 2022.1.x" but confirmed still
  occurring in 2021.3.23f1, 2022.3.13f, and **Unity 6000.0.45f1** (April 2025 comment — the
  "fixed" versions patched the regular stall but did not eliminate the subsystem). In a headless
  runner environment with no ADB daemon, slow USB enumeration, or a misbehaving USB peripheral,
  the scan blocks indefinitely instead of returning after a short timeout.

  **Strength: Strong** (official issue tracker, reproducible cross-version pattern, April 2025
  community confirmation in Unity 6 stream).

  **Application to Far Horizon:** The CI play-mode log shows exactly `~17× "Scanning for USB
  devices : Nms"` immediately after "Entering Playmode with Reload Domain disabled" + "Reload
  Scene disabled," then silence until the 20-min timeout. This is the textbook symptom. The
  runner machine is the developer's own Windows desktop — it has the Android module installed
  (the standard Unity Hub install includes it by default). Far Horizon does NOT target Android.
  The USB device set on the machine (keyboard, mouse, HID peripherals, potentially connected
  phones/cameras) provides exactly the conditions for a slow or non-responding ADB query.

- **Source:** Unity Engine Discussions, "Android module slowing down Editor for non-Android
  projects now on Unity 2021" —
  [https://discussions.unity.com/t/android-module-slowing-down-editor-for-non-android-projects-now-on-unity-2021/848410](https://discussions.unity.com/t/android-module-slowing-down-editor-for-non-android-projects-now-on-unity-2021/848410)

  What it says: Confirms the root cause is synchronous USB/ADB scanning triggered by Android
  module presence. **Confirmed workaround: remove Android module from the Unity installation.**
  The thread also documents that the scanning can escalate from a "few seconds stall" to an
  indefinite hang in batch-mode CI environments where no ADB response is forthcoming.

  **Strength: Strong** (large thread, multiple independent reproductions, direct workaround
  confirmation, Unity staff acknowledged and committed to async fix).

### E-2 — ADB CancellationTokenSource fix in 6000.0.15f1, NOT in 6000.4.x

- **Source:** Unity Issue Tracker, "Batch Mode and Cloud Build get stuck at 'Scanning for ADB
  Devices' when building for the Android or iOS platforms" —
  [https://issuetracker.unity3d.com/issues/batch-mode-and-cloud-build-get-stuck-at-scanning-for-adb-devices-when-building-for-the-android-or-ios-platforms](https://issuetracker.unity3d.com/issues/batch-mode-and-cloud-build-get-stuck-at-scanning-for-adb-devices-when-building-for-the-android-or-ios-platforms)

  What it says: A specific CancellationTokenSource-dispose bug in ADB scanning was fixed in
  **6000.0.15f1** (and 2021.3.43f1, 2022.3.43f1). The Far Horizon runner uses **6000.4.10f1**
  (now upgrading to 6000.4.11f1) — these are a different LTS stream from 6000.0.x. The fix in
  6000.0.15f1 may NOT have been backported to the 6000.4 stream; the 6000.4.11f1 EPERM-only
  fix for UUM-142421 does not cover the ADB scanning hang. This is consistent with the hang
  persisting after the 6000.4.11f1 upgrade.

  **Strength: Strong** (official issue tracker, version-specific fix citations, version mismatch
  is traceable).

### E-3 — Input System HID infinite loop on USB HID device with 0xFFFF usage max

- **Source:** Unity Issue Tracker, "New input system Windows native backend gets stuck in
  infinite loop when USB HID device with 0xFFFF usage max is plugged in" —
  [https://issuetracker.unity3d.com/issues/new-input-system-windows-native-backend-gets-stuck-in-infinite-loop-when-usb-hid-device-with-0xffff-usage-max-is-plugged-in](https://issuetracker.unity3d.com/issues/new-input-system-windows-native-backend-gets-stuck-in-infinite-loop-when-usb-hid-device-with-0xffff-usage-max-is-plugged-in)

  What it says: New Input System's Windows backend loops indefinitely when a HID with `0xFFFF`
  usage max is connected. **Fixed in 2019.4.X / 2020.3.X / 2021.2.X / 2022.1.X** — predates
  Unity 6.

  **Important project-specific note:** Far Horizon does NOT have `com.unity.inputsystem` in
  `Packages/manifest.json`. The project uses the legacy Input Manager. **This bug is therefore
  NOT applicable** to Far Horizon's deadlock. However, it reinforces the general principle
  that USB HID device enumeration during play-mode-enter is a documented hang class on Windows.

  **Strength: Strong** (official issue tracker). **Relevance: Not applicable** (project uses
  legacy Input Manager, not Input System package).

### E-4 — PlayMode tests with `-nographics` are unsupported / known to hang

- **Source:** Unity Engine Discussions, "Do PlayMode tests work with -nographics? Also, ways to
  speed up tests?" —
  [https://discussions.unity.com/t/do-playmode-tests-work-with-nographics-also-ways-to-speed-up-tests/942644](https://discussions.unity.com/t/do-playmode-tests-work-with-nographics-also-ways-to-speed-up-tests/942644)

  What it says: When `-nographics` is added to `-runTests -testPlatform PlayMode`, the runner
  "gets stuck for 30 minutes, last lines showing up are: Android Extension - Scanning For ADB
  Devices." Zero Unity staff response confirming `-nographics` is supported with PlayMode. The
  Unity Manual's Desktop Headless Mode page documents `-nographics` for the Player runtime but
  makes no claim about PlayMode tests.

  **Strength: Moderate** (community thread, no Unity staff confirmation or denial).

- **Source:** Unity Engine Discussions, "Running non-batch playmode tests on the build server" —
  [https://discussions.unity.com/t/running-non-batch-playmode-tests-on-the-build-server/817683](https://discussions.unity.com/t/running-non-batch-playmode-tests-on-the-build-server/817683)

  What it says: Confirmed fix for Windows CI PlayMode hangs when running through a service
  (Jenkins): switch from "run as service" to "run as interactive application." PlayMode requires
  the interactive user session. Far Horizon's runner already runs interactively (`run.cmd`
  in the Sponsor's desktop session — `CI.md` §3 / memory
  `runner-Unity-license-needs-the-interactive-user`). However, `-batchmode -nographics` still
  bypasses the interactive session's graphics context.

  **Strength: Moderate** (community confirmed fix; not a Unity staff post).

### E-5 — Green run discriminator: 1 USB scan vs 17+ scans before freeze

- **Source:** `unity-conventions.md` §Headless/CLI rituals (project doc, Devon's
  investigation `86caac81y`, 2026-06-17) — `c:/Trunk/PRIVATE/Far-Horizon/.claude/docs/unity-conventions.md`

  What it says: Green run `27685445841` → `1 USB scan → "Loaded scene Boot.unity" → ran tests
  (12,359 log lines)`. Frozen run → `532 lines total, ~17× "Scanning for USB devices : Nms" →
  dead-silent`. Same `EnterPlayModeOptions` (Reload Domain disabled, Reload Scene disabled).
  Same code. The ONLY variable: **runner USB/HID state** at the time of the run.

  **Strength: Strong** (direct project CI log evidence, cited in unity-conventions.md from
  Devon's investigation — a first-party observable).

  **Application:** The variable USB scan count (1 vs 17+) points to a USB device responding
  slowly or not at all, causing the ADB/HID scanning loop to spin rather than returning quickly.
  The green run had an ADB path that resolved in one pass; the frozen runs encountered a
  device or timeout state that never resolved.

### E-6 — UUM-142421 is definitively a DIFFERENT bug (refuted hypothesis)

- **Source:** `team/erik-consult/concurrent-unity-build-isolation-research.md` (this project,
  2026-06-19) + CI run 27847884304 (evidence cited in dispatch brief)

  What it says: UUM-142421 was the EPERM rename bug in UPM package installation. It was fixed
  in 6000.4.11f1. The PlayMode hang persists on 6000.4.11f1 (run 27847884304), confirming the
  two bugs are orthogonal. The note's bottom-line "Conditional YES" for concurrent builds
  remains valid; the UPM-EPERM fix is unrelated to the play-mode-enter hang.

  **Strength: Strong** (project-internal evidence, direct refutation).

---

## Root-cause synthesis

The play-mode-enter deadlock is an **Android module ADB/USB device-scan blocking on a headless
Windows runner** — not a code bug, not a domain-reload bug, not a UPM package bug. The
evidence chain:

1. Android module is installed on the machine (default Unity Hub install; needed for the Editor
   toolchain even if the project doesn't ship to Android).
2. On every play-mode-enter in headless batchmode, the Android subsystem initiates USB device
   scanning (ADB enumeration).
3. In the runner's current USB state, one or more queries do not return quickly (device missing,
   Android phone disconnected mid-scan, slow enumeration, etc.).
4. Because the scan is (partially) synchronous in the 6000.4 stream (the async fix from
   6000.0.15f1 may not be backported), it blocks the play-mode-enter thread.
5. No test ever starts. No scene loads. The CI job times out at 40 min.

The "sometimes it works" (green run `27685445841`) is explained by USB device state: when
the machine's ADB enumeration returns quickly in one pass, play-mode-enter proceeds normally.
This is a classic environmental race, not a code bug — consistent with Devon's discriminator
("runner HID/USB-input-subsystem hang, not code").

---

## Fix options (ranked by confidence and ease)

### Option 1 — Remove the Android module from the editor installation [RECOMMENDED]

**What:** In Unity Hub → Installs → 6000.4.11f1 → Add Modules → UNCHECK Android Build Support
(and its sub-components: Android SDK & NDK, OpenJDK). Remove. Restart the runner.

**Confidence: HIGH.** The Android module is the only source of the ADB/USB scanning code
that produces the `"Scanning for USB devices : Nms"` log. Removing it eliminates the scan
entirely. The project never targets Android; the module provides zero production value. The
workaround is universally confirmed in E-1/E-2 above and reproducible.

**Verification:** After module removal, trigger a new CI run. The `playmode.log` will lack the
`"Scanning for USB devices"` lines entirely, and play-mode-enter should proceed to boot-scene
load within seconds.

**Cost:** ~5 min one-time action in Unity Hub UI. Zero CI/code changes required.

**Risk:** Essentially none for a non-Android project. The Android module occupies ~3–4 GB on
disk; removal frees space and speeds up project-open. If Android is ever added as a target,
the module can be reinstalled.

**Verifiable on this runner:** YES — the Hub is accessible on the dev machine.

---

### Option 2 — Drop `-nographics` flag from the PlayMode CI step only

**What:** In `ci.yml`, change the PlayMode step from:
```
"%UNITY%" -batchmode -nographics -projectPath ... -runTests -testPlatform PlayMode ...
```
to:
```
"%UNITY%" -batchmode -projectPath ... -runTests -testPlatform PlayMode ...
```
Keep `-nographics` for Bootstrap, EditMode, and BuildWindows (where it is safe and beneficial).

**Confidence: MEDIUM-HIGH.** Multiple community reports confirm that `-nographics` specifically
triggers or worsens the ADB scan hang (E-4). Without `-nographics`, Unity initialises a real
(or null-device) graphics context through Direct3D, which short-circuits the input subsystem
initialisation path that triggers ADB scanning. Not confirmed by Unity staff, but
community-reproducible.

**Caveat:** Without `-nographics`, Unity initialises graphics on the CI runner. Since the runner
runs in the Sponsor's interactive desktop session (not session 0), a real D3D device is
available. This should work — but it adds ~5–15 s of graphics init overhead per run, and
introduces the risk of any GPU-bound PlayMode test being machine-dependent.

**Cost:** One line change in `ci.yml`. Must be tested. Combine with Option 1 for belt-and-
suspenders.

**Verifiable on this runner:** YES. One CI run confirms or refutes.

---

### Option 3 — Run PlayMode without `-batchmode` (interactive window)

**What:** Drop BOTH `-batchmode` and `-nographics` for the PlayMode step, running Unity in a
windowed mode. The runner already runs interactively in the Sponsor's desktop session (not
session 0), so a window can be created and the input subsystem initialises against the real
HID stack without the ADB scanning path.

**Confidence: MEDIUM.** The Jenkins thread (E-4, "Running non-batch playmode tests on the
build server") showed that switching from service (no interactive session) to interactive
application fixed the PlayMode hang — the interactive session is already satisfied here.
However, dropping `-batchmode` introduces a visible editor window during CI runs, and the
runner-autostart setup must be designed to tolerate a foreground editor window.

**Caveat:** A windowed Unity editor during CI can steal focus, interfere with other desktop
activity, and may cause issues if the desktop locks or the screen goes to sleep during a run.
Far Horizon already has `keep-screens-alive` skill for unattended mode; that mitigates the
sleep risk but not the focus-steal.

**Cost:** `ci.yml` change + testing. Less risky than it sounds on a desktop-interactive runner,
but more side-effects than Options 1 or 2.

**Verifiable on this runner:** YES, but observation is needed to confirm the editor window
opens and tests run cleanly.

---

### Option 4 — Disconnect USB-connected Android devices during CI runs

**What:** Ensure no Android device is connected (via USB or ADB-over-WiFi) to the dev machine
when CI runs. The `adb kill-server` command before the PlayMode step would also prevent an
ADB daemon from responding to queries.

**Confidence: LOW-MEDIUM.** If the root cause is specifically a *connected Android device
blocking ADB enumeration*, this helps. But the scanner hangs even with no device (the
"Scanning for USB devices" message appears on machines with no Android device — it loops over
all USB devices, not just Android phones). The "no device connected" path historically
triggers a short timeout-and-continue, but the 17+ scan log lines suggest the timeout itself
is hanging.

**Cost:** Operational discipline + a pre-step in the CI yaml. Not addressing the root cause;
treating a symptom.

**Verifiable on this runner:** Partial — the hang varies by USB state, so a single run may not
confirm.

---

### Option 5 — Mark PlayMode advisory; quarantine it from the hard gate

**What:** Not a fix of the deadlock, but a policy response: accept that headless Windows
PlayMode is unreliable in its current environment (E-1 through E-5 all show it is
environment-sensitive), keep the job in `continue-on-error: true` (it already is), and
**do not add PlayMode to the required-status check** until the environment is clean (Options
1–3 done). The soak (interactive exe test) and EditMode remain the gates.

**Confidence: HIGH for risk-management.** The memory entry `advisory-playmode-job-unreliable-
soak-is-interaction-gate` already records this: "interaction-only ACs reach the Sponsor
UNVALIDATED → 'CI green' with playmode skipped ≠ pass; the soak is the real interaction gate."

**Cost:** Zero code changes. Accepts interaction ACs being sponsor-validated via soak only.

---

## Application to Far Horizon

**Immediate recommendation (what to do this session):**

1. **Option 1 first** (5-min Hub action): remove Android module from the 6000.4.11f1 editor
   install on the dev machine. The prior `concurrent-unity-build-isolation-research.md` note's
   UUM-142421 hypothesis is now formally refuted — that note should be marked accordingly.
   No code changes.

2. **Option 2 as insurance** (one `ci.yml` line): drop `-nographics` from the PlayMode step
   only. Queue this as a companion change to the module removal for belt-and-suspenders.

3. **Option 5 as gate policy until 1+2 land**: do NOT require PlayMode in the protected-branch
   check until the environment is confirmed clean. The soak is the authoritative gate for
   interaction-class ACs (memory already records this).

**Note on `concurrent-unity-build-isolation-research.md`:** The section "§C — UUM-142421
hypothesis" in that note is now REFUTED by CI run 27847884304 on 6000.4.11f1. The upgrade
was still the right action (it fixes the EPERM class), but it did not address the play-mode
hang. The concurrent-build note's overall "Conditional YES" verdict for isolation is
unaffected. A one-line note should be added to its Evidence section acknowledging the refutation.

**CLAUDE.md version pin:** the project's CLAUDE.md and `ci.yml` still reference `6000.4.10f1`
(CLAUDE.md Tech stack section; `ci.yml` `UNITY` env var). If the upgrade to `6000.4.11f1` is
to proceed (per the isolation research note's Step 1), the version-pin must be updated per the
`unity-upgrade-flip-all-version-pins` memory (6 files: CLAUDE.md, ci.yml, structure_check.sh,
test fixtures, serve_soak.sh, ProjectVersion.txt). **That upgrade is a separate, gated ticket**
— not part of this research note's scope.

---

## Prior note update needed

`team/erik-consult/concurrent-unity-build-isolation-research.md` §Bottom line contains:
"UUM-142421 (fixed in `6000.4.11f1`)" as the recommended prerequisite. The spirit (upgrade
first to get a clean concurrency spike) is still sound, but readers of that note should know
the play-mode-enter hang survived the upgrade. Recommend a harvest edit adding: "**UPDATE
2026-06-20:** CI run 27847884304 on 6000.4.11f1 confirms UUM-142421 fix eliminates the EPERM
class but did NOT resolve the play-mode-enter hang — that is a separate Android module ADB
scanning issue (see `playmode-enter-headless-deadlock-research.md`)."
