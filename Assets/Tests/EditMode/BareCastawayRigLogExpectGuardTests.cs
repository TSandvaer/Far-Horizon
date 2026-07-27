using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Ticket 86cavj8pf — the DURABLE guard for the "bare CastawayCharacter rig emits an undeclared
    /// LogType.Error" bug CLASS, not the three instances that motivated it.
    ///
    /// The class: <c>CastawayCharacter.Awake</c> → <c>BuildModel()</c> logs
    /// <c>[Error] "[CastawayCharacter] modelPrefab not wired — cannot build avatar"</c> whenever the component is
    /// added to a GameObject with no model child + no serialized <c>modelPrefab</c> — i.e. EVERY bare PlayMode
    /// test rig. The Unity Test Framework FAILS any test that emits an UNDECLARED error log
    /// ("Unhandled log message: ... Use UnityEngine.TestTools.LogAssert.Expect"), so such a test goes red for a
    /// reason that has NOTHING to do with its assertions — every assert in it would pass.
    ///
    /// Why it needs a structural guard: the failure is INVISIBLE at authoring time (it needs a PlayMode run) and
    /// the `playmode` CI job is ADVISORY / non-blocking (unity-conventions.md §CI architecture, ticket
    /// 86caapwmt), so a PR introducing it merges GREEN. That is exactly what happened: PR #327 (`250e4e6`,
    /// 86caffwv5) added three CombatPlayModeTests that copied the bare-rig shape WITHOUT the log declaration;
    /// they were red on their own PR run and on every main run after, and the three standing reds then blocked
    /// the advisory→REQUIRED promotion (86camz787). Nine sibling PlayMode suites already handle it correctly
    /// (WasdMovementPlayModeTests:119, RunOnShift, WasdCrouch, JumpOnSpace, JumpClipSelection, Airborne,
    /// CastawayGroundSnap ×3, FloatDiagnostic ×2, LocomotionSamplingHarness, CastawayAnimation) — the convention
    /// existed; only the enforcement didn't.
    ///
    /// The check: SOURCE-SCAN every Assets/Tests/PlayMode/*.cs. Any file that adds a CastawayCharacter in CODE
    /// must ALSO declare the expected error in code — either the precise
    /// <c>LogAssert.Expect(LogType.Error, "[CastawayCharacter] modelPrefab not wired…")</c> (preferred: any OTHER
    /// error still fails the test) or the blunt <c>LogAssert.ignoreFailingMessages = true</c> (the ChopTree /
    /// MineBoulder / MineOre / ForgeSmelt suites' choice, which also swallows unrelated errors).
    ///
    /// Source-scan (not reflection) because the danger is a CODE SHAPE in test setup, not a type/member
    /// signature — the same reason VerifyCaptureDeterminismGuardTests scans source.
    ///
    /// BOUNDED — what this does NOT check: it asserts PRESENCE per file, not one declaration per rig, and not
    /// declaration ORDER (LogAssert.Expect must precede the AddComponent to consume the log). A file with three
    /// rigs and one declaration, or a declaration placed after the AddComponent, still passes here. Counting and
    /// ordering are brittle against helper methods / loops / multi-line Expect calls; presence catches the real
    /// regression (a whole suite copied with NO declaration at all), which is the only shape observed.
    ///
    /// Regression guard: add a new PlayMode test that does `AddComponent&lt;CastawayCharacter&gt;()` in a bare rig
    /// and forget the log declaration → this goes RED in the REQUIRED EditMode job, before the advisory PlayMode
    /// job's red can be dismissed as noise.
    /// </summary>
    public class BareCastawayRigLogExpectGuardTests
    {
        private static string PlayModeTestDir =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "Tests", "PlayMode"));

        private static IEnumerable<string> PlayModeTestFiles()
        {
            Assert.IsTrue(Directory.Exists(PlayModeTestDir),
                          "Assets/Tests/PlayMode must exist: " + PlayModeTestDir);
            return Directory.GetFiles(PlayModeTestDir, "*.cs", SearchOption.TopDirectoryOnly)
                            .OrderBy(p => p);
        }

        // Strip // line comments and /* */ block comments so a pattern QUOTED in a doc-comment is not a false
        // hit — only real CODE counts. Same crude-but-sufficient approach as
        // VerifyCaptureDeterminismGuardTests.StripComments (the test sources don't embed "//" in literals).
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\n]*", string.Empty);
            return src;
        }

        // `AddComponent<CastawayCharacter>()` — the bare-rig construction site.
        private static readonly Regex AddsCastaway =
            new Regex(@"AddComponent\s*<\s*CastawayCharacter\s*>", RegexOptions.Compiled);

        // The precise declaration. Newline-tolerant (CastawayGroundSnap:92 + CastawayAnimation:58 wrap the call
        // across two lines). Matched only up to "modelPrefab not wired" so the em dash in the real message can
        // never make this guard encoding-fragile.
        private static readonly Regex ExpectsModelPrefabError = new Regex(
            @"LogAssert\s*\.\s*Expect\s*\(\s*LogType\s*\.\s*Error\s*,\s*""\[CastawayCharacter\]\s+modelPrefab\s+not\s+wired",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // The blunt alternative several suites use instead.
        private static readonly Regex IgnoresFailingMessages = new Regex(
            @"LogAssert\s*\.\s*ignoreFailingMessages\s*=\s*true", RegexOptions.Compiled);

        [Test]
        public void EveryPlayModeSuite_ThatBuildsABareCastawayRig_DeclaresTheModelPrefabError()
        {
            var offenders = new List<string>();
            int scanned = 0, withRig = 0;

            foreach (string path in PlayModeTestFiles())
            {
                scanned++;
                string code = StripComments(File.ReadAllText(path));
                if (!AddsCastaway.IsMatch(code)) continue;
                withRig++;

                if (ExpectsModelPrefabError.IsMatch(code) || IgnoresFailingMessages.IsMatch(code)) continue;
                offenders.Add(Path.GetFileName(path));
            }

            Assert.Greater(scanned, 0, "the PlayMode test scan found no .cs files — the scan itself is broken");
            Assert.Greater(withRig, 0,
                           "no PlayMode suite adds a CastawayCharacter — the AddsCastaway pattern has gone stale " +
                           "(if the rig shape genuinely changed, retire or retarget this guard deliberately)");

            Assert.IsEmpty(offenders,
                "PlayMode suite(s) build a bare CastawayCharacter rig without declaring its LogType.Error — the " +
                "Unity Test Framework will fail every such test with \"Unhandled log message: '[Error] " +
                "[CastawayCharacter] modelPrefab not wired ...'\" no matter what the assertions do (86cavj8pf). " +
                "Add, immediately BEFORE the AddComponent<CastawayCharacter>() call:\n" +
                "    LogAssert.Expect(LogType.Error, \"[CastawayCharacter] modelPrefab not wired \\u2014 cannot " +
                "build avatar\");\n" +
                "(or LogAssert.ignoreFailingMessages = true in SetUp, which also swallows unrelated errors). " +
                "Offending file(s): " + string.Join(", ", offenders));
        }
    }
}
