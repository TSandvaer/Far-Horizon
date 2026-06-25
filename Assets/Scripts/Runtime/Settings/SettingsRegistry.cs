using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarHorizon.Settings
{
    /// <summary>
    /// The EXTENSIBLE settings registry (ticket 86caa4bqp AC2 — the headline AC). An ORDERED list of
    /// typed <see cref="SettingEntry"/>s, each bound to a live gameplay param. This is the foundation
    /// everything downstream (inventory slots, wood-yield, tool-use speed) plugs into: a future ticket
    /// registers its setting with a few lines (pick the typed entry + bind delegates), and the panel
    /// renders it generically off the archetype — NO UI rebuild.
    ///
    /// THE REGISTRATION API (the clean, extensible surface):
    /// <code>
    ///   registry.AddFloat("walk_speed", "Walk speed", () =&gt; wasd.moveSpeed, v =&gt; wasd.moveSpeed = v,
    ///                     min: 1f, max: 12f, unit: "u/s");
    ///   registry.AddRange("zoom_range", "Zoom range",
    ///                     () =&gt; orbit.minDistance, v =&gt; orbit.minDistance = v,
    ///                     () =&gt; orbit.maxDistance, v =&gt; orbit.maxDistance = v, lower: 2f, upper: 40f);
    ///   registry.AddFloat("run_speed", "Run speed", ..., available: false); // extension hook (AC3)
    /// </code>
    ///
    /// PURE C# (no UnityEngine.Object) so the whole registry contract — register, bind, drive, clamp,
    /// persist, reset — is fully unit-testable in EditMode without a scene or the UIDocument render loop
    /// (AC6). The MonoBehaviour wiring + UI Toolkit view live in <see cref="SettingsCatalog"/> /
    /// <see cref="SettingsPanel"/> respectively; this class holds NO Unity lifecycle, NO statics.
    ///
    /// NO MUTABLE STATICS here (instance state only), so the Configurable-Enter-Play-Mode static-reset
    /// audit (StaticStateResetTests) stays green with no [RuntimeInitializeOnLoadMethod] reset needed
    /// (unity-conventions.md §Configurable Enter Play Mode — the rule applies only if a static is added).
    /// </summary>
    public sealed class SettingsRegistry
    {
        private readonly List<SettingEntry> _entries = new List<SettingEntry>();
        private readonly Dictionary<string, SettingEntry> _byId = new Dictionary<string, SettingEntry>();

        /// <summary>The registered settings in registration order (the panel renders rows in this order).</summary>
        public IReadOnlyList<SettingEntry> Entries => _entries;

        /// <summary>How many settings are registered.</summary>
        public int Count => _entries.Count;

        /// <summary>Fires whenever an entry is registered (the panel rebuilds its row list). Reset on each play-entry is N/A — instance field.</summary>
        public event Action Changed;

        /// <summary>
        /// Register a setting. Ids must be unique (a duplicate throws — a registration bug, not a runtime
        /// path). Returns the entry so the caller can keep a typed handle. Fires <see cref="Changed"/>.
        /// </summary>
        public T Register<T>(T entry) where T : SettingEntry
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (_byId.ContainsKey(entry.Id))
                throw new ArgumentException($"a setting with id '{entry.Id}' is already registered", nameof(entry));
            _entries.Add(entry);
            _byId[entry.Id] = entry;
            Changed?.Invoke();
            return entry;
        }

        // --- Convenience registration helpers (the "a few lines" surface the ACs ask for) ---

        public FloatSettingEntry AddFloat(string id, string label, Func<float> get, Action<float> set,
            float min, float max, bool available = true, string unit = "")
            => Register(new FloatSettingEntry(id, label, get, set, min, max, available, unit));

        public RangeSettingEntry AddRange(string id, string label,
            Func<float> getMin, Action<float> setMin, Func<float> getMax, Action<float> setMax,
            float lower, float upper, bool available = true, string unit = "")
            => Register(new RangeSettingEntry(id, label, getMin, setMin, getMax, setMax, lower, upper, available, unit));

        public IntSettingEntry AddInt(string id, string label, Func<int> get, Action<int> set,
            int min, int max, int step = 1, bool available = true, string unit = "")
            => Register(new IntSettingEntry(id, label, get, set, min, max, step, available, unit));

        /// <summary>
        /// Remove a registered entry by id (no-op + false if not present). Used to REPLACE a greyed
        /// extension-hook row with its live binding once the owning feature lands — e.g. chop flips the
        /// reserved `tool_use_speed` row live (ticket 86caa4c5c V1): `Populate` registers it greyed,
        /// `PopulateChop` removes-then-re-adds it bound to the real swing-speed field. Because
        /// <see cref="Register{T}"/> THROWS on a duplicate id, the only safe live-rebind path is
        /// remove-then-add — this is that seam. Fires <see cref="Changed"/> on a real removal.
        /// </summary>
        public bool Remove(string id)
        {
            if (string.IsNullOrEmpty(id) || !_byId.TryGetValue(id, out var entry)) return false;
            _byId.Remove(id);
            _entries.Remove(entry);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Look up a registered entry by id (null if not registered).</summary>
        public SettingEntry Get(string id) => _byId.TryGetValue(id, out var e) ? e : null;

        /// <summary>True if a setting with this id is registered.</summary>
        public bool Has(string id) => _byId.ContainsKey(id);

        /// <summary>Re-apply every entry's current value to the game (used after a bulk load).</summary>
        public void ApplyAll()
        {
            for (int i = 0; i < _entries.Count; i++) _entries[i].Apply();
        }

        /// <summary>Load every entry from PlayerPrefs (AC5) and drive the params, so soak tweaks survive a relaunch.</summary>
        public void LoadAll()
        {
            for (int i = 0; i < _entries.Count; i++) _entries[i].LoadFromPrefs();
        }

        /// <summary>Reset every entry to its captured default and apply (AC5 reset-to-defaults).</summary>
        public void ResetAll()
        {
            for (int i = 0; i < _entries.Count; i++) _entries[i].ResetToDefault();
        }
    }
}
