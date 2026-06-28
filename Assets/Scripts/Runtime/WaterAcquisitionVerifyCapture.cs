using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the WATER ACQUISITION loop (ticket 86cafc6vx AC6 — the GET
    /// side that closes the thirst loop). Sibling of LootPromptVerifyCapture / FreshwaterPondVerifyCapture: a
    /// -verifyWater flag that, in the BUILT exe, drives the WHOLE end-to-end loop at the pond and SELF-ASSERTS it:
    /// "Press E to collect water" prompt SHOWS near the pond → E loots ONE water into the inventory → a left-click
    /// on the selected water drinks it → the thirst bar RISES. Captures frames at each beat + quits non-zero on
    /// any failure, so a reviewer's re-run is a real gate, not a smoke test.
    ///
    /// WHY THIS HOOK EXISTS (the shipped-build capture gate — CLAUDE.md hard rule; unity-conventions.md §editor-
    /// vs-runtime): the loop LOGIC is green in EditMode + PlayMode + wired in Boot.unity, but the generic CI
    /// -captureGate only shoots the DEFAULT SPAWN frame — the player stands mid-field, OUTSIDE the pond's loot
    /// range, so the "collect water" prompt is correctly HIDDEN and NO water/drink beat is exercised. The one
    /// surface this ticket delivers — collecting water at the pond + drinking it — had ZERO built-frame evidence.
    /// This hook positions the player IN the pond's loot range so LootPrompt resolves the pond + paints "Press E
    /// to collect water", drives the loot + drink seams (the input-independent RequestLoot / RequestUseClick
    /// analogs, since the shipped exe has no key/mouse device here), and asserts the inventory + thirst deltas.
    ///
    /// THE DRIVE (no input device in the shipped exe — teleport the player into range, then drive the seams):
    ///   • find the FreshwaterPond (the IPickable water source) + the player's PickableLooter + LootPrompt +
    ///     the LeftClickConsume + the ThirstNeed — the SAME components Boot.unity ships (found, not assumed);
    ///   • NAVMESH-SAFE teleport the looter.player to ~0.6 × the pond's own LootRange from the pond via
    ///     agent.Warp (the canonical ClickToMove teleport seam — a raw transform set near the pond would be
    ///     re-snapped back onto navmesh by the agent, dragging the player out of range; LootPromptVerifyCapture's
    ///     #162 fix). NEVER ClickToMove.MoveTo (DEAD under WASD locomotion — WasdMovement zeroes agent velocity).
    ///
    /// SELF-ASSERTS (guard the deliverable, not a proxy):
    ///   1. PROMPT SHOW STATE: looter.NearestInRange() == the pond AND LootPrompt.BuildLabel(...) ==
    ///      "Press E to collect water" — the prompt's _label is set (OnGUI draws, not hidden);
    ///   2. PROMPT RENDERED: the centred-low prompt band reads DISTINCT from a control band above it (the IMGUI
    ///      plate+label actually painted into the swapchain frame, not just logic-green);
    ///   3. E-LOOT: PickableLooter.RequestLoot() → exactly one WaterId entered the inventory (the GET side, AC1);
    ///   4. DRINK: select the water in the belt + LeftClickConsume.RequestUseClick() → thirst RISES (AC3).
    /// Quits non-zero on ANY failure (or a missing component — a build-side regression).
    ///
    /// Inert unless launched with -verifyWater (the normal game / boot capture is unaffected):
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyWater [-captureDir &lt;dir&gt;]
    /// Captures water_prompt.png (player in range → "collect water" prompt SHOWING) + water_drink.png (after the
    /// drink). MUST run WINDOWED, not -batchmode (ScreenCapture needs a real swapchain — spike iter-4).
    ///
    /// ISOLATION (the own-file / own-flag rule): a NEW isolated file with its OWN -verifyWater flag + its OWN
    /// verify_water_gate.sh + its OWN ci.yml step, so it never collides with other in-flight capture work.
    ///
    /// NO MUTABLE STATICS (instance state only) — needs no [RuntimeInitializeOnLoadMethod] reset.
    /// </summary>
    public class WaterAcquisitionVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        // Gameplay-representative over-shoulder framing (the orbit pitch the player sees — NOT a top-down editor
        // angle; memory verify-grounding-soaks-by-gameplay-cam-visual). Mirrors LootPromptVerifyCapture.
        public float viewYaw = 25f;
        public float viewPitch = 22f;
        public float viewDistance = 6.5f;
        public int warmupFrames = 8;
        public int settleFrames = 16;
        // Fraction of the pond's OWN LootRange to stand the player at — well inside reach so NearestInRange()
        // resolves the pond with margin (a value near 1.0 would sit on the boundary where capture jitter flips
        // in/out of range). 0.6 is "genuinely at the water's edge", matching the visible waterline.
        public float standFraction = 0.6f;

        void Start()
        {
            if (HasArg("-verifyWater"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // === Find the components the deliverable rides on (found, not assumed) ===
            var pond = Object.FindAnyObjectByType<FreshwaterPond>();
            var looter = Object.FindAnyObjectByType<PickableLooter>();
            var prompt = Object.FindAnyObjectByType<LootPrompt>();
            var consume = Object.FindAnyObjectByType<LeftClickConsume>();
            var thirst = Object.FindAnyObjectByType<ThirstNeed>();
            Debug.Log("[WaterAcquisitionVerifyCapture] pond=" + (pond != null) + " looter=" + (looter != null) +
                      " prompt=" + (prompt != null) + " consume=" + (consume != null) + " thirst=" + (thirst != null));
            if (pond == null || looter == null || prompt == null || consume == null || thirst == null)
            {
                Debug.LogError("[WaterAcquisitionVerifyCapture] a required component is missing from Boot.unity " +
                               "(pond/looter/prompt/consume/thirst) — the water-acquisition wiring is absent " +
                               "(build-side regression signal)");
                yield return null; Application.Quit(1); yield break;
            }

            var pondPick = (IPickable)pond;
            var inventory = looter.inventory != null ? looter.inventory : Object.FindAnyObjectByType<Inventory>();
            if (inventory == null)
            {
                Debug.LogError("[WaterAcquisitionVerifyCapture] no Inventory in scene — cannot prove the GET side");
                yield return null; Application.Quit(1); yield break;
            }

            // Park the orbit rig; park the camera by hand (the proven SeaVerifyCapture / LootPromptVerifyCapture
            // pattern). Camera.main rides the gameplay render path (Zone-D post + skybox) so the look matches play.
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null) orbit.enabled = false;
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[WaterAcquisitionVerifyCapture] no Camera.main — cannot frame the prompt");
                yield return null; Application.Quit(1); yield break;
            }
            cam.fieldOfView = 40f;
            var camGo = cam.gameObject;

            Transform player = looter.player != null ? looter.player : looter.transform;
            for (int i = 0; i < warmupFrames; i++) yield return null;

            // === Teleport the player INTO the pond's loot range (agent.Warp — NOT a raw set / NOT MoveTo) ===
            float range = pondPick.LootRange;
            Vector3 pondPos = pondPick.LootPosition;
            Quaternion yawRot = Quaternion.Euler(0f, viewYaw, 0f);
            Vector3 approach = yawRot * Vector3.back;                  // from the pond toward the camera-near side
            Vector3 standPos = pondPos + approach * (range * standFraction);
            standPos.y = pondPos.y;
            TeleportPlayer(player, standPos);
            Vector3 actualPos = player.position;
            Vector3 toPond = pondPos - actualPos; toPond.y = 0f;
            if (toPond.sqrMagnitude > 1e-4f) player.rotation = Quaternion.LookRotation(toPond);

            // Let the looter + prompt Update (resolve → _label) + OnGUI (paint) run on the new position.
            FrameOverShoulder(camGo, player.position);
            for (int i = 0; i < settleFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();

            // SELF-ASSERT 1 — the prompt SHOW state: the looter resolves the POND + the label is "collect water".
            IPickable target = looter.NearestInRange();
            bool resolvedPond = ReferenceEquals(target, pondPick);
            string label = LootPrompt.BuildLabel(target, looter.lootKey);
            bool labelIsCollectWater = label == "Press " + looter.lootKey + " to collect water";
            float planarDist = Vector2.Distance(new Vector2(actualPos.x, actualPos.z), new Vector2(pondPos.x, pondPos.z));
            Debug.Log("[WaterAcquisitionVerifyCapture] PROMPT: stood at " + actualPos.ToString("F2") + " planarDist=" +
                      planarDist.ToString("F2") + " (range=" + range.ToString("F2") + "); NearestInRange=" +
                      (target != null ? target.DisplayName : "null") + " resolvedPond=" + resolvedPond +
                      " label=\"" + label + "\" isCollectWater=" + labelIsCollectWater);

            ShotTo(Path.Combine(dir, "water_prompt.png"));
            yield return new WaitForEndOfFrame();

            // SELF-ASSERT 2 — the prompt RENDERED (the centred-low band reads DISTINCT from a control band above).
            bool promptRendered = CheckPromptBandRendered(out float bandLuma, out float ctrlLuma, out float deltaLuma,
                                                          out float darkFrac);
            Debug.Log("[WaterAcquisitionVerifyCapture] RENDER: band luma=" + bandLuma.ToString("F3") + " control=" +
                      ctrlLuma.ToString("F3") + " |delta|=" + deltaLuma.ToString("F3") + " darkFrac=" +
                      darkFrac.ToString("F3") + " -> rendered=" + promptRendered);

            // SELF-ASSERT 3 — the E-LOOT (the GET side, AC1): RequestLoot → exactly one WaterId in the inventory.
            int waterBefore = inventory.Model.CountItem(ItemCatalog.WaterId);
            looter.RequestLoot();
            yield return null; // the looter consumes the latch on Update
            yield return null;
            int waterAfter = inventory.Model.CountItem(ItemCatalog.WaterId);
            bool gotOneWater = waterAfter == waterBefore + 1;
            Debug.Log("[WaterAcquisitionVerifyCapture] LOOT: water " + waterBefore + " -> " + waterAfter +
                      " (E loots ONE water -> gotOneWater=" + gotOneWater + ")");

            // SELF-ASSERT 4 — the DRINK (AC3): select the water in the belt + RequestUseClick → thirst RISES.
            bool selectedWater = SelectWaterInBelt(inventory);
            float thirstBefore01 = thirst.Current01;
            consume.RequestUseClick();
            yield return null; // the consume consumes the latch on Update
            yield return null;
            float thirstAfter01 = thirst.Current01;
            bool thirstRose = thirstAfter01 > thirstBefore01 + 1e-4f;
            Debug.Log("[WaterAcquisitionVerifyCapture] DRINK: selectedWater=" + selectedWater + " thirst01 " +
                      thirstBefore01.ToString("F3") + " -> " + thirstAfter01.ToString("F3") +
                      " (left-click drink raises thirst -> thirstRose=" + thirstRose + ")");

            // Capture the post-drink frame (the gameplay view after the loop completed).
            FrameOverShoulder(camGo, player.position);
            for (int i = 0; i < 6; i++) yield return null;
            yield return new WaitForEndOfFrame();
            ShotTo(Path.Combine(dir, "water_drink.png"));
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.5f);

            bool pass = resolvedPond && labelIsCollectWater && promptRendered && gotOneWater && selectedWater && thirstRose;
            Debug.Log("[WaterAcquisitionVerifyCapture] verification complete (resolvedPond=" + resolvedPond +
                      " collectWaterLabel=" + labelIsCollectWater + " promptRendered=" + promptRendered +
                      " gotOneWater=" + gotOneWater + " thirstRose=" + thirstRose + ") => PASS=" + pass + " -> " + dir);
            Application.Quit(pass ? 0 : 1);
        }

        // Move the looted water to a belt slot + select it so left-click consumes it. Water is a belt-eligible
        // Consumable (#152) so AddItem already fills the belt; select the slot that holds water.
        private static bool SelectWaterInBelt(Inventory inventory)
        {
            var belt = inventory.Model.BeltSlots;
            for (int i = 0; i < belt.Count; i++)
            {
                if (!belt[i].IsEmpty && belt[i].Def.Id == ItemCatalog.WaterId)
                {
                    inventory.Model.SelectBelt(i);
                    return true;
                }
            }
            return false;
        }

        // NavMesh-safe teleport (the #162 fix — LootPromptVerifyCapture's pattern): the player carries a
        // NavMeshAgent (ClickToMove requires one) which OWNS the transform — a raw `player.position = standPos`
        // gets re-projected back onto valid navmesh on the agent's next update, dragging the player AWAY from the
        // pond before NearestInRange() reads → resolvedPond=False. Warp the AGENT so its internal position tracks
        // the teleport. NEVER ClickToMove.MoveTo (DEAD under WASD locomotion). Fall back to a raw set only when
        // there is no agent (a degenerate rig). The looter resolves PLANAR (XZ) distance, so a sub-unit snap is fine.
        private static void TeleportPlayer(Transform player, Vector3 standPos)
        {
            var agent = player.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                Vector3 warpTo = standPos;
                if (NavMesh.SamplePosition(standPos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    warpTo = hit.position;
                if (agent.Warp(warpTo)) return; // agent now owns this position; the transform stays put
            }
            player.position = standPos; // no-agent fallback
        }

        // Park the camera over-shoulder on a world target at the gameplay orbit pitch/yaw/distance.
        private void FrameOverShoulder(GameObject camGo, Vector3 target)
        {
            Vector3 look = target + new Vector3(0f, 1.0f, 0f);
            Quaternion rot = Quaternion.Euler(viewPitch, viewYaw, 0f);
            Vector3 offset = rot * new Vector3(0f, 0f, -viewDistance);
            camGo.transform.position = look + offset;
            camGo.transform.LookAt(look);
        }

        // Read back the SHOW frame + assert the LootPrompt's IMGUI band actually RENDERED (the SAME band geometry
        // + luma-delta signal as LootPromptVerifyCapture.CheckPromptBandRendered — the prompt draws a w=360 h=34
        // plate centred horizontally at y = Screen.height - 96, a 0.55-alpha dark plate + cream text). A painted
        // prompt makes the band read measurably DARKER than a control band just above it (luma delta); an IMGUI
        // that silently failed to render leaves the band == the control. The luma DELTA is the primary, overlay-
        // independent signal; the dark-plate FRACTION is a re-calibrated secondary OR-path (PR #162 decoupling).
        private bool CheckPromptBandRendered(out float bandLuma, out float controlLuma, out float deltaLuma,
                                             out float darkPlateFrac)
        {
            int w = Screen.width, h = Screen.height;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            const float plateW = 360f, plateH = 34f;
            int bx0 = Mathf.Clamp((int)((w - plateW) * 0.5f) + 6, 0, w - 1);
            int bx1 = Mathf.Clamp((int)((w + plateW) * 0.5f) - 6, 0, w);
            int byBLbottom = 62; // bottom-left y of the prompt band (h - (h-96+34) = 62)
            int by0 = Mathf.Clamp(byBLbottom + 4, 0, h - 1);
            int by1 = Mathf.Clamp(byBLbottom + (int)plateH - 4, 0, h);
            int cy0 = Mathf.Clamp(byBLbottom + (int)plateH + 24, 0, h - 1);
            int cy1 = Mathf.Clamp(cy0 + ((int)plateH - 8), 0, h);

            double bandSum = 0; int bandN = 0; int dark = 0;
            for (int y = by0; y < by1; y++)
            for (int x = bx0; x < bx1; x++)
            {
                Color c = tex.GetPixel(x, y);
                float luma = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                bandSum += luma; bandN++;
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
            deltaLuma = Mathf.Abs(bandLuma - controlLuma);
            darkPlateFrac = bandN > 0 ? (float)dark / bandN : 0f;
            return deltaLuma > 0.10f || darkPlateFrac > 0.05f;
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[WaterAcquisitionVerifyCapture] wrote " + file);
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
