using System.Collections;
using UnityEngine;

namespace FarHorizon
{
    // THROWAWAY runtime trace (86ca8rdkp) — gated on -rigTrace / -groundTrace / -armTrace. Deleted before the PR.
    //
    //  -rigTrace   : dumps the runtime world transforms of the player root, the CastawayAvatar root, the FBX
    //                Model node, the SMR, the mixamorig:Hips + RightHand bones, and the HeroAxe (legs-up class).
    //  -groundTrace: (RE-SOAK soak-fix #2 "he STILL walks elevated") drives the agent toward the SEAWARD
    //                foreshore (where the Zone-D terrain DIPS below the flat NavMesh slab) and per-frame dumps
    //                EVERY Ground-layer raycast hit (sorted high->low) + which one a single Physics.Raycast
    //                picks (the snap's current source) + the VISIBLE-terrain hit (renderer-enabled collider) +
    //                the resulting avatar-feet Y. This is the diagnose-via-trace that PROVES whether the snap
    //                snaps to the wrong (flat-slab) surface on the foreshore.
    //  -armTrace   : dumps the right/left upper-arm bone LOCAL->world axis mapping (which local axis spreads
    //                the hand outward / raises it) so the CastawayArmPose offset axes are measured, not guessed.
    public class RuntimeRigTrace : MonoBehaviour
    {
        void Start()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
            {
                if (a == "-rigTrace") { StartCoroutine(Run()); return; }
                if (a == "-groundTrace") { StartCoroutine(RunGroundTrace()); return; }
                if (a == "-armTrace") { StartCoroutine(RunArmTrace()); return; }
            }
        }

        private IEnumerator Run()
        {
            for (int i = 0; i < 60; i++) yield return null; // let agent/anim settle

            var cc = Object.FindAnyObjectByType<CastawayCharacter>();
            if (cc == null) { Debug.Log("[rig-trace] NO castaway"); Application.Quit(0); yield break; }

            var agent = cc.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
            Debug.Log($"[rig-trace] playerRoot world={(agent != null ? agent.transform.position.ToString("F3") : "?")} scale={(agent != null ? agent.transform.lossyScale.ToString("F3") : "?")}");
            Debug.Log($"[rig-trace] avatarRoot world={cc.transform.position.ToString("F3")} localScale={cc.transform.localScale.ToString("F3")} lossyScale={cc.transform.lossyScale.ToString("F3")}");

            var smr = cc.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null)
            {
                smr.updateWhenOffscreen = true;
                Debug.Log($"[rig-trace] SMR node='{smr.transform.name}' world={smr.transform.position.ToString("F3")} localScale={smr.transform.localScale.ToString("F3")} lossyScale={smr.transform.lossyScale.ToString("F3")}");
                Debug.Log($"[rig-trace] SMR worldBounds center={smr.bounds.center.ToString("F3")} size={smr.bounds.size.ToString("F3")}");
            }
            var anim = cc.GetComponentInChildren<Animator>(true);
            if (anim != null)
                Debug.Log($"[rig-trace] Animator applyRootMotion={anim.applyRootMotion} updateMode={anim.updateMode} avatar={(anim.avatar != null ? anim.avatar.name : "<null>")} ctrl={(anim.runtimeAnimatorController != null)}");

