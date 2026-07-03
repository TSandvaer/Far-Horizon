using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// 86cahnmjv — "player finger sticks out like it's broken" REGRESSION GUARD, on the REAL rig.
    ///
    /// The bug class: <see cref="CastawayFingerCurl"/> composes a grip onto the clip pose while a weapon is
    /// the selected belt item. The FINGER curl axis was measured (+local-X wraps the haft) — but the THUMB
    /// chain's local frame is MIRRORED on this rig, so the original blanket +X thumb offset moved the thumb
    /// tip AWAY from the fist (measured +0.028 at +14°X via CharacterAssetGen.ThumbOpposeAxisTrace): the
    /// thumb dangled below the haft as a stiff straight digit — the Sponsor's "broken finger". A pure
    /// synthetic-rig test cannot catch an axis-SIGN error (identity bone frames hide the mirroring), so this
    /// guard samples the REAL Idle.fbx skeleton under the REAL Breathing-Idle pose, applies the SHIPPED
    /// component offsets exactly as LateUpdate does, and asserts the OPPOSE INVARIANT: the thumb tip must
    /// move TOWARD the gripping fist, into a natural wrap band (not pierce through it either).
    ///
    /// If a future retune flips the thumb sign back to the finger family (+X), or a character/rig swap
    /// changes the thumb frame so the authored offset stops opposing, this test goes RED headlessly —
    /// before any build or soak.
    /// </summary>
    public class CastawayThumbOpposeTests
    {
        private const string IdleFbxPath = "Assets/Art/Character/Castaway/Idle.fbx";
        private const string BreathingIdleFbxPath = "Assets/Art/Character/Castaway/Breathing Idle.fbx";
        private const string BreathingIdleClip = "CastawayBreathingIdle";

        // The natural-wrap band for the thumb-tip-to-fist distance under the composed grip (metres at avatar
        // authoring scale). Measured: clip-pose dangle = 0.042 (the defect); the authored (−18,−8,−8) closes
        // to 0.0135. The band accepts a healthy wrap while failing BOTH defect classes: dangling out
        // (> 0.030 — the broken-finger read) and piercing through the fingers (< 0.004).
        private const float WrapMax = 0.030f;
        private const float PierceMin = 0.004f;

        [Test]
        public void ThumbOffset_OpposesTheGrip_OnTheRealRig()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbxPath);
            Assert.IsNotNull(fbx, "Idle.fbx (with-skin castaway) missing at " + IdleFbxPath);
            AnimationClip breathing = FindClip(BreathingIdleFbxPath, BreathingIdleClip);
            Assert.IsNotNull(breathing, "Breathing Idle clip missing — the pose under which the grip composes");

            var model = Object.Instantiate(fbx);
            var curlGo = new GameObject("curl");
            try
            {
                model.transform.localScale = Vector3.one;
                var smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.IsNotNull(smr, "no SkinnedMeshRenderer on the castaway FBX");

                Transform hand = Bone(smr, "righthand");
                Transform[] thumb =
                {
                    Bone(smr, "righthandthumb1"), Bone(smr, "righthandthumb2"), Bone(smr, "righthandthumb3"),
                };
                Transform[] fingers =
                {
                    Bone(smr, "righthandindex1"), Bone(smr, "righthandindex2"), Bone(smr, "righthandindex3"),
                    Bone(smr, "righthandmiddle1"), Bone(smr, "righthandmiddle2"), Bone(smr, "righthandmiddle3"),
                    Bone(smr, "righthandring1"), Bone(smr, "righthandring2"), Bone(smr, "righthandring3"),
                };
                Transform index2 = Bone(smr, "righthandindex2");
                Transform middle2 = Bone(smr, "righthandmiddle2");
                Assert.IsNotNull(hand, "righthand wrist bone missing from the SMR bone array");
                foreach (var t in thumb) Assert.IsNotNull(t, "a thumb bone is missing from the SMR bone array");
                Assert.IsNotNull(index2, "righthandindex2 missing");
                Assert.IsNotNull(middle2, "righthandmiddle2 missing");

                // The SHIPPED offsets, composed exactly as CastawayFingerCurl.LateUpdate does.
                var curl = curlGo.AddComponent<CastawayFingerCurl>();
                curl.RebuildCached();
                var fingerOffset = Quaternion.Euler(curl.fingerCurlDeg, 0f, 0f);
                var thumbOffset = curl.ThumbOffset;

                // Grip-state pose: breathing idle + the finger fist (what the player sees while holding).
                breathing.SampleAnimation(model, breathing.length * 0.2f);
                foreach (var f in fingers) if (f != null) f.localRotation = f.localRotation * fingerOffset;

                Vector3 fistMid = (index2.position + middle2.position) * 0.5f;
                float distBefore = Vector3.Distance(TipWorld(thumb), fistMid);

                foreach (var t in thumb) t.localRotation = t.localRotation * thumbOffset;
                fistMid = (index2.position + middle2.position) * 0.5f;
                float distAfter = Vector3.Distance(TipWorld(thumb), fistMid);

                Assert.Less(distAfter, distBefore - 0.005f,
                    $"the shipped thumb offset must move the thumb tip TOWARD the gripping fist (oppose) — " +
                    $"before={distBefore:F4} after={distAfter:F4}. A non-opposing offset is the " +
                    "'finger sticks out like it's broken' defect (86cahnmjv).");
                Assert.Less(distAfter, WrapMax,
                    $"thumb tip must land in the natural-wrap band (< {WrapMax}) — got {distAfter:F4}: " +
                    "the thumb still dangles off the grip (the broken-finger read).");
                Assert.Greater(distAfter, PierceMin,
                    $"thumb tip must not pierce through the fist (> {PierceMin}) — got {distAfter:F4}.");
            }
            finally
            {
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(curlGo);
            }
        }

        [Test]
        public void ThumbCurlEuler_StaysInTheMeasuredOpposeFamily_NegativeX()
        {
            var go = new GameObject("curl");
            try
            {
                var curl = go.AddComponent<CastawayFingerCurl>();
                Assert.Less(curl.thumbCurlEuler.x, 0f,
                    "the thumb curl X must stay NEGATIVE — the measured oppose family on this rig " +
                    "(ThumbOpposeAxisTrace: +X pushes the thumb OUT of the grip — the 86cahnmjv defect). " +
                    "If a rig swap legitimately changes the thumb frame, re-run the trace and update " +
                    "BOTH the offset and this guard from the new measurement.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static Vector3 TipWorld(Transform[] thumb)
        {
            Transform end = thumb[2].childCount > 0 ? thumb[2].GetChild(0) : null;
            return end != null ? end.position : thumb[2].position + (thumb[2].position - thumb[1].position);
        }

        private static Transform Bone(SkinnedMeshRenderer smr, string token)
        {
            foreach (var b in smr.bones)
            {
                if (b == null) continue;
                string n = b.name.ToLowerInvariant();
                int colon = n.LastIndexOf(':');
                if (colon >= 0) n = n.Substring(colon + 1);
                if (n == token) return b;
            }
            return null;
        }

        private static AnimationClip FindClip(string fbxPath, string token)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is AnimationClip clip && clip.name.Contains(token) && !clip.name.StartsWith("__preview__"))
                    return clip;
            return null;
        }
    }
}
