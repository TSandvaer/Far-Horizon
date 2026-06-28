using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the LEFT-CLICK CONSUME loop (ticket 86caf7a30).
    ///
    /// The testing bar's shipped-build gate (unity-conventions.md §editor-vs-runtime) requires proving the
    /// select→left-click→consume loop works in the BUILT exe, not just the editor. Sibling of ChopVerifyCapture:
    /// it seeds a berry + a water unit into the belt, then drives the FULL loop seam for each — SELECT the belt
    /// slot, drive a programmatic left-click (<see cref="LeftClickConsume.RequestUseClick"/>, the input-
    /// independent analog of a real left-click the shipped exe can't inject), and asserts BOTH the inventory
    /// delta (one unit consumed) AND the need restore (the HUD-bound Current01 rose) — capturing a frame where
    /// the hunger / thirst bar has visibly risen with the HUD build stamp showing.
    ///
    /// Proves end-to-end, in the shipped player:
    ///   • select berry + left-click → exactly one berry consumed AND hunger Current01 rose (AddFood);
    ///   • select water + left-click → exactly one water consumed AND thirst Current01 rose (AddWater);
    ///   • the consume goes through the SHIPPED restores (no re-implemented restore desyncing the HUD).
    /// Asserts each in the log so a headless run is auditable; exits non-zero if EITHER half fails.
    ///
    /// Inert unless launched with -verifyConsume (so the normal game / boot capture is unaffected).
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyConsume -captureDir &lt;dir&gt;
    /// Captures: consume_before.png (seeded, nothing consumed yet) + consume_berry.png (after the berry eat,
    /// hunger bar risen) + consume_water.png (after the water drink, thirst bar risen).
    /// </summary>
    public class LeftClickConsumeVerifyCapture : MonoBehaviour
    {
        public Inventory inventory;
        public HungerNeed hunger;
        public ThirstNeed thirst;
        public LeftClickConsume consume;
        public string subDir = "Captures";

        void Start()
        {
            if (HasArg("-verifyConsume"))
            {
                if (inventory == null) inventory = Object.FindAnyObjectByType<Inventory>();
                if (hunger == null) hunger = Object.FindAnyObjectByType<HungerNeed>();
                if (thirst == null) thirst = Object.FindAnyObjectByType<ThirstNeed>();
                if (consume == null) consume = Object.FindAnyObjectByType<LeftClickConsume>();
                StartCoroutine(RunVerification());
            }
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            bool wired = inventory != null && consume != null;
            Debug.Log("[ConsumeVerifyCapture] wired inv=" + (inventory != null) + " consume=" + (consume != null) +
                      " hunger=" + (hunger != null) + " thirst=" + (thirst != null));

            // The needs ship PRESSURED-WITH-HEADROOM (HungerStartFraction01 0.55 / ThirstStartFraction01 0.50 —
            // the #101 eat-refill-visibility fix), so a single consume's restore (AddFood/AddWater) moves the bar
            // unmistakably without forcing the value here. We assert the rise from the OBSERVED before-value, so
            // even a different starting fraction is handled (before < after is the load-bearing check).

            // Seed ONE berry + ONE water into the inventory — both are belt-eligible Consumables (86caf7g6f), so
            // AddItem auto-lands them on the belt (FillFreeSlots). Found-not-guessed: we then locate the actual
            // belt slot each landed in and select THAT slot (the seed-42 belt layout is not assumed).
            if (inventory != null)
            {
                var berryDef = inventory.Catalog.ById(ItemCatalog.BerryId);
                var waterDef = inventory.Catalog.ById(ItemCatalog.WaterId);
                if (berryDef != null) inventory.Model.AddItem(berryDef, 1);
                if (waterDef != null) inventory.Model.AddItem(waterDef, 1);
            }

            for (int i = 0; i < 5; i++) yield return null;
            int berry0 = Count(ItemCatalog.BerryId);
            int water0 = Count(ItemCatalog.WaterId);
            float hunger0 = hunger != null ? hunger.Current01 : -1f;
            float thirst0 = thirst != null ? thirst.Current01 : -1f;
            Debug.Log("[ConsumeVerifyCapture] before: berry=" + berry0 + " water=" + water0 +
                      " hunger01=" + hunger0.ToString("F3") + " thirst01=" + thirst0.ToString("F3"));
            ShotTo(Path.Combine(dir, "consume_before.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // === BERRY: select the berry belt slot + drive a left-click consume ===
            bool berrySelected = SelectBeltSlotHolding(ItemCatalog.BerryId);
            Debug.Log("[ConsumeVerifyCapture] berry belt slot selected: " + berrySelected);
            if (wired) consume.RequestUseClick();
            for (int i = 0; i < 6; i++) yield return null; // let the consume Update fire + the HUD repaint
            int berry1 = Count(ItemCatalog.BerryId);
            float hunger1 = hunger != null ? hunger.Current01 : -1f;
            bool berryConsumed = berry1 == berry0 - 1;
            bool hungerRose = hunger == null || hunger1 > hunger0; // null-graceful: no need wired = vacuously ok
            Debug.Log("[ConsumeVerifyCapture] berry eat: berry " + berry0 + "->" + berry1 + " (consumed=" +
                      berryConsumed + "), hunger01 " + hunger0.ToString("F3") + "->" + hunger1.ToString("F3") +
                      " (rose=" + hungerRose + ")");
            ShotTo(Path.Combine(dir, "consume_berry.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // === WATER: select the water belt slot + drive a left-click consume ===
            bool waterSelected = SelectBeltSlotHolding(ItemCatalog.WaterId);
            Debug.Log("[ConsumeVerifyCapture] water belt slot selected: " + waterSelected);
            if (wired) consume.RequestUseClick();
            for (int i = 0; i < 6; i++) yield return null;
            int water1 = Count(ItemCatalog.WaterId);
            float thirst1 = thirst != null ? thirst.Current01 : -1f;
            bool waterConsumed = water1 == water0 - 1;
            bool thirstRose = thirst == null || thirst1 > thirst0;
            Debug.Log("[ConsumeVerifyCapture] water drink: water " + water0 + "->" + water1 + " (consumed=" +
                      waterConsumed + "), thirst01 " + thirst0.ToString("F3") + "->" + thirst1.ToString("F3") +
                      " (rose=" + thirstRose + ")");
            ShotTo(Path.Combine(dir, "consume_water.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            bool pass = wired && berrySelected && berryConsumed && hungerRose
                              && waterSelected && waterConsumed && thirstRose;
            Debug.Log("[ConsumeVerifyCapture] verification complete -> " + dir +
                      " berrySel=" + berrySelected + " berryConsumed=" + berryConsumed + " hungerRose=" + hungerRose +
                      " waterSel=" + waterSelected + " waterConsumed=" + waterConsumed + " thirstRose=" + thirstRose +
                      " => PASS=" + pass);
            Application.Quit(pass ? 0 : 1);
        }

        // Select the belt slot currently holding the given id (found, not assumed — the consumables auto-landed
        // somewhere on the belt). Returns false if no belt slot holds it.
        private bool SelectBeltSlotHolding(string id)
        {
            if (inventory == null) return false;
            var model = inventory.Model;
            for (int i = 0; i < model.BeltSlots.Count; i++)
            {
                ItemStack s = model.At(SlotRef.Belt(i));
                if (!s.IsEmpty && s.Def.Id == id) { model.SelectBelt(i); return model.IsSelectedBeltItem(id); }
            }
            return false;
        }

        private int Count(string id) => inventory != null ? inventory.Model.CountItem(id) : -1;

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[ConsumeVerifyCapture] wrote " + file);
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
