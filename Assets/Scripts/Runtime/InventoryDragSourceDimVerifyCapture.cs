using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the #90 DRAG-DUPLICATE fix (source-dim-on-drag).
    ///
    /// The Sponsor reported a "duplicate" appearing while HOLDING THE MOUSE DOWN on the berries in the
    /// inventory: the item showed in TWO places at once — the source slot kept rendering its full icon AND
    /// the #drag-ghost followed the cursor. The fix DIMS the source slot's item content (icon/chip/badge)
    /// for the duration of the drag (the .slot--dragging-source USS class), so the item reads in ONE place
    /// (the ghost). This probe drives a LIVE drag in the BUILT exe (BeginDrag on the berry slot), holds it,
    /// and captures a frame — the source slot must read EMPTY/dimmed while the ghost carries the item. The
    /// testing bar's shipped-build gate (unity-conventions.md §editor-vs-runtime) requires proving a
    /// UX-visible USS-state fix in the BUILT player, not the editor (resolvedStyle can diverge).
    ///
    /// Inert unless launched with -verifyInvDragDim (the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyInvDragDim -captureDir &lt;dir&gt;
    /// Captures inv_drag_source_dim.png (pack open, drag of the berries ACTIVE: source dimmed, ghost up).
    /// NO mutable statics (StaticStateResetTests audit stays green) — instance + coroutine state only.
    /// </summary>
    public class InventoryDragSourceDimVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";

        void Start()
        {
            if (HasArg("-verifyInvDragDim")) StartCoroutine(RunVerification());
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
                Debug.Log("[inv-drag-dim] FATAL: inv=" + (inv != null) + " ui=" + (ui != null));
                Application.Quit(2);
                yield break;
            }

            // Reproduce the Sponsor's scenario: BERRIES in the pack (the exact item he dragged). Pick up the
            // axe too so the belt is populated and the screenshot reads as a real inventory. Berries go in via
            // the model's AddItem seam (no façade berry-shim exists; this mirrors AddWood internally).
            inv.PickUpAxe();
            var berryDef = inv.Catalog.ById(ItemCatalog.BerryId);
            if (berryDef != null) inv.Model.AddItem(berryDef, 5);
            yield return null;

            ui.SetOpen(true);
            for (int i = 0; i < 8; i++) yield return null;

            // The berries land in inventory slot 0 (first pack slot). Begin a drag from there — the live
            // lifecycle seam: arms the ghost AND dims the source.
            var src = SlotRef.Inventory(0);
            ui.BeginDrag(src);
            for (int i = 0; i < 6; i++) yield return null;

            // Log the source-slot state so the dim is auditable in the log too: the icon's resolved
            // visibility must be Hidden while the drag is active (the #90 dup-fix), and the ghost up.
            var root = ui.GetComponent<UIDocument>()?.rootVisualElement;
            var grid = root?.Q<VisualElement>("inv-grid");
            var ghost = root?.Q<VisualElement>("drag-ghost");
            bool srcDimmed = ui.IsSourceDimmed(src);
            string iconVis = "n/a";
            if (grid != null)
            {
                foreach (var cell in grid.Children())
                {
                    var icon = cell.Q<VisualElement>("icon");
                    if (icon != null) { iconVis = icon.resolvedStyle.visibility.ToString(); break; }
                }
            }
            bool ghostUp = ghost != null && ghost.resolvedStyle.display == DisplayStyle.Flex;
            Debug.Log("[inv-drag-dim] dragActive srcDimmed=" + srcDimmed + " slot0IconVisibility=" + iconVis +
                      " ghostVisible=" + ghostUp);

            for (int i = 0; i < 4; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "inv_drag_source_dim.png"), 1);
            Debug.Log("[inv-drag-dim] wrote inv_drag_source_dim.png -> " + dir);

            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.5f);
            Debug.Log("[inv-drag-dim] DONE");
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
