using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Forces the standalone player to open BORDERLESS FULLSCREEN at native resolution on a normal launch
    /// (ticket 86ca8ce6y SOAKFIX8 — FIX3, "the exe opens as a small ~1280×720 window; fill the widescreen").
    ///
    /// WHY a runtime force and not just the Player Setting: ProjectSettings already ships fullscreenMode=1
    /// (FullScreenWindow) + defaultIsNativeResolution=1, yet the Sponsor still gets a small window. ROOT CAUSE
    /// — Unity standalone PERSISTS the last-used resolution + screen mode to the per-user registry
    /// (HKCU\Software\&lt;company&gt;\&lt;product&gt;\Screenmanager *). The team's capture gate (capture_gate.sh)
    /// has launched the exe WINDOWED at 1280×720 many times on the Sponsor's machine (-screen-fullscreen 0
    /// -screen-width 1280 -screen-height 720); those windowed values got written to the registry, and EVERY
    /// later plain double-click launch reads the PERSISTED values FIRST — overriding the fullscreen build
    /// default. So the Player Setting alone can't win against stale PlayerPrefs. This component re-asserts
    /// borderless-native at boot, which beats the persisted state.
    ///
    /// CAPTURE-GATE SAFETY (the hard constraint): the QA capture path launches with -screen-fullscreen 0 and
    /// drives -captureGate / -verify* / -shot. This component is INERT whenever ANY of those flags is present,
    /// so it NEVER fights a windowed capture launch — the capture gate keeps producing 1280×720 windowed
    /// frames. It only acts on a normal Sponsor launch (no capture/verify flags), exactly the double-click case.
    ///
    /// Serialized onto the Boot object editor-time (sibling of the capture components) per the editor-vs-
    /// runtime serialization trap — a runtime-added component could ship absent.
    /// </summary>
    public class FullscreenBoot : MonoBehaviour
    {
        // The capture/verify CLI flags that mean "do NOT force fullscreen" — every flag that launches the exe
        // for a windowed capture/verification run. If any is present, this component stays inert so the
        // -screen-fullscreen 0 windowed launch is untouched (the QA capture path must keep producing windowed
        // frames — verified by serve_soak / capture_gate after this change).
        private static readonly string[] CaptureFlags =
        {
            "-captureGate", "-shot",
            "-verifyMove", "-verifyCraft", "-verifyChop", "-verifyLoop",
            "-verifyAxe", "-verifyAxeFacings", "-verifyCastaway", "-verifyHair", "-verifySea", "-verifyRock",
            // Belt-and-suspenders: if the launch explicitly asked for windowed, honour it.
            "-screen-fullscreen",
        };

        void Start()
        {
            if (!ShouldForceFullscreen(System.Environment.GetCommandLineArgs()))
            {
                // A capture/verify launch (windowed) — leave the screen exactly as the CLI set it.
                Debug.Log("[FullscreenBoot] capture/verify launch detected — NOT forcing fullscreen (windowed capture preserved)");
                return;
            }

            // Normal play: force borderless fullscreen at the primary display's NATIVE resolution. This beats
            // any stale persisted Screenmanager registry values (the small-window root cause) and fills the
            // Sponsor's widescreen.
            var native = Screen.currentResolution;
            int w = Mathf.Max(native.width, 1);
            int h = Mathf.Max(native.height, 1);
            Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
            Debug.Log($"[FullscreenBoot] forced borderless fullscreen at native {w}x{h} (FullScreenWindow) — " +
                      "overrides any persisted windowed PlayerPrefs");
        }

        /// <summary>
        /// True when the launch is a NORMAL play launch (force fullscreen); false when any capture/verify flag
        /// is present (stay inert so QA's windowed -screen-fullscreen 0 capture is never overridden). Pure +
        /// static so the capture-gate-safety contract is regression-guarded without spawning a real player
        /// (FullscreenBootPlayModeTests). This is the load-bearing safety property: a regression that made
        /// FullscreenBoot fire under -captureGate would break every windowed QA capture.
        /// </summary>
        public static bool ShouldForceFullscreen(string[] args)
        {
            if (args == null) return true;
            foreach (string a in args)
                foreach (string flag in CaptureFlags)
                    if (a == flag) return false;
            return true;
        }
    }
}
