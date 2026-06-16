using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Forces the standalone player to open in WINDOWED mode at a reasonable default window size on a normal
    /// launch (BIG ROUND ISLAND N3, 86ca9a7qn — Sponsor: "launch in WINDOWED mode, NOT borderless-fullscreen").
    ///
    /// HISTORY: this component originally forced BORDERLESS FULLSCREEN (ticket 86ca8ce6y SOAKFIX8 — the Sponsor
    /// then wanted the exe to fill his widescreen). The Sponsor has now REVERSED that preference — he wants a
    /// normal WINDOW. So the runtime force flips to Windowed (the class name + serialization site stay so the
    /// scene-presence guard + the capture-gate-safety contract are unchanged).
    ///
    /// WHY a runtime force and not just the Player Setting: Unity standalone PERSISTS the last-used resolution +
    /// screen mode to the per-user registry (HKCU\Software\&lt;company&gt;\&lt;product&gt;\Screenmanager *). The
    /// borderless-fullscreen build SOAKFIX8 shipped wrote a FULLSCREEN mode into the Sponsor's registry; a plain
    /// double-click of a later build reads that PERSISTED fullscreen FIRST, overriding the new windowed Player
    /// Setting. So the Player Setting alone can't win against the stale persisted state — this component
    /// re-asserts Windowed at boot, which beats the persisted registry value. (ProjectSettings is ALSO flipped
    /// to fullscreenMode=Windowed for a clean-registry first launch.)
    ///
    /// CAPTURE-GATE SAFETY (the hard constraint): the QA capture path launches with -screen-fullscreen 0 and
    /// drives -captureGate / -verify* / -shot. This component is INERT whenever ANY of those flags is present,
    /// so it NEVER fights a windowed capture launch — the capture gate keeps producing 1280×720 windowed
    /// frames at its own size. It only acts on a normal Sponsor launch (no capture/verify flags), exactly the
    /// double-click case.
    ///
    /// Serialized onto the Boot object editor-time (sibling of the capture components) per the editor-vs-
    /// runtime serialization trap — a runtime-added component could ship absent.
    /// </summary>
    public class FullscreenBoot : MonoBehaviour
    {
        // Reasonable default windowed size (16:9). Large enough to play comfortably, small enough to read as a
        // window (not maximised). The Sponsor can resize/maximise from the OS window chrome as he likes.
        public const int WindowedWidth = 1600;
        public const int WindowedHeight = 900;
        // The capture/verify CLI flags that mean "do NOT force fullscreen" — every flag that launches the exe
        // for a windowed capture/verification run. If any is present, this component stays inert so the
        // -screen-fullscreen 0 windowed launch is untouched (the QA capture path must keep producing windowed
        // frames — verified by serve_soak / capture_gate after this change).
        private static readonly string[] CaptureFlags =
        {
            "-captureGate", "-shot",
            "-verifyMove", "-verifyCraft", "-verifyChop", "-verifyLoop",
            "-verifyAxe", "-verifyAxeFacings", "-verifyCastaway", "-verifyHair", "-verifySea", "-verifyRock",
            // Newer capture/verify launches (must also stay inert so their windowed capture size is preserved).
            "-verifyIsland", "-verifyWorldLook", "-verifyWalkGround", "-verifyFloatDiag",
            // Belt-and-suspenders: if the launch explicitly asked for windowed, honour it.
            "-screen-fullscreen",
        };

        void Start()
        {
            if (!ShouldForceFullscreen(System.Environment.GetCommandLineArgs()))
            {
                // A capture/verify launch (windowed) — leave the screen exactly as the CLI set it.
                Debug.Log("[FullscreenBoot] capture/verify launch detected — NOT forcing window mode (windowed capture size preserved)");
                return;
            }

            // Normal play (BIG ROUND ISLAND N3): force WINDOWED at a reasonable default size. This beats any
            // stale persisted Screenmanager registry value (e.g. the fullscreen the prior SOAKFIX8 build wrote)
            // so the Sponsor's double-click opens a normal window, not borderless fullscreen.
            Screen.SetResolution(WindowedWidth, WindowedHeight, FullScreenMode.Windowed);
            Debug.Log($"[FullscreenBoot] forced WINDOWED at {WindowedWidth}x{WindowedHeight} (FullScreenMode.Windowed) — " +
                      "overrides any persisted fullscreen PlayerPrefs (N3 Sponsor request)");
        }

        /// <summary>
        /// True when the launch is a NORMAL play launch (force the windowed mode); false when any capture/verify
        /// flag is present (stay inert so QA's windowed -screen-fullscreen 0 capture size is never overridden).
        /// Pure + static so the capture-gate-safety contract is regression-guarded without spawning a real
        /// player (FullscreenBootPlayModeTests). This is the load-bearing safety property: a regression that
        /// made FullscreenBoot fire under -captureGate would override every windowed QA capture's size.
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
