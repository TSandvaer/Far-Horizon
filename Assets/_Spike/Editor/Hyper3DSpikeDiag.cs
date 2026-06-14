using UnityEditor;
using UnityEngine;

namespace FarHorizon.Spike.EditorTools
{
    // THROWAWAY diagnose-via-trace: dump every sub-asset name in the two spike FBX so the clip-name
    // tokens are known empirically, not guessed (the loop-match returned 0 — the names aren't "Idle"/"Walk").
    public static class Hyper3DSpikeDiag
    {
        public static void Dump()
        {
            foreach (var fbx in new[] { Hyper3DSpikeGen.IdleFbx, Hyper3DSpikeGen.WalkFbx })
            {
                Debug.Log("[SPIKEDIAG] ===== " + fbx + " =====");
                var objs = AssetDatabase.LoadAllAssetsAtPath(fbx);
                foreach (var o in objs)
                {
                    string extra = "";
                    if (o is AnimationClip c) extra = $" len={c.length:F2}s looping={c.isLooping} legacy={c.legacy}";
                    if (o is Avatar a) extra = $" valid={a.isValid} human={a.isHuman}";
                    Debug.Log($"[SPIKEDIAG]   {o.GetType().Name}: '{o.name}'{extra}");
                }
                var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (importer != null)
                {
                    Debug.Log("[SPIKEDIAG]   -- defaultClipAnimations --");
                    foreach (var dc in importer.defaultClipAnimations)
                        Debug.Log($"[SPIKEDIAG]     defaultClip '{dc.name}' take='{dc.takeName}'");
                }
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
