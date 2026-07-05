using System;
using UnityEngine;

namespace FarHorizon.Settings
{
    /// <summary>
    /// ARCHETYPE A (Uma §2.2 .setting-row--slider) — a single float bound to a live gameplay param via a
    /// getter/setter pair, rendered as a slider + a fixed-width numeric readout. Used by walk speed (AC3,
    /// live) and — as named extension hooks (AC3, Available=false) — run speed / jump height / tool-use
    /// speed until those features land.
    ///
    /// THE BIND CONTRACT: the entry never stores the value itself — it READS the live param through
    /// <see cref="_get"/> and WRITES it through <see cref="_set"/>, so the entry and the game can never
    /// disagree (changing the slider drives the param immediately, AC2). The setter is the single
    /// authority: <see cref="SetValue"/> clamps to [Min, Max], writes the param, then persists to
    /// PlayerPrefs (AC5) — one path, no split write.
    /// </summary>
    public sealed class FloatSettingEntry : SettingEntry
    {
        private readonly Func<float> _get;
        private readonly Action<float> _set;
        private readonly float _default;

        /// <summary>Slider lower bound (the smallest value the Sponsor can dial).</summary>
        public float Min { get; }

        /// <summary>Slider upper bound (the largest value the Sponsor can dial).</summary>
        public float Max { get; }

        public override Archetype Kind => Archetype.Slider;

        /// <param name="get">Reads the live param (e.g. () =&gt; wasd.moveSpeed).</param>
        /// <param name="set">Writes the live param (e.g. v =&gt; wasd.moveSpeed = v) — drives the game immediately.</param>
        /// <param name="min">Slider floor.</param>
        /// <param name="max">Slider ceiling.</param>
        /// <param name="available">false = a not-yet-built extension hook (AC3), greyed + "(soon)"; setter inert.</param>
        /// <param name="persist">false = a DIAL-TO-BAKE INSTRUMENT row (86cah90cp round-3): the row drives the
        /// live param but never touches PlayerPrefs — its persistence mechanism is the BAKE, not prefs. Twice
        /// (#223 round-1 legacy key, round-3 validly-stamped key) a persisted world-look override silently
        /// stomped a freshly-baked sun at every boot — on soaks AND -verify* gates alike. LoadFromPrefs on a
        /// non-persist row also DELETES any lingering key from earlier persisting builds (one-time self-heal).</param>
        public FloatSettingEntry(string id, string label, Func<float> get, Action<float> set,
            float min, float max, bool available = true, string unit = "", bool persist = true)
            : base(id, label, available, unit)
        {
            Persist = persist;
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
            Min = Mathf.Min(min, max);
            Max = Mathf.Max(min, max);
            // Capture the param's value at registration time as the bake-able default (AC5 reset).
            _default = ClampValue(_get());
        }

        /// <summary>The live param value, clamped into [Min, Max] for display.</summary>
        public float Value => ClampValue(_get());

        /// <summary>The captured default (the value at registration — the current shipped default).</summary>
        public float Default => _default;

        /// <summary>false = dial-to-bake instrument row: drives the live param, never reads/writes PlayerPrefs
        /// (the bake is its persistence — 86cah90cp round-3; see the ctor doc).</summary>
        public bool Persist { get; }

        /// <summary>
        /// Set the live param (AC2 — drives the game immediately): clamp to [Min, Max], write through the
        /// bind setter, then persist to PlayerPrefs (AC5). The SINGLE write authority. Inert (returns the
        /// current value) for an unavailable extension hook — we never drive a param that doesn't exist.
        /// </summary>
        public float SetValue(float v)
        {
            if (!Available) return Value;
            float clamped = ClampValue(v);
            _set(clamped);
            if (Persist)
            {
                PlayerPrefs.SetFloat(PrefsKey, clamped);
                // STALE-DEFAULT STAMP (86cah90cp sun-fidelity fix): record WHICH baked default this override was
                // persisted under. LoadFromPrefs only honours the override while the row's registration default is
                // STILL that default — when a bake moves the default, the stamp mismatch discards the stale override
                // so the new baked look actually shows (the #223 defect: a persisted sun_elevation=18 from the
                // elev-18 era silently overrode the freshly-baked 12 on every launch, and re-persisted itself).
                PlayerPrefs.SetFloat(DefaultStampKey, _default);
            }
            return clamped;
        }

        public override void Apply()
        {
            if (!Available) return;
            _set(Value); // re-assert the (clamped) live value
        }

        public override void LoadFromPrefs()
        {
            if (!Available) return;
            if (!Persist)
            {
                // DIAL-TO-BAKE INSTRUMENT row (86cah90cp round-3): never apply a persisted override — the baked
                // default IS the shipped look. Also SELF-HEAL: delete any lingering key an earlier persisting
                // build left behind. The round-3 defect this kills: fh.settings.sun_elevation=18 sat in the
                // registry stamped .def=8 — VALIDLY stamped under the CURRENT default, so the round-2 stamp
                // invalidation could never discard it, and every boot re-applied 18 over the Sponsor-approved
                // 8° bake (and re-freshened its own stamp via SetValue's re-persist). No stamp scheme can
                // distinguish that from a deliberate dial — the cause-level fix is that bake-driven world-look
                // rows have no business persisting at all (their dial session ends in a BAKE).
                if (PlayerPrefs.HasKey(PrefsKey)) PlayerPrefs.DeleteKey(PrefsKey);
                if (PlayerPrefs.HasKey(DefaultStampKey)) PlayerPrefs.DeleteKey(DefaultStampKey);
                return;
            }
            if (!PlayerPrefs.HasKey(PrefsKey)) return;
            // STALE-DEFAULT INVALIDATION (86cah90cp sun-fidelity fix). A persisted override is only valid
            // against the baked default it was dialed under. If the stamp is missing (legacy key from before
            // stamping — e.g. the Sponsor's fh.settings.sun_elevation=18) or differs from the CURRENT
            // registration-time default (the bake moved), the override is STALE: discard it and let the new
            // baked default show. A user re-dial after the new bake re-persists with a fresh stamp, so a
            // deliberate tweak still survives relaunches (AC5) — it just can't outlive the default it was
            // dialed against.
            bool stampedUnderCurrentDefault = PlayerPrefs.HasKey(DefaultStampKey)
                && Mathf.Approximately(PlayerPrefs.GetFloat(DefaultStampKey), _default);
            if (!stampedUnderCurrentDefault)
            {
                PlayerPrefs.DeleteKey(PrefsKey);
                PlayerPrefs.DeleteKey(DefaultStampKey);
                return; // keep the (new) baked default — the whole point of the bake
            }
            SetValue(PlayerPrefs.GetFloat(PrefsKey));
        }

        /// <summary>The sibling PlayerPrefs key holding the baked default the persisted value was dialed
        /// under (the stale-override invalidation stamp — see <see cref="LoadFromPrefs"/>).</summary>
        public string DefaultStampKey => PrefsKey + ".def";

        public override void ResetToDefault()
        {
            if (!Available) return;
            SetValue(_default);
        }

        /// <summary>True when the live value has been dialed off its registration-time default (AC9 badge).
        /// An unavailable hook never drives a param, so it can never differ (no false badge on a greyed row).</summary>
        public override bool DiffersFromDefault => Available && !Mathf.Approximately(Value, _default);

        private float ClampValue(float v) => Mathf.Clamp(v, Min, Max);
    }
}
