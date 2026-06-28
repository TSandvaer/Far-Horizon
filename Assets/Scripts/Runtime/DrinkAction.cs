using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// The in-game DRINK input (ticket 86caamkv7, AC3) — the player call-site for the (tested) drink seam.
    /// ⚠ 86caf7a30 REMOVES this proximity-drink-at-pond trigger (<see cref="inputEnabled"/> defaults false →
    /// Q no longer drinks): drinking is now a LEFT-CLICK on the selected WATER belt item (LeftClickConsume), part
    /// of the unified input model. The proximity seam below stays for coverage (DrinkActionPlayModeTests +
    /// FreshwaterPond), but is no longer bound to a key. The pond remains the world fresh-water SOURCE — water
    /// enters the belt via E-at-pond loot (86caf7a6q), then left-click drinks it.
    /// Pressing the drink key (when enabled) attempts ONE hand-scoop at the <see cref="FreshwaterPond"/>: it
    /// restores a small amount of thirst IF the castaway is in range of the pond, through the atomic seam the tests cover
    /// (<see cref="FreshwaterPond.DrinkScoop"/> → <see cref="ThirstNeed.TryDrinkScoop"/> → in-range gate +
    /// <see cref="ThirstNeed.AddWater"/>). Repeatable: each press is one scoop, the castaway drinks several
    /// times at the pond (the fiction: "drinks with his hand, satisfying small amount of thirst with EACH
    /// scoop"). This is the thirst sibling of <see cref="EatBerryAction"/> (the hunger eat input).
    ///
    /// === Why Q, NOT right-click (collision-free + layout-safe) ===
    /// Right-click is ALREADY bound: <see cref="OrbitCamera"/> uses RMB-held for camera orbit, so binding
    /// drink to right-click would collide. The hotkey path is collision-free. <see cref="drinkKey"/> defaults
    /// to Q — a LETTER key adjacent to WASD, free across the input surface (E is the eat key; WASD/Shift/Space/
    /// Tab/B are taken; the F-gated nudge tools use T/G/Y/H/U/J/I/O/K/L but not Q). Q is LAYOUT-AGNOSTIC: the
    /// alpha block sits at ~the same physical position on a Danish keyboard as on US (the Sponsor uses a Danish
    /// layout — unity-conventions.md §Input; NEVER bind a soak-facing control to a punctuation key, which
    /// shifts on Danish). Drinking requires being AT the pond — pressing Q anywhere else is a clean no-op.
    ///
    /// === The seam (atomic, proximity-gated — reuses the tested ThirstNeed path) ===
    /// <see cref="TryDrinkOneScoop"/> calls <see cref="FreshwaterPond.DrinkScoop"/>, which runs the in-range
    /// predicate through <see cref="ThirstNeed.TryDrinkScoop"/> and restores ONLY when in range. If no pond is
    /// wired (or no ThirstNeed) it is a graceful no-op (AC3b — no null-ref). PUBLIC + scene-free so the
    /// PlayMode test drives it without a key device.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// The component + its pond ref are wired editor-time by BootstrapProject/MovementCameraScene (serialized
    /// into Boot.unity), NOT added at Awake. The Awake FindObjectOfType call is a build-safety net only.
    ///
    /// === Trace instrumentation (no-new-class-without-trace discipline) ===
    /// One-shot `[drink-trace]` lines on the first successful scoop + the first out-of-range press so the
    /// drink input's runtime behaviour is readable from the build log (diagnose-via-trace).
    /// </summary>
    public class DrinkAction : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("The freshwater pond a scoop drinks from. Wired at bootstrap; scene-found fallback. May be " +
                 "null — drinking then is a clean no-op (no null-ref).")]
        public FreshwaterPond pond;

        [Header("Input")]
        [Tooltip("The key that historically drank one scoop (Q). 86caf7a30 REMOVES the proximity-drink-at-pond " +
                 "trigger — drinking is now a LEFT-CLICK on the selected WATER belt item (LeftClickConsume), part " +
                 "of the unified input model. With inputEnabled=false this key is NOT read, so Q no longer drinks. " +
                 "Kept as a serialized field for back-compat + in case a key path is ever re-introduced.")]
        public KeyCode drinkKey = KeyCode.Q;

        [Tooltip("86caf7a30: whether this action reads its key. FALSE by default now — the proximity-drink-at-pond " +
                 "trigger is REMOVED (drinking moves to left-click the selected water belt item via " +
                 "LeftClickConsume). A live Q binding here would be a SECOND consume path (the 'two ways to drink' " +
                 "silent regression AC4 forbids). The tested proximity seam TryDrinkOneScoop() is PRESERVED (called " +
                 "directly by DrinkActionPlayModeTests + FreshwaterPond.DrinkScoop) for coverage; only the key " +
                 "INPUT stands down. The pond stays the world fresh-water SOURCE — water enters the belt via " +
                 "E-at-pond loot (86caf7a6q), then left-click drinks it.")]
        public bool inputEnabled = false;

        private bool _tracedFirstDrink;
        private bool _tracedFirstFar;

        void Awake()
        {
            // Serialized ref is the source of truth (bootstrap wires it editor-time). This is a build-safety
            // net only — never the path the shipped build relies on.
            if (pond == null) pond = FindObjectOfType<FreshwaterPond>();
        }

        void Update()
        {
            // 86caf7a30: the proximity-drink-at-pond trigger is REMOVED — drinking is now a LEFT-CLICK on the
            // selected WATER belt item (LeftClickConsume). With inputEnabled=false this action does NOT read Q,
            // so there is no second consume path (AC4 — no dead input path that also drinks). The TryDrinkOneScoop
            // seam below stays tested + callable for DrinkActionPlayModeTests + FreshwaterPond coverage.
            if (!inputEnabled) return;

            // Legacy Input.* (the project is activeInputHandler=0 — unity-conventions.md §Input; same idiom
            // EatBerryAction uses for E). Edge-triggered: one scoop per key press.
            if (Input.GetKeyDown(drinkKey)) TryDrinkOneScoop();
        }

        /// <summary>
        /// Drink ONE scoop: restore thirst IF the castaway is at the pond, through the tested
        /// <see cref="FreshwaterPond.DrinkScoop"/> seam (proximity gate + restore are inseparable). Returns
        /// true iff a scoop was drunk; a press away from the pond (or with no pond/ThirstNeed wired) is a clean
        /// no-op. Public + scene-free so the PlayMode test drives the action without a key device.
        /// </summary>
        public bool TryDrinkOneScoop()
        {
            if (pond == null) return false;

            bool drank = pond.DrinkScoop();

            if (drank)
            {
                if (!_tracedFirstDrink)
                {
                    _tracedFirstDrink = true;
                    DrinkTrace("drank 1 scoop at the pond (key=" + drinkKey +
                               ", thirst=" + (pond.thirst != null ? pond.thirst.Current01.ToString("F2") : "?") + ")");
                }
            }
            else if (!_tracedFirstFar)
            {
                _tracedFirstFar = true;
                DrinkTrace("drink pressed away from the pond -> no-op (key=" + drinkKey + ")");
            }
            return drank;
        }

        // [drink-trace] diagnostic logging — EDITOR/dev-only. [Conditional("UNITY_EDITOR")] strips the call +
        // its argument evaluation (the string concat) from the shipped IL2CPP release exe so the trace costs
        // the player nothing (unity6-mastery §5/§10). The first-press guards keep it one-shot.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void DrinkTrace(string msg) => Debug.Log("[drink-trace] " + msg);
    }
}
