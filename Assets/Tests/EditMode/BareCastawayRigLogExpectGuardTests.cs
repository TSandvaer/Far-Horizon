using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace FarHorizon.EditTests
{
    /// <summary>
    /// Ticket 86cavj8pf (hardened by 86caxh9b5) — the DURABLE guard for the "bare CastawayCharacter rig emits
    /// an undeclared LogType.Error" bug CLASS, not the three instances that motivated it.
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
    /// the advisory→REQUIRED promotion (86camz787).
    ///
    /// Measured at the merged-#338 head (this guard's own scan, `Assets/Tests/PlayMode`): 66 `.cs` files
    /// scanned, **14 build a bare castaway rig** — **11 declare the error via `LogAssert.Expect` across 16
    /// sites** (AirborneAirControl, CastawayAnimation, CastawayGroundSnap ×3, CombatPlayMode ×3, FloatDiagnostic
    /// ×2, JumpClipSelectionAndLanding, JumpOnSpace, LocomotionSamplingHarness, RunOnShift, WasdCrouch,
    /// WasdMovement) and **3 use the blunter `LogAssert.ignoreFailingMessages = true`** (ChopTree, MineBoulder,
    /// MineOre). The convention existed; only the enforcement didn't.
    ///
    /// The check: SOURCE-SCAN every `.cs` under `Assets/Tests/PlayMode` **recursively**. Any file that adds a
    /// CastawayCharacter in CODE must ALSO declare the expected error in code — either the precise
    /// <c>LogAssert.Expect(LogType.Error, "[CastawayCharacter] modelPrefab not wired…")</c> (preferred: any OTHER
    /// error still fails the test) or the blunt <c>LogAssert.ignoreFailingMessages = true</c> (the ChopTree /
    /// MineBoulder / MineOre suites' choice, which also swallows unrelated errors) — and it must declare
    /// **AT LEAST AS MANY times as it builds rigs** (see "counting", below).
    ///
    /// Source-scan (not reflection) because the danger is a CODE SHAPE in test setup, not a type/member
    /// signature — the same reason VerifyCaptureDeterminismGuardTests scans source.
    ///
    /// --- Two hardenings over the original presence-based / top-level-only form (86caxh9b5, #338 review NITs
    /// 2 + 4, comment 5094960660) ---
    ///
    /// 1. **RECURSIVE scan** (`SearchOption.AllDirectories`, was `TopDirectoryOnly`). `Assets/Tests/PlayMode` is
    ///    flat today, so there was no live gap — but a suite added in a FUTURE subfolder escaped coverage
    ///    SILENTLY, because the `withRig > 0` staleness assert still passed off the top-level files.
    /// 2. **COUNT-based, not presence-based.** The original passed a file the moment it contained ONE
    ///    declaration, so a 4th bare-rig test added inside the already-3-Expect `CombatPlayModeTests.cs` was
    ///    greenable — the single likeliest next recurrence. Now: `declarations >= rigSites`, per file.
    ///    Greenable on the real tree by construction (every declaring file matches exactly 1:1 at site level —
    ///    3/3, 3/3, 2/2, rest 1/1), so this tightening cost nothing to adopt.
    ///
    /// BOUNDED — what this still does NOT check, and the one FALSE-POSITIVE shape:
    /// - It counts SOURCE SITES, not runtime instantiations. A rig built inside a `for` loop (one site, N
    ///   objects) with one declaration passes. Not observed in-tree; `ignoreFailingMessages` is the escape.
    /// - It does not check declaration ORDER. It does not need to: UTF matches `Expect` against messages the
    ///   test has ALREADY received, so a declaration later in the same setup still consumes an earlier message
    ///   (`CastawayGroundSnapPlayModeTests` adds the rig at :85, declares at :92, and is green). What fails is
    ///   declaring it in a DIFFERENT test, or not at all.
    /// - **False positive to know about:** one declaration in `[SetUp]`/`[UnitySetUp]` (which fires before EVERY
    ///   test) alongside N rigs built inline in N separate tests is CORRECT code that this counts as N-vs-1 and
    ///   REDS. No file in-tree has that shape. If you hit it, the fix is cheap and is also the better style the
    ///   convention already asks for: move the rig construction next to the declaration (a shared `BuildRig()`
    ///   helper, the CastawayGroundSnap / FloatDiagnostic pattern), or declare per test.
    ///
    /// Regression guard: add a new PlayMode test that does `AddComponent&lt;CastawayCharacter&gt;()` in a bare rig
    /// and forget the log declaration → this goes RED in the REQUIRED EditMode job, before the advisory PlayMode
    /// job's red can be dismissed as noise. Both hardenings carry their own permanent negative controls below
    /// (`Guard_RedsOnAnOffenderInASubdirectory_…`, `Guard_RedsOnASecondBareRig_…`) which run the SAME scan over
    /// a synthetic temp tree — so a future "simplification" back to top-level-only or presence-only reds here.
    /// </summary>
    public class BareCastawayRigLogExpectGuardTests
    {
        private static string PlayModeTestDir =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "Tests", "PlayMode"));

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

        // Strip // line comments and /* */ block comments so a pattern QUOTED in a doc-comment is not a false
        // hit — only real CODE counts. Same crude-but-sufficient approach as
        // VerifyCaptureDeterminismGuardTests.StripComments (the test sources don't embed "//" in literals).
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\n]*", string.Empty);
            return src;
        }

        internal sealed class ScanResult
        {
            public readonly List<string> Offenders = new List<string>();
            public int Scanned;            // every .cs found under the root, recursively
            public int WithRig;            // files that build at least one bare castaway rig
            public int RigSites;           // total AddComponent<CastawayCharacter> sites
            public int DeclarationSites;   // total precise LogAssert.Expect sites
            public int BluntFiles;         // files taking the ignoreFailingMessages escape
        }

        /// <summary>
        /// The whole guard, as a pure function of a directory, so the synthetic negative controls below exercise
        /// the EXACT code path the real-tree test runs (not a re-implementation of it).
        /// </summary>
        internal static ScanResult Scan(string rootDir)
        {
            var result = new ScanResult();
            string root = Path.GetFullPath(rootDir);

            foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).OrderBy(p => p))
            {
                result.Scanned++;
                string code = StripComments(File.ReadAllText(path));

                int rigs = AddsCastaway.Matches(code).Count;
                if (rigs == 0) continue;
                result.WithRig++;
                result.RigSites += rigs;

                if (IgnoresFailingMessages.IsMatch(code)) { result.BluntFiles++; continue; }

                int declarations = ExpectsModelPrefabError.Matches(code).Count;
                result.DeclarationSites += declarations;
                if (declarations >= rigs) continue;

                result.Offenders.Add(Relative(root, path) +
                                     " (rig sites=" + rigs + ", declarations=" + declarations + ")");
            }

            return result;
        }

        private static string Relative(string root, string path)
        {
            string full = Path.GetFullPath(path);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                full = full.Substring(root.Length)
                           .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return full.Replace('\\', '/');
        }

        [Test]
        public void EveryPlayModeSuite_ThatBuildsABareCastawayRig_DeclaresTheModelPrefabError()
        {
            Assert.IsTrue(Directory.Exists(PlayModeTestDir),
                          "Assets/Tests/PlayMode must exist: " + PlayModeTestDir);

            ScanResult scan = Scan(PlayModeTestDir);

            Assert.Greater(scan.Scanned, 0, "the PlayMode test scan found no .cs files — the scan itself is broken");
            Assert.Greater(scan.WithRig, 0,
                           "no PlayMode suite adds a CastawayCharacter — the AddsCastaway pattern has gone stale " +
                           "(if the rig shape genuinely changed, retire or retarget this guard deliberately)");

            Assert.IsEmpty(scan.Offenders,
                "PlayMode suite(s) build MORE bare CastawayCharacter rigs than they declare LogType.Errors for — " +
                "the Unity Test Framework will fail every undeclared such test with \"Unhandled log message: " +
                "'[Error] [CastawayCharacter] modelPrefab not wired ...'\" no matter what the assertions do " +
                "(86cavj8pf). Add, in the same test or SetUp as the AddComponent<CastawayCharacter>() call:\n" +
                "    LogAssert.Expect(LogType.Error, \"[CastawayCharacter] modelPrefab not wired \\u2014 cannot " +
                "build avatar\");\n" +
                "one declaration per rig-construction SITE (or LogAssert.ignoreFailingMessages = true in SetUp, " +
                "which also swallows unrelated errors). NOTE: one declaration in [SetUp] covering N rigs built " +
                "inline in N separate tests is correct at runtime but counts as N-vs-1 here — move the rig " +
                "construction next to the declaration (a shared BuildRig() helper, as CastawayGroundSnap and " +
                "FloatDiagnostic do) or declare per test. Offending file(s): " +
                string.Join(", ", scan.Offenders));
        }

        [Test]
        public void GuardItself_ScansTheRealPlayModeSet_NotAnEmptyOrDriftedGlob()
        {
            // Defence against a silent false-green: if the path/glob drifted and matched ZERO (or only a couple
            // of) files, the guard above would vacuously PASS. Anchor on count floors + a known member so a path
            // regression fails LOUD. Measured at the merged-#338 head: scanned=66, withRig=14, rigSites=19,
            // declarationSites=16, bluntFiles=3.
            ScanResult scan = Scan(PlayModeTestDir);

            Assert.Greater(scan.Scanned, 40,
                "the guard must scan the real PlayMode suite set (>40 .cs files) — a near-zero count means the " +
                "path/glob drifted and the guard is vacuously passing (false-green). Found: " + scan.Scanned +
                " under " + PlayModeTestDir);
            Assert.GreaterOrEqual(scan.WithRig, 10,
                "at least 10 PlayMode suites are known to build a bare castaway rig; found " + scan.WithRig +
                " — the AddsCastaway pattern has drifted or the scan lost files");
            Assert.GreaterOrEqual(scan.RigSites, scan.WithRig,
                "rig SITES cannot be fewer than the files carrying them — counting is broken");
            Assert.IsTrue(
                Directory.GetFiles(PlayModeTestDir, "CombatPlayModeTests.cs", SearchOption.AllDirectories).Any(),
                "CombatPlayModeTests.cs (the 86cavj8pf reference suite) must be in the scanned set");
        }

        // ---------------------------------------------------------------------------------------------------
        // Permanent negative controls. Each writes a synthetic tree to a temp dir and runs the SAME Scan(),
        // so reverting either hardening (recursion / counting) reds one of these. Temp dirs stay OUT of
        // Assets/ deliberately — a committed offender under Assets/Tests/PlayMode would red the real-tree test.
        // ---------------------------------------------------------------------------------------------------

        private const string RigLine = "            var c = go.AddComponent<CastawayCharacter>();\n";
        private const string DeclLine =
            "            LogAssert.Expect(LogType.Error, \"[CastawayCharacter] modelPrefab not wired - cannot build avatar\");\n";
        private const string BluntLine = "            LogAssert.ignoreFailingMessages = true;\n";

        private static string MakeTempTree(params (string relPath, string body)[] files)
        {
            string root = Path.Combine(Path.GetTempPath(), "fh-barerig-guard-" + Guid.NewGuid().ToString("N"));
            foreach ((string relPath, string body) in files)
            {
                string full = Path.Combine(root, relPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, "public class Synthetic {\n        public void T() {\n" + body + "        }\n}\n");
            }
            return root;
        }

        private static void Nuke(string root)
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { /* temp dir */ }
        }

        [Test]
        public void Guard_RedsOnAnOffenderInASubdirectory_NotJustTheTopLevel()
        {
            // NIT 2 (#338 review): with SearchOption.TopDirectoryOnly this tree scans CLEAN — the top-level file
            // is compliant and `withRig > 0` still passes, so the subfolder offender escapes SILENTLY.
            string root = MakeTempTree(
                ("CompliantTopLevelTests.cs", DeclLine + RigLine),
                ("Locomotion/OffenderInASubfolderTests.cs", RigLine));
            try
            {
                ScanResult scan = Scan(root);
                Assert.AreEqual(2, scan.Scanned, "both synthetic files must be scanned (recursion)");
                Assert.AreEqual(2, scan.WithRig);
                CollectionAssert.IsNotEmpty(scan.Offenders,
                    "an undeclared bare rig in a PlayMode SUBFOLDER must be caught — this is the " +
                    "TopDirectoryOnly hole (86caxh9b5 / #338 NIT 2). Reverting to TopDirectoryOnly reds here.");
                Assert.IsTrue(scan.Offenders.Single().StartsWith("Locomotion/OffenderInASubfolderTests.cs"),
                              "the offender must be reported by its path RELATIVE to Assets/Tests/PlayMode so a " +
                              "subfolder file is identifiable; got: " + scan.Offenders.Single());
            }
            finally { Nuke(root); }
        }

        [Test]
        public void Guard_RedsOnASecondBareRig_InAnAlreadyDeclaringFile_CountNotPresence()
        {
            // NIT 4 (#338 review): the exact next-recurrence shape — a 4th bare-rig test dropped into a file
            // that already declares (CombatPlayModeTests has 3). A presence check scans this CLEAN.
            string root = MakeTempTree(
                ("AlreadyCompliantTests.cs", DeclLine + RigLine + DeclLine + RigLine + DeclLine + RigLine + RigLine));
            try
            {
                ScanResult scan = Scan(root);
                Assert.AreEqual(1, scan.WithRig);
                Assert.AreEqual(4, scan.RigSites);
                Assert.AreEqual(3, scan.DeclarationSites);
                CollectionAssert.IsNotEmpty(scan.Offenders,
                    "a 4th bare rig inside an already-declaring file must be caught — this is the presence-check " +
                    "hole (86caxh9b5 / #338 NIT 4). Reverting to a presence check reds here.");
                StringAssert.Contains("rig sites=4, declarations=3", scan.Offenders.Single(),
                                      "the failure must report the counts so the author sees WHICH side is short");
            }
            finally { Nuke(root); }
        }

        [Test]
        public void Guard_StaysGreen_OnMatchedCounts_AndOnTheBluntIgnoreEscape()
        {
            // The other half of a real negative control: prove the tightening does not red COMPLIANT shapes —
            // 1:1 declarations, N:N declarations, the ignoreFailingMessages escape, and a rig-free helper file.
            string root = MakeTempTree(
                ("OneToOneTests.cs", DeclLine + RigLine),
                ("Deep/Nested/ThreeToThreeTests.cs", DeclLine + RigLine + DeclLine + RigLine + DeclLine + RigLine),
                ("BluntlyIgnoresTests.cs", BluntLine + RigLine + RigLine),
                ("NoRigHelper.cs", "            var x = 1;\n"));
            try
            {
                ScanResult scan = Scan(root);
                Assert.AreEqual(4, scan.Scanned);
                Assert.AreEqual(3, scan.WithRig);
                Assert.AreEqual(1, scan.BluntFiles);
                CollectionAssert.IsEmpty(scan.Offenders,
                    "matched counts (at any depth) and the ignoreFailingMessages escape must stay GREEN — the " +
                    "count hardening must not red compliant suites. Offenders: " +
                    string.Join(", ", scan.Offenders));
            }
            finally { Nuke(root); }
        }
    }
}