            foreach (var t in cc.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant(); int c = n.LastIndexOf(':'); if (c >= 0) n = n.Substring(c + 1);
                if (n == "hips" || n == "righthand")
                    Debug.Log($"[rig-trace] bone '{t.name}' world={t.position.ToString("F3")} lossyScale={t.lossyScale.ToString("F3")}");
            }
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "HeroAxe")
                    Debug.Log($"[rig-trace] HeroAxe world={t.position.ToString("F3")} lossyScale={t.lossyScale.ToString("F3")}");

            Application.Quit(0);
        }

        // RE-SOAK soak-fix #2 — "he STILL walks elevated". Drive the agent toward the SEAWARD foreshore and
        // dump, per frame: ALL Ground-layer raycast hits (sorted by Y, high->low), the single-raycast pick
        // (what the snap currently uses), the topmost RENDERER-ENABLED (visible) terrain hit, and the feet Y.
        private IEnumerator RunGroundTrace()
        {
            for (int i = 0; i < 60; i++) yield return null; // let agent/anim settle

            var cc = Object.FindAnyObjectByType<CastawayCharacter>();
            if (cc == null) { Debug.Log("[ground-trace] NO castaway"); Application.Quit(0); yield break; }
            var agent = cc.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
            Transform root = agent != null ? agent.transform : cc.transform.parent;
            int groundMask = 1 << LayerMask.NameToLayer("Ground");

            // Drive the agent SEAWARD (−Z) toward the foreshore where the terrain dips below the flat slab.
            // The campfire is at (4,-8); aim a touch past it so we cross the dipping band while WALKING.
            if (agent != null && agent.isOnNavMesh)
            {
                agent.SetDestination(new Vector3(2f, 0f, -9f));
                Debug.Log("[ground-trace] driving agent SEAWARD to (2,0,-9) across the dipping foreshore");
            }
            else Debug.Log("[ground-trace] agent not on NavMesh — sampling at spawn only");

            for (int frame = 0; frame < 140; frame++)
            {
                Vector3 rp = root.position;
                Vector3 origin = rp + Vector3.up * 3f;
                var hits = Physics.RaycastAll(origin, Vector3.down, 15f, groundMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y)); // high -> low

                // The single-ray pick = the CLOSEST hit to the origin (Unity returns nearest for Raycast).
                bool single = Physics.Raycast(origin, Vector3.down, out var singleHit, 15f, groundMask, QueryTriggerInteraction.Ignore);

                // The VISIBLE terrain hit = the topmost hit whose collider has a renderer-ENABLED MeshRenderer
                // (the flat NavMesh slab's renderer is disabled — it's a collision proxy only).
                float visY = float.NaN; string visName = "<none>";
                foreach (var h in hits)
                {
                    var mr = h.collider.GetComponent<MeshRenderer>();
                    if (mr != null && mr.enabled) { visY = h.point.y; visName = h.collider.name; break; }
                }

                bool walking = agent != null && agent.velocity.sqrMagnitude > 0.02f;
                if (frame % 10 == 0 || frame == 139)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append($"[ground-trace] f{frame} walk={walking} root=({rp.x:F2},{rp.z:F2}) feetY={cc.transform.position.y:F3} snapLocalY={cc.GroundSnapLocalY:F3} | hits=");
                    foreach (var h in hits)
                    {
                        var mr = h.collider.GetComponent<MeshRenderer>();
                        sb.Append($"[{h.collider.name} Y={h.point.y:F3} rend={(mr != null && mr.enabled)}] ");
                    }
                    sb.Append($"| singlePick={(single ? singleHit.collider.name + " Y=" + singleHit.point.y.ToString("F3") : "MISS")}");
                    sb.Append($" | visibleTerrain={visName} Y={visY:F3}");
                    Debug.Log(sb.ToString());
                }
                yield return null;
            }

            Application.Quit(0);
        }

        // -armTrace — measure the upper-arm bone LOCAL->world axis mapping so CastawayArmPose's spread/raise
        // axes are MEASURED, not guessed (the first-guess-axis overturned lesson). For each upper-arm bone,
        // probe how a +5° rotation about each LOCAL axis moves the child hand in WORLD space.
        private IEnumerator RunArmTrace()
        {
            for (int i = 0; i < 60; i++) yield return null;
            var cc = Object.FindAnyObjectByType<CastawayCharacter>();
            if (cc == null) { Debug.Log("[arm-trace] NO castaway"); Application.Quit(0); yield break; }

            foreach (var t in cc.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant(); int c = n.LastIndexOf(':'); if (c >= 0) n = n.Substring(c + 1);
                if (n != "rightarm" && n != "leftarm") continue;
                Transform hand = null;
                foreach (var h in t.GetComponentsInChildren<Transform>(true))
                {
                    string hn = h.name.ToLowerInvariant(); int hc = hn.LastIndexOf(':'); if (hc >= 0) hn = hn.Substring(hc + 1);
                    if (hn == (n == "rightarm" ? "righthand" : "lefthand")) { hand = h; break; }
                }
                if (hand == null) continue;
                Vector3 rest = hand.position;
                Quaternion saved = t.localRotation;
                foreach (var (axisName, axis) in new[] { ("X", Vector3.right), ("Y", Vector3.up), ("Z", Vector3.forward) })
                {
                    t.localRotation = saved * Quaternion.AngleAxis(5f, axis);
                    Vector3 d = hand.position - rest;
                    Debug.Log($"[arm-trace] {n} +5°local{axisName} -> hand world delta=({d.x:F4},{d.y:F4},{d.z:F4})");
                    t.localRotation = saved;
                }
            }
            Application.Quit(0);
        }
    }
}
