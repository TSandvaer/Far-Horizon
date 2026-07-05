using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the DRAG-GHOST MISPOSITION fix (86caffw9h — the recurring
    /// "the dragged bag item suddenly displays in a different position while I drag it"; recurrence 86cajrtr1).
    ///
    /// ROOT CAUSE the fix addresses: <see cref="InventoryUI.PositionGhostAtScreenPoint"/> converts the cursor
    /// (screen px, Y-up) to panel space with FLIP-THEN-<c>RuntimePanelUtils.ScreenToPanel</c>. The ORIGINAL
    /// (#174) bug flipped Y but never applied the panel SCALE (PanelScaleMode.ScaleWithScreenSize, refRes
    /// 1920x1080), so at a non-1080p window (panel scale != 1) the ghost rendered scale× off the cursor. That
    /// scale term is now in place; this gate proves it holds at a NON-1080p window (else it false-greens).
    ///
    /// 86cajrtr1 — WHY THE GATE READ 14.77 px OFF ON ITS FIRST REAL RUN (run 28680351313): the probe used to
    /// ride the LIVE OS cursor (Input.mousePosition) during an active drag. In an automated windowed launch
    /// there is NO controlled mouse — Input.mousePosition returned an OFF-WINDOW, frame-unstable value
    /// (cursorScreen=(-1198,1361)); the ghost was positioned from one frame's cursor and the assert compared it
    /// against a LATER frame's cursor read, so the two diverged by the inter-frame cursor jitter — NOT a
    /// scale-math error (ScreenToPanel and worldBound share ONE panel space, and the ghost's center-origin
    /// scale:1.1 provably preserves its worldBound center, so the production math is exact at ANY scale). The
    /// fix: drive a KNOWN, on-screen cursor DETERMINISTICALLY (via <see cref="InventoryUI.ShowGhostForVerification"/>,
    /// which raises the ghost WITHOUT arming a drag, so Update's per-frame OS-cursor tracking never overwrites
    /// the synthetic point). This is the SAME production seam a real drag drives (PositionGhostAtMouse ->
    /// PositionGhostAtScreenPoint), at the SAME non-1080p scale, minus the uncontrolled OS-cursor noise.
    ///
    /// What this probe does (inert unless launched with -verifyInvDragGhostPos):
    ///   FarHorizon.exe -screen-fullscreen 0 -screen-width 2560 -screen-height 1440 \
    ///                  -verifyInvDragGhostPos -captureDir &lt;dir&gt;
    ///   • opens the pack with berries (the exact item class the Sponsor dragged),
    ///   • raises the drag-ghost carrying the berry icon (no live drag — deterministic),
    ///   • ASSERTS the panel scale is genuinely != 1 (else the test is meaningless — false-green guard),
    ///   • drives SEVERAL known on-screen cursor points through the production positioning path and ASSERTS the
    ///     ghost's actual panel-space center ≈ the EXPECTED panel point for each (both via the same
    ///     ScreenToPanel) — quits NON-ZERO if any diverges, so a re-broken fix FAILS the gate,
    ///   • captures inv_drag_ghost_pos.png with the ghost sitting ON the last driven point.
    /// NO mutable statics (StaticStateResetTests audit stays green) — instance + coroutine state only.
    /// </summary>
    public class InventoryDragGhostPosVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";

        // On-screen driven cursor points (screen px, Y-UP) at 2560x1440. Spread across the window and
        // deliberately FAR from the origin (large X/Y): a dropped scale term makes err grow ~ (scale-1)*coord,
        // so far-from-origin points keep the gate sensitive to the ORIGINAL 86caffw9h scale regression.
        private static readonly Vector2[] DrivenPoints =
        {
            new Vector2(1280f, 720f),   // center
            new Vector2(2180f, 260f),   // far top-right (large X — most sensitive to a dropped scale term)
            new Vector2(360f, 1180f),   // far bottom-left
            new Vector2(1960f, 980f),   // upper-right quadrant
        };

        private const float TolPanelPx = 2f;

        void Start()
        {
            if (HasArg("-verifyInvDragGhostPos")) StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            for (int i = 0; i < 10; i++) yield return null;

            var inv = Object.FindAnyObjectByType<Inventory>();
            var ui = Object.FindAnyObjectByType<InventoryUI>();
            if (inv == null || ui == null)
            {
                Debug.Log("[inv-drag-ghost] FATAL: inv=" + (inv != null) + " ui=" + (ui != null));
                Application.Quit(2);
                yield break;
            }

            // Seed berries in the pack + the axe so the inventory reads real, then open the pack.
            inv.PickUpAxe();
            var berryDef = inv.Catalog.ById(ItemCatalog.BerryId);
            if (berryDef != null) inv.Model.AddItem(berryDef, 5);
            yield return null;

            ui.SetOpen(true);
            for (int i = 0; i < 8; i++) yield return null;

            // The window MUST be non-1080p (panel scale != 1) or this verification is meaningless. Compute the
            // effective panel scale from the round-trip: a screen-X delta of `scale` panel units == 1 screen px,
            // so 1 panel unit -> `scale` screen px. We read it off ScreenToPanel of two screen points.
            float panelScale = MeasurePanelScale(ui);
            Debug.Log("[inv-drag-ghost] screen=" + Screen.width + "x" + Screen.height +
                      " panelScale=" + panelScale.ToString("F4"));
            if (Mathf.Abs(panelScale - 1f) < 0.02f)
            {
                Debug.Log("[inv-drag-ghost] FATAL: panel scale ~= 1 (window is ~1080p) — the bug is INVISIBLE " +
                          "here; relaunch at a NON-1080p size (e.g. -screen-width 2560 -screen-height 1440).");
                Application.Quit(3);
                yield break;
            }

            // Raise the ghost WITHOUT arming a drag so Update's per-frame OS-cursor tracking (which only runs
            // while _dragging) does NOT overwrite the synthetic points we drive below. This is the 86cajrtr1
            // fix: the old probe rode the live OFF-WINDOW OS cursor and measured inter-frame jitter, not the
            // scale math.
            ui.ShowGhostForVerification(berryDef);
            for (int i = 0; i < 8; i++) yield return null;

            float maxErr = 0f;
            Vector2 worstPt = Vector2.zero;
            bool anyNull = false;
            foreach (var pt in DrivenPoints)
            {
                // Drive the KNOWN screen point through the EXACT production positioning path a real drag uses.
                ui.PositionGhostAtScreenPoint(pt);
                for (int i = 0; i < 6; i++) yield return null;   // let layout settle on the synthetic point

                Vector2? actual = ui.GhostCenterPanelPoint();
                Vector2? expected = ui.ExpectedGhostPanelCenter(pt);
                if (!actual.HasValue || !expected.HasValue)
                {
                    Debug.Log("[inv-drag-ghost] FATAL: ghost/panel not laid out at pt=" + pt +
                              " (actual=" + actual.HasValue + " expected=" + expected.HasValue + ")");
                    anyNull = true;
                    break;
                }

                float err = Vector2.Distance(actual.Value, expected.Value);
                Debug.Log("[inv-drag-ghost] drivenScreen=" + pt + " ghostPanelCenter=" + actual.Value +
                          " expectedPanelCenter=" + expected.Value + " errPanelPx=" + err.ToString("F2"));
                if (err > maxErr) { maxErr = err; worstPt = pt; }
            }

            if (anyNull)
            {
                Application.Quit(4);
                yield break;
            }

            // Every driven point's ghost center must land on that point's panel position (within sub-pixel
            // layout rounding). A scale-less (pre-#174) build would diverge by ~ (scale-1)*coord — tens-to-
            // hundreds of px at the far points.
            if (maxErr > TolPanelPx)
            {
                Debug.Log("[inv-drag-ghost] FAIL: worst ghost-off-cursor = " + maxErr.ToString("F2") +
                          " panel px at " + worstPt + " (> " + TolPanelPx + ") — drag-ghost misposition NOT fixed. " +
                          ui.GhostGeomDiag());
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "inv_drag_ghost_pos.png"), 1);
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                Application.Quit(5);
                yield break;
            }

            for (int i = 0; i < 4; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "inv_drag_ghost_pos.png"), 1);
            Debug.Log("[inv-drag-ghost] PASS: ghost tracks the cursor at all " + DrivenPoints.Length +
                      " points (worst err=" + maxErr.ToString("F2") + " panel px, scale=" +
                      panelScale.ToString("F4") + ") — wrote inv_drag_ghost_pos.png -> " + dir);

            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.5f);
            Debug.Log("[inv-drag-ghost] DONE");
            Application.Quit(0);
        }

        /// <summary>
        /// Effective panel scale (screen px per panel unit) read off the panel's own ScreenToPanel: convert two
        /// screen points 100 px apart in X and measure the panel-X delta; scale = screenDelta / panelDelta.
        /// Uses the SAME panel the ghost is on, so this is the true runtime scale at the current resolution.
        /// </summary>
        private static float MeasurePanelScale(InventoryUI ui)
        {
            Vector2? a = ui.ExpectedGhostPanelCenter(new Vector2(100f, 100f));
            Vector2? b = ui.ExpectedGhostPanelCenter(new Vector2(200f, 100f));
            if (!a.HasValue || !b.HasValue) return 1f;
            float panelDeltaX = Mathf.Abs(b.Value.x - a.Value.x);
            if (panelDeltaX < 0.0001f) return 1f;
            return 100f / panelDeltaX;
        }

        private string ResolveDir()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-captureDir") return Path.GetFullPath(args[i + 1]);
            string baseDir = Application.isEditor
                ? Path.Combine(Application.dataPath, "..", subDir)
                : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", subDir);
            return Path.GetFullPath(baseDir);
        }

        private bool HasArg(string flag)
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
                if (a == flag) return true;
            return false;
        }
    }
}
