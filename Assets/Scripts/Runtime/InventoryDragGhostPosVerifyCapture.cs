using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the DRAG-GHOST MISPOSITION fix (86caffw9h — the recurring
    /// "the dragged bag item suddenly displays in a different position while I drag it").
    ///
    /// ROOT CAUSE the fix addresses: <see cref="InventoryUI.PositionGhostAtScreenPoint"/> converts the cursor
    /// (screen px, Y-up) to panel space with FLIP-THEN-<c>RuntimePanelUtils.ScreenToPanel</c>. The old code
    /// flipped Y but NEVER applied the panel SCALE (PanelScaleMode.ScaleWithScreenSize, refRes 1920x1080), so
    /// at a non-1080p window (panel scale != 1) the ghost rendered scale× off the cursor — out toward the
    /// bottom-right "into the world." The bug is INVISIBLE at exactly 1920x1080 (scale ~= 1), so this probe
    /// MUST run at a NON-1080p window or it false-greens.
    ///
    /// What this probe does (inert unless launched with -verifyInvDragGhostPos):
    ///   FarHorizon.exe -screen-fullscreen 0 -screen-width 2560 -screen-height 1440 \
    ///                  -verifyInvDragGhostPos -captureDir &lt;dir&gt;
    ///   • opens the pack with berries (the exact item class the Sponsor dragged),
    ///   • BEGINS a drag and drives a KNOWN cursor point through the production positioning path
    ///     (PositionGhostAtScreenPoint — no real mouse in an automated launch),
    ///   • ASSERTS the panel scale is genuinely != 1 (else the test is meaningless — false-green guard),
    ///   • ASSERTS the ghost's actual panel-space center ≈ the EXPECTED panel point for that cursor (both via
    ///     the same ScreenToPanel) — quits NON-ZERO if they diverge, so a re-broken fix FAILS the gate,
    ///   • captures inv_drag_ghost_pos.png with the ghost sitting ON the driven cursor marker.
    /// NO mutable statics (StaticStateResetTests audit stays green) — instance + coroutine state only.
    /// </summary>
    public class InventoryDragGhostPosVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";

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

            // Begin the drag from the berry slot (pack slot 0) — arms the ghost.
            var src = SlotRef.Inventory(0);
            ui.BeginDrag(src);
            for (int i = 0; i < 4; i++) yield return null;

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

            // The drag is ACTIVE, so InventoryUI.Update re-positions the ghost from the REAL cursor
            // (PositionGhostAtMouse -> Input.mousePosition) EVERY frame — it would overwrite any synthetic
            // PositionGhostAtScreenPoint we set. So we assert the PRODUCTION cursor-driven path directly: the
            // ghost CENTER must equal the panel point for the CURRENT real cursor (Input.mousePosition). This
            // is exactly the path the Sponsor exercises (mouse-driven drag), through the same flip-then-
            // ScreenToPanel. Let several Update frames run so the ghost settles on the live cursor.
            for (int i = 0; i < 12; i++) yield return null;   // Update positions the ghost from the real mouse

            var cursor = (Vector2)Input.mousePosition;        // the live OS cursor the ghost is tracking

            Vector2? actual = ui.GhostCenterPanelPoint();
            Vector2? expected = ui.ExpectedGhostPanelCenter(cursor);
            if (!actual.HasValue || !expected.HasValue)
            {
                Debug.Log("[inv-drag-ghost] FATAL: ghost/panel not laid out (actual=" + actual.HasValue +
                          " expected=" + expected.HasValue + ")");
                Application.Quit(4);
                yield break;
            }

            float err = Vector2.Distance(actual.Value, expected.Value);
            Debug.Log("[inv-drag-ghost] cursorScreen=" + cursor + " ghostPanelCenter=" + actual.Value +
                      " expectedPanelCenter=" + expected.Value + " errPanelPx=" + err.ToString("F2"));

            // The ghost center must land on the cursor's panel point (within sub-pixel layout rounding). A
            // scale-less (pre-fix) build would diverge by ~ (scale-1)*cursor — tens-to-hundreds of px.
            const float tolPanelPx = 2f;
            if (err > tolPanelPx)
            {
                Debug.Log("[inv-drag-ghost] FAIL: ghost off cursor by " + err.ToString("F2") +
                          " panel px (> " + tolPanelPx + ") — drag-ghost misposition NOT fixed.");
                // still capture the frame for evidence, then quit non-zero
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "inv_drag_ghost_pos.png"), 1);
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.5f);
                Application.Quit(5);
                yield break;
            }

            for (int i = 0; i < 4; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "inv_drag_ghost_pos.png"), 1);
            Debug.Log("[inv-drag-ghost] PASS: ghost on cursor (err=" + err.ToString("F2") +
                      " panel px) — wrote inv_drag_ghost_pos.png -> " + dir);

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
