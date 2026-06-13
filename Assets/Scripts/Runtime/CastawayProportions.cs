using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Proportion measurement for the chunky-cartoon castaway guard (ticket 86ca8ca1m). The chunky
    /// read's single biggest lever is the HEAD : TOTAL-HEIGHT ratio ("heads tall") — a realistic
    /// character reads ~7-8 heads tall; the chibi base is INTRINSICALLY chunky. This helper measures
    /// that ratio off a built CastawayCharacter's serialized skinned mesh, so an EditMode guard can pin
    /// the shipped scene's castaway in the chunky band (loose — it catches a REGRESSION to a realistic
    /// many-heads-tall base, not soak tuning).
    ///
    /// MEASUREMENT:
    ///   heads-tall = totalHeight / headHeight, where
    ///     totalHeight = the BAKED skinned mesh's world-Y extent, and
    ///     headHeight  = (baked top) - (Head bone world Y) — the crown-above-the-head-bone span.
    ///   On the chibi (Head_05 bone) this measures ~1.07 STABLY (verified deterministic across
    ///   rebuilds, 2026-06-13). This is a PROPORTION FINGERPRINT for REGRESSION detection, not an
    ///   anatomical heads-tall: the BakeMesh world extent is in the SMR's bind-pose-transform space, so
    ///   the absolute number is lower than the visual "heads tall" — what matters is that it cleanly
    ///   SEPARATES the chibi (~1.07) from any realistic many-heads base (a realistic Quaternius mesh
    ///   measured far higher via the same BakeMesh path — PR #25). The guard catches a swap back to a
    ///   non-chunky base, NOT the precise toy ratio.
    ///
    /// WHY BAKE, not SkinnedMeshRenderer.bounds (the trap, unity-conventions.md §Editor-vs-runtime): a
    /// SMR DESERIALIZED from a never-rendered scene (EditMode, scene just opened) returns STALE baked
    /// bounds — a guard reading `.bounds` can measure a wrong ratio on the SHIPPED (deserialized) scene
    /// while passing on a fresh in-session build. `BakeMesh(useScale:true)` snapshots the CURRENT bone
    /// poses+scales into real vertices deterministically, independent of render state, so the same ratio
    /// is measured whether the avatar was just built or loaded from the serialized scene. Verts bake in
    /// the SMR's LOCAL space; we transform to WORLD via the SMR's localToWorldMatrix to match the Head
    /// bone's world Y (consistent space = valid ratio).
    ///
    /// Pure runtime API (BakeMesh + Transform — no UnityEditor dependency) so it runs in both EditMode
    /// tests and any future in-game proportion check.
    /// </summary>
    public static class CastawayProportions
    {
        // The Head bone token. Matched by .Contains (case-insensitive) so an armature-prefixed or
        // suffixed name ("Head_05") still hits; excludes "HeadTop_End" (the crown-end helper bone).
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
            // — the deserialized-SMR stale-bounds trap). useScale=true so any bone scale lands.
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

        /// <summary>
        /// The Head bone transform under the avatar (or null). Public so callers can assert it exists.
        ///
        /// MATCHES AGAINST THE SKINNED-MESH BONE ARRAY, not arbitrary transforms (the trap caught
        /// 2026-06-13): the chibi hierarchy carries a mesh-GROUP node literally named "head" at the model
        /// origin (worldY 0) alongside the real skeleton bone "Head_05" (worldY ~0.43). A bare
        /// GetComponentsInChildren&lt;Transform&gt; scan matched the origin "head" node first and measured a
        /// nonsense ratio. The SkinnedMeshRenderer.bones array is the ACTUAL skeleton, so restricting the
        /// search to it skips mesh-group nodes. Excludes the crown-end helper ("HeadTop_End") and any
        /// end/dummy. Falls back to a transform scan only if no SMR/bones are present.
        /// </summary>
        public static Transform FindHeadBone(Transform avatarRoot)
        {
            if (avatarRoot == null) return null;

            var smr = avatarRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.bones != null)
            {
                foreach (var bone in smr.bones)
                {
                    if (bone == null) continue;
                    string n = bone.name.ToLowerInvariant();
                    if (n.Contains(HeadBoneToken) && !n.Contains("top") && !n.Contains("end") &&
                        !n.Contains("dummy"))
                        return bone;
                }
            }

            // Fallback (no skinned mesh / bones): scan transforms, but skip the mesh-group "head" node
            // at origin by requiring the head sit ABOVE the avatar root (a real head bone is up the body).
            foreach (var t in avatarRoot.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains(HeadBoneToken) && !n.Contains("top") && !n.Contains("end") &&
                    !n.Contains("dummy") && t.position.y > avatarRoot.position.y + 0.05f)
                    return t;
            }
            return null;
        }

        // ---- The chunky band (ticket 86ca8ca1m). LOOSE on purpose: the chibi base measures ~1.07 on
        // this BakeMesh fingerprint (verified deterministic). The guard catches a REGRESSION to a
        // realistic many-heads base (e.g. an accidental swap back to the Quaternius character, which
        // measures far higher), NOT the exact chibi value. Banded around the measured ratio with
        // generous margin so it is robust to mesh/pose/importer drift but still reds on a realistic base. ----
        public const float MinHeadsTall = 0.7f;
        public const float MaxHeadsTall = 1.8f;

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
