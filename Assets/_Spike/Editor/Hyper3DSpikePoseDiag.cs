using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarHorizon.Spike.EditorTools
{
    // THROWAWAY diagnose-via-trace: the shipped capture renders a smooth amber CONE, not a chibi. Isolate
    // whether it's (a) the import/mesh itself, (b) the Humanoid retarget, or (c) the bind pose, by sampling
    // key skinned vertices + bone world positions at REST (bind) vs with the Idle clip sampled, directly in
    // the editor — no build needed. If head/feet collapse toward the hips, it's a retarget/avatar mangle.
    public static class Hyper3DSpikePoseDiag
    {
        public static void Dump()
        {
            EditorSceneManager.OpenScene(Hyper3DSpikeGen.ScenePath, OpenSceneMode.Single);
            var animator = Object.FindFirstObjectByType<Animator>(FindObjectsInactive.Include);
            var smr = Object.FindFirstObjectByType<SkinnedMeshRenderer>(FindObjectsInactive.Include);
            if (animator == null || smr == null) { Debug.LogError("[POSEDIAG] missing animator/smr"); Bail(1); return; }

            Debug.Log($"[POSEDIAG] animator.isHuman={animator.isHuman} hasController={animator.runtimeAnimatorController != null} avatar={(animator.avatar != null ? animator.avatar.name : "NULL")}");

            // Key humanoid bones (if the avatar is human, GetBoneTransform resolves them).
            if (animator.isHuman)
            {
                foreach (var hb in new[] { HumanBodyBones.Head, HumanBodyBones.Hips, HumanBodyBones.LeftFoot,
                                           HumanBodyBones.RightHand, HumanBodyBones.Spine })
                {
                    var t = animator.GetBoneTransform(hb);
                    Debug.Log($"[POSEDIAG]   bone {hb}: " + (t != null ? $"world{t.position}" : "NULL"));
                }
            }

            // Baked-mesh extent (the real skinned silhouette) — but compute it CORRECTLY (the SMR node has
            // localScale 100, so use smr.bounds which is already world AABB).
            Debug.Log($"[POSEDIAG] smr.bounds world: center{smr.bounds.center} size{smr.bounds.size}");

            // Mesh vertex span in the SMR mesh's own local space (sharedMesh, pre-skin) — proves the source
            // mesh is a real humanoid, not already-collapsed geometry.
            var sm = smr.sharedMesh;
            if (sm != null)
                Debug.Log($"[POSEDIAG] sharedMesh '{sm.name}' bounds local: center{sm.bounds.center} size{sm.bounds.size} verts={sm.vertexCount} bones(bindposes)={sm.bindposes.Length}");

            // SMR.bones array sanity: how many, and do they resolve to the mixamorig skeleton?
            Debug.Log($"[POSEDIAG] smr.bones.Length={smr.bones.Length} rootBone={(smr.rootBone != null ? smr.rootBone.name : "NULL")}");
            int nullBones = 0; foreach (var b in smr.bones) if (b == null) nullBones++;
            Debug.Log($"[POSEDIAG] smr null bones={nullBones}");

            Bail(0);
        }

        private static void Bail(int code) { if (Application.isBatchMode) EditorApplication.Exit(code); }
    }
}
