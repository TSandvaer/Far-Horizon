using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace FarHorizon
{
    /// <summary>
    /// Verification-only shipped-build capture for the FIND-IN-WORLD weapon (ticket 86cah7y5b AC6). Sibling of
    /// <see cref="WaterAcquisitionVerifyCapture"/> / <see cref="LootPromptVerifyCapture"/>: a <c>-verifyWeaponFind</c>
    /// flag that, in the BUILT exe, walks the WHOLE find route and SELF-ASSERTS it, then quits non-zero on any
    /// failure — so a reviewer's re-run is a real gate, not a smoke test.
    ///
    /// WHY THIS HOOK EXISTS — the class it is built to catch:
    /// <b>a PlayMode `renderer.enabled` assert has already let an INVISIBLE-IN-HAND weapon ship twice</b>
    /// (soak-3 / soak-4, 86cav8y74). Logic-green in EditMode/PlayMode is NOT evidence the piece is actually in
    /// the castaway's hand in the IL2CPP frame. So this gate asserts the in-hand state THREE ways that a bare
    /// `enabled` flag cannot fake — the seat renderer is enabled AND the belt→held sync selected the IRON SWORD
    /// family index AND the seat's world bounds are non-degenerate and inside the camera frustum — and then
    /// captures the frame so a human eyeballs it.
    ///
    /// PHYSICAL-WORLD ANCHOR + SIDE PROFILE (lowpoly-quality.md §0; Sponsor directive 2026-06-24, the PR #130
    /// pond→mound lesson): the find is an iron sword driven POINT-DOWN INTO a weathered stump — blade buried,
    /// grip up, a thing you pull UP and OUT. Blade-down-vs-blade-up is invisible from above and at player-eye
    /// and OBVIOUS side-on, so this gate shoots a dedicated SIDE-PROFILE frame (weaponfind_side.png) alongside
    /// the gameplay one, and asserts the geometric fact behind the anchor: the weapon's lowest point sits BELOW
    /// the stump's top face (it really is IN the wood) at the worst point of whatever motion its placement
    /// permits — which, for the shipped Embedded find, is none at all. "Driven into" is a claim about a FIXED
    /// relationship, so the anchor now has a motion half as well as a geometric one (self-assert 1b).
    ///
    /// THE DRIVE (no input device in the shipped exe — teleport into range, then drive the seams):
    ///   • find the active <see cref="FarHorizon.Combat.WorldWeaponFind"/>, the player's <see cref="PickableLooter"/>
    ///     + <see cref="LootPrompt"/>, and the held-seat <see cref="HeldWeaponCycleDebug"/> — the SAME components
    ///     Boot.unity ships (found, not assumed);
    ///   • NAVMESH-SAFE teleport via agent.Warp (the canonical seam — a raw transform set gets re-snapped by the
    ///     agent and drags the player out of range; the #162 fix). NEVER ClickToMove.MoveTo (DEAD under WASD).
    ///
    /// SELF-ASSERTS (guard the deliverable, not a proxy):
    ///   1. RESTING + EMBEDDED: a find is active in the seeded scene, CanLoot, and its blade is BELOW the stump
    ///      top even at peak bob (the real-world anchor, checked geometrically — not by eye alone);
    ///   1b. THE PLACEMENT → MOTION GATE (AC7): the weapon transform is SAMPLED over ~90 frames and must match
    ///      what its <see cref="FarHorizon.Combat.FindPlacement"/> demands — DEAD STILL for an Embedded /
    ///      RestingOn find, both channels LIVE for a Loose one. The STILL half is the regression guard for the
    ///      2026-08-02 soak rejection ("the sword is floating, moving in the stump"); it is measured from the
    ///      frame rather than read off the placement field precisely so that a bypass of the gate in
    ///      <c>Update</c>, or any other component writing that transform, still reds it;
    ///   2. PROMPT: looter.NearestInRange() resolves the FIND and the shared LootPrompt label reads
    ///      "Press E to pick up an iron sword" (the existing widget, no second prompt);
    ///   3. E-LOOT: RequestLoot() → exactly ONE sword_iron entered the inventory; a SECOND RequestLoot adds
    ///      NOTHING (the find is spent — AC6's "a second E does nothing");
    ///   4. IN-HAND: select the sword in the belt → the held seat renderer is ENABLED, the belt→held sync landed
    ///      on SwordIronFamilyIndex, and the seat's bounds are real + on-screen.
    /// Quits non-zero on ANY failure (or a missing component — a build-side regression signal).
    ///
    /// Inert unless launched with -verifyWeaponFind (the normal game / boot capture is unaffected):
    ///   FarHorizon.exe -screen-fullscreen 0 -verifyWeaponFind [-captureDir &lt;dir&gt;]
    /// MUST run WINDOWED, not -batchmode (ScreenCapture needs a real swapchain — spike iter-4).
    ///
    /// ISOLATION (the own-file / own-flag rule): a NEW isolated file with its OWN -verifyWeaponFind flag + its
    /// OWN verify_weaponfind_gate.sh, so it never collides with other in-flight capture work.
    ///
    /// NO MUTABLE STATICS (instance state only) — needs no [RuntimeInitializeOnLoadMethod] reset.
    /// </summary>
    public class WeaponFindVerifyCapture : MonoBehaviour
    {
        public string subDir = "Captures";
        // Gameplay-representative over-shoulder framing (the orbit pitch the player actually sees — NOT a
        // top-down editor angle; [[verify-grounding-soaks-by-gameplay-cam-visual]]).
        public float viewYaw = 25f;
        public float viewPitch = 18f;
        public float viewDistance = 5.5f;
        // SIDE-PROFILE framing: a level, near-horizontal eye at about the stump's own height, so blade-DOWN /
        // grip-UP is unmistakable. Pitch ~6° (not 0) so the ground plane still reads and the silhouette is not
        // floating in a void. CLOSE (1.5u) and on the -90° side: the first draft used +90° at 2.6u and put the
        // camera INSIDE a scatter rock, which filled the entire frame — the side shot showed neither the stump
        // nor the sword. A close camera clears the surrounding scatter; the subject is ~0.8u tall, so 1.5u
        // still frames it whole.
        public float sidePitch = 6f;
        public float sideDistance = 1.5f;
        // Height above the site origin the side profile looks AT — the stump's top face, where the anchor's
        // "blade below / grip above" boundary actually is.
        public float sideLookHeight = 0.5f;
        public int warmupFrames = 8;
        public int settleFrames = 16;
        // Fraction of the find's OWN LootRange to stand the player at — well inside reach so NearestInRange()
        // resolves it with margin (near 1.0 would sit on the boundary where capture jitter flips in/out).
        public float standFraction = 0.6f;

        void Start()
        {
            if (HasArg("-verifyWeaponFind"))
                StartCoroutine(RunVerification());
        }

        private IEnumerator RunVerification()
        {
            string dir = ResolveDir();
            Directory.CreateDirectory(dir);

            // === Find the components the deliverable rides on (found, not assumed) ===
            var pool = Object.FindAnyObjectByType<FarHorizon.Combat.WeaponFindPool>();
            var looter = Object.FindAnyObjectByType<PickableLooter>();
            var prompt = Object.FindAnyObjectByType<LootPrompt>();
            var cycle = Object.FindAnyObjectByType<HeldWeaponCycleDebug>();
            FarHorizon.Combat.WorldWeaponFind find = ResolveActiveFind();
            Debug.Log("[WeaponFindVerifyCapture] pool=" + (pool != null) + " find=" + (find != null) +
                      " looter=" + (looter != null) + " prompt=" + (prompt != null) + " heldSeat=" + (cycle != null));
            if (find == null || looter == null || prompt == null || cycle == null)
            {
                // DIAGNOSE, don't just fail. `find=False` has TWO very different causes and the first run of
                // this gate reported it with a message that named only one of them — sending the reader hunting
                // for absent wiring when the wiring was perfect and the sites had merely been switched OFF at
                // runtime (the sentinel-clobber fixed in WeaponFindPool.ActiveFindCount). Separate the lenses
                // so the log says WHICH it is:
                //   authored==0            → the scene really is missing the finds (a bootstrap regression);
                //   authored>0, active==0  → they exist but every site is disabled (a findability-dial bug).
                int authored = Object.FindObjectsByType<FarHorizon.Combat.WorldWeaponFind>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                int active = Object.FindObjectsByType<FarHorizon.Combat.WorldWeaponFind>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
                Debug.LogError("[WeaponFindVerifyCapture] a required component is missing from Boot.unity " +
                               "(find/looter/prompt/heldSeat). WorldWeaponFind sites: authored(Include)=" +
                               authored + " active(Exclude)=" + active +
                               (authored > 0 && active == 0
                                   ? " — the sites ARE authored but EVERY ONE is disabled: this is a findability-dial " +
                                     "bug (WeaponFindPool active count resolved to 0), NOT absent wiring"
                                   : " — the find-in-world wiring is absent (build-side regression signal)") +
                               "; poolActiveCount=" + (pool != null ? pool.ActiveFindCount.ToString() : "n/a"));
                yield return null; Application.Quit(1); yield break;
            }

            var findPick = (IPickable)find;
            var inventory = looter.inventory != null ? looter.inventory : Object.FindAnyObjectByType<Inventory>();
            if (inventory == null)
            {
                Debug.LogError("[WeaponFindVerifyCapture] no Inventory in scene — cannot prove the loot");
                yield return null; Application.Quit(1); yield break;
            }

            // Park the orbit rig; drive the camera by hand (the proven LootPromptVerifyCapture pattern).
            // Camera.main rides the gameplay render path (Zone-D post + skybox) so the look matches play.
            var orbit = Object.FindAnyObjectByType<OrbitCamera>();
            if (orbit != null) orbit.enabled = false;
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[WeaponFindVerifyCapture] no Camera.main — cannot frame the find");
                yield return null; Application.Quit(1); yield break;
            }
            cam.fieldOfView = 40f;
            var camGo = cam.gameObject;

            Transform player = looter.player != null ? looter.player : looter.transform;
            for (int i = 0; i < warmupFrames; i++) yield return null;

            // === SELF-ASSERT 1 — RESTING + EMBEDDED (the real-world anchor, checked geometrically) ===
            // The blade must sit BELOW the stump's top face even at the TOP of the attract bob. If the bob ever
            // lifted the tip clear of the wood the piece would read as a hovering pickup, not a sword left in a
            // stump — the anchor sentence, not a colour/byte metric ([[physical-features-anchor-realworld-not-metric]]).
            bool restingCanLoot = findPick.CanLoot;
            bool embedded = CheckBladeEmbedded(find, out float weaponLowY, out float weaponHighY,
                                               out float stumpTopY, out float peakBob,
                                               out float planarOff, out float stumpReach);
            Debug.Log("[WeaponFindVerifyCapture] REST: canLoot=" + restingCanLoot + " bladeTipY=" +
                      weaponLowY.ToString("F3") + " gripTopY=" + weaponHighY.ToString("F3") + " stumpTopY=" +
                      stumpTopY.ToString("F3") + " peakBob=+/-" + peakBob.ToString("F3") +
                      " -> bladeInTheWood=" + ((weaponLowY + peakBob) < stumpTopY) +
                      " gripStandsProud=" + ((weaponHighY - peakBob) > stumpTopY + 0.1f) +
                      " bladeOverTheStump=" + (planarOff <= stumpReach) + " (planarOff=" +
                      planarOff.ToString("F3") + "u stumpReach=" + stumpReach.ToString("F3") + "u)" +
                      " => anchorHolds=" + embedded);

            // === SELF-ASSERT 1b — THE PLACEMENT → MOTION GATE, MEASURED IN THE SHIPPED FRAME (AC7) ===
            // The Sponsor's rule (2026-08-02 soak): an item DRIVEN INTO or RESTING ON something is STILL; an
            // item LYING LOOSE may bob. This assert samples the REAL transform over real frames and requires
            // whichever behaviour the find's placement demands — so it is a two-sided gate, not a one-sided one:
            //
            //   STILL placement (Embedded / RestingOn) → BOTH channels must be DEAD, exactly.
            //     This is the direct regression guard for the defect the soak rejected verbatim: "the sword is
            //     floating, moving in the stump". It reds if anyone re-enables the cue on an embedded find, by
            //     any route — flipping the placement, bypassing the Effective* accessors in Update, or a future
            //     component writing the same transform. A `placement == Embedded` field read could NOT catch the
            //     last two; sampling the frame can.
            //
            //   LOOSE placement → BOTH channels must be LIVE (the original bar: a cue must not rest on a SINGLE
            //     channel, and "the code for it exists" is not evidence the frame moves — a pool re-apply, a
            //     zeroed amplitude or a visual ref on the wrong transform all leave the code intact and the
            //     frame dead).
            //
            // The shipped `sword_iron` find takes the STILL branch. The LOOSE branch has no shipped instance
            // today and is kept because the rule is general: the first Loose find authored gets its cue gated by
            // the same measurement rather than by a fresh promise.
            var cueT = find.visual != null ? find.visual : find.transform;
            float minY = float.MaxValue, maxY = float.MinValue, minYaw = float.MaxValue, maxYaw = float.MinValue;
            for (int i = 0; i < 90; i++)   // ~1.5s at 60fps — over a full period of the slower (sway) channel
            {
                minY = Mathf.Min(minY, cueT.localPosition.y);
                maxY = Mathf.Max(maxY, cueT.localPosition.y);
                float yaw = cueT.localRotation.eulerAngles.y;
                minYaw = Mathf.Min(minYaw, yaw);
                maxYaw = Mathf.Max(maxYaw, yaw);
                yield return null;
            }
            float bobSpan = maxY - minY;
            float swaySpan = Mathf.DeltaAngle(minYaw, maxYaw);
            if (swaySpan < 0f) swaySpan = -swaySpan;

            bool cueShouldMove = find.CueMoves;
            bool cueGateOk;
            if (cueShouldMove)
            {
                // Thresholds are a fraction of the EFFECTIVE amplitude, so a channel that is merely SMALL still
                // passes while a channel that is DEAD (exactly 0, or clamped off) fails.
                bool ch1Live = bobSpan > Mathf.Abs(find.EffectiveBobAmplitude) * 0.5f && bobSpan > 0.001f;
                bool ch2Live = swaySpan > Mathf.Abs(find.EffectiveSwayDegrees) * 0.5f && swaySpan > 0.1f;
                cueGateOk = ch1Live && ch2Live;
                Debug.Log("[WeaponFindVerifyCapture] CUE(placement=" + find.placement + " -> may move): CH1 bob span=" +
                          bobSpan.ToString("F4") + "u live=" + ch1Live + " | CH2 sway span=" +
                          swaySpan.ToString("F2") + "deg live=" + ch2Live + " => twoChannelCue=" + cueGateOk);
            }
            else
            {
                // STILL means STILL. The tolerances are float-noise-tight rather than "small": the Update path
                // writes the base pose exactly once and never touches the transform again, so the sampled span
                // must be ZERO, not merely subtle. A 0.05u bob is 9% of this sword's visible length and reads as
                // hovering — anything a loose tolerance would wave through is the defect itself.
                bool ch1Still = bobSpan <= 1e-4f;
                bool ch2Still = swaySpan <= 1e-2f;
                cueGateOk = ch1Still && ch2Still;
                Debug.Log("[WeaponFindVerifyCapture] CUE(placement=" + find.placement + " -> MUST BE STILL): CH1 bob span=" +
                          bobSpan.ToString("F5") + "u still=" + ch1Still + " | CH2 sway span=" +
                          swaySpan.ToString("F4") + "deg still=" + ch2Still + " => stillInItsHost=" + cueGateOk +
                          " (authored-but-inert amp=" + find.bobAmplitude.ToString("F3") + "u / " +
                          find.swayDegrees.ToString("F2") + "deg; effective=" +
                          find.EffectiveBobAmplitude.ToString("F3") + "u / " +
                          find.EffectiveSwayDegrees.ToString("F2") + "deg)");
            }

            // Frame + shoot the RESTING weapon at gameplay framing (AC6 capture a).
            Vector3 findPos = findPick.LootPosition;
            FrameOverShoulder(camGo, findPos, viewYaw, viewPitch, viewDistance);
            for (int i = 0; i < settleFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();
            ShotTo(Path.Combine(dir, "weaponfind_rest.png"));
            yield return new WaitForEndOfFrame();

            // SIDE-PROFILE frame (the silhouette gate) — same subject, level eye at the stump's top face, so
            // blade-DOWN/grip-UP is unmistakable. The author eyeballs THIS one before review; the two-part
            // geometric assert above is its pair.
            FrameAt(camGo, findPos + Vector3.up * sideLookHeight, viewYaw - 90f, sidePitch, sideDistance);
            for (int i = 0; i < 8; i++) yield return null;
            yield return new WaitForEndOfFrame();
            ShotTo(Path.Combine(dir, "weaponfind_side.png"));
            yield return new WaitForEndOfFrame();

            // === Teleport the player INTO the find's loot range (agent.Warp — NOT a raw set / NOT MoveTo) ===
            float range = findPick.LootRange;
            Quaternion yawRot = Quaternion.Euler(0f, viewYaw, 0f);
            Vector3 standPos = findPos + (yawRot * Vector3.back) * (range * standFraction);
            standPos.y = findPos.y;
            TeleportPlayer(player, standPos);
            Vector3 actualPos = player.position;
            Vector3 toFind = findPos - actualPos; toFind.y = 0f;
            if (toFind.sqrMagnitude > 1e-4f) player.rotation = Quaternion.LookRotation(toFind);

            FrameOverShoulder(camGo, player.position, viewYaw, viewPitch, viewDistance + 1f);
            for (int i = 0; i < settleFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();

            // === SELF-ASSERT 2 — the shared LootPrompt resolves the FIND and names it ===
            IPickable target = looter.NearestInRange();
            bool resolvedFind = ReferenceEquals(target, findPick);
            string label = LootPrompt.BuildLabel(target, looter.lootKey);
            string wantLabel = "Press " + looter.lootKey + " to pick up " +
                               FarHorizon.Combat.WorldWeaponFind.DefaultDisplayName;
            bool labelOk = label == wantLabel;
            float planarDist = Vector2.Distance(new Vector2(actualPos.x, actualPos.z), new Vector2(findPos.x, findPos.z));
            Debug.Log("[WeaponFindVerifyCapture] PROMPT: stood at " + actualPos.ToString("F2") + " planarDist=" +
                      planarDist.ToString("F2") + " (range=" + range.ToString("F2") + "); resolvedFind=" +
                      resolvedFind + " label=\"" + label + "\" wanted=\"" + wantLabel + "\" ok=" + labelOk);

            // === SELF-ASSERT 3 — the E-LOOT, and that a SECOND press does nothing ===
            int before = inventory.Model.CountItem(ItemCatalog.SwordIronId);
            looter.RequestLoot();
            yield return null; // the looter consumes the latch on Update
            yield return null;
            int afterFirst = inventory.Model.CountItem(ItemCatalog.SwordIronId);
            looter.RequestLoot();
            yield return null;
            yield return null;
            int afterSecond = inventory.Model.CountItem(ItemCatalog.SwordIronId);
            bool gotOne = afterFirst == before + 1;
            bool secondIsNoOp = afterSecond == afterFirst;
            Debug.Log("[WeaponFindVerifyCapture] LOOT: sword_iron " + before + " -> " + afterFirst + " -> " +
                      afterSecond + " (gotOne=" + gotOne + " secondPressNoOp=" + secondIsNoOp +
                      " findStillLootable=" + findPick.CanLoot + ")");

            // Let the eased pickup arc finish so the in-hand frame is not caught mid-flight.
            float arcGuard = 0f;
            while (find.IsArcing && arcGuard < 3f) { arcGuard += Time.deltaTime; yield return null; }

            // === SELF-ASSERT 4 — IN-HAND (the class this gate exists for) ===
            bool selected = SelectSwordInBelt(inventory);
            yield return null; // Inventory.Changed → HeldWeaponCycleDebug.SyncHeldVisualToSelection + gate re-apply
            yield return null;

            bool seatEnabled = false, meshIsSwordIron = false, boundsReal = false, onScreen = false;
            var holder = cycle.MeshHolder;
            if (holder != null)
            {
                var rend = holder.GetComponent<Renderer>();
                seatEnabled = rend != null && rend.enabled && rend.gameObject.activeInHierarchy;
                meshIsSwordIron = cycle.CurrentIndex == HeldWeaponCycleDebug.SwordIronFamilyIndex;
                if (rend != null)
                {
                    var b = rend.bounds;
                    boundsReal = b.size.sqrMagnitude > 1e-5f;
                    Vector3 vp = cam.WorldToViewportPoint(b.center);
                    onScreen = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
                }
            }
            Debug.Log("[WeaponFindVerifyCapture] IN-HAND: selectedInBelt=" + selected +
                      " isSwordIronSelected=" + inventory.IsSwordIronSelectedInBelt +
                      " seatRendererEnabled=" + seatEnabled + " heldFamilyIndex=" + cycle.CurrentIndex +
                      " (want " + HeldWeaponCycleDebug.SwordIronFamilyIndex + ") mesh=" + meshIsSwordIron +
                      " boundsReal=" + boundsReal + " onScreen=" + onScreen);

            // Frame the CASTAWAY (not the stump) for the in-hand shot — AC6 capture (b).
            FrameOverShoulder(camGo, player.position, viewYaw + 150f, viewPitch, 3.4f);
            for (int i = 0; i < 10; i++) yield return null;
            yield return new WaitForEndOfFrame();
            ShotTo(Path.Combine(dir, "weaponfind_inhand.png"));
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.5f);

            bool pass = restingCanLoot && embedded && cueGateOk && resolvedFind && labelOk && gotOne
                        && secondIsNoOp && selected && seatEnabled && meshIsSwordIron && boundsReal && onScreen;
            Debug.Log("[WeaponFindVerifyCapture] verification complete (canLoot=" + restingCanLoot +
                      " embedded=" + embedded + " placement=" + find.placement +
                      " cueGate(" + (cueShouldMove ? "mustMove" : "mustBeStill") + ")=" + cueGateOk +
                      " resolvedFind=" + resolvedFind + " label=" + labelOk +
                      " gotOne=" + gotOne + " secondNoOp=" + secondIsNoOp + " selected=" + selected +
                      " seatEnabled=" + seatEnabled + " swordIronMesh=" + meshIsSwordIron +
                      " boundsReal=" + boundsReal + " onScreen=" + onScreen + ") => PASS=" + pass + " -> " + dir);
            Application.Quit(pass ? 0 : 1);
        }

        // The ACTIVE find (the pool disables the sites beyond ActiveFindCount, so an inactive site's component
        // must not be picked as the subject). FindObjectsOfType(false) already skips inactive GameObjects.
        private static FarHorizon.Combat.WorldWeaponFind ResolveActiveFind()
        {
            var all = Object.FindObjectsByType<FarHorizon.Combat.WorldWeaponFind>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].IsAvailable) return all[i];
            return all.Length > 0 ? all[0] : null;
        }

        /// <summary>
        /// The REAL-WORLD ANCHOR check — and it takes BOTH halves of the anchor sentence, because the first
        /// draft only took one and shipped nonsense.
        ///
        /// "An iron sword driven POINT-DOWN INTO a weathered stump — blade buried, GRIP UP where a hand would
        /// close on it." That is THREE claims, and this check needed all three before it stopped lying:
        ///   (a) the blade tip is BELOW the stump's top face — it is IN the wood;
        ///   (b) the grip stands ABOVE that face — you can SEE it and reach for it;
        ///   (c) the blade is OVER the stump at all — "into the stump" is a claim about WHERE, not only height.
        ///
        /// Draft 1 asserted only (a). A sword swallowed WHOLE by the stump satisfies (a) perfectly, and that is
        /// what shipped: a frame of a bare stump with no weapon in it, gate green (weaponLowY=-0.746 vs
        /// stumpTopY=0.475). Draft 2 added (b) — and shipped green AGAIN on a frame showing the sword standing
        /// point-down IN THE BARE GRASS about a metre from an empty stump, because (a) and (b) are BOTH Y-only
        /// and a sword at the right HEIGHT but displaced sideways passes both. Twice the numbers were green and
        /// the picture was nonsense; both times only an eyeballed frame caught it (lowpoly-quality.md §0, the
        /// PR #130 pond→mound lesson). All three are now required, and (a)/(b) are evaluated at the WORST point
        /// of the attract bob (peak up for buried, peak down for visible) so the anchor holds through the whole
        /// cue rather than only at rest.
        /// </summary>
        private static bool CheckBladeEmbedded(FarHorizon.Combat.WorldWeaponFind find,
                                               out float weaponLowY, out float weaponHighY,
                                               out float stumpTopY, out float peakBob,
                                               out float planarDist, out float stumpReach)
        {
            weaponLowY = float.MaxValue; weaponHighY = float.MinValue; stumpTopY = float.MinValue;
            // The EFFECTIVE amplitude, not the authored one: on an embedded find the placement gate makes the
            // real peak bob 0, and measuring against a motion that cannot happen would hold the seat to a
            // margin it does not need — and, worse, would let a reader think the shipped sword still bobs.
            peakBob = Mathf.Abs(find.EffectiveBobAmplitude);
            planarDist = float.MaxValue; stumpReach = 0f;

            var weaponT = find.visual != null ? find.visual : find.transform;
            bool haveWeapon = false;
            Bounds wb = new Bounds();
            foreach (var r in weaponT.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!haveWeapon) { wb = r.bounds; haveWeapon = true; } else wb.Encapsulate(r.bounds);
                weaponLowY = Mathf.Min(weaponLowY, r.bounds.min.y);
                weaponHighY = Mathf.Max(weaponHighY, r.bounds.max.y);
            }

            bool haveStump = false;
            Bounds sb = new Bounds();
            var stumpT = find.transform.Find("FindStump");
            if (stumpT != null)
                foreach (var r in stumpT.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    if (!haveStump) { sb = r.bounds; haveStump = true; } else sb.Encapsulate(r.bounds);
                    stumpTopY = Mathf.Max(stumpTopY, r.bounds.max.y);
                }

            if (!haveWeapon || !haveStump || stumpTopY == float.MinValue) return false;

            bool bladeInTheWood = (weaponLowY + peakBob) < stumpTopY;          // (a) tip stays buried at peak UP
            bool gripStandsProud = (weaponHighY - peakBob) > stumpTopY + 0.1f; // (b) grip visible at peak DOWN

            // (c) THE PLANAR HALF — added after this gate shipped GREEN on a frame showing the sword standing
            // point-down IN THE BARE GRASS about a metre from an EMPTY stump. Checks (a) and (b) were both
            // Y-only, and a sword at exactly the right HEIGHT but displaced sideways satisfies both perfectly:
            // the metric was green on nonsense and only the eyeballed side-profile caught it
            // (lowpoly-quality.md §0, the PR #130 pond→mound lesson). "Driven INTO the stump" is a claim about
            // WHERE, not just how high — so require the weapon's planar centre to sit inside the stump's own
            // planar footprint.
            planarDist = Vector2.Distance(new Vector2(wb.center.x, wb.center.z),
                                          new Vector2(sb.center.x, sb.center.z));
            stumpReach = Mathf.Max(sb.extents.x, sb.extents.z);
            bool bladeOverTheStump = planarDist <= stumpReach;

            return bladeInTheWood && gripStandsProud && bladeOverTheStump;
        }

        // Move the looted sword onto the belt if it is not already there, then SELECT it — the same TryMove +
        // SelectBelt seam a player drives. Inventory.PickUpWeapon lands a tool on the BELT first (AddToolToBelt),
        // so the normal path is "already on the belt, just select it"; the pack branch is the belt-full fallback.
        private static bool SelectSwordInBelt(Inventory inventory)
        {
            var model = inventory.Model;
            var belt = model.BeltSlots;
            for (int i = 0; i < belt.Count; i++)
            {
                if (!belt[i].IsEmpty && belt[i].Def.Id == ItemCatalog.SwordIronId)
                {
                    model.SelectBelt(i);
                    return model.IsSelectedBeltItem(ItemCatalog.SwordIronId);
                }
            }

            int packIndex = -1;
            var pack = model.InventorySlots;
            for (int i = 0; i < pack.Count; i++)
                if (!pack[i].IsEmpty && pack[i].Def.Id == ItemCatalog.SwordIronId) { packIndex = i; break; }
            if (packIndex < 0) return false; // no sword anywhere — the loot step must have failed

            int beltIndex = -1;
            for (int i = 0; i < belt.Count; i++)
                if (belt[i].IsEmpty) { beltIndex = i; break; }
            if (beltIndex < 0) return false;

            if (!model.TryMove(SlotRef.Inventory(packIndex), SlotRef.Belt(beltIndex))) return false;
            model.SelectBelt(beltIndex);
            return model.IsSelectedBeltItem(ItemCatalog.SwordIronId);
        }

        // NavMesh-safe teleport (the #162 fix): the player carries a NavMeshAgent which OWNS the transform — a
        // raw `player.position = standPos` is re-projected onto valid navmesh on the agent's next update and
        // drags the player AWAY before NearestInRange() reads. Warp the AGENT instead. NEVER ClickToMove.MoveTo
        // (DEAD under WASD locomotion). Raw set only when there is no agent (a degenerate rig).
        private static void TeleportPlayer(Transform player, Vector3 standPos)
        {
            var agent = player.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                Vector3 warpTo = standPos;
                if (NavMesh.SamplePosition(standPos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    warpTo = hit.position;
                if (agent.Warp(warpTo)) return;
            }
            player.position = standPos;
        }

        // Park the camera over-shoulder on a GROUND target at a given yaw/pitch/distance (adds the standard
        // 0.7u eye-height lift the other capture gates use).
        private void FrameOverShoulder(GameObject camGo, Vector3 target, float yaw, float pitch, float distance)
            => FrameAt(camGo, target + new Vector3(0f, 0.7f, 0f), yaw, pitch, distance);

        // Park the camera looking at an EXACT world point (no implicit lift) — the side profile aims at the
        // stump's top face, where the anchor's blade-below / grip-above boundary is.
        private void FrameAt(GameObject camGo, Vector3 look, float yaw, float pitch, float distance)
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            camGo.transform.position = look + rot * new Vector3(0f, 0f, -distance);
            camGo.transform.LookAt(look);
        }

        private void ShotTo(string file)
        {
            ScreenCapture.CaptureScreenshot(file, 1);
            Debug.Log("[WeaponFindVerifyCapture] wrote " + file);
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
