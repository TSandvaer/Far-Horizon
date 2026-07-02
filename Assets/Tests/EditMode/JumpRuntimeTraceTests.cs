using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FarHorizon;
using FarHorizon.EditorTools;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// RUNTIME JUMP-TRACE wiring guards (ticket 86caaqhj5 — the "pulled back on landing" RE-DIAGNOSIS instrument).
    ///
    /// The dispatch RE-DIAGNOSED the jump pull-back as a MOVEMENT/visual bug (the camera follow is RULED OUT:
    /// the Sponsor cranked airborneFollowLerp to 10000+ with ZERO effect — a perfect-follow camera would have
    /// fixed a camera bug; it did not). And EditMode camera-follow traces PASSED 3× while the soak FAILED, so the
    /// verification must come from a RUNTIME trace, not EditMode. CastawayCharacter logs a per-frame [JumpTrace]
    /// line to Player.log — since 86cahhfp4 (C1) ONLY under the -jumpTrace launch flag (JumpTraceEnabled): the
    /// shipped DEFAULT jump is trace-silent (the always-on ~700-char log + stack capture was a per-frame hitch +
    /// GC source in every shipped jump), while a -jumpTrace relaunch restores the exact same diagnostics.
    ///
    /// These guards pin BOTH sides:
    ///  - the silent-instrument bug class (a trace that never fires — the CaptureGate/FloatTrace silent-killer,
    ///    unity-conventions.md §Component-not-serialized): with the flag ENABLED, the window must OPEN on a
    ///    grounded jump (so the per-frame log fires), and must NOT open without a jump;
    ///  - the shipped-waste regression (86cahhfp4 C1): WITHOUT the flag, a grounded jump must NOT open the
    ///    window — the default build never pays the trace.
    ///
    /// PLUS the asset-side anchor for the Sponsor's root-motion HYPOTHESIS (relayed via orchestrator): a Mixamo
    /// "jump forward" clip can lunge the body forward; the jump FBX is imported lockRootPositionXZ=true so the
    /// ROOT-NODE XZ motion is locked in-place (NavMeshAgent owns world XZ; applyRootMotion=false at runtime). If a
    /// future re-import flips that flag, a jump clip would extract forward root motion and re-introduce a
    /// visual-vs-entity XZ divergence — this guard catches that regression in headless CI before a soak.
    ///
    /// NO PlayMode fixture (the dispatch forbids it + local PlayMode deadlocks on this machine): TryJump opens
    /// the window SYNCHRONOUSLY when grounded, so the wiring is assertable in EditMode with a bare GameObject.
    /// </summary>
    public class JumpRuntimeTraceTests
    {
        // THE SHIPPED-WASTE GUARD (86cahhfp4 C1, the flag's OFF side — the machine gate "jump-trace is inert
        // without the flag"): WITHOUT -jumpTrace (JumpTraceEnabled defaults false; Awake never runs in EditMode
        // and the property is the launch-flag seam), a grounded jump must run normally but NEVER open the trace
        // window — the shipped default build pays no per-frame [JumpTrace] log / stack capture on jumps.
        [Test]
        public void TryJump_WithoutJumpTraceFlag_JumpRunsButTraceWindowStaysClosed()
        {
            var go = new GameObject("castaway-trace-test");
            try
            {
                var c = go.AddComponent<CastawayCharacter>();
                c.groundSnap = true;   // a grounded rig has a settled baseline to launch from (TryJump requires it)

                Assert.IsFalse(c.JumpTraceEnabled,
                    "the jump-trace instrument must default OFF (enabled only by the -jumpTrace launch flag)");

                bool started = c.TryJump();
                Assert.IsTrue(started, "the jump itself must be unaffected by the trace flag (gameplay unchanged)");
                Assert.IsTrue(c.IsAirborne, "TryJump must still put the castaway airborne without the flag");
                Assert.IsFalse(c.JumpTraceActive,
                    "WITHOUT -jumpTrace the trace window must stay CLOSED — an open window here means the " +
                    "shipped default build is back to paying the per-frame ~700-char [JumpTrace] log + managed " +
                    "stack capture on every jump (the 86cahhfp4 C1 waste this flag removed)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // THE SILENT-INSTRUMENT GUARD (the flag's ON side — the machine gate "jump-trace fires with the flag"):
        // with -jumpTrace enabled, a grounded jump must OPEN the trace window (so the per-frame [JumpTrace]
        // line fires through the arc). TryJump (grounded-only) opens it synchronously — no scene/anim/agent.
        [Test]
        public void TryJump_WithJumpTraceFlag_OpensTheRuntimeTraceWindow_SoTheJumpTraceFires()
        {
            var go = new GameObject("castaway-trace-test");
            try
            {
                var c = go.AddComponent<CastawayCharacter>();
                c.groundSnap = true;   // a grounded rig has a settled baseline to launch from (TryJump requires it)
                c.JumpTraceEnabled = true; // the -jumpTrace launch flag (Awake's parse), via the test seam

                Assert.IsFalse(c.JumpTraceActive,
                    "the jump-trace window must be CLOSED before any jump (silent in a non-jump frame)");

                bool started = c.TryJump();
                Assert.IsTrue(started, "a grounded TryJump must start the jump (the precondition for the trace)");
                Assert.IsTrue(c.IsAirborne, "TryJump must put the castaway airborne");
                Assert.IsTrue(c.JumpTraceActive,
                    "with -jumpTrace enabled, TryJump must OPEN the runtime jump-trace window so the per-frame " +
                    "[JumpTrace] line logs to Player.log through the arc — the instrument that captures the " +
                    "'pulled back' ground truth. A closed window here is the silent-instrument bug class " +
                    "(a trace that never fires) — the C1 flag must PRESERVE the diagnosis path, not break it.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // A re-press while already airborne must NOT re-open / disturb the window (no double-jump; idempotent).
        [Test]
        public void TryJump_WhileAirborne_DoesNotReopenOrDisturbTheWindow()
        {
            var go = new GameObject("castaway-trace-test");
            try
            {
                var c = go.AddComponent<CastawayCharacter>();
                c.groundSnap = true;
                c.JumpTraceEnabled = true; // trace-enabled run (the window semantics under -jumpTrace)
                Assert.IsTrue(c.TryJump(), "the first grounded jump starts");
                Assert.IsTrue(c.JumpTraceActive, "the window opens on the first jump");

                bool second = c.TryJump();
                Assert.IsFalse(second, "a mid-air re-press must be ignored (no double-jump)");
                Assert.IsTrue(c.JumpTraceActive, "the window stays open across the ignored re-press (still mid-jump)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // A rig with grounding OFF cannot jump (no settled baseline), so the trace window must NOT open EVEN
        // WITH the flag enabled — proves the window is gated on an actual lift-off, not opened spuriously.
        [Test]
        public void NoJumpWithoutGroundSnap_TraceWindowStaysClosed()
        {
            var go = new GameObject("castaway-trace-test");
            try
            {
                var c = go.AddComponent<CastawayCharacter>();
                c.groundSnap = false;  // no grounded baseline → TryJump returns false → no jump → no trace
                c.JumpTraceEnabled = true; // flag ON — the closed window below is the no-jump gate, not the flag

                Assert.IsFalse(c.TryJump(), "a rig with groundSnap off has no baseline to launch from — no jump");
                Assert.IsFalse(c.IsAirborne, "no jump means not airborne");
                Assert.IsFalse(c.JumpTraceActive, "with no jump, the trace window must stay CLOSED (no spurious open)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ASSET-SIDE ROOT-MOTION ANCHOR (the Sponsor's hypothesis at the import level). The jump FBX must import
        // with lockRootPositionXZ=true so the clip's ROOT-NODE forward motion is locked in-place (the NavMeshAgent
        // owns world XZ; applyRootMotion=false at runtime). A regression that flips this flag would extract forward
        // root motion onto a jump clip and re-introduce a visual-vs-entity XZ divergence — the "pulled back" class.
        [Test]
        public void JumpClips_ImportInPlace_LockRootPositionXZ_NoForwardRootMotion()
        {
            AssertJumpClipLocksRootXZ(CharacterAssetGen.JumpIdleFbxPath, CharacterAssetGen.JumpIdleClip);
            AssertJumpClipLocksRootXZ(CharacterAssetGen.JumpRunningFbxPath, CharacterAssetGen.JumpRunningClip);
        }

        private static void AssertJumpClipLocksRootXZ(string fbxPath, string clipName)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            Assert.IsNotNull(importer, fbxPath + " must be importable");
            ModelImporterClipAnimation match = default;
            bool found = false;
            foreach (var ca in importer.clipAnimations)
                if (ca.name.Contains(clipName)) { match = ca; found = true; break; }
            Assert.IsTrue(found, fbxPath + " must carry a clip matching '" + clipName + "'");
            Assert.IsTrue(match.lockRootPositionXZ,
                clipName + " must import lockRootPositionXZ=true — the jump clip's ROOT-NODE forward motion is " +
                "locked in-place (the NavMeshAgent owns world XZ; applyRootMotion=false). If this flips true→false " +
                "the jump would extract forward root motion and re-introduce the visual-vs-entity 'pulled back' " +
                "divergence (86caaqhj5 root-motion hypothesis).");
        }
    }
}
