using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the BUILD MENU (ticket 86catpvpa) — the sibling of
    /// <see cref="SettingsVerifyCapture"/> for the C build menu (both are UI-Toolkit modal panels).
    ///
    /// The testing bar's shipped-build capture gate (unity-conventions.md §editor-vs-runtime) requires
    /// proving the UX-visible menu OPENS and behaves in the BUILT exe, not just the editor (a UIDocument-
    /// render surface needs built-exe evidence — editor is necessary, never sufficient). A real C keystroke
    /// + row click can't be injected into a scripted/windowed build, so this drives the menu programmatically
    /// through the SAME seams the click path uses (<see cref="BuildMenuUI.Open"/> /
    /// <see cref="BuildMenuUI.SelectPlaceable"/>).
    ///
    /// CAPTURES the following frames (then quits):
    ///   buildmenu_closed.png  — the gameplay frame BEFORE opening (world, menu hidden).
    ///   buildmenu_open.png    — the menu OPEN at spawn (empty pack → rows greyed / unaffordable, cost shown).
    ///   buildmenu_placing.png — AFTER granting the table's materials + selecting its row: the menu is CLOSED
    ///                           and the ① free-cursor ghost placement is ACTIVE (the translucent table ghost).
    ///
    /// and LOGS the ground-truth proofs the gate greps (verify_buildmenu_gate.sh):
    ///   rows=N ... menuOpen=True gated=True         — the menu lists the placeables + is modal (constraint 4:
    ///                                                 while open, UiInputGate swallows world input — no click leak).
    ///   unaffordableNonInteractive=True             — selecting a greyed (unaffordable) row is REFUSED: no menu
    ///                                                 close, no placement entered (the AC "non-interactive").
    ///   selectedEnteredGhost=True                   — after granting the table's mats, selecting its (now
    ///                                                 Buildable) row CLOSES the menu and ENTERS the ① ghost flow
    ///                                                 (the regression guard: a select MUST enter the ghost flow).
    ///
    /// Inert unless launched with -verifyBuildMenu (so the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyBuildMenu -captureDir &lt;dir&gt;
    /// Windowed (NOT -batchmode — ScreenCapture needs a real swapchain for a UI-Toolkit overlay; the overlay
    /// composites to the backbuffer, not a camera RenderTexture — unity-conventions.md §Headless).
    ///
    /// The inventory grant is RUNTIME-ONLY (Model.AddItem — resets each launch), so it never pollutes a soak
    /// (a fresh launch starts from an empty pack); no PlayerPrefs snapshot/restore is needed (unlike settings).
    /// </summary>
    public class BuildMenuVerifyCapture : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        public BuildMenuUI menu;
        public CraftingTablePlacement tablePlacement;
        public Inventory inventory;

        public string subDir = "Captures";

        void Start()
        {
            if (HasArg("-verifyBuildMenu")) StartCoroutine(Verify());
        }

        private IEnumerator Verify()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // Let the scene + the menu's OnEnable register rows / build the view.
            for (int i = 0; i < 10; i++) yield return null;

            if (menu == null) menu = Object.FindAnyObjectByType<BuildMenuUI>();
            if (tablePlacement == null) tablePlacement = Object.FindAnyObjectByType<CraftingTablePlacement>();
            if (inventory == null) inventory = Object.FindAnyObjectByType<Inventory>();
            if (menu == null || tablePlacement == null || inventory == null)
            {
                Debug.LogError("[BuildMenuVerifyCapture] missing wiring (menu/tablePlacement/inventory) — the " +
                               "build menu did not ship (component-not-serialized trap). Capture aborted.");
                Application.Quit();
                yield break;
            }

            // 1. CLOSED — the world before opening.
            menu.Close();
            for (int i = 0; i < 5; i++) yield return null;
            ShotTo(Path.Combine(dir, "buildmenu_closed.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 2. OPEN — the menu lists the placeables; at spawn the pack is empty so rows read greyed /
            //    unaffordable (cost shown). Constraint 4: while OPEN the menu is MODAL, so UiInputGate must
            //    swallow world input (a menu click can never leak to a world verb).
            menu.Open();
            for (int i = 0; i < 8; i++) yield return null;
            int rows = menu.Placeables.Count;
            BuildRowState tableState = menu.RowStateOf(tablePlacement);
            bool gated = UiInputGate.CaptureWorldInput;
            Debug.Log($"[BuildMenuVerifyCapture] rows={rows} tableRow={tableState} menuOpen={menu.IsOpen} " +
                      $"gated={gated} (rows must be > 0; menuOpen=True; gated=True — the modal menu swallows " +
                      $"world input so a click never leaks to a world verb, constraint 4).");
            ShotTo(Path.Combine(dir, "buildmenu_open.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // 2b. UNAFFORDABLE NON-INTERACTIVE — selecting a greyed row must be REFUSED (no menu close, no
            //     placement entered). At the empty spawn pack the table row is unaffordable.
            bool selectEmpty = menu.SelectPlaceable(tablePlacement);
            bool placingAfterEmpty = tablePlacement.IsPlacing;
            bool unaffordableNonInteractive = !selectEmpty && !placingAfterEmpty;
            Debug.Log($"[BuildMenuVerifyCapture] unaffordable NON-interactive: selectEmpty={selectEmpty} " +
                      $"placingAfterEmpty={placingAfterEmpty} unaffordableNonInteractive={unaffordableNonInteractive} " +
                      $"(a greyed/unaffordable row must NOT enter placement — the AC 'non-interactive').");
            if (!menu.IsOpen) menu.Open(); // defensive — a refused select must have LEFT the menu open

            // 3. GRANT the table's materials → the row becomes Buildable → selecting it CLOSES the menu and
            //    ENTERS the ① free-cursor ghost placement (the REGRESSION GUARD). Runtime grant only (resets
            //    each launch — no soak pollution).
            GrantResource(ItemCatalog.WoodId, tablePlacement.BuildWoodCost);
            GrantResource(ItemCatalog.StoneId, tablePlacement.BuildStoneCost);
            for (int i = 0; i < 4; i++) yield return null; // let the inventory Changed event refresh the row
            BuildRowState tableStateAfterGrant = menu.RowStateOf(tablePlacement);
            bool selected = menu.SelectPlaceable(tablePlacement);
            bool placing = tablePlacement.IsPlacing;
            bool menuClosed = !menu.IsOpen;
            bool selectedEnteredGhost = selected && placing;
            Debug.Log($"[BuildMenuVerifyCapture] select entered ghost: tableRowAfterGrant={tableStateAfterGrant} " +
                      $"selected={selected} placing={placing} menuClosed={menuClosed} " +
                      $"selectedEnteredGhost={selectedEnteredGhost} (after granting mats the row is Buildable; " +
                      $"selecting it MUST close the menu AND enter the ① ghost flow — the regression guard).");
            for (int i = 0; i < 8; i++) yield return null; // let the ghost show + settle
            ShotTo(Path.Combine(dir, "buildmenu_placing.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            bool pass = rows > 0 && tableState == BuildRowState.Unaffordable &&
                        unaffordableNonInteractive && selectedEnteredGhost;
            Debug.Log($"[BuildMenuVerifyCapture] verification complete -> {dir} rows={rows} " +
                      $"unaffordableNonInteractive={unaffordableNonInteractive} " +
                      $"selectedEnteredGhost={selectedEnteredGhost} => PASS={pass}");
            Application.Quit();
        }

        private void GrantResource(string id, int amount)
        {
            if (inventory == null || inventory.Model == null || inventory.Catalog == null || amount <= 0) return;
            var def = inventory.Catalog.ById(id);
            if (def != null) inventory.Model.AddItem(def, amount);
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[BuildMenuVerifyCapture] wrote " + file);
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
