using System.Collections;
using System.IO;
using UnityEngine;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the LOOT-PROXIMITY PROMPT (ticket 86cafc6ud AC2 —
    /// "Press E to pick up {name}"). Sibling of FreshwaterPondVerifyCapture / SeaVerifyCapture / Settings-
    /// VerifyCapture: a -verifyLoot flag that drives the SHOW state of the prompt in the BUILT exe and snaps
    /// a frame where the IMGUI tooltip actually RENDERS, then SELF-ASSERTS it.
    ///
    /// WHY THIS HOOK EXISTS (the gap Tess QA blocked PR #158 on, comment 4825495098): the prompt LOGIC is
    /// green in EditMode (BuildLabel show/hide/name) + wired in Boot.unity, but the generic CI -captureGate
    /// only shoots the DEFAULT SPAWN frame — where the player stands mid-field, OUTSIDE the tightened
    /// ~1.0–1.2u loot range, so the prompt is correctly HIDDEN. The one surface this ticket exists to deliver
    /// — the prompt actually showing near a bush — had ZERO built-frame evidence. The shipped-build capture
    /// gate (CLAUDE.md hard rule; unity-conventions.md §editor-vs-runtime — IMGUI green in EditMode ≠ legible
    /// in the IL2CPP frame; the spike "legs-up" + #130 stale-log false-greens) requires a BUILT frame proving
    /// the IMGUI plate+label renders legibly. This hook positions the player IN loot range of a RIPE bush so
    /// LootPrompt.Update resolves the bush + OnGUI paints the tooltip, then captures + self-asserts it.
    ///
    /// THE DRIVE (no input device in the shipped exe — teleport the player into range, found not assumed):
    ///   • find a RIPE BerryBush (any IPickable with CanLoot==true — the wired bush is RIPE at spawn);
    ///   • find the player's PickableLooter + its LootPrompt (the SAME components Boot.unity ships);
    ///   • teleport looter.player to ~0.55 × the bush's own LootRange from the bush (well inside reach), so
    ///     looter.NearestInRange() resolves THAT bush — exactly the single-source-of-truth the prompt reads;
    ///   • frame the gameplay orbit camera over-shoulder on the player+bush, settle so Update+OnGUI fire.
    ///
    /// SELF-ASSERTS (guard the deliverable, not a proxy):
    ///   1. SHOW-STATE REACHED (logic): looter.NearestInRange() == the bush AND LootPrompt.BuildLabel(...) is
    ///      a non-empty "Press E to pick up berries" — the prompt's _label is set, so OnGUI draws (not hidden).
    ///   2. PROMPT RENDERED (the frame): the prompt band (centred low, y = Screen.height-96, w=360 h=34) reads
    ///      DISTINCT from a control band just above it — the dark plate + cream text actually painted into the
    ///      swapchain frame (an IMGUI that silently failed to render would leave the band == the control).
    /// Logs the verdict lines + quits non-zero if EITHER fails (or the bush/looter/prompt is missing — a
    /// build-side regression: dropped from Boot.unity), so a reviewer's re-run is a real gate, not a smoke test.
    ///
    /// Inert unless launched with -verifyLoot (the normal game / boot capture is unaffected):
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyLoot [-captureDir &lt;dir&gt;]
    /// Captures loot_prompt_far.png (player at spawn → prompt HIDDEN, the contrast frame) + loot_prompt.png
    /// (player in range → prompt SHOWING), then quits. MUST run WINDOWED, not -batchmode (ScreenCapture needs
    /// a real swapchain — spike iter-4; editor RenderTexture mis-renders URP, hero-axe PR #21).
    ///
    /// ISOLATION (the dispatch's own-file / own-flag rule): this is a NEW isolated file with its OWN -verifyLoot
    /// flag + its OWN verify_loot_gate.sh + its OWN ci.yml step, so it never collides with other in-flight
    /// capture work (e.g. a parallel hunger-cap HUD capture).
    /// </summary>
    public class LootPromptVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        // Gameplay-representative over-shoulder framing of the player + bush (the orbit pitch the player sees
        // the world at — NOT a high-angle/editor top-down, memory verify-grounding-soaks-by-gameplay-cam-visual).
        // Close enough the centred-low prompt band + the bush + the player all read in one judgeable frame.
        public float viewYaw = 25f;
        public float viewPitch = 22f;
        public float viewDistance = 6.5f;
        public int warmupFrames = 8;
        public int settleFrames = 16;
        // Fraction of the bush's OWN LootRange to stand the player at — well inside reach so NearestInRange()
        // resolves the bush with margin (a value near 1.0 would sit on the boundary where capture-frame jitter
        // could flip in/out of range). 0.55 is "genuinely close, the arm's-reach the prompt promises".
        public float standFraction = 0.55f;

        void Start()
        {
            if (HasArg("-verifyLoot"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // === Find the components the prompt deliverable rides on (found, not assumed) ===
            var looter = Object.FindAnyObjectByType<PickableLooter>();
            var prompt = Object.FindAnyObjectByType<LootPrompt>();
            Debug.Log("[LootPromptVerifyCapture] looter found: " + (looter != null) +
                      " prompt found: " + (prompt != null));
            if (looter == null || prompt == null)
            {
                Debug.LogError("[LootPromptVerifyCapture] PickableLooter and/or LootPrompt missing from scene — " +
                               "the loot-prompt wiring is absent from Boot.unity (build-side regression signal)");
                yield return null; Application.Quit(1); yield break;
            }

            // Find a RIPE, loot-able BerryBush — the IPickable the prompt will name "berries". CanLoot==true means
            // a berry bush that is RIPE with a wired inventory (the spawn state of the wired bush). Found, not
            // assumed (we don't hard-code the (-6,7) wired position — any ripe bush proves the show state).
            BerryBush bush = null;
            foreach (var b in Object.FindObjectsByType<BerryBush>(FindObjectsSortMode.InstanceID))
            {
                if (b != null && (b as IPickable).CanLoot) { bush = b; break; }
            }
            Debug.Log("[LootPromptVerifyCapture] ripe loot-able BerryBush found: " + (bush != null) +
                      (bush != null ? " at " + bush.transform.position.ToString("F2") +
                                      " (LootRange=" + ((IPickable)bush).LootRange.ToString("F2") + ")" : ""));
            if (bush == null)
            {
                Debug.LogError("[LootPromptVerifyCapture] no RIPE loot-able BerryBush in scene — cannot drive the " +
                               "prompt SHOW state (build-side regression: the bush was dropped from Boot.unity)");
                yield return null; Application.Quit(1); yield break;
            }

            // The player transform the looter measures range from (its serialized ref). Teleporting THIS is what
            // brings the prompt into range — the prompt reads looter.NearestInRange() which measures from here.
            Transform player = looter.player != null ? looter.player : looter.transform;

            // Stop the orbit rig re-driving Camera.main each LateUpdate; we park the camera by hand (the proven
            // SeaVerifyCapture / FreshwaterPondVerifyCapture pattern). Camera.main rides the gameplay render path
            // (Zone-D post + skybox), so the warm-graded look + the IMGUI overlay match the Sponsor's view.
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null) orbit.enabled = false;
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[LootPromptVerifyCapture] no Camera.main — cannot frame the prompt");
                yield return null; Application.Quit(1); yield break;
            }
            cam.fieldOfView = 40f;
            var camGo = cam.gameObject;

            for (int i = 0; i < warmupFrames; i++) yield return null;

            // === CONTRAST FRAME — player AT spawn, prompt HIDDEN (the hide case, for reviewer contrast) ===
            // Frame the spawn player so the captured frame is gameplay-representative; the prompt is correctly
            // hidden (player is outside the tightened range), so this frame shows the band EMPTY.
            FrameOverShoulder(camGo, player.position);
            for (int i = 0; i < settleFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();
            IPickable farTarget = looter.NearestInRange();
            string farLabel = LootPrompt.BuildLabel(farTarget, looter.lootKey);
            Debug.Log("[LootPromptVerifyCapture] FAR (spawn): NearestInRange=" +
                      (farTarget != null ? farTarget.DisplayName : "null") + " label=\"" + farLabel + "\" " +
                      "(expect HIDDEN — empty label when out of range)");
            ShotTo(Path.Combine(dir, "loot_prompt_far.png"));
            yield return new WaitForEndOfFrame();
            yield return null;

            // === SHOW FRAME — teleport the player INTO loot range of the bush, prompt SHOWING ===
            // Stand the player standFraction × the bush's own LootRange away, on the camera-near side of the bush
            // so the over-shoulder framing shows the player facing the bush with the prompt centred-low in frame.
            float range = ((IPickable)bush).LootRange;
            Vector3 bushPos = bush.transform.position;
            // Approach from the -viewYaw direction so the camera (placed behind the player along viewYaw) looks
            // PAST the player TO the bush — player in lower frame, bush ahead, prompt band centred low.
            Quaternion yawRot = Quaternion.Euler(0f, viewYaw, 0f);
            Vector3 approach = yawRot * Vector3.back; // unit dir from the bush toward the camera-near side
            Vector3 standPos = bushPos + approach * (range * standFraction);
            standPos.y = player.position.y; // keep the player's grounded Y (don't lift off the terrain)
            player.position = standPos;
            // Face the player toward the bush (cosmetic — the prompt is range-driven, not facing-driven — but it
            // reads right in the capture).
            Vector3 toBush = bushPos - standPos; toBush.y = 0f;
            if (toBush.sqrMagnitude > 1e-4f) player.rotation = Quaternion.LookRotation(toBush);

            // Let the looter's Update + the prompt's Update (resolve → _label) + OnGUI (paint) all run on the new
            // position. The prompt resolves in Update and paints in OnGUI, so a few frames + an end-of-frame.
            FrameOverShoulder(camGo, player.position);
            for (int i = 0; i < settleFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();

            // SELF-ASSERT 1 — SHOW STATE REACHED (logic): the looter resolves the bush + the prompt label is set.
            IPickable target = looter.NearestInRange();
            bool resolvedBush = ReferenceEquals(target, (IPickable)bush);
            string label = LootPrompt.BuildLabel(target, looter.lootKey);
            bool labelShown = !string.IsNullOrEmpty(label);
            float planarDist = Vector2.Distance(new Vector2(standPos.x, standPos.z), new Vector2(bushPos.x, bushPos.z));
            Debug.Log("[LootPromptVerifyCapture] SHOW: stood at " + standPos.ToString("F2") + " planarDist=" +
                      planarDist.ToString("F2") + " (range=" + range.ToString("F2") + "); NearestInRange=" +
                      (target != null ? target.DisplayName : "null") + " resolvedBush=" + resolvedBush +
                      " label=\"" + label + "\" labelShown=" + labelShown);

            // Capture the SHOW frame BEFORE the pixel read (ScreenCapture writes the rendered frame to disk;
            // ReadPixels reads the same backbuffer for the rendered-band assert).
            ShotTo(Path.Combine(dir, "loot_prompt.png"));
            yield return new WaitForEndOfFrame();

            // SELF-ASSERT 2 — PROMPT RENDERED (the frame): the centred-low prompt band reads DISTINCT from a
            // control band just above it. A correctly-painted dark plate + cream text differs measurably from
            // the terrain/sky above; an IMGUI that silently failed to render leaves the band == the control.
            bool promptRendered = CheckPromptBandRendered(out float bandLuma, out float ctrlLuma,
                                                          out float bandVar, out float deltaLuma, out float darkFrac);
            Debug.Log("[LootPromptVerifyCapture] RENDER: prompt-band luma=" + bandLuma.ToString("F3") +
                      " control-band luma=" + ctrlLuma.ToString("F3") + " |delta|=" + deltaLuma.ToString("F3") +
                      " bandVar=" + bandVar.ToString("F4") + " darkPlateFrac=" + darkFrac.ToString("F3") +
                      " -> rendered=" + promptRendered);

            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            bool pass = resolvedBush && labelShown && promptRendered;
            Debug.Log("[LootPromptVerifyCapture] verification complete (resolvedBush=" + resolvedBush +
                      " labelShown=" + labelShown + " promptRendered=" + promptRendered + ") => PASS=" + pass +
                      " -> " + dir);
            // Fail loud in the shipped build if the prompt SHOW state was not reached (logic) OR the IMGUI tooltip
            // did not actually paint into the frame (render) — the exe exit code IS the gate verdict.
            Application.Quit(pass ? 0 : 1);
        }

        // Park the camera over-shoulder on a world target at the gameplay orbit pitch/yaw/distance, looking at
        // the target (raised a touch toward the player's torso so the framing reads as gameplay, not feet).
        private void FrameOverShoulder(GameObject camGo, Vector3 target)
        {
            Vector3 look = target + new Vector3(0f, 1.0f, 0f);
            Quaternion rot = Quaternion.Euler(viewPitch, viewYaw, 0f);
            Vector3 offset = rot * new Vector3(0f, 0f, -viewDistance);
            camGo.transform.position = look + offset;
            camGo.transform.LookAt(look);
        }

        /// <summary>
        /// Read back the SHOW frame and assert the LootPrompt's IMGUI band actually RENDERED. The prompt draws a
        /// w=360 h=34 plate centred horizontally at y = Screen.height - 96 (LootPrompt.OnGUI), with a low-alpha
        /// DARK plate (PlateAlpha 0.55 black over the scene) + CREAM bold text. We sample that band (in
        /// ReadPixels bottom-left coords, so the band is near the BOTTOM of the buffer) and a CONTROL band the
        /// same size just ABOVE it (clear of the plate). A painted prompt makes the band read measurably DIFFERENT
        /// from the control (the dark plate drags luma down + the cream text adds bright neutral pixels → higher
        /// variance + a dark-pixel fraction the control lacks). Returns true when the band reads as a rendered
        /// prompt (distinct from the control AND carrying the dark-plate signature). Pure geometry of the OnGUI
        /// rect — no scene knowledge — so it tracks the prompt's actual draw position.
        /// </summary>
        private bool CheckPromptBandRendered(out float bandLuma, out float controlLuma, out float bandVariance,
                                             out float deltaLuma, out float darkPlateFrac)
        {
            int w = Screen.width, h = Screen.height;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            // OnGUI rect (top-left origin): x = (w-360)/2, y_topleft = h - 96, height 34. Convert to ReadPixels
            // bottom-left coords: yBL = h - (y_topleft + height) = h - (h - 96 + 34) = 96 - 34 = 62 from the
            // bottom. Sample a band a touch INSIDE the plate so anti-aliased edges don't dilute the read.
            const float plateW = 360f, plateH = 34f;
            int bx0 = Mathf.Clamp((int)((w - plateW) * 0.5f) + 6, 0, w - 1);
            int bx1 = Mathf.Clamp((int)((w + plateW) * 0.5f) - 6, 0, w);
            // y_topleft of the plate top = h - 96; plate bottom (top-left) = h - 96 + 34 = h - 62.
            // bottom-left band: from yBL = 62 + 4 up to yBL = 62 + 34 - 4.
            int byBLbottom = 62;
            int by0 = Mathf.Clamp(byBLbottom + 4, 0, h - 1);
            int by1 = Mathf.Clamp(byBLbottom + (int)plateH - 4, 0, h);
            // Control band: same x, an equal-height band ABOVE the plate (further from the bottom), clear of it.
            int cy0 = Mathf.Clamp(byBLbottom + (int)plateH + 24, 0, h - 1);
            int cy1 = Mathf.Clamp(cy0 + ((int)plateH - 8), 0, h);

            double bandSum = 0, bandSum2 = 0; int bandN = 0; int dark = 0;
            for (int y = by0; y < by1; y++)
            for (int x = bx0; x < bx1; x++)
            {
                Color c = tex.GetPixel(x, y);
                float luma = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                bandSum += luma; bandSum2 += luma * luma; bandN++;
                // Dark-plate signature: the 0.55-alpha black plate pulls scene pixels notably dark + near-neutral.
                float maxc = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                float minc = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                if (luma < 0.35f && (maxc - minc) < 0.18f) dark++;
            }
            double ctrlSum = 0; int ctrlN = 0;
            for (int y = cy0; y < cy1; y++)
            for (int x = bx0; x < bx1; x++)
            {
                Color c = tex.GetPixel(x, y);
                ctrlSum += 0.299f * c.r + 0.587f * c.g + 0.114f * c.b; ctrlN++;
            }
            Object.Destroy(tex);

            bandLuma = bandN > 0 ? (float)(bandSum / bandN) : 0f;
            controlLuma = ctrlN > 0 ? (float)(ctrlSum / ctrlN) : 0f;
            bandVariance = bandN > 0 ? Mathf.Max(0f, (float)(bandSum2 / bandN) - bandLuma * bandLuma) : 0f;
            deltaLuma = Mathf.Abs(bandLuma - controlLuma);
            darkPlateFrac = bandN > 0 ? (float)dark / bandN : 0f;

            // RENDERED iff the band carries the dark-plate signature (a clear fraction of dark near-neutral pixels
            // the control lacks) AND it reads distinct from the control band (the plate+text changed the band).
            // Either a strong dark-plate fraction OR a clear luma delta with band texture proves the IMGUI painted;
            // require the dark-plate fraction as the load-bearing signal (the plate is the unmistakable tell — the
            // scene behind it is varied terrain/sky that the 0.55 black plate visibly darkens).
            return darkPlateFrac > 0.20f && deltaLuma > 0.02f;
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[LootPromptVerifyCapture] wrote " + file);
        }

        private string ResolveDir()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-captureDir") return Path.GetFullPath(args[i + 1]);
            string baseDir = Application.isEditor
                ? Path.Combine(Application.dataPath, "..", subDir)
                : Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", subDir);
            return Path.GetFullPath(baseDir);
        }

        private bool HasArg(string flag)
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
                if (a == flag) return true;
            return false;
        }
    }
}
