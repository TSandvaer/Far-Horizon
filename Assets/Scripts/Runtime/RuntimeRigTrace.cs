using System.Collections;
using UnityEngine;

namespace FarHorizon
{
    // THROWAWAY runtime trace (86ca8rdkp) — gated on -rigTrace. Logs the runtime world transforms of the
    // player root, the CastawayAvatar root, the FBX Model node, the SMR, the mixamorig:Hips + RightHand
    // bones, and the HeroAxe across several frames, so we can SEE where the runtime castaway/axe actually
    // sit (the gameplay capture showed the character displaced off-spawn — diagnose-via-trace the cause).
    // Deleted before the PR.
    public class RuntimeRigTrace : MonoBehaviour
    {
        void Start()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (a == "-rigTrace") { StartCoroutine(Run()); return; }
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
    }
}
