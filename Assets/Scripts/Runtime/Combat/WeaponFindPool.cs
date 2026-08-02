using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon.Combat
{
    /// <summary>
    /// The AUTHORED POOL of world weapon finds + the per-tier FINDABILITY dial (ticket 86cah7y5b AC2/AC5).
    ///
    /// === Why a pool at all, when the default is ONE find (AC2) ===
    /// The Sponsor's 2026-07-27 decision is `sword_iron`, ONE per island region — but AC2/AC5 both say the
    /// COUNT and SPREAD stay soak-tunable, and AC5 makes findability a per-difficulty-tier dimension. A dial
    /// that can only ever go down is a half-dial, so the scatter AUTHORS a small pool of seeded candidate
    /// sites and this component ENABLES the first <see cref="activeFindCount"/> of them (default 1). Dialling
    /// the knob up in the soak reveals the already-placed extra sites — the SAME shape as
    /// <see cref="FarHorizon.MineOre.SetActiveNodeCount"/> / the `iron_ore_rarity` rarity dial, which is the
    /// live precedent AC5 names. A disabled site is switched OFF, so it is invisible AND its
    /// <see cref="WorldWeaponFind"/> is not discovered as loot-able by the looter.
    ///
    /// === The dead-knob guard (AC5 constraint, the PopulateBoar note) ===
    /// The live console row writes BOTH the active field AND the ACTIVE tier's per-tier entry (see
    /// SettingsCatalog.PopulateWeaponFind), so a later <see cref="ApplyDifficulty"/> reads back the DIALLED
    /// value rather than clobbering it with the baked default. Without that pairing the knob is dead the
    /// moment a tier is re-applied — the exact class the `PopulateBoar` note in SettingsCatalog.cs records.
    ///
    /// === Defaults (AC5) ===
    /// The same find count on ALL THREE tiers (easy == medium == hard == 1) until the Sponsor asks for a
    /// rarity split at soak — his call, per AC5's stated default. The per-tier fields exist and are wired, so
    /// the split is a value change, not a code change.
    ///
    /// === Serialization (unity-conventions.md §editor-vs-runtime) ===
    /// Authored EDITOR-TIME by MovementCameraScene.BuildWeaponFinds with its <see cref="findRoot"/> +
    /// <see cref="deathHandler"/> refs serialized into Boot.unity — never added at Awake. The site pool is
    /// discovered from the root's children ONCE in <see cref="InitialiseFindability"/> (called from
    /// <see cref="Awake"/>, not Start — see <see cref="ActiveFindCount"/> for the shipped-build defect that
    /// forced the move), not per-frame.
    ///
    /// NO MUTABLE STATICS (instance fields only) — no SubsystemRegistration reset needed.
    /// </summary>
    public sealed class WeaponFindPool : MonoBehaviour
    {
        [Header("Wiring (serialized editor-time)")]
        [Tooltip("Parent of the authored find SITES. Every child carrying a WorldWeaponFind is a pool site; " +
                 "ActiveFindCount enables the first N. Wired editor-time by BuildWeaponFinds.")]
        public Transform findRoot;

        [Tooltip("The DeathHandler whose tier is the ACTIVE difficulty (the BoarAI.ActiveTier idiom). " +
                 "Medium when unwired. Read live so a tier change during play resolves correctly.")]
        public FarHorizon.Combat.DeathHandler deathHandler;

        [Header("Findability (AC5) — how many of the authored sites actually hold a weapon")]
        [Tooltip("Active find count. -1 = seed from the MEDIUM per-tier value in Start (so a bare test can " +
                 "override it first). Clamped to the authored pool size. default - Sponsor-soak tunes.")]
        public int activeFindCount = -1;

        [Header("Per-tier find count (AC5). ApplyDifficulty copies the active tier's value into activeFindCount.")]
        [Tooltip("Finds on EASY. Same as the other tiers by default — AC5's stated default is no rarity split " +
                 "until the Sponsor asks for one at soak.")]
        public int easyFindCount = DefaultFindCount;
        [Tooltip("Finds on MEDIUM (the default tier).")]
        public int medFindCount = DefaultFindCount;
        [Tooltip("Finds on HARD.")]
        public int hardFindCount = DefaultFindCount;

        /// <summary>The AC2 default — ONE find per island region (Sponsor decision 2026-07-27). A named
        /// constant so the bootstrap, the settings row and the tests read ONE source, never a literal.</summary>
        public const int DefaultFindCount = 1;

        /// <summary>Dial band floor — 0 finds (a world with no gift in it) is a legitimate hard-tier setting.</summary>
        public const int FindCountMin = 0;

        /// <summary>Dial band ceiling. The scatter authors this many candidate SITES, so the console row can
        /// never ask for more finds than there are places to put them.</summary>
        public const int FindCountMax = 4;

        private readonly List<WorldWeaponFind> _sites = new List<WorldWeaponFind>();
        private bool _resolved;

        /// <summary>The authored candidate sites (the whole pool, active or not). Tests + capture read it.</summary>
        public IReadOnlyList<WorldWeaponFind> Sites { get { EnsureDiscovered(); return _sites; } }

        /// <summary>
        /// How many sites currently hold a find — the RESOLVED live dial value. Never returns the raw
        /// <c>-1</c> sentinel.
        ///
        /// THE DEFECT THIS GUARDS (shipped-build, first -verifyWeaponFind run — `pool=True find=False`,
        /// ci-out/verify-weaponfind.log): the AC5 settings row binds its getter to THIS property, and
        /// <c>SettingsPanel.Start()</c> registers + <c>ApplyAll()</c>s it. Start order between that panel and
        /// this component is UNDEFINED, so the panel can read the count BEFORE this pool has resolved the
        /// sentinel. <see cref="FarHorizon.Settings.IntSettingEntry"/> clamps whatever it reads into
        /// [<see cref="FindCountMin"/>, <see cref="FindCountMax"/>] = [0, 4] and writes it straight back
        /// through <see cref="SetActiveFindCount"/> — so a leaked <c>-1</c> became <c>0</c>, permanently
        /// destroying the sentinel; <see cref="Awake"/>/Start then saw a legitimate-looking 0, skipped the
        /// tier seed, and DISABLED EVERY AUTHORED SITE. A dial band whose floor is 0 turns "not yet resolved"
        /// into "none of them", silently and with every test green.
        ///
        /// Resolving in the getter is pure (no writes, no SetActive) and makes the sentinel unobservable to
        /// ANY reader regardless of lifecycle order — belt to <see cref="Awake"/>'s braces.
        /// </summary>
        public int ActiveFindCount => activeFindCount >= 0 ? activeFindCount : CountForTier(ActiveTier);

        /// <summary>The ACTIVE difficulty tier — read live from the DeathHandler surface (Medium if unwired).
        /// Mirrors <see cref="FarHorizon.Combat.BoarAI.ActiveTier"/>.</summary>
        public FarHorizon.SurvivalNeed.DifficultyTier ActiveTier =>
            deathHandler != null ? deathHandler.tier : FarHorizon.SurvivalNeed.DifficultyTier.Medium;

        // AWAKE, not Start — and this is load-bearing, not a style choice. Unity guarantees EVERY Awake
        // completes before ANY Start, so resolving the sentinel here puts this pool in a settled state before
        // SettingsPanel.Start() can register + ApplyAll() the AC5 row against it. Doing this in Start left the
        // two components in an UNDEFINED order, and when the panel won it wrote 0 over the sentinel and killed
        // every find in the shipped build (see the ActiveFindCount doc).
        private void Awake() => InitialiseFindability();

        /// <summary>
        /// Discover the authored sites, resolve the <c>-1</c> sentinel from the active tier, and apply the
        /// resulting count. IDEMPOTENT — safe to call from <see cref="Awake"/>, from a headless test, or from
        /// any earlier reader that needs the pool settled.
        ///
        /// Public because it is the only honest seam an EditMode test has: Unity does not run
        /// <see cref="Awake"/> on components added outside play mode, so without this the site-enable/disable
        /// behaviour — the exact behaviour that shipped broken — was not reachable from a headless guard at all.
        /// </summary>
        public void InitialiseFindability()
        {
            if (_resolved) return;
            _resolved = true;
            EnsureDiscovered();
            if (activeFindCount < 0) activeFindCount = CountForTier(ActiveTier);
            ApplyActiveCount();
            FindPoolTrace("pool of " + _sites.Count + " authored find site(s); active=" + activeFindCount +
                          " (tier=" + ActiveTier + ")");
        }

        /// <summary>
        /// Copy the tier's per-tier find count into the active field + re-apply (AC5 — "read the active
        /// difficulty setting"). Mirrors <see cref="FarHorizon.Combat.BoarEnemy.ApplyDifficulty"/>. A find the
        /// player has ALREADY looted is not resurrected by a tier change: an enabled site whose weapon is gone
        /// stays gone (the site object re-enables, but its WorldWeaponFind reports CanLoot false).
        /// </summary>
        public void ApplyDifficulty(FarHorizon.SurvivalNeed.DifficultyTier tier)
        {
            activeFindCount = CountForTier(tier);
            ApplyActiveCount();
        }

        /// <summary>The per-tier map read (pure — the EditMode guard asserts the three tiers directly).</summary>
        public int CountForTier(FarHorizon.SurvivalNeed.DifficultyTier tier)
        {
            switch (tier)
            {
                case FarHorizon.SurvivalNeed.DifficultyTier.Easy: return easyFindCount;
                case FarHorizon.SurvivalNeed.DifficultyTier.Hard: return hardFindCount;
                default: return medFindCount;
            }
        }

        /// <summary>
        /// Live setter the `weapon find count` console row binds to (the SetActiveNodeCount idiom). Clamps to
        /// [0, pool size] and enables the first N sites. LIVE — no dead knob.
        /// </summary>
        public void SetActiveFindCount(int count)
        {
            // A negative arrival re-arms the sentinel rather than being written through as a raw negative that
            // a downstream clamp would silently read as "zero finds" (the shipped defect's shape).
            activeFindCount = count < 0 ? CountForTier(ActiveTier) : count;
            ApplyActiveCount();
        }

        // Enable the first activeFindCount sites; disable the rest. Clamped to the authored pool so a console
        // ceiling above the pool size can never over-reach. A disabled site is switched OFF: invisible AND
        // undiscoverable by the looter (FindObjectsOfType(true) finds it, but CanLoot short-circuits on the
        // inactive GameObject's component never ticking — and the resolve skips it because it is inactive).
        private void ApplyActiveCount()
        {
            if (!_resolved) return; // the pool isn't discovered yet (InitialiseFindability applies it once)
            int pool = _sites.Count;
            int active = pool == 0 ? 0 : Mathf.Clamp(activeFindCount, 0, pool);
            activeFindCount = active; // keep the field honest (clamped to what the pool can actually show)
            for (int i = 0; i < _sites.Count; i++)
            {
                var site = _sites[i];
                if (site == null) continue;
                bool on = i < active;
                if (site.gameObject.activeSelf != on) site.gameObject.SetActive(on);
            }
        }

        private void EnsureDiscovered()
        {
            if (_sites.Count > 0) return;
            var root = findRoot != null ? findRoot : transform;
            // Include inactive: a previously-disabled site must be re-findable when the dial goes back up.
            root.GetComponentsInChildren(true, _sites);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void FindPoolTrace(string msg) => Debug.Log("[weaponfind-trace] " + msg);
    }
}
