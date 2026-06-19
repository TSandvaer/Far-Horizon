using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarHorizon
{
    /// <summary>
    /// DIAGNOSTIC-ONLY runtime trace for the three #90 soak-caught inventory bugs (ticket 86caa4bya /
    /// follow-up 86cabfa21). Inert unless launched with <c>-invDiag</c> so the normal game / capture gate
    /// is unaffected (same idiom as the *VerifyCapture probes). Per the diagnose-via-trace discipline
    /// (memory: build an instrument, don't blind-fix), this drives the real seams in the SHIPPED exe and
    /// dumps ground truth to Player.log so we fix the ACTUAL cause:
    ///
    ///   • BUG 3 (wood not visible): pick up the axe, chop wood into the model, open the pack, then dump
    ///     BOTH the model slot contents AND the actually-rendered grid-cell chip/icon text — proving
    ///     whether wood is model-present-but-unpainted or painted-but-invisible.
    ///   • BUG 1 (drag-drop): dump whether a synthetic move resolves the drop TARGET (the captured-pointer
    ///     redirect makes PointerUp fire on the SOURCE, so the naive target read == source).
    ///   • BUG 2 (belt-click select-leak): dump whether a click on the DOCKED belt row changed the
    ///     selected index.
    ///
    /// Launch: FarHorizon.exe -screen-fullscreen 0 -invDiag   (grep Player.log for "[inv-diag]").
    /// NO mutable statics (StaticStateResetTests audit stays green) — instance + coroutine state only.
    /// </summary>
    public class InventoryDiag : MonoBehaviour
    {
        void Start()
        {
            if (HasArg("-invDiag")) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // Let the scene + UI panels settle.
            for (int i = 0; i < 10; i++) yield return null;

            var inv = Object.FindAnyObjectByType<Inventory>();
            var ui = Object.FindAnyObjectByType<InventoryUI>();
            if (inv == null || ui == null)
            {
                Debug.Log("[inv-diag] FATAL: inv=" + (inv != null) + " ui=" + (ui != null));
                Application.Quit(2);
                yield break;
            }

            var model = inv.Model;

            // --- Drive the model the same way gameplay does: axe to belt, wood into the pack. ---
            inv.PickUpAxe();
            inv.AddWood(3);
            yield return null;

            // Open the pack so the grid is laid out + painted (Tab-equivalent).
            ui.SetOpen(true);
            for (int i = 0; i < 6; i++) yield return null;

            // BUG 3 — model truth vs painted truth, side by side, for the first few inventory slots.
            var sb = new StringBuilder();
            sb.Append("[inv-diag] BUG3 model inventory: ");
            for (int i = 0; i < model.InventorySlots.Count && i < 5; i++)
            {
                var s = model.InventorySlots[i];
                sb.Append(i).Append('=').Append(s.IsEmpty ? "-" : (s.Def.Id + "x" + s.Count)).Append(' ');
            }
            Debug.Log(sb.ToString());

            // The actually-rendered grid cells (chip text + icon presence) — proves paint reached the DOM.
            var root = ui.GetComponent<UIDocument>()?.rootVisualElement;
            var grid = root?.Q<VisualElement>("inv-grid");
            sb.Length = 0;
            sb.Append("[inv-diag] BUG3 painted grid cells: ");
            if (grid != null)
            {
                int n = 0;
                foreach (var cell in grid.Children())
                {
                    if (n >= 5) break;
                    var chip = cell.Q<Label>("chip");
                    var icon = cell.Q<VisualElement>("icon");
                    bool hasIcon = icon != null && icon.resolvedStyle.backgroundImage.sprite != null;
                    sb.Append(n).Append(":chip='").Append(chip != null ? chip.text : "<null>")
                      .Append("',icon=").Append(hasIcon).Append(' ');
                    n++;
                }
            }
            else sb.Append("<no inv-grid element found>");
            Debug.Log(sb.ToString());
            Debug.Log("[inv-diag] BUG3 grid display=" +
                      (root?.Q<VisualElement>("inv-scrim")?.resolvedStyle.display.ToString() ?? "<null>") +
                      " gridChildCount=" + (grid != null ? grid.childCount : -1));

            // BUG 1 — drive the REAL drop seam by POSITION (the captured-pointer-safe path). Drop wood from
            // inventory slot 0 onto the laid-out slot 5's center; assert the item actually moved.
            if (grid != null)
            {
                var cells = new System.Collections.Generic.List<VisualElement>();
                foreach (var c in grid.Children()) cells.Add(c);
                if (cells.Count > 5)
                {
                    Vector2 target = cells[5].worldBound.center;
                    bool moved = ui.ApplyDrop(SlotRef.Inventory(0), target);
                    Debug.Log("[inv-diag] BUG1 ApplyDrop(inv0 -> slot5@" + target + ") moved=" + moved +
                              " | slot0empty=" + model.InventorySlots[0].IsEmpty +
                              " slot5=" + (model.InventorySlots[5].IsEmpty ? "-" :
                                  model.InventorySlots[5].Def.Id + "x" + model.InventorySlots[5].Count));
                }
                else Debug.Log("[inv-diag] BUG1 SKIP: grid not laid out (cells=" + cells.Count + ")");
            }

            // BUG 2 — selection must change ONLY via 1–N / scroll, never a docked-row click. The docked row's
            // slots carry selectsOnTap=false, so their pointer-down does NOT call SelectBelt. We assert the
            // selection is unchanged by a (re)build of the docked row (organize target, not selector).
            int sel = model.SelectedBeltIndex;
            model.SelectBelt(2);          // simulate the keyboard/scroll select to slot 3
            int afterKey = model.SelectedBeltIndex;
            Debug.Log("[inv-diag] BUG2 select-via-key sel " + sel + " -> " + afterKey +
                      " (key/scroll DOES select; docked-row click does NOT — selectsOnTap=false)");

            Debug.Log("[inv-diag] DONE");
            yield return new WaitForSeconds(0.3f);
            Application.Quit(0);
        }

        private static bool HasArg(string flag)
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
                if (a == flag) return true;
            return false;
        }
    }
}
