using System.Globalization;
using System.Text;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// THE LIVE PER-CLASS SWING-AIM NUDGE (86cb6v03j round 2) — the Sponsor's knob, not a derived value.
    ///
    /// === Why this exists ===
    /// Round 1 DERIVED the per-class swing-aim seat deltas by a closed-form fit
    /// (<see cref="HeldToolRig.SwingAimAxe"/> and siblings) and the Sponsor soak-FAILED them by eye:
    /// *"the axe is still positioned wrong on swing, sword is also wrong"* (2026-08-18, build stamp
    /// `zoned | 2026-08-18T14:08:36Z | a1c1e22`). The shipped gate PASSED that exact exe. Two rejections on one
    /// surface is the [[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]] trigger: stop deriving a value
    /// only his eye can judge and hand him the handle. He dials at soak; his numbers get baked; the gate is
    /// recalibrated against the baked values afterwards (round 3, NOT this round).
    ///
    /// === The contract that makes it safe to ship ===
    /// EVERY dial defaults to 0, and 0 means EXACTLY what ships today — not "approximately", not "within float
    /// tolerance". <see cref="Compose"/> SHORT-CIRCUITS on an all-zero nudge and returns the baked euler
    /// UNTOUCHED, so no quaternion round-trip can perturb the shipped value by an ulp. That short-circuit is
    /// the load-bearing line of this round and it has its own test
    /// (SwingAimNudgeTests.AllDialsZero_EveryClass_ReturnsBakedEulerBitForBit).
    ///
    /// === The nudge is RIGHT-MULTIPLIED, never added component-wise ===
    /// `unity6-mastery.md` §5 forbids accumulating a rotation dial by adding a delta to one euler component
    /// ("near ±90° pitch this is gimbal-locked and some orientations become UNREACHABLE — a full 360° yaw hunt
    /// that never lands"; fixed once already in <c>AxeNudgeTool.ComposeLocalRot</c>, 86caffwv5). The swing-aim
    /// deltas reach 163.6° (dagger yaw) and 108.5° (spear yaw), i.e. squarely in the band where a component-wise
    /// sum stops behaving like a rotation. So the composition is
    /// <c>Quaternion.Euler(baked) * Quaternion.Euler(nudge)</c> — the nudge applied in the TOOL'S OWN frame,
    /// the same frame the F9 dial and the mine seat delta nudge in, so DIALLED == BAKED == APPLIED.
    ///
    /// === Exactly bakeable, by construction ===
    /// The composed result is handed back as an EULER (<see cref="Effective"/>), which is precisely the shape
    /// <see cref="HeldToolRig.SwingAimAxe"/> and its siblings are declared in. The orchestrator bakes the
    /// `effective=(x, y, z)` triple this class prints STRAIGHT into those constants with no arithmetic — no
    /// re-derivation, no re-fit, no chance of a transcription error changing what the Sponsor approved.
    /// <see cref="Readout"/> is that hand-off artefact.
    ///
    /// === Scope bound ===
    /// This class changes NO shipped value. It adds a channel whose neutral element is the shipped value. The
    /// pickaxe gets a dial like the other four (Sponsor decision — he judges it by eye at this soak) while its
    /// shipped default stays the Sponsor-passed <c>mineSeatEulerDelta</c> route it has always had.
    ///
    /// MUTABLE RUNTIME STATIC → carries the mandatory
    /// [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] reset (unity-conventions.md §Configurable Enter
    /// Play Mode; StaticStateResetTests audits the whole asmdef for it). Without it a dial left at 30° in one
    /// editor play-entry would silently become the default for every later one — a debug knob becoming the ship
    /// value, which is the exact failure this ticket is trying to stop.
    /// </summary>
    public static class SwingAimNudge
    {
        /// <summary>How many weapon classes carry a dial — axe / pickaxe / dagger / spear / sword.
        /// Derived from the CastawayCharacter class constants so a 6th class cannot silently miss a dial.</summary>
        public const int ClassCount = CastawayCharacter.WeaponClassSword + 1;

        /// <summary>Per-axis dial band, degrees. ±180 covers every reachable orientation on ONE axis (a full turn
        /// is 360, and ±180 reaches all of it), so the Sponsor can never find an aim he cannot dial to. Chosen as
        /// the geometric bound rather than a guess at "how far he'll want to go" — a band tuned to an expectation
        /// is the calibrate-against-achievement mistake this codebase has already paid for.</summary>
        public const float LimitDeg = 180f;

        /// <summary>Axis index → name, used by BOTH the console row labels and the readout, so a row cannot be
        /// labelled "pitch" while the readout calls the same number something else.</summary>
        public static readonly string[] AxisNames = { "pitch", "yaw", "roll" };

        // The live dials. Degrees, per class, x=pitch y=yaw z=roll. All-zero == ships-today.
        private static readonly Vector3[] _nudge = new Vector3[ClassCount];

        // Bumped on every accepted write. The readout/log flusher watches this instead of diffing five vectors
        // per frame, and it is what lets the IMGUI panel cache its text instead of rebuilding it every OnGUI
        // (unity6-mastery §5 — no per-frame string concatenation).
        private static int _revision;

        /// <summary>Increments on every accepted dial write. A consumer that caches rendered text compares this
        /// to know when its cache is stale.</summary>
        public static int Revision => _revision;

        /// <summary>True while EVERY dial is still 0 — i.e. the build is behaving bit-for-bit as it ships.
        /// The readout overlay and the log flush both key off this, which is what keeps every -verify* capture
        /// byte-unchanged: at default there is nothing to draw and nothing to log.</summary>
        public static bool IsPristine
        {
            get
            {
                for (int i = 0; i < ClassCount; i++)
                    if (_nudge[i] != Vector3.zero) return false;
                return true;
            }
        }

        /// <summary>The dialled nudge for a class (degrees, x=pitch y=yaw z=roll). Out-of-range → zero.</summary>
        public static Vector3 Get(int weaponClass)
            => (uint)weaponClass < ClassCount ? _nudge[weaponClass] : Vector3.zero;

        /// <summary>One axis of a class's nudge (0=pitch, 1=yaw, 2=roll). The console rows' getter.</summary>
        public static float GetAxis(int weaponClass, int axis)
        {
            Vector3 v = Get(weaponClass);
            return axis == 0 ? v.x : axis == 1 ? v.y : v.z;
        }

        /// <summary>Write one axis of a class's nudge (the console rows' setter). Clamped to ±<see
        /// cref="LimitDeg"/>. A write that does not change the value does NOT bump <see cref="Revision"/>, so
        /// re-applying the same value (the registry's ApplyAll on startup does exactly that) cannot make a
        /// pristine build look dialled.</summary>
        public static void SetAxis(int weaponClass, int axis, float degrees)
        {
            if ((uint)weaponClass >= ClassCount) return;
            float v = Mathf.Clamp(degrees, -LimitDeg, LimitDeg);
            Vector3 cur = _nudge[weaponClass];
            Vector3 next = cur;
            if (axis == 0) next.x = v; else if (axis == 1) next.y = v; else next.z = v;
            if (next == cur) return;
            _nudge[weaponClass] = next;
            _revision++;
        }

        /// <summary>Clear every dial back to the shipped behaviour. Not wired to a key — the console's own
        /// reset-to-defaults drives the rows, which drives this — but exposed so a test can restore the
        /// pristine state without reaching into the array.</summary>
        public static void ClearAll()
        {
            // Only bumps the revision if something ACTUALLY changed - the same no-op guard SetAxis carries, and
            // for the same reason: Revision means "a dialled value moved", so clearing an already-pristine build
            // must leave it indistinguishable from one nobody touched. Without this, any caller that defensively
            // clears (a test SetUp, a reset-to-defaults on a stock launch) would mark the build dialled, the
            // readout would paint and the log would fill on a build at its shipped default.
            bool changed = false;
            for (int i = 0; i < ClassCount; i++)
            {
                if (_nudge[i] == Vector3.zero) continue;
                _nudge[i] = Vector3.zero;
                changed = true;
            }
            if (changed) _revision++;
        }

        /// <summary>
        /// THE COMPOSITION — the baked per-class swing-aim euler with the Sponsor's live nudge applied in the
        /// tool's own frame. This is the ONLY place the two are combined.
        ///
        /// The all-zero SHORT-CIRCUIT is not an optimisation: <c>Quaternion.Euler(v).eulerAngles</c> is NOT
        /// guaranteed to return <c>v</c> bit-for-bit (it re-decomposes, and for these deltas — e.g. the dagger's
        /// 163.6° yaw — it can legitimately return a DIFFERENT triple naming the same rotation). Round-tripping
        /// at default would therefore ship a different literal than 70583d8 does, which is exactly what this
        /// round promised not to do. Short-circuiting makes "0 == ships today" a structural property rather than
        /// a tolerance claim.
        /// </summary>
        public static Vector3 Compose(Vector3 baked, Vector3 nudge)
            => nudge == Vector3.zero
                 ? baked
                 : (Quaternion.Euler(baked) * Quaternion.Euler(nudge)).eulerAngles;

        /// <summary>The EFFECTIVE swing-aim euler for a class — what the rig applies right now, and the exact
        /// triple to bake into <see cref="HeldToolRig.SwingAimAxe"/> and its siblings.</summary>
        public static Vector3 Effective(int weaponClass)
            => Compose(HeldToolRig.SwingAimBakedEulerForClass(weaponClass), Get(weaponClass));

        /// <summary>Display name for a weapon class — one seam for the console labels, the readout and the log
        /// block, so the Sponsor's screenshot and the log he hands back name the classes identically.</summary>
        public static string ClassName(int weaponClass)
        {
            switch (weaponClass)
            {
                case CastawayCharacter.WeaponClassAxe:     return "axe";
                case CastawayCharacter.WeaponClassPickaxe: return "pickaxe";
                case CastawayCharacter.WeaponClassDagger:  return "dagger";
                case CastawayCharacter.WeaponClassSpear:   return "spear";
                case CastawayCharacter.WeaponClassSword:   return "sword";
                default:                                   return "class" + weaponClass;
            }
        }

        /// <summary>The log-line prefix every readout line carries — a single stable needle so the values can be
        /// pulled out of a multi-megabyte Player.log with one grep.</summary>
        public const string LogTag = "[swing-aim-dial]";

        /// <summary>
        /// THE HAND-BACK ARTEFACT — every class, its dialled nudge and its EFFECTIVE euler, as text.
        ///
        /// Printed with <see cref="CultureInfo.InvariantCulture"/> (DOTS, always). The Sponsor's machine runs a
        /// DANISH locale and this project has already shipped a gate log that mixed comma- and dot-decimals on
        /// one run (PR #439 §7); a bake artefact that renders `-15,500` is one careless copy away from being
        /// read as two numbers. The bake target is a C# literal, so the invariant form is also the form that can
        /// be pasted straight into the source.
        /// </summary>
        public static string Readout()
        {
            var sb = new StringBuilder(512);
            for (int c = 0; c < ClassCount; c++)
            {
                Vector3 n = Get(c);
                Vector3 e = Effective(c);
                sb.Append(LogTag).Append(' ').Append(ClassName(c).PadRight(8))
                  .Append(" nudge=(").Append(F(n.x)).Append(", ").Append(F(n.y)).Append(", ").Append(F(n.z))
                  .Append(")  effective=(").Append(F(e.x)).Append(", ").Append(F(e.y)).Append(", ").Append(F(e.z))
                  .Append(')');
                if (c < ClassCount - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        private static string F(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < ClassCount; i++) _nudge[i] = Vector3.zero;
            _revision = 0;
        }
    }
}
