using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarHorizon.Spike.EditorTools
{
    // THROWAWAY diagnose-via-trace: open the built spike scene and dump the avatar's REAL world bounds +
    // transform chain so the capture framing is fed ground truth (the captures came out as a giant smear =
    // camera buried in the mesh = the bounds the capture computed are wrong).
    public static class Hyper3DSpikeSceneDiag
    {
        public static void Dump()
        {
            EditorSceneManager.OpenScene(Hyper3DSpikeGen.ScenePath, OpenSceneMode.Single);
            var smr = Object.FindFirstObjectByType<SkinnedMeshRenderer>(FindObjectsInactive.Include);
            if (smr == null) { Debug.LogError("[SCENEDIAG] no SMR in spike scene"); if (Application.isBatchMode) EditorApplication.Exit(1); return; }

            Debug.Log($"[SCENEDIAG] SMR '{smr.name}' .bounds(world)=center{smr.bounds.center} size{smr.bounds.size}");
            Debug.Log($"[SCENEDIAG] SMR.localBounds=center{smr.localBounds.center} size{smr.localBounds.size}");
            var t = smr.transform;
            while (t != null)
            {
                Debug.Log($"[SCENEDIAG]   xform '{t.name}' localPos{t.localPosition} localScale{t.localScale} lossyScale{t.lossyScale} worldPos{t.position}");
                t = t.parent;
            }

            // What BakeMesh(useScale:true) actually returns (the capture's basis).
            var mesh = new Mesh();
            smr.BakeMesh(mesh, true);
            var verts = mesh.vertices;
            if (verts.Length > 0)
            {
                var lb = new Bounds(verts[0], Vector3.zero);
                for (int i = 1; i < verts.Length; i++) lb.Encapsulate(verts[i]);
                Debug.Log($"[SCENEDIAG] BakeMesh(useScale) LOCAL-vert bounds: center{lb.center} size{lb.size} (verts={verts.Length})");
                // capture applies TRS(pos,rot,one) — show that world bound
                Matrix4x4 m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                var wb = new Bounds(m.MultiplyPoint3x4(verts[0]), Vector3.zero);
                for (int i = 1; i < verts.Length; i++) wb.Encapsulate(m.MultiplyPoint3x4(verts[i]));
                Debug.Log($"[SCENEDIAG] capture-basis WORLD bounds (TRS pos/rot/one): center{wb.center} size{wb.size}");
            }
            Object.DestroyImmediate(mesh);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
