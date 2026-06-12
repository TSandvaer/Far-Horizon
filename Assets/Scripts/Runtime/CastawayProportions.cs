using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Proportion measurement for the cartoonish-stylization guard (ticket 86ca8ca1m, Uma's
    /// castaway-style-v2 brief §2/§4). The chunky read's single biggest lever is the HEAD : TOTAL-HEIGHT
    /// ratio ("heads tall") — the reference reads ~3 heads tall vs a realistic ~7-8. This helper measures
    /// that ratio off a built CastawayCharacter's serialized skinned mesh, so an EditMode guard can pin
    /// the shipped scene's castaway in the chunky band (loose, soak-tunable — a future rig/mesh edit that
    /// flattens the head fails CI the same way the luma guard catches a dark shirt).
    ///
    /// MEASUREMENT (validated empirically on the live mesh, 2026-06-12 — NOT a guess):
    ///   heads-tall = totalHeight / headHeight, where
    ///     totalHeight = the BAKED skinned mesh's world-Y extent (crown to feet), and
    ///     headHeight  = (baked top) - (Head bone world Y) — the crown-above-neck span.
    ///
    /// WHY BAKE, not SkinnedMeshRenderer.bounds (the trap diagnosed 2026-06-12): a FRESHLY-BUILT SMR
    /// reports bounds that reflect a bone-baseline scale, but a SMR DESERIALIZED from a never-rendered
    /// scene (EditMode, scene just opened) returns STALE baked bounds that ignore the scale — so a guard
    /// reading `.bounds` measured the un-chunked ~7.9 heads on the SHIPPED scene while passing on a
    /// fresh in-session build. `BakeMesh(useScale:true)` snapshots the CURRENT bone poses+scales into
    /// real vertices deterministically, independent of render state, so the same ratio is measured
    /// whether the avatar was just built or loaded from the serialized scene. Verts bake in the SMR's
    /// LOCAL space; we transform to WORLD via the SMR's localToWorldMatrix to match the Head bone's
    /// world Y (consistent space = valid ratio).
    ///
    /// Pure runtime API (BakeMesh + Transform — no UnityEditor dependency) so it runs in both EditMode
    /// tests and any future in-game proportion check.
    /// </summary>
    public static class CastawayProportions
    {
        // The Head bone token (Quaternius Animated-Men armature — verified by probing the FBX). Matched
        // by .Contains (case-insensitive) so an armature-prefixed name ("HumanArmature|Head") still hits.
        private const string HeadBoneToken = "head";

        /// <summary>
        /// Heads-tall ratio of a built castaway avatar. Returns NaN if the avatar has no skinned mesh or
        /// no Head bone (so callers can fail loudly rather than silently pass a bogus ratio).
        /// </summary>
        public static float MeasureHeadsTall(CastawayCharacter castaway)
        {
            if (castaway == null) return float.NaN;
            var smr = castaway.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null || smr.sharedMesh == null) return float.NaN;

            Transform head = FindHeadBone(castaway.transform);
            if (head == null) return float.NaN;

            // Bake the CURRENT bone poses+scales into real verts (deterministic, render-state-independent
            // — the deserialized-SMR stale-bounds trap). useScale=true so the bone-baseline scale lands.
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            var verts = baked.vertices;
            if (verts.Length == 0)
            {
                DestroyMesh(baked);
                return float.NaN;
            }

            // Transform baked-local verts to WORLD so the extent matches the Head bone's world Y.
            Matrix4x4 l2w = smr.transform.localToWorldMatrix;
            float topY = float.NegativeInfinity, botY = float.PositiveInfinity;
            for (int i = 0; i < verts.Length; i++)
            {
                float y = l2w.MultiplyPoint3x4(verts[i]).y;
                if (y > topY) topY = y;
                if (y < botY) botY = y;
            }
            DestroyMesh(baked);

            float total = topY - botY;
            float headHeight = topY - head.position.y;
            if (headHeight <= 0.0001f || total <= 0.0001f) return float.NaN;
            return total / headHeight;
        }

        /// <summary>The Head bone transform under the avatar (or null). Public so callers can assert it exists.</summary>
        public static Transform FindHeadBone(Transform avatarRoot)
        {
            if (avatarRoot == null) return null;
            foreach (var t in avatarRoot.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLowerInvariant().Contains(HeadBoneToken))
                    return t;
            return null;
        }

        // ---- The chunky band (Uma castaway-style-v2 §2/§4). LOOSE on purpose: the baseline is ~3.0
        // heads, the Sponsor soak may push toward 2.5 ("cuter") or the mesh may sit a touch above 3.0 —
        // the guard catches a REGRESSION to the realistic ~7-8 heads (or a bobblehead < 2.5), not the
        // soak-tuning within the toy range. ----
        public const float MinHeadsTall = 2.5f;
        public const float MaxHeadsTall = 3.3f;

        public static bool IsChunky(float headsTall) =>
            !float.IsNaN(headsTall) && headsTall >= MinHeadsTall && headsTall <= MaxHeadsTall;

        // Destroy a temp mesh safely in both edit-time (tests / authoring) and play-time contexts.
        private static void DestroyMesh(Mesh m)
        {
            if (m == null) return;
            if (Application.isPlaying) Object.Destroy(m);
            else Object.DestroyImmediate(m);
        }
    }
}
