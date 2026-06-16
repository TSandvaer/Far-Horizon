using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// 86ca9zcjn guard for the held-axe FOLLOW-THE-ARM design choice (Sponsor, soak 6bcc1bc) — REPURPOSED from
    /// the 86ca9ykp0 walk-bounce/ratchet test.
    ///
    /// THE DESIGN CHOICE: the Sponsor explicitly REVERSED the old "the axe changes position when I walk"
    /// preference. The held axe must now FOLLOW the right arm's natural swing during locomotion (a stabilized
    /// axe that stays put reads DETACHED from the hand mid-stride). So the prior swing-stabilizer / grip-anchor
    /// AND the vertical-decouple bounce/ratchet fix are REMOVED — the axe rides the RAW hand bone.
    ///
    /// THE BUG CLASS (the NEW contract, not the old one): per-step swing is now ALLOWED (it's wanted) — but
    /// CUMULATIVE drift across walk steps is FORBIDDEN (no monotonic ratchet). Riding the RAW hand returns the
    /// axe to its pose every walk cycle (no anchor integration, no eased accumulation), so the bounded-ness is
    /// by construction — but we GUARD it: the SETTLED axe world-Y must RETURN TO BASELINE after each walk leg
    /// with no cumulative climb over ≥5 steps, AND the axe must ACTUALLY swing with the arm per step (a frozen
    /// axe is now the bug — it would read detached). A "the axe moved at all"-style proxy would pass during a
    /// ratchet, so we assert the END-TO-END settled-Y delta across steps (the quantity the Sponsor judges).
    ///
    /// ROOT GEOMETRY (the real hierarchy this test reproduces faithfully):
    ///   root (CastawayCharacter — never yaws)
    ///     └ model (_model — YAWS with facing AND its local-Y BOBS per-frame: modelSoleGround cancels the
    ///              Mixamo WALK-clip hip-lift; Idle≈0, Walk≈−0.66)
    ///         └ hand (bone — the Animator writes the WALK-clip pose: a per-step arm-SWING PLUS the hip-LIFT
    ///                 baseline while walking; both expressed in the model-local frame)
    ///             └ axe (HeldAxeRig — rides the RAW hand)
    /// Because modelSoleGround CANCELS the hip-lift at the model level, the hand's WORLD-Y returns to the same
    /// place each idle pause (no net hip-lift survives to world space), so the axe — riding the raw world hand —
    /// returns to baseline with no ratchet. The per-step swing is the live arm-swing the Sponsor wants to see.
    /// </summary>
    public class HeldAxeWalkBouncePlayModeTests
    {
        private GameObject _root;
        private Transform _model;     // yaws + local-Y BOBS (modelSoleGround analogue)
        private Transform _hand;      // arm-swings + carries the walk hip-lift baseline while "walking"
        private Transform _axe;
        private HeldAxeRig _rig;

        // Rest hand pose in the MODEL-LOCAL frame (a non-trivial bone offset off the body center-line).
        private static readonly Vector3 HandRestLocal = new Vector3(0.3f, 1.2f, 0.1f);

        private void BuildRig()
        {
            _root = new GameObject("CastawayRoot");

            var modelGo = new GameObject("Model");
            modelGo.transform.SetParent(_root.transform, false);
            _model = modelGo.transform;

            var handGo = new GameObject("mixamorig:RightHand");
            handGo.transform.SetParent(_model, false);
            handGo.transform.localPosition = HandRestLocal;
            handGo.transform.localRotation = Quaternion.Euler(35f, 12f, -48f); // a rotated bone frame (§FBX trap)
            handGo.transform.localScale = Vector3.one * 1.8f;                  // avatar-root scale (Hyper3D rig)
            _hand = handGo.transform;

            var axeGo = new GameObject("HeroAxe");
            axeGo.transform.SetParent(_hand, false);
            axeGo.transform.localScale = Vector3.one * 0.45f;
            _rig = axeGo.AddComponent<HeldAxeRig>();
            _rig.hand = _hand;
            _rig.worldOffsetFromHand = new Vector3(0.08f, -0.14f, -0.04f);
            _rig.relEuler = new Vector3(16f, 2f, -82f);
            _rig.followDamp = 0f;          // the SHIPPED value — RAW follow, the per-step swing is visible
            _axe = axeGo.transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
        }

        // Idle: model child at rest (no bob), hand at rest pose. Drive several frames so the pose settles.
        private IEnumerator SettleIdle(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                _model.localPosition = new Vector3(0f, 0f, 0f);              // idle: modelSoleGround ≈ 0
                _hand.localPosition = HandRestLocal;                        // idle hand at rest
                yield return null;
            }
        }

        // One WALK leg: the WALK clip lifts the hand bone ~+0.66u (the Mixamo hip-lift) AND swings it per step;
        // modelSoleGround drives _model.localPosition.y to CANCEL that lift at the model level (so the rendered
        // mesh stays grounded). The cancel means the hand's WORLD-Y returns each cycle — so the raw-follow axe
        // swings per step but does not ratchet.
        private IEnumerator WalkLeg(int frames)
        {
            const float hipLift = 0.66f;     // the measured Mixamo WALK-clip body-lift (Idle 0, Walk +0.66)
            for (int i = 0; i < frames; i++)
            {
                float ph = i * 0.30f;
                float swingY = 0.30f * Mathf.Sin(ph);
                float swingZ = 0.15f * Mathf.Cos(ph);
                _hand.localPosition = HandRestLocal + new Vector3(0f, hipLift + swingY, swingZ);
                _model.localPosition = new Vector3(0f, -hipLift, 0f); // modelSoleGround cancels the hip-lift
                yield return null;
            }
        }

        // ===== AC3: per-step swing ALLOWED, CUMULATIVE drift FORBIDDEN. Measure the SETTLED axe-Y across
        // alternating walk/idle legs; it must RETURN TO BASELINE between legs (no monotonic ratchet). =====
        [UnityTest]
        public IEnumerator SettledAxeY_ReturnsToBaseline_NoCumulativeRatchet_OverFiveWalkSteps()
        {
            BuildRig();
            yield return SettleIdle(50);
            float baseline = _axe.position.y;
            Debug.Log($"[AxeWalkTest] baseline settledAxeY={baseline:F5}");

            const int steps = 6;
            var settledY = new float[steps];
            for (int s = 1; s <= steps; s++)
            {
                yield return WalkLeg(60);          // a full walk leg (the swing moment)
                yield return SettleIdle(50);       // return to idle
                float settled = _axe.position.y;
                settledY[s - 1] = settled;
                Debug.Log($"[AxeWalkTest] step {s} settledAxeY={settled:F5} " +
                          $"cumulativeDrift={(settled - baseline):+0.00000;-0.00000}");
            }

            // RATCHET SIGNATURE (headless-robust — Time.deltaTime≈0 makes absolute magnitudes tiny, but a
            // ratchet's monotone climb survives regardless): count strictly-increasing settled-Y steps. Riding
            // the RAW hand returns to baseline each leg (the modelSoleGround cancel means no net hip-lift
            // survives to world-Y), so settled-Y is FLAT (≈0 increasing steps). A regression that re-introduced
            // a body-frame anchor would integrate the hip-lift and climb (monotoneUpRun → steps-1).
            int monotoneUpRun = 0;
            for (int i = 1; i < steps; i++)
                if (settledY[i] > settledY[i - 1] + 1e-5f) monotoneUpRun++;
            float totalSpread = Mathf.Abs(settledY[steps - 1] - baseline);
            Debug.Log($"[AxeWalkTest] monotoneUpRun={monotoneUpRun}/{steps - 1} totalSpread={totalSpread:F6}");

            Assert.Less(monotoneUpRun, 2,
                $"the held axe settled-Y RATCHETED upward on {monotoneUpRun}/{steps - 1} consecutive walk steps " +
                $"(totalSpread {totalSpread:F5}u; series=[{string.Join(", ", System.Array.ConvertAll(settledY, y => y.ToString("F5")))}]) " +
                "— it must RETURN TO BASELINE between legs (no CUMULATIVE drift; 86ca9zcjn AC3). Riding the RAW " +
                "hand is bounded by construction; a re-introduced grip anchor that integrates the hip-lift ratchets.");

            // And the settled-Y must be back at baseline (bounded), not parked somewhere far off.
            Assert.Less(totalSpread, 0.02f,
                $"the held axe settled-Y is {totalSpread:F5}u from baseline after {steps} walk legs — it must " +
                "RETURN to where it rests at idle (no cumulative drift; 86ca9zcjn AC3).");
        }

        // AC1/AC2: the axe must ACTUALLY swing WITH the arm during a continuous walk (a frozen axe reads
        // DETACHED — the bug the Sponsor reversed). The axe world-Y peak-to-peak must be a LARGE fraction of the
        // raw hand's own swing — i.e. it FOLLOWS the arm, it is NOT re-locked.
        [UnityTest]
        public IEnumerator AxeFollowsTheArmSwing_DuringContinuousWalk_NotReLocked()
        {
            BuildRig();
            yield return SettleIdle(50);

            const float hipLift = 0.66f;
            float axeMin = float.PositiveInfinity, axeMax = float.NegativeInfinity;
            float handMin = float.PositiveInfinity, handMax = float.NegativeInfinity;
            for (int i = 0; i < 120; i++)
            {
                float ph = i * 0.30f;
                _hand.localPosition = HandRestLocal +
                                      new Vector3(0f, hipLift + 0.30f * Mathf.Sin(ph), 0.15f * Mathf.Cos(ph));
                _model.localPosition = new Vector3(0f, -hipLift, 0f);
                yield return null;
                if (i < 5) continue;
                float ay = _axe.position.y, hy = _hand.position.y;
                axeMin = Mathf.Min(axeMin, ay); axeMax = Mathf.Max(axeMax, ay);
                handMin = Mathf.Min(handMin, hy); handMax = Mathf.Max(handMax, hy);
            }
            float axeBand = axeMax - axeMin;
            float handBand = handMax - handMin;
            Debug.Log($"[AxeWalkTest] continuous-walk axe world-Y band={axeBand:F5} hand world-Y band={handBand:F5}");

            // The hand must actually swing (test setup) and the axe must FOLLOW it (≥70% of the hand's swing —
            // riding the raw hand, the axe pivot tracks the hand almost 1:1, minus the small fixed offset effect).
            Assert.Greater(handBand, 0.1f, $"the synthetic hand must swing (got {handBand:F4}u) — the test setup");
            Assert.Greater(axeBand, handBand * 0.7f,
                $"the held axe world-Y swung only {axeBand:F4}u while the hand swung {handBand:F4}u — the axe must " +
                "FOLLOW the arm's natural swing (≥70% of the hand swing; 86ca9zcjn AC1/AC2). A near-frozen axe is " +
                "the DETACHED-mid-stride read the Sponsor reversed — it must NOT be re-locked.");
        }

        // The facing contract must SURVIVE: the axe still TURNS with facing driven on the model child (a
        // frozen-in-world axe is also wrong). Riding the raw hand, the facing yaw passes through immediately.
        [UnityTest]
        public IEnumerator AxeStillTurnsWithFacing_Soakfix8And9xz00ContractPreserved()
        {
            BuildRig();
            _model.localRotation = Quaternion.Euler(0f, 0f, 0f);
            yield return SettleIdle(50);
            Quaternion axeAt0 = _axe.rotation;

            _model.localRotation = Quaternion.Euler(0f, 90f, 0f);
            for (int i = 0; i < 40; i++) { _hand.localPosition = HandRestLocal; _model.localPosition = Vector3.zero; yield return null; }
            Quaternion axeAt90 = _axe.rotation;

            float turn = Quaternion.Angle(axeAt0, axeAt90);
            Assert.Greater(turn, 45f,
                $"the held axe's world rotation barely moved ({turn:F1}°) when facing yawed 90° on the model child " +
                "— riding the raw hand, facing must still pass through (86ca9xz00 contract).");
        }
    }
}
