using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// AC2 (ticket 86cafdevx) — the DURABLE form of "audit + harden every *VerifyCapture harness for
    /// determinism" (the #162 flake class). The one-time audit (2026-06-28) found exactly ONE harness with
    /// the dangerous pattern — LootPromptVerifyCapture — and Drew fixed it in #162 (commit 3eaf546:
    /// deterministic wired-bush pick + NavMeshAgent.Warp). Every other *VerifyCapture is already clean: it
    /// targets a SINGLETON (pond/panel/orbit/inventory), CREATES its own deterministic prop
    /// (FlatShading/Rim/WeaponSet spawn a primitive at a fixed position), or drives the player via the
    /// gameplay NavMesh seam (Craft/Chop/Campfire/WalkGrounding use player.MoveTo / agent.SetDestination with
    /// fixed or reachability-resolved targets — NOT a raw teleport).
    ///
    /// This test makes that property STRUCTURAL so a FUTURE harness can't silently regress to the #162
    /// pattern (the chop-of-tomorrow the ticket exists to protect). It SOURCE-SCANS every
    /// Assets/Scripts/Runtime/*VerifyCapture.cs and FAILS if either #162-class danger appears:
    ///
    ///   (1) RAW PLAYER TELEPORT: an assignment to a `player`/`agent` transform's `.position` (the player owns
    ///       a NavMeshAgent that re-snaps a raw transform set back onto navmesh → the moved object drifts away
    ///       before the in-range/visual read; #162 root cause). A player teleport MUST go through
    ///       NavMeshAgent.Warp(NavMesh.SamplePosition(...)). Camera/prop teleports are fine and excluded.
    ///
    ///   (2) NON-DETERMINISTIC SCATTERED-TARGET PICK: a FindObjectsByType&lt;BerryBush|ChopTree&gt; iteration
    ///       that is NOT accompanied by a deterministic selector in the same file (a name match like
    ///       `name == "BerryBush"` for the wired instance, OR NavMesh.SamplePosition reachability resolution).
    ///       The #162 harness picked "first ripe by InstanceID" — build-unstable order → it selected a
    ///       coast-edge scatter bush off-navmesh → resolve null → false-fail.
    ///
    /// Source-scan (not reflection) because the danger is a CODE SHAPE, not a type/member signature.
    /// Regression guard: write a new -verify harness that teleports the player with a raw position set, or
    /// picks a scattered bush/tree by bare InstanceID order, and this goes RED at CI step 2 — before the
    /// 20-min capture gate ever runs the flaky harness.
    /// </summary>
    public class VerifyCaptureDeterminismGuardTests
    {
        private static string RuntimeDir =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts", "Runtime"));

        private static IEnumerable<string> VerifyCaptureFiles()
        {
            Assert.IsTrue(Directory.Exists(RuntimeDir), "Assets/Scripts/Runtime must exist: " + RuntimeDir);
            return Directory.GetFiles(RuntimeDir, "*VerifyCapture.cs", SearchOption.TopDirectoryOnly)
                            .OrderBy(p => p);
        }

        // Strip // line comments and /* */ block comments so a danger pattern QUOTED in a doc-comment (e.g.
        // LootPromptVerifyCapture's own "a raw `player.position = standPos`" explanation) is NOT a false hit —
        // only real CODE counts. Crude but sufficient: the harnesses don't embed "//" inside string literals.
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\n]*", string.Empty);
            return src;
        }

        [Test]
        public void NoVerifyCapture_TeleportsThePlayerWithARawPositionSet()
        {
            // The player/agent transform owns a NavMeshAgent; a raw `.position =` is re-snapped away (#162).
            // Match an assignment to a var named player|agent (the known locomotion seam) `.position =` —
            // NOT `==`, NOT a camera/prop transform. `agent.transform.position =` and `player.position =` both
            // caught; `camGo.transform.position =` / `prop.transform.position =` are correctly NOT matched.
            var raw = new Regex(@"\b(player|agent)\b[\w\.]*\.position\s*=\s*[^=]", RegexOptions.Compiled);

            var offenders = new List<string>();
            foreach (var file in VerifyCaptureFiles())
            {
                string code = StripComments(File.ReadAllText(file));
                foreach (Match m in raw.Matches(code))
                {
                    // Allow the documented no-agent FALLBACK in LootPromptVerifyCapture's TeleportPlayer: a raw
                    // set guarded INSIDE a method that also Warps the agent. The guard: the same file must
                    // reference NavMeshAgent.Warp — proving the raw set is the degenerate-rig fallback, not the
                    // primary teleport. A file that does a raw player.position set WITHOUT any agent.Warp is the
                    // #162 pattern and fails.
                    if (code.Contains(".Warp(")) continue;
                    offenders.Add(Path.GetFileName(file) + " :: \"" + m.Value.Trim() + "\"");
                }
            }

            Assert.IsEmpty(offenders,
                "A *VerifyCapture teleports the player/agent with a RAW transform `.position =` and never calls " +
                "NavMeshAgent.Warp — the #162 flake (the agent re-snaps the raw set back onto navmesh, dragging " +
                "the moved object away before the read). Teleport the player via NavMeshAgent.Warp(NavMesh." +
                "SamplePosition(...)) (the LootPromptVerifyCapture.TeleportPlayer pattern). Offenders:\n  " +
                string.Join("\n  ", offenders));
        }

        [Test]
        public void EveryVerifyCapture_PickingAScatteredTarget_UsesADeterministicSelector()
        {
            // A harness that iterates a SCATTERED population (FindObjectsByType<BerryBush|ChopTree>) to choose a
            // teleport/move target must pick DETERMINISTICALLY — a name match for the wired instance, OR a
            // NavMesh.SamplePosition reachability resolve — never "first by InstanceID order" (#162). We can't
            // parse intent, so the structural proxy: any file that calls FindObjectsByType on a scattered
            // interactable type MUST also contain a deterministic selector token in the same file.
            var scatteredPick = new Regex(
                @"FindObjectsByType\s*<\s*(BerryBush|ChopTree)\s*>", RegexOptions.Compiled);
            // Deterministic-selector evidence: a name-equality pick of a wired instance, or navmesh reachability.
            var deterministic = new Regex(
                @"\.name\s*==|gameObject\.name\s*==|NavMesh\.SamplePosition", RegexOptions.Compiled);

            var offenders = new List<string>();
            foreach (var file in VerifyCaptureFiles())
            {
                string code = StripComments(File.ReadAllText(file));
                if (!scatteredPick.IsMatch(code)) continue;       // doesn't iterate a scattered population
                if (deterministic.IsMatch(code)) continue;        // has a deterministic selector → OK
                offenders.Add(Path.GetFileName(file));
            }

            Assert.IsEmpty(offenders,
                "A *VerifyCapture iterates a SCATTERED interactable population (FindObjectsByType<BerryBush|" +
                "ChopTree>) but carries NO deterministic selector (no `name ==` wired-instance pick and no " +
                "NavMesh.SamplePosition reachability resolve) — the #162 'first ripe by InstanceID' flake class " +
                "(build-unstable order → wrong/unreachable instance → false-fail). Prefer the deterministic " +
                "WIRED instance by name, or resolve reachability via NavMesh.SamplePosition. Offenders:\n  " +
                string.Join("\n  ", offenders));
        }

        [Test]
        public void GuardItself_ScansARealNonEmptySetOfVerifyCaptures()
        {
            // Defence against a silent false-green: if the glob/path drifted and matched ZERO files, both
            // guards above would vacuously PASS. Anchor on a known count floor so a path regression fails loud.
            var files = VerifyCaptureFiles().ToList();
            Assert.Greater(files.Count, 10,
                "the determinism guard must scan the real *VerifyCapture set (>10 files) — a near-zero count " +
                "means the glob/path drifted and the guards are vacuously passing (false-green). Found: " +
                files.Count + " under " + RuntimeDir);
            // The #162 harness must be in the scanned set (sanity: the canonical example is present).
            Assert.IsTrue(files.Any(f => Path.GetFileName(f) == "LootPromptVerifyCapture.cs"),
                "LootPromptVerifyCapture.cs (the #162 reference harness) must be in the scanned set");
        }
    }
}
