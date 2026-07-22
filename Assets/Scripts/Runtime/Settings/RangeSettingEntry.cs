using System;
using UnityEngine;

namespace FarHorizon.Settings
{
    /// <summary>
    /// ARCHETYPE B (Uma §2.2 .setting-row--range) — a MIN–MAX pair bound to a live system's two clamp
    /// ends, rendered as a dual-thumb range with two readouts. Used by the orbit-camera ZOOM range
    /// (minDistance / maxDistance) and the VIEW-ANGLE range (minPitch / maxPitch) — AC3.
    ///
    /// AC4 — BOTH ENDS, AND THE LIVE SYSTEM CLAMPS TO THEM. The entry exposes both the min and the max as
    /// independently-driven values via two getter/setter pairs, and enforces the invariant
    /// LowerLimit ≤ min ≤ max ≤ UpperLimit every write (a thumb dragged past its partner snaps to it — the
    /// clamp-hit feedback). Writing either end pushes it straight into the live system (OrbitCamera clamps
    /// distance/pitch to its min/max fields every LateUpdate, so a tightened range re-clamps the live
    /// camera immediately — AC2/AC4, no restart).
    ///
    /// PURE C# / fully EditMode-testable; PlayerPrefs persists both ends (AC5). The setters are the single
    /// write authority (apply live + persist in one step).
    /// </summary>
    public sealed class RangeSettingEntry : SettingEntry
    {
        private readonly Func<float> _getMin;
        private readonly Action<float> _setMin;
        private readonly Func<float> _getMax;
        private readonly Action<float> _setMax;
        private readonly float _defaultMin;
        private readonly float _defaultMax;

        /// <summary>The hard floor the MIN end can never go below (the system's absolute lower limit).</summary>
        public float LowerLimit { get; }

        /// <summary>The hard ceiling the MAX end can never go above (the system's absolute upper limit).</summary>
        public float UpperLimit { get; }

        public override Archetype Kind => Archetype.Range;

        /// <param name="getMin/setMin">Read/write the system's MIN clamp end (e.g. orbit.minDistance).</param>
        /// <param name="getMax/setMax">Read/write the system's MAX clamp end (e.g. orbit.maxDistance).</param>
        /// <param name="lowerLimit">Absolute floor for the min thumb.</param>
        /// <param name="upperLimit">Absolute ceiling for the max thumb.</param>
        public RangeSettingEntry(string id, string label,
            Func<float> getMin, Action<float> setMin, Func<float> getMax, Action<float> setMax,
            float lowerLimit, float upperLimit, bool available = true, string unit = "")
            : base(id, label, available, unit)
        {
            _getMin = getMin ?? throw new ArgumentNullException(nameof(getMin));
            _setMin = setMin ?? throw new ArgumentNullException(nameof(setMin));
            _getMax = getMax ?? throw new ArgumentNullException(nameof(getMax));
            _setMax = setMax ?? throw new ArgumentNullException(nameof(setMax));
            LowerLimit = Mathf.Min(lowerLimit, upperLimit);
            UpperLimit = Mathf.Max(lowerLimit, upperLimit);
            _defaultMin = Mathf.Clamp(_getMin(), LowerLimit, UpperLimit);
            _defaultMax = Mathf.Clamp(_getMax(), LowerLimit, UpperLimit);
        }

        /// <summary>The live MIN end, clamped into the valid band for display.</summary>
        public float MinValue => Mathf.Clamp(_getMin(), LowerLimit, Mathf.Min(_getMax(), UpperLimit));

        /// <summary>The live MAX end, clamped into the valid band for display.</summary>
        public float MaxValue => Mathf.Clamp(_getMax(), Mathf.Max(_getMin(), LowerLimit), UpperLimit);

        public float DefaultMin => _defaultMin;
        public float DefaultMax => _defaultMax;

        /// <summary>
        /// Set the MIN end (AC4): clamp to [LowerLimit, currentMax] so min can never exceed max, write it
        /// into the live system, persist. The live system then clamps its runtime value to the new range
        /// immediately (AC2). Inert for an unavailable hook.
        /// </summary>
        public float SetMin(float v)
        {
            if (!Available) return MinValue;
            float clamped = ClampMin(v, _getMax());
            _setMin(clamped);
            PlayerPrefs.SetFloat(PrefsKey + ".min", clamped);
            return clamped;
        }

        /// <summary>Set the MAX end (AC4): clamp to [currentMin, UpperLimit], write live, persist.</summary>
        public float SetMax(float v)
        {
            if (!Available) return MaxValue;
            float clamped = ClampMax(v, _getMin());
            _setMax(clamped);
            PlayerPrefs.SetFloat(PrefsKey + ".max", clamped);
            return clamped;
        }

        public override void Apply()
        {
            if (!Available) return;
            // Re-assert both ends in an order that preserves the min ≤ max invariant regardless of how the
            // live fields currently sit: widen the max first, then tighten the min into it.
            _setMax(ClampMax(_getMax(), LowerLimit));
            _setMin(ClampMin(_getMin(), _getMax()));
        }

        public override void LoadFromPrefs()
        {
            if (!Available) return;
            // MAX first so a tightened min loaded after still clamps against the loaded max (invariant-safe).
            if (PlayerPrefs.HasKey(PrefsKey + ".max")) SetMax(PlayerPrefs.GetFloat(PrefsKey + ".max"));
            if (PlayerPrefs.HasKey(PrefsKey + ".min")) SetMin(PlayerPrefs.GetFloat(PrefsKey + ".min"));
        }

        public override void ResetToDefault()
        {
            if (!Available) return;
            SetMax(_defaultMax);
            SetMin(_defaultMin);
        }

        /// <summary>True when EITHER end has been dialed off its registration-time default (AC9 badge).
        /// An unavailable hook never drives the system, so it can never differ (no false badge on a greyed row).</summary>
        public override bool DiffersFromDefault =>
            Available && (!Mathf.Approximately(MinValue, _defaultMin) || !Mathf.Approximately(MaxValue, _defaultMax));

        // min ∈ [LowerLimit, currentMax]  (can't exceed the max thumb, can't drop below the hard floor)
        private float ClampMin(float v, float currentMax)
            => Mathf.Clamp(v, LowerLimit, Mathf.Min(currentMax, UpperLimit));

        // max ∈ [currentMin, UpperLimit]  (can't drop below the min thumb, can't exceed the hard ceiling)
        private float ClampMax(float v, float currentMin)
            => Mathf.Clamp(v, Mathf.Max(currentMin, LowerLimit), UpperLimit);
    }
}
