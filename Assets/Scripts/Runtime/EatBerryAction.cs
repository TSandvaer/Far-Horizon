using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The in-game EAT input (ticket 86caa5zz3 — #101 soak-fix: "I can't eat berries"). The code eat-seam
    /// already existed and is tested END TO END (<see cref="BerryBush.EatBerry"/> →
    /// <see cref="HungerNeed.TryEatBerry"/> → atomic consume one berry + restore hunger), but NOTHING in the
    /// build invoked it — there was no player INPUT bound to eating. This component is that missing call-site:
    /// pressing the eat key consumes ONE berry from the inventory and restores hunger, all through the SAME
    /// atomic seam the tests cover (so the all-or-nothing guarantee is preserved — a berry is never consumed
    /// without the restore, nor restored without the consume).
    ///
    /// === Why a hotkey, NOT right-click (diagnose-first — the ticket said "right-click OR a hotkey") ===
    /// Right-click is ALREADY bound: <see cref="OrbitCamera"/> uses RMB-held (Input.GetMouseButton(1)) for
    /// camera orbit, so binding eat to right-click would collide with the camera. The hotkey path is the
    /// collision-free choice. <see cref="eatKey"/> defaults to E — the standard "interact/use" key, free
    /// across the whole input surface (WASD/Shift/Space/Tab/1–5/LMB/RMB are all taken; the F-gated nudge tools
    /// use T/G/Y/H/U/J + arrows but not E). Eating does NOT require the inventory pack to be open or a berry to
    /// be "selected" — berries are a Resource and the model keeps Resources OFF the belt (ItemDef.IsBeltEligible
    /// is Tool-only), so there is no "selected belt berry" to right-click; eating draws from the inventory
    /// grid directly.
    ///
    /// === The seam (atomic, all-or-nothing — reuses the tested HungerNeed path) ===
    /// <see cref="TryEatOneBerry"/> runs the inventory consume delegate
    /// (<c>() =&gt; inventory.Model.RemoveItem(BerryId, 1)</c> — itself all-or-nothing: debits nothing +
    /// returns false when no berry is held) through <see cref="HungerNeed.TryEatBerry"/>, which restores hunger
    /// ONLY if the consume succeeded. If no <see cref="HungerNeed"/> is wired it consumes gracefully with no
    /// restore + no null-ref (AC5b). PUBLIC + scene-free so the PlayMode test drives it without a key device.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The component + its Inventory/HungerNeed refs are wired editor-time by BootstrapProject (serialized into
    /// Boot.unity), NOT added at Awake. The Awake FindObjectOfType calls are a build-safety net only.
    ///
    /// === Trace instrumentation (no-new-class-without-trace discipline) ===
    /// One-shot `[eat-trace]` lines on the first successful eat + the first empty-handed press so the eat
    /// input's runtime behaviour is readable from the build log (diagnose-via-trace).
    /// </summary>
    public class EatBerryAction : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The inventory eaten berries are consumed from. Wired at bootstrap; scene-found fallback.")]
        public Inventory inventory;

        [Tooltip("The hunger need a berry restores. Wired at bootstrap; scene-found fallback. May be null — " +
                 "eating then consumes the berry gracefully with no restore (AC5b, no null-ref).")]
        public HungerNeed hunger;

        [Header("Input")]
        [Tooltip("The key that eats one berry. E by default (the interact/use key — free across the input " +
                 "map; right-click is taken by the orbit camera, so a hotkey is the collision-free choice).")]
        public KeyCode eatKey = KeyCode.E;

        private bool _tracedFirstEat;
        private bool _tracedFirstEmpty;

        void Awake()
        {
            // Serialized refs are the source of truth (BootstrapProject wires them editor-time). These are a
            // build-safety net only — never the path the shipped build relies on.
            if (inventory == null) inventory = FindObjectOfType<Inventory>();
            if (hunger == null) hunger = FindObjectOfType<HungerNeed>();
        }

        void Update()
        {
            // Legacy Input.* (the project is activeInputHandler=0 — unity-conventions.md §Input; same idiom
            // InventoryUI uses for Tab / number keys). Edge-triggered: eat once per key press.
            if (Input.GetKeyDown(eatKey)) TryEatOneBerry();
        }

        /// <summary>
        /// Eat ONE berry: consume exactly one from the inventory AND restore hunger, atomically, through the
        /// tested <see cref="HungerNeed.TryEatBerry"/> seam (consume + restore are inseparable). Returns true
        /// iff a berry was eaten; a no-berry press is a clean no-op (no negative inventory, no hunger change).
        /// Public + scene-free so the PlayMode test drives the action without a key device.
        /// </summary>
        public bool TryEatOneBerry()
        {
            if (inventory == null) return false;

            // All-or-nothing consume delegate: RemoveItem debits nothing + returns false when no berry is held.
            System.Func<bool> consumeOneBerry = () => inventory.Model.RemoveItem(ItemCatalog.BerryId, 1);

            bool eaten = hunger != null
                ? hunger.TryEatBerry(consumeOneBerry)   // atomic: restore ONLY if the consume succeeded
                : consumeOneBerry();                     // AC5b graceful no-HungerNeed: consume only, no restore

            if (eaten)
            {
                if (!_tracedFirstEat)
                {
                    _tracedFirstEat = true;
                    EatTrace("ate 1 berry (key=" + eatKey + ", restore=" + (hunger != null) +
                             ") -> berries=" + inventory.Model.CountItem(ItemCatalog.BerryId) +
                             (hunger != null ? ", hunger=" + hunger.Current01.ToString("F2") : ""));
                }
            }
            else if (!_tracedFirstEmpty)
            {
                _tracedFirstEmpty = true;
                EatTrace("eat pressed with NO berries held -> no-op (key=" + eatKey + ")");
            }
            return eaten;
        }

        // [eat-trace] diagnostic logging — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call +
        // its argument evaluation (the string concat) from the shipped IL2CPP release exe so the trace costs
        // the player nothing (unity6-mastery §5/§10). The first-press guards keep it one-shot.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void EatTrace(string msg) => Debug.Log("[eat-trace] " + msg);
    }
}
