using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using FarHorizon;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// IMGUI Layout-pass opt-out guard (ticket 86cahhfp4, C2a — poly-style plan §5 item 8).
    ///
    /// Every OnGUI in the runtime assembly draws with EXPLICIT Rects only (zero GUILayout.* calls — verified
    /// by grep at authoring time and structurally re-verified here per component instance). IMGUI still runs a
    /// full Layout event pass per frame for every OnGUI component unless the component sets
    /// <c>useGUILayout = false</c> — pure per-frame waste for explicit-Rect UIs (an extra OnGUI invocation per
    /// component per frame + layout bookkeeping). C2a sets the opt-out in every component's Awake.
    ///
    /// This guard is SELF-EXTENDING: it reflects over the whole FarHorizon.Runtime assembly for MonoBehaviours
    /// that declare OnGUI, instantiates each on a bare GameObject, invokes its Awake (Awake does not auto-run
    /// in EditMode), and asserts the layout pass is opted out. A FUTURE OnGUI component that forgets
    /// <c>useGUILayout = false</c> (or forgets the Awake entirely) fails this test by name — the same
    /// reflect-the-assembly pattern as StaticStateResetTests. (Precedent for reflection-invoking Awake:
    /// HeldToolRigTests.)
    ///
    /// If a future OnGUI component genuinely NEEDS GUILayout (it must then keep useGUILayout = true), do NOT
    /// weaken this guard — exempt that one type via the explicit allowlist below with a comment naming why,
    /// so the exemption is a reviewed decision rather than a silent default.
    /// </summary>
    public class ImguiLayoutPassTests
    {
        // Types allowed to KEEP the Layout pass (must actually use GUILayout.*). Empty today — the whole
        // runtime draws with explicit Rects. Add a type here ONLY with a justifying comment.
        private static readonly Type[] LayoutAllowlist = Array.Empty<Type>();

        // The 11 components C2a opted out at authoring time (2026-07-02). The scan below is the real guard;
        // this count only pins that the scan is actually FINDING the OnGUI family (an empty scan must not
        // false-green the suite if a refactor breaks the reflection).
        private const int KnownOnGuiComponentsAtAuthoringTime = 11;

        [Test]
        public void EveryOnGuiComponent_OptsOutOfTheImguiLayoutPass_InAwake()
        {
            List<Type> onGuiTypes = FindRuntimeOnGuiComponentTypes();

            Assert.GreaterOrEqual(onGuiTypes.Count, KnownOnGuiComponentsAtAuthoringTime,
                "the reflection scan must find at least the 11 OnGUI components known at C2a authoring time — " +
                "a smaller count means the scan itself broke (which would false-green everything it guards). " +
                $"Found: {string.Join(", ", onGuiTypes)}");

            var failures = new List<string>();
            foreach (Type t in onGuiTypes)
            {
                if (Array.IndexOf(LayoutAllowlist, t) >= 0) continue;

                var go = new GameObject("imgui-layout-guard-" + t.Name);
                try
                {
                    var comp = (MonoBehaviour)go.AddComponent(t);
                    InvokeDeclaredAwake(comp);
                    if (comp.useGUILayout)
                        failures.Add(t.Name);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            Assert.IsEmpty(failures,
                "every OnGUI component must set useGUILayout = false in Awake (C2a, 86cahhfp4): the runtime " +
                "draws with explicit Rects only, so the IMGUI Layout event pass is pure per-frame waste. " +
                "Offenders (add the Awake opt-out, or — only if the component truly uses GUILayout.* — add it " +
                "to the reviewed allowlist in this test): " + string.Join(", ", failures));
        }

        // Every MonoBehaviour in the FarHorizon.Runtime assembly that declares an OnGUI anywhere in its
        // (non-engine) inheritance chain. Concrete + attachable types only.
        private static List<Type> FindRuntimeOnGuiComponentTypes()
        {
            Assembly runtime = typeof(BootHud).Assembly; // the FarHorizon.Runtime asmdef assembly
            var result = new List<Type>();
            foreach (Type t in runtime.GetTypes())
            {
                if (!typeof(MonoBehaviour).IsAssignableFrom(t)) continue;
                if (t.IsAbstract || t.ContainsGenericParameters) continue;
                if (DeclaresMethodInChain(t, "OnGUI")) result.Add(t);
            }
            return result;
        }

        private static bool DeclaresMethodInChain(Type t, string method)
        {
            for (Type cur = t; cur != null && cur != typeof(MonoBehaviour); cur = cur.BaseType)
            {
                if (cur.GetMethod(method,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly) != null)
                    return true;
            }
            return false;
        }

        // Awake does not auto-run in EditMode (no ExecuteAlways on runtime components) — invoke the most-derived
        // declared Awake by reflection, exactly as the component would receive it at runtime. Every runtime
        // Awake is bare-GameObject-safe by project convention (null-guarded fallbacks); a throw here is itself
        // a finding (the component could not survive its own Awake on a minimal scene).
        private static void InvokeDeclaredAwake(MonoBehaviour comp)
        {
            for (Type cur = comp.GetType(); cur != null && cur != typeof(MonoBehaviour); cur = cur.BaseType)
            {
                MethodInfo awake = cur.GetMethod("Awake",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (awake != null && awake.GetParameters().Length == 0)
                {
                    awake.Invoke(comp, null);
                    return;
                }
            }
            // No declared Awake anywhere in the chain: nothing to invoke — useGUILayout keeps Unity's
            // default (true) and the assert above reports the type as an offender.
        }
    }
}
