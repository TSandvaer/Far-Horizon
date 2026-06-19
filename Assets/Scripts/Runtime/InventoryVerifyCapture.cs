using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the #90 ITEM-ICON CENTERING fix (ticket 86caa4bya).
    ///
    /// The testing bar's shipped-build gate (unity-conventions.md §editor-vs-runtime) requires proving a
    /// UX-visible fix in the BUILT exe, not the editor — and a USS layout fix is the textbook editor-vs-
    /// runtime case (resolvedStyle + UI Toolkit panel layout can diverge from editor preview). The #90 soak
    /// caught the wood-bundle icon overflowing UP-AND-LEFT past its slot's top edge; the fix pins icon+chip
    /// position:absolute (so an empty in-flow chip Label no longer reserves line-box height that pushed the
    /// column-flow stack taller than the 64px cell). This probe drives the SAME model seam as InventoryDiag
    /// (axe to belt, wood into the pack), opens the pack, and captures a frame of the laid-out grid — so the
    /// centering is judged BY EYE from the shipped player, with the HUD build stamp visible.
    ///
    /// Inert unless launched with -verifyInvIcons (so the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyInvIcons -captureDir &lt;dir&gt;
    /// Captures inv_icons_open.png (pack open, wood in slot 0, axe in belt slot 0), then quits.
    /// NO mutable statics (StaticStateResetTests audit stays green) — instance + coroutine state only.
    /// </summary>
    public class InventoryVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";

        void Start()
        {
            if (HasArg("-verifyInvIcons")) StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // Let the scene + UI panels settle (same warm-up InventoryDiag uses).
            for (int i = 0; i < 10; i++) yield return null;

            var inv = Object.FindAnyObjectByType<Inventory>();
            var ui = Object.FindAnyObjectByType<InventoryUI>();
            if (inv == null || ui == null)
            {
                Debug.Log("[inv-icons] FATAL: inv=" + (inv != null) + " ui=" + (ui != null));
                Application.Quit(2);
                yield break;
            }

            // Drive the model the same way gameplay does: axe to belt, wood into the pack (so BOTH a belt
            // icon and a pack-grid icon are on screen — the fix must center both).
            inv.PickUpAxe();
            inv.AddWood(3);
            yield return null;

            // Open the pack so the grid is laid out + painted, then let layout settle before the shot.
            ui.SetOpen(true);
            for (int i = 0; i < 8; i++) yield return null;

            // Log the resolved icon rect vs its slot rect so the centering is auditable in the log too —
            // a contained icon's worldBound sits INSIDE the slot's worldBound (no top-edge overflow).
            var root = ui.GetComponent<UIDocument>()?.rootVisualElement;
            var grid = root?.Q<VisualElement>("inv-grid");
            if (grid != null)
            {
                foreach (var cell in grid.Children())
                {
                    var icon = cell.Q<VisualElement>("icon");
                    if (icon != null && icon.resolvedStyle.backgroundImage.sprite != null)
                    {
                        Rect sb = cell.worldBound, ib = icon.worldBound;
                        bool contained = ib.xMin >= sb.xMin - 0.5f && ib.yMin >= sb.yMin - 0.5f &&
                                         ib.xMax <= sb.xMax + 0.5f && ib.yMax <= sb.yMax + 0.5f;
                        Debug.Log("[inv-icons] grid icon slot=" + sb + " icon=" + ib +
                                  " contained=" + contained);
                        break;
                    }
                }
            }

            for (int i = 0; i < 4; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "inv_icons_open.png"), 1);
            Debug.Log("[inv-icons] wrote inv_icons_open.png -> " + dir);

            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.5f);
            Debug.Log("[inv-icons] DONE");
            Application.Quit(0);
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
