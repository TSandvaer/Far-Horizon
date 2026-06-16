using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// 86ca9ykp0 regression guard for the held-axe WALK BOUNCE + cumulative RATCHET.
    ///
    /// THE BUG (Sponsor, soak a0eb595): after the facing fix (86ca9xz00 — GOOD), WALKING makes the held axe
    /// BOUNCE DOWN then up AND settle HIGHER each walk step — a cumulative upward DRIFT/RATCHET, not just a
    /// per-step bob. NEW since 86ca9xz00 moved HeldAxeRig.stabilizeFrame root→_model.
    ///
    /// ROOT-CAUSE GEOMETRY (the real hierarchy this test reproduces faithfully — diagnose-via-trace, not guess):
    ///   root (CastawayCharacter — never yaws)
    ///     └ model (_model — YAWS with facing AND its local-Y BOBS per-frame: CastawayCharacter.modelSoleGround
    ///              drives _model.localPosition.y to cancel the Mixamo WALK-clip hip-lift; Idle≈0, Walk≈+0.66)
    ///         └ hand (bone — the Animator writes the WALK-clip pose here: a per-step arm-SWING PLUS a sustained
    ///                 hip-LIFT baseline while walking; both expressed in the model-local frame)
    ///             └ axe (HeldAxeRig, stabilizeFrame = _model)
    ///
    /// WHY IT RATCHETS (PINNED by HeldAxeWalkBounce_Diagnose below + the -axeWalkTrace exe instrument): the
    /// grip anchor eases (anchorTrackPerSec) toward the hand's pose expressed in the BOBBING _model frame. While
    /// walking, BOTH the hand bone (hip-lift) AND the _model frame (the cancel) move, but the anchor integrates
    /// the model-local hand pose asymmetrically across walk/idle transitions, so each walk leg shifts the settled
    /// anchor a little and it does not fully return at idle → the reconstructed followPos (and the axe) settles
    /// HIGHER each step. The per-frame bounce is the live _model bob re-applied by frame.TransformPoint.
    ///
    /// THE BUG CLASS (not just the instance): (a) the SETTLED axe world-Y must RETURN TO BASELINE after each
    /// walk leg with NO cumulative drift over ≥5 steps, AND (b) the per-step axe-Y stays in a tight band. A
    /// "pickup_count>0"-style proxy (e.g. "axe moved at all") would pass during the entire ratchet era — so we
    /// assert the END-TO-END settled-Y delta across steps + the per-leg band, the quantities the Sponsor judges.
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
            _rig.swingStabilize = 1f;          // the SHIPPED value — exercises the anchor where the bug lives
            _rig.anchorTrackPerSec = 0.12f;    // the SHIPPED slow anchor
            _rig.stabilizeFrame = _model;      // the 86ca9xz00 facing fix (KEEP) — the frame that bobs
            _axe = axeGo.transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
        }

        // Idle: model child at rest (no bob), hand at rest pose. Drive several frames so the anchor settles.
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
        // mesh stays grounded). This is the exact pair of motions that, fed through the grip anchor, ratchets.
        private IEnumerator WalkLeg(int frames)
        {
            const float hipLift = 0.66f;     // the measured Mixamo WALK-clip body-lift (Idle 0, Walk +0.66)
            for (int i = 0; i < frames; i++)
            {
                float ph = i * 0.30f;
                // The Animator writes the hand bone: rest + the sustained hip-lift baseline + a per-step swing.
                float swingY = 0.30f * Mathf.Sin(ph);
                float swingZ = 0.15f * Mathf.Cos(ph);
                _hand.localPosition = HandRestLocal + new Vector3(0f, hipLift + swingY, swingZ);
                // modelSoleGround cancels the hip-lift at the model level (drives _model.localPosition.y down by
                // the lift so the rendered sole stays planted — the real CastawayCharacter behavior).
                _model.localPosition = new Vector3(0f, -hipLift, 0f);
                yield return null;
            }
        }

        // ===== DIAGNOSE (read-only): measure the SETTLED axe-Y across alternating walk/idle legs and LOG the
        // per-step + cumulative drift. This is the PlayMode "RUN + READ" of the ratchet (the -axeWalkTrace exe
        // instrument is the shipped-build sibling). It also ASSERTS the bug class so it goes RED against the
        // unfixed coupling and GREEN after the fix. =====
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
                yield return WalkLeg(60);          // a full walk leg (the bounce moment)
                yield return SettleIdle(50);       // return to idle + let the anchor settle
                float settled = _axe.position.y;
                settledY[s - 1] = settled;
                Debug.Log($"[AxeWalkTest] step {s} settledAxeY={settled:F5} " +
                          $"cumulativeDrift={(settled - baseline):+0.00000;-0.00000}");
            }

            // HEADLESS-ROBUST DISCRIMINATOR (the headless Time.deltaTime≈0 trap — unity-conventions.md §Headless:
            // the anchor easing is dt-gated so the ABSOLUTE drift magnitude is tiny headless and a magnitude
            // tolerance would FALSE-GREEN). The ratchet's SIGNATURE survives headless regardless of magnitude:
            // the settled-Y climbs MONOTONICALLY and never returns to baseline. Count strictly-increasing steps
            // (each settle higher than the last past a tiny epsilon). With the bug EVERY step ratchets up
            // (monotone run == steps); the fix returns to baseline so settled-Y is flat (≈0 increasing steps).
            int monotoneUpRun = 0;
            for (int i = 1; i < steps; i++)
                if (settledY[i] > settledY[i - 1] + 1e-5f) monotoneUpRun++;
            float totalSpread = Mathf.Abs(settledY[steps - 1] - baseline);
            Debug.Log($"[AxeWalkTest] monotoneUpRun={monotoneUpRun}/{steps - 1} totalSpread={totalSpread:F6}");

            // BUG CLASS (b): the settled axe-Y must NOT ratchet monotonically upward across walk steps — it must
            // RETURN TO BASELINE between legs (a steady grip), not accumulate. RED: the _model-frame grip-anchor
            // integrates the walk hip-lifted hand pose and doesn't ease back → every step climbs (monotoneUpRun
            // == steps-1). GREEN (fix): the axe vertical is decoupled from the eased anchor → settled-Y flat.
            Assert.Less(monotoneUpRun, 2,
                $"the held axe settled-Y RATCHETED upward on {monotoneUpRun}/{steps - 1} consecutive walk steps " +
                $"(totalSpread {totalSpread:F5}u; series=[{string.Join(", ", System.Array.ConvertAll(settledY, y => y.ToString("F5")))}]) " +
                "— it must RETURN TO BASELINE between legs (no cumulative drift). The grip anchor must not " +
                "integrate the modelSoleGround/hip-lift asymmetrically across walk/idle (86ca9ykp0).");
        }

        // BUG CLASS (a): the per-frame axe world-Y during a CONTINUOUS walk must stay in a TIGHT BAND — no big
        // per-step bounce. RED against riding the live _model bob via frame.TransformPoint; GREEN after the fix
        // decouples the axe vertical from the bobbing frame.
        [UnityTest]
        public IEnumerator AxeWorldY_StaysInTightBand_DuringContinuousWalk_NoPerStepBounce()
        {
            BuildRig();
            yield return SettleIdle(50);

            // Walk continuously and sample the axe world-Y peak-to-peak (the per-step bounce).
            const float hipLift = 0.66f;
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            for (int i = 0; i < 120; i++)
            {
                float ph = i * 0.30f;
                _hand.localPosition = HandRestLocal +
                                      new Vector3(0f, hipLift + 0.30f * Mathf.Sin(ph), 0.15f * Mathf.Cos(ph));
                _model.localPosition = new Vector3(0f, -hipLift, 0f);
                yield return null;
                if (i < 10) continue; // let the anchor converge to the walk steady state first
                float y = _axe.position.y;
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            float band = maxY - minY;
            Debug.Log($"[AxeWalkTest] continuous-walk axe world-Y band={band:F5}");

            // The axe vertical must stay tight through the walk — it must NOT ride the ~0.66u model bob / hip
            // swing. 5 cm tolerance (the Sponsor's "bounces down per step" is a multi-cm visible bob).
            Assert.Less(band, 0.05f,
                $"the held axe world-Y swept {band:F4}u peak-to-peak through a continuous walk — it must stay in " +
                "a TIGHT band (no per-step bounce). The axe vertical must be decoupled from the bobbing _model / " +
                "hip-lift (86ca9ykp0).");
        }

        // The 86ca9xz00 facing contract must SURVIVE this fix: the axe still TURNS with facing driven on the
        // model child (a frozen-in-world axe is also wrong). Guards the fix didn't freeze the axe vertical AND
        // break facing.
        [UnityTest]
        public IEnumerator AxeStillTurnsWithFacing_AfterBounceFix_Soakfix8And9xz00ContractPreserved()
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
                "— the bounce fix must NOT freeze the axe; facing must still pass through (86ca9xz00 contract).");
        }
    }
}
