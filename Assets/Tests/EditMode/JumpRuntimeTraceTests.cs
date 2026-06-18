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
    /// verification must come from a RUNTIME trace, not EditMode. CastawayCharacter now AUTO-logs a per-frame
    /// [JumpTrace] line to Player.log on EVERY jump (no toggle/launch-arg) so the Sponsor just plays and the
    /// orchestrator reads ground truth.
    ///
    /// These guards pin the BUG CLASS the silent-instrument failure family warns of (a trace that never fires —
    /// the CaptureGate/FloatTrace silent-killer, unity-conventions.md §Component-not-serialized): the jump-trace
    /// window must OPEN on a grounded jump (so the per-frame log fires), and it must NOT open without a jump.
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
        // THE SILENT-INSTRUMENT GUARD: a grounded jump must OPEN the trace window (so the per-frame [JumpTrace]
        // line auto-fires through the arc). TryJump (grounded-only) opens it synchronously — no scene/anim/agent.
        [Test]
        public void TryJump_OpensTheRuntimeTraceWindow_SoTheJumpTraceAutoFires()
        {
            var go = new GameObject("castaway-trace-test");
            try
            {
                var c = go.AddComponent<CastawayCharacter>();
                c.groundSnap = true;   // a grounded rig has a settled baseline to launch from (TryJump requires it)

                Assert.IsFalse(c.JumpTraceActive,
                    "the jump-trace window must be CLOSED before any jump (silent in a non-jump frame)");

                bool started = c.TryJump();
                Assert.IsTrue(started, "a grounded TryJump must start the jump (the precondition for the trace)");
                Assert.IsTrue(c.IsAirborne, "TryJump must put the castaway airborne");
                Assert.IsTrue(c.JumpTraceActive,
                    "TryJump must OPEN the runtime jump-trace window so the per-frame [JumpTrace] line auto-logs " +
                    "to Player.log through the arc — the instrument that captures the 'pulled back' ground truth. " +
                    "A closed window here is the silent-instrument bug class (a trace that never fires).");
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
                Assert.IsTrue(c.TryJump(), "the first grounded jump starts");
                Assert.IsTrue(c.JumpTraceActive, "the window opens on the first jump");

                bool second = c.TryJump();
                Assert.IsFalse(second, "a mid-air re-press must be ignored (no double-jump)");
                Assert.IsTrue(c.JumpTraceActive, "the window stays open across the ignored re-press (still mid-jump)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // A rig with grounding OFF cannot jump (no settled baseline), so the trace window must NOT open — proves
        // the window is gated on an actual lift-off, not opened spuriously.
        [Test]
        public void NoJumpWithoutGroundSnap_TraceWindowStaysClosed()
        {
            var go = new GameObject("castaway-trace-test");
            try
            {
                var c = go.AddComponent<CastawayCharacter>();
                c.groundSnap = false;  // no grounded baseline → TryJump returns false → no jump → no trace

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
