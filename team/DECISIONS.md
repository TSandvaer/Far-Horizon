# Decision Log — Far Horizon

> **Append protocol (carried from RandomGame, adopted there 2026-05-15):** this file is centralized. Agents NEVER edit it directly — record `Decision draft:` lines in final reports; Priya batches them into a single PR. The orchestrator logs Sponsor-made and cross-role decisions directly.

Append-only. Format:

```
## YYYY-MM-DD — <short title>
- Decided by: <Sponsor | Priya | orchestrator>
- Decision: <one sentence>
- Why: <the load-bearing reason>
- Reversibility: <reversible | one-way>
- Affects: <roles or systems>
```

Godot-era decisions (2026-05-02 → 2026-06-12) live in the archived RandomGame repo: `c:/Trunk/PRIVATE/RandomGame/team/DECISIONS.md`.

---

## 2026-07-22 — Weapon held-seat: ONE dial per weapon class across all tiers (stone-axe zero-lock RETIRED)

- Decided by: Sponsor (verbatim "use the same dial for rock and metal", soak-swings-6 final F9 dial; baked by Drew in PR #327 round 7)
- Decision: Each weapon CLASS (axe / pickaxe / spear / sword / dagger) uses ONE dialed held-seat (offset/euler/scale) applied identically to its wood/stone/iron tiers — per-tier seat divergence is retired. The original stone axe's zero-locked index-0 seat (ApplyCurrent restored the captured baseline byte-unchanged) is RETIRED: `HeldWeaponCycleDebug.ApplyCurrent` now composes index-0's array seat like every other weapon (backward-compatible with the old zero array), and `Awake` seats the equipped chop axe at spawn. The axe SCALE stays 1.0 (the held-scale dial still refuses the axe); `HeldAxeV3/V4` rig constants are UNTOUCHED (the class euler composes on the approved rig baseline).
- Why: the Sponsor wanted all material tiers of a weapon to read IDENTICALLY in-hand; one per-class seat is simpler + drift-guarded, and the equipped stone chop-axe now matches the approved wood-axe look.
- Reversibility: reversible (seat constants + the ApplyCurrent composition are code; drift-guarded by `HeroAxeSceneTests._SameDialAcrossTiers_86caffwv5` + per-tier equality pins + the axe scale-1.0 guard)
- Affects: `HeldWeaponCycleDebug` / `HeroAxeSceneTests`, held-weapon visual seating; Drew + Devon + Tess. Source: PR #327 (86caffwv5) r7, comment 5034253841.

## 2026-07-22 — Boar soak PASS → PR #332 merged; 2nd-enemy mesh route ratified (snake-style C#-baked)

- Decided by: Sponsor (soak popup, 2026-07-22 afternoon)
- Decision: Boar soak on `soak-boar-1` (stamp `9f76ec7`) PASSED in full — charge feel, emergent spear-beats-boar matchup legibility, AND the look; #332 merged as `0dc4844`, ticket 86cah7ydt complete. The AC5 route deviation is RATIFIED: 2nd-enemy meshes follow the snake's C#-editor-baked + procedurally-posed route (no rig — sidesteps the FBX-helicopter class); Blender-authored silhouettes remain optional swap-tickets (the swap-hatch is Devon-verified drop-in).
- Why: the systemic matchup proof (spear 18.0/hit vs axe 10.5/hit purely from reach + pierce-tag composition, zero table — guard test deletes the tag and the bonus vanishes) is the locked-decision-5 payoff; the route deviation is precedent-consistent (snake) and avoids the just-proven Blender round-trip trap. Sponsor's eye confirmed what the metrics could not (matchup LEGIBILITY).
- Reversibility: reversible (mesh swap-hatch; dials all per-tier tunable)
- Affects: combat cluster (3rd-enemy tickets inherit the route), asset-routing doc (creature-route footnote → Priya batch), quality-bars.md (matchup-legibility bar → Priya appends)

## 2026-07-22 — v4 right-hand fix: defer again + Option-C feasibility spike; never Blender-re-export the rigged character

- Decided by: Sponsor (popup, 2026-07-22 midday)
- Decision: (1) PR #330 merges as a safety PR (byte-revert + FBX v7700 canary + raw-parse instrument) on Devon's byte-identity verification — no re-soak needed at net-zero visual diff. (2) The right-hand defect stays DEFERRED (second deferral); a research-lane spike proves/kills Option C (raw-FBX binary weight edit, no hierarchy re-export) before any fix route is chosen; Option B (Mixamo re-rig) explicitly not chosen now. (3) Standing engineering rule from the incident: NEVER re-export an already-rigged Mixamo character through Blender's FBX exporter — it rebakes rest orientations on most bones (33/42 measured) and helicopters all zero-rest Generic clips; clip-layer bpy edits remain OK; enforcement = the v7700 canary test.
- Why: Option A proved structurally impossible (PR #330 comment 5044931437); Option B discards the accepted left-hand dial and re-rolls the defect-producing auto-rig; the defect is cosmetic and already Sponsor-accepted once ("ill fix the hands later", 2026-07-20). Evidence-first spike beats gambling the build lane.
- Reversibility: reversible (spike is throwaway; routes stay open)
- Affects: Drew/Devon (character pipeline), 86cau4za2 (back to `to do` post-merge), boar sequencing (unblocked — build lane frees at #330 merge)

## 2026-07-22 — Swings cluster ships: mini-soak-8 PASS → PR #327 merged (re-appended after the 08:09 revert)

- Decided by: Sponsor
- Decision: Mini-soak of `Build/soak-swings-8` (stamp `58ae23d`) PASSED — walk-into-boulder blocks at touching distance and click-mine fires from the blocked spot; #327 merged as `250e4e6`; tickets 86caffwv5 + 86caffwuz complete.
- Why: The r8 carve-tighten resolved the only soak-7 reject (blocked a body-length out); all machine gates were already green (CI green after capture-job rerun — the 2026-07-21 failure was a runner shutdown signal mid-job, not code; Devon APPROVE_WITH_NITS r7; Tess QA PASS + SERVE GO incl. the r8 played-check, comment 5042990009).
- Reversibility: reversible (revert PR #327)
- Affects: Drew/Devon/Tess (swings surface); board (cluster #2 boar unblocked; round-9 right-hand investigation done — fix pending Sponsor sequencing)

## 2026-07-21 — Erik Rigify verdict accepted: STAY Mixamo (reconstructed 2026-07-22)

- Decided by: Sponsor (implicit accept — no pushback on the served verdict)
- Decision: stay on the Mixamo pipeline; bad clips get clip-layer bpy repair (à la SneakGaitCurveFix); Rigify re-enters only if a human animator ever joins (open question to Sponsor, unanswered).
- Why: Rigify is a control rig with zero clips; game-export friction + re-rig blast radius (Generic bindings, Animator, all 15 seats) = Very High for zero gain. Research note: team/erik-consult/rigify-vs-mixamo-research.md (merged #328).
- Reversibility: reversible (future re-evaluation)
- Affects: animation asset routing
- Provenance: re-appended 2026-07-22 after the 08:09:34Z working-tree revert wiped the original uncommitted entry; sources = 2026-07-21 session save + PR #327 trail.

## 2026-07-21 — Loot prompt anchor: above the character's head (reconstructed 2026-07-22)

- Decided by: Sponsor (confirmed the orchestrator's delegated recommendation)
- Decision: interaction/loot prompts render above the castaway's head (tier-aware ore text; the belt-overlapping prompt relocated).
- Why: readability at gameplay framing; the old anchor collided with the belt UI.
- Reversibility: reversible
- Affects: LootPrompt / HUD
- Provenance: re-appended 2026-07-22 after the 08:09:34Z working-tree revert; sources = 2026-07-21 session save + PR #327 trail.

## 2026-07-21 — Ore spec KEPT: wood pickaxe mines boulders only (reconstructed 2026-07-22)

- Decided by: Sponsor ("Keep spec" popup, 2026-07-21)
- Decision: wood pickaxe mines BOULDERS only; iron ore requires a stone/iron pickaxe; the tier-aware tooltip "Needs stone pickaxe" is the UX cue.
- Why: progression legibility — the tooltip carries the teaching; the shipped spec matched his intent.
- Reversibility: reversible (spec/dial change)
- Affects: mining progression, LootPrompt copy
- Provenance: re-appended 2026-07-22 after the 08:09:34Z working-tree revert wiped the original uncommitted entry; sources = 2026-07-21 session save + PR #327 trail.

## 2026-07-19 — Combat cluster: SPEC PREP starts now; implementation gated on #317 (v4 activation) merge

- Decided by: Sponsor (orchestrator popup, 2026-07-19)
- Decision: Combat-cluster **spec prep begins immediately** (AC-flesh, design brief, sequencing) while `#317` (castaway v4 activation) finishes; **implementation of every combat ticket still waits for `#317`'s merge**. The six cluster tickets — swings `86caffwv5`, boar `86cah7ydt`, find-in-world weapons `86cah7y5b`, weapon-roster expansion `86cah7ym9`, additional status effects `86cah7yuh`, HP-HUD polish + heal sources `86cah7z2q` — stay `to do` (prep ≠ implementation) and each carries a `#317-merge` implementation gate.
- Why: v4 is the live hero the combat verbs animate on (swings play on the castaway animator); prepping specs in parallel keeps the non-build lane full without dispatching impl against a hero that is mid-activation. Splitting prep from impl lets the design settle before code starts.
- Reversibility: reversible (prep is docs/ACs only; no code committed until #317 merges)
- Affects: combat cluster (6 tickets), Devon + Drew + Uma + Tess; sequencing per the sibling decision below

## 2026-07-19 — Combat cluster order: SWINGS first, boar second

- Decided by: Sponsor (orchestrator popup, 2026-07-19)
- Decision: The combat cluster is sequenced **swings first** (`86caffwv5` — attack animation per weapon: a Mixamo clip per weapon class, one-click-one-strike active input) **then the wild boar** (`86cah7ydt` — 2nd enemy + weapon-vs-mob matchup proof). Recorded on both tickets.
- Why: a weapon that reads as a real attack is the foundation the enemy matchup builds on — the boar's "spear beats boar via reach + weak-to-pierce" proof only lands once the swings feel like real attacks. Sponsor-picked order.
- Reversibility: reversible (sequencing only)
- Affects: combat cluster dispatch order, Drew + Devon

## 2026-07-08 — Crafting system redesigned: placed recipe-menu table + 3 tiers + unified place-to-build

- Decided by: Sponsor + orchestrator (grill-resolved, ticket `86camz6n0`, 2026-07-08; grounded on the forge-soak feedback in `86camyvzw`/`86camyvwn`)
- Decision: The crafting surface is redesigned. (1) **Unified place-to-build**: the crafting table (wood+stone), forge (much more stone), and campfire are all placed by the player and are **INVISIBLE until placed** (retires the pre-visible fixed spots). (2) **Crafting table = a recipe MENU** — recipes grouped by tier, greyed until the tier is unlocked AND affordable, click-to-craft; RETIRES the `CraftSpot` auto-craft stump + the free-mint `CraftAxe` path. (3) **Three tiers WOOD→STONE→IRON**, each with axe/pickaxe/spear/dagger/sword (~15 recipes) crafted via a **material-cost seam** (`InventoryModel.RemoveItem` all-or-nothing → `AddToolToBelt`). (4) **Tier-gated loop**: hand-gather sticks+pebbles → table → wood tools → wood-pick mines STONE from boulders (NEW) → stone tools → stone-pick mines IRON-ORE (shipped #287) → forge smelts → BARS (shipped #292) → iron tools. Re-scoped into 4 build-lane tickets (① table foundation + wood tier, ② boulder-mining + stone tier, ③ forge place-to-build rework + iron tier, ④ full-chain soak). Absorbs I-4 `86cakkmy2` + forge-vis `86camyvzw` + NIT `86camw8rm` (→③) and I-5 `86cakkn15` (→④); icons `86camyvwn` stays a separate fable session. Full spec: `team/priya-pl/crafting-system-spec.md`.
- Why: The Sponsor's forge-soak (2026-07-08, build `4cb464b`) rejected pre-visible/auto-built structures ("must NOT be visible before it is built — the player builds it by gathering the ingredients and PLACING it"); the grill generalised that to every structure + turned the thin one-recipe stump into a real tiered recipe menu, giving the survival arc its full gather→craft→upgrade spine. Model-A's shipped mine/smelt mechanics are reused; its "extend the thin CraftSpot bench" assumption is superseded.
- Reversibility: reversible per ticket (each of ①–④ is a discrete PR); the design direction is Sponsor-locked
- Affects: crafting/inventory/structures/UI, Devon + Drew + Tess + Uma; supersedes `iron-model-a-spec.md` on the crafting-table question

---

## 2026-06-12 — Project founded: Far Horizon (Sponsor-directed)

- Decided by: Sponsor (sequence of popup decisions, recorded verbatim on RandomGame ClickUp ticket 86ca85ttd)
- Decision: The Unity production project is **Far Horizon** — a FRESH Unity 6/URP project (the eval spike stays a read-only reference, not graduated), new private GitHub repo `TSandvaer/Far-Horizon`, new ClickUp list `901523878268`; milestones split M-U1 (bootstrap + deliberate ports) / M-U2 (thin survival loop: ONE need → craft axe → chop → campfire); PixelLab subscription kept idle; the Godot repo/list archive read-only.
- Why: Engine decision 2026-06-12 (migrate to Unity — RandomGame DECISIONS.md; evidence: spike verdict YES on all capabilities + all style gates passed: character "appealing", "i love zone D + quality", "zone c approved"). Fresh-over-graduate and the name were the Sponsor's explicit picks.
- Reversibility: one-way in practice
- Affects: everything — repo, tracker, roadmap, all roles

## 2026-06-12 — Bootstrap exception: U1/U2 land direct on main

- Decided by: orchestrator
- Decision: The U1 Unity skeleton (root commit 3a6ef5c) and the U2 orchestration scaffold commit straight to `main`; PR-flow + protected-main discipline is binding from U3 onward.
- Why: An empty repo has no main to branch from and no reviewers' worktrees yet; both commits are recorded on their tickets (86ca86fb7 / 86ca86fgy) with full evidence.
- Reversibility: reversible (convention forward-looking)
- Affects: git protocol, all roles

## 2026-06-12 — WARMTH is the single M-U2 survival need

- Decided by: Sponsor (orchestrator popup, recorded on ticket 86ca8bd9m / U2-1)
- Decision: The one decaying need that drives the thin M-U2 loop is **WARMTH** — cold creeps in; the campfire (U2-4) is what answers it. No second need, no hunger/energy, no shelter (those are M-U3+ proposals).
- Why: Fits the castaway-washed-ashore fiction (wet, cold, one pressing need) and keeps M-U2 thin per the Sponsor's locked one-need-→-craft-axe-→-chop-→-campfire loop. Shipped: WarmthNeed model (PR #11), campfire satisfaction (PR #15), full-cycle PlayMode coverage (PR #16).
- Reversibility: reversible (the single-need model generalizes to two needs in M-U3)
- Affects: survival loop, HUD, all M-U2 content tickets

## 2026-06-12 — Art-direction board rebased to chunky cartoon low-poly (whole game)

- Decided by: Sponsor (in-chat, evening — "throwing a lot of stuff in the inspiration folder, deleted the old genre"; captured in art-direction.md board v2 + Uma's style-guide-v2 PR #17)
- Decision: The entire art-direction board is REBASED to **chunky stylized cartoon low-poly** across all three surfaces — character, tools/props, world/nature. The 2026-06-08 lush-garden/courtyard references are deleted. On the castaway specifically the change is **STYLE ONLY**: the chunky/cartoon stylization transfers to the LOCKED young/hopeful identity (the reference's bearded rugged adult is NOT adopted; `_castaway_judge/` sheets remain the identity ground truth).
- Why: Sponsor replaced the whole inspiration board with a coherent toy-like, saturated, faceted-flat-shaded direction; the warm/lush FEELING and small-player/big-alive-world north-star carry, only the rendering style shifts. Drove the style wave (axe PR #21, blob trees PR #22) and the castaway stylization ticket 86ca8ca1m.
- Reversibility: one-way in practice (board content replaced; downstream assets re-skinned)
- Affects: all visual/level/prop/palette work, Uma + Drew + Devon

## 2026-06-12 — Vertex-color inline-materials pattern for multi-colored low-poly props

- Decided by: Drew (blob-canopy tree implementation, ticket 86ca8ce7j / PR #22)
- Decision: Multi-colored low-poly props bake their per-region colors (e.g. the blob canopy's CanopyShadow/Body/Top) into **vertex color** and render through ONE shared custom `FarHorizon/LowPolyVertexColor` material rather than multiple per-color `.mat` assets; materials are assigned to `sharedMaterial` and serialized into the scene, NOT persisted as standalone asset files.
- Why: URP/Lit ignores vertex color, and per-instance color-jittered `.mat` assets cause asset churn (unity-conventions.md low-poly section). One vertex-color material keeps the faceted multi-value look while avoiding the churn; falls back to flat green if the shader is unresolved. Proven in LowPolyZoneGen.cs (canopy) and the terrain beach→field ramp.
- Reversibility: reversible (rendering convention; swappable per-prop)
- Affects: world/prop content systems, Drew

## 2026-06-13 — Castaway base SWAPPED to a sourced chunky-cartoon rig (Mini Chibi Kid)

- Decided by: Sponsor (2026-06-13, after the cartoon-ify attempts failed; recorded on ticket 86ca8ca1m + STATE.md + decisions-while-away.md)
- Decision: Stop editing the realistic Quaternius mesh and **SOURCE a pre-rigged chunky-cartoon base**. Chosen: Sketchfab **"Mini Chibi Kid" by joaobaltieri** (UID `6feb5bd7ade54b5fac25a0e1e5fbe729`, **CC-BY**), integrated as PR #26 (branch `devon/chibi-castaway-integration`). It ships its own Idle/Walk/Run animations on a Mixamo-style humanoid rig, the cartoon face the Sponsor asked for (white-sclera/black-pupil eyes + bushy brows), young/hopeful, ~1442 faces. PR #25 (Quaternius bone-scale path) is kept open as fallback until the chibi is proven.
- Why: A whole evening of cartoon-ifying the realistic head failed — vertex sculpt mangled arms/hands; bone-scale gave a clean chunky body but the cartoon face could not be sculpted onto a socket-set realistic skull. Lesson (now in unity-conventions.md): **when a base mesh fights the target style after 2+ edit attempts, stop editing and source a purpose-built base** — vet for license (prefer CC-BY, avoid CC-NonCommercial for a potentially-commercial game), low face count, and critically whether it ships its own animation set.
- Reversibility: reversible (revert the squash merge / iterate via recolor or base-swap; PR #25 fallback retained)
- Affects: player character, Devon + Uma, animation/rig pipeline

## 2026-06-13 — PR-merge to protected `main` is NOT orchestrator-auto-decidable (always Sponsor-gated)

- Decided by: auto-mode classifier boundary (recorded in decisions-while-away.md, 2026-06-13 0620 UTC)
- Decision: Merging a PR to the protected `main` branch is **always Sponsor-gated** on this project, regardless of auto-mode / orchestrator-autonomy state. The promoted "routine-PR-merge when CI green + peer reviewer attached" auto-decide class does NOT apply here — the classifier denies it as an externally-visible action the Sponsor never explicitly approved.
- Why: The orchestrator tried to auto-merge PR #26 (CI green, Tess APPROVE with independent shipped-exe reproduction) under the routine-merge class; the auto-mode classifier blocked it. Correct boundary — not retried. The look-verdict (post-merge soak) is the Sponsor's gate, so the merge itself must be Sponsor-approved.
- Reversibility: reversible (governance convention; reaffirmable per the never-auto-decide externally-visible-action rule)
- Affects: orchestrator, git protocol, all merge flows

## 2026-06-13 — Axe sourced as a CC-BY hatchet (procedural axe didn't read as an axe)

- Decided by: Sponsor (2026-06-13 — "the axe does not look like an axe"; recorded in STATE.md)
- Decision: The procedural hero axe (PR #21) didn't read as an axe, so the Sponsor chose to **source one**. Sourced + committed: Sketchfab **"One-handed stylized axe" by Viktor.G** (UID `d2e3f8682d71425ba2bf72f3e3d78f7c`, **CC-BY**) — a rustic leather-wrapped hatchet that reads unmistakably as an axe (branch `orch/castaway-axe-asset` @ `79b903b`, `Assets/Art/Props/CastawayAxe/`). Integration (replace the procedural axe + attach to the chibi's hand bone) is a separate PR **sequenced AFTER the chibi (PR #26) lands**.
- Why: The procedural prop read ambiguously; a sourced chunky-cartoon hatchet matches the style-guide tool language (ref 21h08_08) and reads correctly. Sequenced after the chibi because the axe attaches to the chibi's hand bone.
- Reversibility: reversible (asset swap; integration not yet merged)
- Affects: survival loop hero prop, Devon, asset pipeline

## 2026-06-13 — Castaway identity recolor (sandy hair / khaki) is a tunable soak follow-up

- Decided by: Sponsor (recorded on ticket 86ca8ca1m + STATE.md)
- Decision: The chibi ships with its default look now; the young/hopeful identity recolor (sandy hair, khaki) is a **deliberate tunable follow-up judged from the soak**, not a blocker on the base swap landing.
- Why: Decouples the structural base-swap decision (proportions + rig + animations) from the subjective identity-recolor tuning, which is best judged against the shipped-build soak rather than pre-specified.
- Reversibility: reversible (recolor is a tuning pass)
- Affects: player character, Uma + Devon

## 2026-06-13 — Asset-sourcing/creation route: Sketchfab + Blender-MCP; AI generators need Sponsor keys

- Decided by: Sponsor (Blender-MCP capability flagged 2026-06-12; AI-gen "Both" 2026-06-13; recorded in CLAUDE.md, unity-conventions.md, STATE.md)
- Decision: The asset-sourcing/creation route for Far Horizon is **Blender + Blender MCP** — Sketchfab search/import (sourcing existing assets) and procedural Blender modeling. The AI text/image-to-3D generators (Hyper3D Rodin, Hunyuan3D) are available and enabled in Blender but **require the Sponsor to supply API keys** (Rodin MAIN_SITE mode needs a key; Hunyuan3D needs a Tencent secret pair) — keys PENDING. Sketchfab works with just an account API key (already set).
- Why: Sketchfab + procedural Blender cover the immediate need (chibi base, hatchet, world props); the AI generators are a future lever gated on Sponsor-supplied keys. PixelLab is explicitly OFF Far Horizon's books — the Sponsor uses that subscription for other projects ("im using pixellab for other projects, dont worry about it", 2026-06-12); pixel-art-native and ruled out for this game's 3D characters/world.
- Reversibility: reversible (tooling route; generators enable once keys arrive)
- Affects: all asset creation, orchestrator R&D lane, Devon + Drew + Uma

## 2026-06-13 — Castaway BASE SWAP completed + recolored to identity (chibi shipped)

- Decided by: Sponsor (base choice) + Devon/Uma (integration + recolor execution)
- Decision: The castaway base swap is COMPLETE. The cartoon-face-on-realistic-Quaternius-head route FAILED (a whole evening of head-sculpt/bone-scale attempts couldn't put a cartoon face on a socket-set realistic skull). Sponsor sourced a pre-rigged chunky-cartoon base — Sketchfab **"Mini Chibi Kid"** (CC-BY) — integrated via PR #26 (squash `9dd317f`), then recolored to our young/hopeful identity (sandy hair, warm khaki) via PR #32 (squash `46f2a9d`, combined scene-integration PR). The recolor is a **luma-preserving UV-cell atlas PNG repaint** (the bound `_BaseMap` PNG bytes change; materials/import config unchanged) — explicitly NOT a material tint.
- Why: Lesson (now in unity-conventions.md): when a base mesh fights the target style after 2+ edit attempts, stop editing and source a purpose-built base — vet for license (prefer CC-BY), low face count, and critically whether it ships its own animations. Mini Chibi Kid ships its own Idle/Walk/Run on a Mixamo-style humanoid rig, which is why it won over re-sculpting. Atlas-repaint over material-tint preserves the toon's per-cell luma shading.
- Reversibility: reversible (revert the squash merges / re-repaint the atlas in ≤1 PR; PR #25 Quaternius fallback was closed superseded)
- Affects: player character, Devon + Uma, animation/rig + recolor pipeline

## 2026-06-13 — Axe re-done as a sourced rustic hatchet (procedural axe didn't read)

- Decided by: Sponsor (base choice) + Devon (integration)
- Decision: The procedural hero axe (PR #21) didn't read as an axe, so it was replaced with the sourced Sketchfab **"One-handed stylized axe" by Viktor.G** (CC-BY) — a rustic leather-wrapped hatchet — integrated and attached to the chibi's right-hand bone (`RightHand_010`) via PR #29 (squash `3f3a3b6`). The procedural `HeroAxeMesh` path was retired (deleted with its tests).
- Why: The procedural prop read ambiguously; a sourced chunky-cartoon hatchet matches the style-guide tool language and reads correctly. Sequenced after the chibi (PR #26) because the axe attaches to the chibi's hand bone. Scale-trap (a 267× lossy-scale giant-axe) was caught and fixed to ~0.43u on the ~0.95u kid before merge.
- Reversibility: reversible (asset swap in ≤1 PR)
- Affects: survival-loop hero prop, Devon, asset pipeline

## 2026-06-13 — M-U3 REDIRECTED: survival-mechanic → SCENE COMPLETION ("finish the scene, water at the beach")

- Decided by: Sponsor (verbatim 2026-06-13: "finish the scene, i want water at the beach")
- Decision: M-U3 is redirected away from the next survival mechanic (second need / food / day-night, per survival-roadmap §3) toward **SCENE COMPLETION** — finishing the shore scene so it reads as a beach, not a clearing. First beat shipped: a stylized low-poly beach ocean brought into the SHIPPED soak scene (`MovementCameraScene`/Boot.unity) — Uma's direction PR #28 (`b78da67`), implemented + integrated via PR #32 (`46f2a9d`, regenerated Boot.unity). PRs #30 (beach ocean) and #31 (castaway recolor) were CLOSED superseded — their work landed combined in #32.
- Why: The castaway washed ashore but the scene had no coast (flat warm ground, blob trees, no water) — it read as "a clearing," not "a beach." Adding water completes the washed-ashore premise and makes the small-player/big-alive-world north-star visible in one frame. The Sponsor's redirect takes priority over the roadmap's next-mechanic default.
- Reversibility: reversible (milestone scoping; the survival-mechanic roadmap items remain queued behind scene completion)
- Affects: M-U3 milestone scope, the shipped scene, Uma + Drew + Devon, the board

## 2026-06-13 — `main` merges are Sponsor-gated (governance CONFIRMED)

- Decided by: auto-mode classifier boundary, then Sponsor (explicit batch approval)
- Decision: Merging any PR to protected `main` is **always Sponsor-gated** on this project, regardless of auto-mode / orchestrator-autonomy state — the promoted "routine-PR-merge when CI green + peer reviewer attached" auto-decide class does NOT apply here. CONFIRMED in practice: the orchestrator's attempt to auto-merge PR #26 was correctly denied by the auto-mode classifier (not retried); the Sponsor then explicitly approved the castaway/scene-completion batch (#26/#28/#29/#32), which merged with `--admin --squash`.
- Why: A `main` merge is an externally-visible action the Sponsor never blanket-approved, and the look-verdict (post-merge soak) is the Sponsor's gate — so the merge itself must be Sponsor-approved. (Supersedes/reaffirms the 2026-06-13 governance note above; recorded with the batch-approval outcome.)
- Reversibility: reversible (governance convention; reaffirmable per the never-auto-decide externally-visible-action rule)
- Affects: orchestrator, git protocol, all merge flows

## 2026-06-13 — AI-gen held in reserve; Sketchfab is the default free asset route

- Decided by: Sponsor (asset-route confirmation through the castaway/axe wave)
- Decision: The default asset-sourcing route is **Sketchfab search/import** (free, account-key only) — proven through the chibi base + hatchet this wave. The AI image/text-to-3D generator **Hyper3D (Rodin)** is held IN RESERVE behind a **$96/mo Business-API gate** (MAIN_SITE mode needs a paid key the Sponsor hasn't supplied); do not assume it as a route until the Sponsor opts into that cost.
- Why: Sketchfab covers the immediate need at zero marginal cost; the paid AI generator is a future lever, not a baseline. Keeps the asset pipeline free-by-default and the cost decision explicitly the Sponsor's. (Refines the 2026-06-13 asset-route decision with the cost-gate specifics surfaced this wave.)
- Reversibility: reversible (route preference; Hyper3D enables once the Sponsor supplies the Business-API key)
- Affects: all asset creation, orchestrator R&D lane, Devon + Drew + Uma

## 2026-06-13 — M-U2 loop-feel verdict = FUN → M-U3 unblocked

- Decided by: Sponsor (loop-soak verdict)
- Decision: The M-U2 thin survival loop (one need → craft axe → chop → campfire) soaked as **FUN** per the Sponsor. That verdict was THE gate on starting M-U3; with it given, M-U3 (redirected to scene completion — see above) is unblocked.
- Why: The thin-first loop was deliberately gated on a real-feel verdict before expanding scope; "fun" confirms the foundation is worth building on and releases the next milestone.
- Reversibility: n/a (a verdict, not a reversible config)
- Affects: roadmap sequencing, all M-U3 work

## 2026-06-13 — Held props on the chibi rig are posed in WORLD space, not bone-local (267× lossy-scale trap)

- Decided by: Devon (held-axe attach, ticket 86ca8ce6y / PR #39 trace; recorded in STATE.md + unity-conventions.md §FBX)
- Decision: A prop attached to an imported-rig bone on the height-normalized chibi FBX is **parented, then posed in WORLD space** (set world position+rotation after `SetParent`, size by `worldTarget ÷ bone.lossyScale`) — NOT by nudging bone-local offsets. The attach bone is resolved from the `SkinnedMeshRenderer.bones` array BY NAME (`RightHand_010`), never a `transform.Find`/hierarchy name-scan (the rig carries trap nodes — a mesh-group `head` at the origin, a `RightHand.Dummy_011` sibling — that a scan matches first).
- Why: `RightHand_010` carries a ~267× `lossyScale` and arbitrarily-rotated local axes (local +Y maps to world ≈`(0.48,−0.84,0.23)`, mostly DOWN). A naive local scale shipped a 30–50u GIANT axe once; later a local-offset "lift" shoved the axe sideways to a 0.43u sliver at the hip — the literal "no axe" soak bug. World-space posing after parenting is deterministic on these rigs where bone-local is not.
- Reversibility: reversible (attach convention; re-pose in ≤1 PR)
- Affects: held-prop pipeline, Devon, any future bone-attached prop

## 2026-06-13 — NO-AXE root cause: invisible hip-sliver hidden by a false-green zoom-to-fit verify capture

- Decided by: Devon (root-cause trace, ticket 86ca8ce6y/86ca8ca1m / PR #39; recorded in STATE.md + unity-conventions.md)
- Decision: The recurring "I see no axe" soak complaint (flagged 3×) was traced — NOT to a broken craft/equip path — to the held axe being a **0.43u blade-down sliver at the hip (~3.7% of the real 14u/55° orbit frame = invisible)**, while `-verifyAxe`'s zoom-to-fit close-up went **FALSE-GREEN** (a subject-fit capture renders the prop at a fixed apparent size regardless of its real gameplay scale). Fix: world-space pose to ~1.0u seated at the chest (blade flat, ~8.6% frame) + a new `StumpAxe` (inverse-`HasAxe` gate) planting the hatchet upright in the chopping block VISIBLE FROM SPAWN; and the standing rule that any "is X visible to the player" gate captures from a **FIXED-ORBIT** frame matching real gameplay distance/FOV, never a zoom-to-fit close-up.
- Why: A capture that auto-zooms to its subject cannot validate gameplay-SCALE visibility — it is the third instance of the false-green-capture class (after the no-post verify cam and the stale-SMR-bounds framing). The fix had to address both the geometry (world-space pose) and the gate (fixed-orbit capture) or the bug would recur green.
- Reversibility: reversible (pose + capture-rig convention)
- Affects: held-axe + stump-axe, Devon, all visibility verify gates, Tess

## 2026-06-13 — SEA root cause: water was BACKFACE-CULLED (inverted winding), not occluded — winding flipped

- Decided by: Drew (root-cause trace, tickets 86ca8fet0 / PR #38; recorded in STATE.md + unity-conventions.md)
- Decision: The "I see no ocean / grey pond / too sky-cyan" soak complaints were traced to the water mesh rendering **ZERO pixels because it was backface-culled**, NOT occluded by foreground terrain. The sea grid lays its rows near→far in DECREASING world Z but reused the +Z terrain grid's triangle index order → faces wound the opposite way → −Y normals → default URP `Cull Back` culled them from the above-looking gameplay cam. Fix = **reverse the water triangle winding** in `LowPolyZoneGen.BuildWaterEdge`; the earlier geometry chases (slope/deepen/overlap) and the camera-pitch/occlusion hypotheses were REVERTED as wrong-cause. Proven: magenta cross-build diff `0 → 55,103 px` (5.98% frame, N=8 deterministic); a `-seaWaterOnly` probe (hide every other mesh) still showed 0 sea px BEFORE the flip, disproving occlusion. Guard = `WaterFacesUpTests` (every `Water_Play` normal·+Y > 0).
- Why: A color/material/camera tweak can never fix a not-rendering mesh; the magenta-diff proved invisibility and the isolate-probe pinned the cause to winding (the same family as the foliage opposite-winding bug). This closes weeks of "sea looks wrong" tweaks that were all chasing a symptom (fog/sky masquerading as water).
- Reversibility: reversible (winding flip; one mesh-gen method)
- Affects: ocean rendering, Drew, `LowPolyZoneGen`, all reused-grid mesh gen

## 2026-06-13 — Gray beach slab = the TestGround collision proxy; renderer disabled (kept as collider)

- Decided by: Drew (root-cause + fix, ticket 86ca8feuf-adjacent / PR #38 `f455853`; recorded in STATE.md + unity-conventions.md)
- Decision: The grey slab the Sponsor saw on the beach is the flat-Y0 `TestGround` slab (moss-grey `(0.42,0.46,0.40)`) built by `MovementCameraScene.BuildFlatGround` as the **NavMesh / click-move COLLISION PROXY** — it pokes ABOVE the Zone-D sand only on the seaward foreshore where the visual terrain DIPS below Y0 (inland the sand rises above Y0 and hides it). Fix: **disable its `MeshRenderer`** (kept-but-disabled so `.bounds` still resolves for the water-occlusion test) and KEEP the collider → NavMesh + click-move stay bit-identical, zero path regression. Guard `TestGround_IsCollisionProxyOnly_RendererDisabled_NoGreySlab`. When U5 replaces the env surface, fold the collider into the real terrain + delete the placeholder.
- Why: The slab is collision-only; deleting the GameObject would break the occlusion test that reads its bounds, and removing the collider would regress NavMesh/click-move. Disabling just the renderer removes the visual artifact with zero gameplay change.
- Reversibility: reversible (one renderer flag; folds into real terrain at U5)
- Affects: beach scene, Drew, NavMesh/click-move, U5 terrain work

## 2026-06-13 — Binary-scene integration playbook validated (regenerate-on-rebase + merge-tree pre-flight)

- Decided by: orchestrator (integration of #38 + #39 → #40; playbook at `team/orchestrator/integration-39-38-playbook.md`, validated by workflow run `wf_d63e952a-804`)
- Decision: The regenerate-on-rebase pattern for multiple scene-baking PRs (proven on #32/#36) is now a **standing playbook**: base the integration branch on the larger changeset, `git merge --no-ff` the other, take `--theirs/--ours` PROVISIONALLY on the binaries (`Boot.unity` + `BuildStamp.txt`), then **MANDATORY re-bake** via `serve_soak.sh` (`BootstrapProject.Run`) so both features land in the regenerated scene, and gate with BOTH features' scene-presence EditMode tests GREEN TOGETHER (the half-baked-scene gate). A `git merge-tree --write-tree` PRE-FLIGHT predicts the conflict surface (for #38+#39: only 3 files overlap; `MovementCameraScene.cs` auto-merges clean; the two binaries regenerate) so the integration is dispatched with a known-clean expectation.
- Why: Binary, bootstrap-generated `Boot.unity` cannot be hand-merged; the silent-drop failure (ship only one branch's bake) is real and the dual-feature test gate is what catches it. Pre-flighting via merge-tree turns a risky integration into a mechanical one with a written conflict map.
- Reversibility: reversible (process convention; playbook lives in orchestrator docs)
- Affects: orchestrator integration flow, all scene-baking PRs, Devon + Drew + Tess

## 2026-06-14 — Sponsor soak decisions: axe-tweak = in-game NUDGE TOOL; sea-color + axe-head ACCEPTED; auto-status OFF

- Decided by: Sponsor (2026-06-14 soak of PR #40 / `31ce95c` + /sponsor-questions-walkthrough; recorded verbatim in STATE.md resume header)
- Decision: Four Sponsor calls on his 2026-06-14 return. (1) **Axe-tweak mechanism = an in-game, build-gated NUDGE TOOL** — rather than the team iterating exact held/stump-axe transforms, Devon ships a sane DEFAULT plus a debug tool the Sponsor drives himself (select prop → nudge pos+rot → read live values on the HUD → report → bake). (2) **Sea color ACCEPTED** — the now-visible teal sea is liked; saturation polish is DEFERRED, not a blocker. (3) **Axe-head ACCEPTED** — the slate/steel sourced-hatchet head "genuinely looks like an axe"; the earlier barn-red recolor idea is DROPPED. (4) **auto-status OFF** — cron `03029456` cancelled, state file `enabled=false`; re-arm only on an explicit Sponsor ask.
- Why: (1) The held/stump-axe placement is a subjective-feel call best dialed by the Sponsor against the real gameplay view — a nudge tool ends the over-iterate loop and lets him set it once. (2)/(3) Locking the two "good enough / liked" visual calls stops further color-chase churn. (4) The Sponsor is back at the keyboard, so the away-orchestration pulse is unneeded.
- Reversibility: reversible (saturation polish remains a future tweak; auto-status re-armable; nudge tool is build-gated debug, not shipped UX)
- Affects: held/stump-axe placement (Devon), sea + axe-head polish backlog, orchestrator cadence

## 2026-06-15 — Castaway re-generated (Hyper3D→Mixamo) + adopted on the GENERIC rig

- Decided by: Sponsor (concept pick + ADOPT call) + Devon (in-engine rig finding; ticket `86ca8r72j` spike → `86ca8rdkp` adoption)
- Decision: The Sketchfab chibi is REPLACED by a freshly-generated chunky-low-poly castaway (concept art → Rodin Gen-2.5 Image-to-3D, Quad 8k/symmetric/de-lit → Mixamo Standard-Skeleton auto-rig, Idle+Walk). The viability spike (`86ca8r72j`) proved it imports + animates + reads on-style in a shipped URP exe; the Sponsor chose ADOPT, integrated under `86ca8rdkp`. **Load-bearing rig call: ship on the GENERIC (transform-path-bind) rig, NOT Mixamo Humanoid** — the Humanoid muscle-retarget EXPLODES the skinned mesh at runtime (cone displacement) under the scaled scene hierarchy; the spike's bounds-following camera HID it. Generic renders clean. New right-hand bone `mixamorig:RightHand` (lossyScale 1 — no 267× trap). Recolor = luma-preserving HSV remap (toon gradient kept).
- Why: A purpose-generated base beats re-skinning a fighting mesh (the chibi/Quaternius lesson). The Humanoid-explosion is invisible to a spike capture whose camera follows the mesh bounds — the shipped-build capture gate at gameplay framing is what surfaced it. Generic-rig transform-path binding sidesteps the muscle-retarget that detonates under non-uniform scene scale.
- Reversibility: reversible (revert the adoption PR; the rig choice is an import-setting + wiring change in ≤1 PR)
- Affects: player character, rig/animation pipeline, Devon + Uma + Tess, `character-pipeline.md` §Step 4 + `unity-conventions.md` §FBX

## 2026-06-15 — During-walk float = exponential-smoothing LAG (not snap-pick); ship a dial WITH its gauge

- Decided by: Devon (4th-attempt root-cause) + Sponsor (escalation: "you have to add logging or nudging"; ticket `86ca8rdkp`)
- Decision: The recurring "grounded standing, elevated walking" complaint is the EXPONENTIAL-SMOOTHING-LAG class, NOT a snap-pick error. At rest the snap is exact (gap 0.000); a constant-rate (k=18) filter lagged the descending foreshore ~1.2cm at 5.5 u/s while moving, and the blob shadow compounded it (driven off the RAW target while feet rode the SMOOTHED Y). Fix: speed-adaptive snap rate (`snapRateMove` 60 ≫ `snapRateRest` 18) + shadow off the avatar's ACTUAL world-Y + a Sponsor-dialable `groundYOffset`. **And: don't ship a dial without its gauge** — the float was chased for many iterations until a LIVE on-screen measurement (the F8/F9 FloatDiagnostic GAP readout, ~1Hz `[FloatTrace]` log) PROVED feet track the foreshore within ≤2.6mm the whole walk. "Is it fixed" is now answered by a number, not argument.
- Why: After 2+ rejects on a subjective-feel target the unstick/instrument rule fires — a gauge ends the argue-loop and makes the next dial precise (memory [[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]]). The lag-vs-pick distinction matters because color/value tweaks can never fix a timing-lag bug.
- Reversibility: reversible (snap-rate + shadow-source are tunable; the diagnostic is build-gated debug)
- Affects: avatar grounding (Devon), the F8/F9 diagnostic, all snapped-avatar locomotion percepts, Tess

## 2026-06-15 — Held-prop stabilization in the BODY-ROOT local frame, not world space

- Decided by: Devon (held-axe walk-shift root-cause; ticket `86ca8rdkp`)
- Decision: A held prop that shifts/clips as the holding arm animates through the walk cycle is stabilized by anchoring its grip in the **BODY-ROOT local frame** (a grip-anchor in `HeldAxeRig`), NOT in world space. Measured: world-space posing made the grip drift WORSE (0.93→1.5u) because world lags locomotion; the body-root grip-anchor cut it to →0.18u (81% better). (Note: this REFINES the 2026-06-13 "held props posed in WORLD space" decision for the OLD chibi `RightHand_010` rig — that rig carried a 267× lossy-scale + arbitrary local axes that made bone-local non-deterministic; the new Mixamo `mixamorig:RightHand` has lossyScale 1, so body-root-local stabilization is now both possible and superior for swing-stability.)
- Why: World-space pose is recomputed each frame from a lagging locomotion transform, so a clip-driven arm swing drags the prop; a body-root-relative anchor moves WITH the body and only the local swing remains, which is what stabilizing damps.
- Reversibility: reversible (grip-anchor frame is a one-method change)
- Affects: held-prop pipeline, Devon, any bone-attached prop on the new rig

## 2026-06-15 — Vista = discrete grounded land/island clusters + fog recession (supersedes Erik's far-ring)

- Decided by: Drew (root-cause + production call; ticket `86ca8t9pq`)
- Decision: The far-horizon vista is built from **discrete GROUNDED land/island clusters** (faceted `FacetedLandmass` shelves, e.g. ~18 peaks in ~6 island clusters on landmass bases +2-6u, ≥5/12 azimuth sectors left open sky/sea) with **fog-only atmospheric recession** — NOT Erik's far-encircling mountain-ring + deep per-cluster fade-tint. Erik's ring+deep-fade caused "floating translucent shards" (the mountain ROOT CAUSE was a DOUBLE-FADE: per-cluster tint 0.45-0.82 × Exp² fog both washing far clusters 70-95% to horizon). Fix capped tint (`MtnFadeCap` 0.25), pulled clusters in (`MtnDistanceScale` 0.55), dropped the 950u ghost range, grounded each on an island shelf. Open sky now dominates.
- Why: A grounded, opaque, discrete-cluster vista reads as solid distant land; an encircling translucent ring with stacked fade fights the fog and produces shards. Erik's winding/surface hypothesis was REFUTED by trace (shader was opaque alpha=1, winding green) — the failure was compositional (double-fade), not a render bug.
- Reversibility: reversible (vista-gen parameters + landmass mesh; tunable in ≤1 PR)
- Affects: world-look vista (Drew), `LowPolyZoneGen`, the art board's far-horizon read, Erik-consult routing

## 2026-06-15 — Diagnose-via-trace BEFORE fixing — geometry/threshold subjective tuning is trace-swept first

- Decided by: orchestrator + Drew + Devon (pattern hardened across the whole 2026-06-15 saga; tickets `86ca8t9pq` / `86ca8rdkp`)
- Decision: Geometry- and threshold-bound subjective tuning is **trace-swept against a headless geom model + scene-verified BEFORE serving a soak** — never fixed on a naive hypothesis. Naive framings were overturned REPEATEDLY this saga: walk-elevated (4 attempts; was smoothing-lag, then shadow-stranded-above-feet, then renderer-disabled-slab-pick); "water elevated" (was vista-islands DRAPING over the play space, not water-Y, not sea-extent, not occlusion — all trace-refuted); finger-mangle (was an OPEN clip-hand around the haft, skinning CLEAN — not a re-weight); shoreline foam (was a steep ramp + coarse water grid stranding foam over deep water, not "kept-only-shifted"); sky greyish (was the over-shoulder orbit framing the MID/horizon band — saturate THAT band, not the zenith — plus a STALE committed `GradientSky.mat` masking the source palette). Each was caught only by trace (`-hideVista`, magenta cross-build diff, `-groundTrace`, isolation probes), never by the first plausible fix.
- Why: The intuitive fix-shape was wrong more often than right on geometry/threshold-bound percepts this saga; trace is the cheap instrument that prevents the expensive soak-overturn loop (Erik's #1 accuracy pattern — Diagnose-Before-Fix kills 2-4 overturns/defect).
- Reversibility: n/a (a process convention; lives in TESTING_BAR / dispatch discipline)
- Affects: all subjective-visual/geometry tuning, Devon + Drew + Tess + orchestrator, Erik's accuracy patterns

## 2026-06-15 — Custom URP skybox shader must use standard skybox-pass render state

- Decided by: Drew (root-cause via `-flatSky` probe; ticket `86ca8t9pq`)
- Decision: A custom URP skybox shader assigned to `RenderSettings.skybox` MUST use **standard skybox-pass render state** (`Cull Off` / `ZWrite Off`, object-space direction, normal clip). The `GradientSkybox.shader` was forcing depth (`positionCS.xyww` on a Background-queue SubShader) → it drew OVER scene geometry → whole-frame wash. Fixed to the standard skybox-pass state.
- Why: A Background-queue shader that writes/forces depth paints across the frame instead of behind geometry — sibling of the magenta/cull-back false-symptom family. The skybox pass has a prescribed render state; deviating from it makes the sky occlude the world.
- Reversibility: reversible (shader render-state block; one-shader change)
- Affects: sky rendering (Drew), `GradientSkybox.shader`, `unity-conventions.md` §Editor-vs-runtime

## 2026-06-15 — In-house asset routes confirmed over paid AI-3D tools (3D-Agent declined)

- Decided by: Sponsor (asked Erik to evaluate; Erik recommended AGAINST; ticket `86ca92vrk` + the world-look-quality consult)
- Decision: The asset route stays **in-house — procedural + URP Shader Graph (world/props) + Hyper3D Rodin → Mixamo (characters)**. The paid AI-3D generator **3D-Agent.com is NOT adopted** (Erik's eval: photoreal output, no low-poly control; doesn't fit the chunky-cartoon direction; existing routes already cover world-look assets, characters/props, and the asset pipeline). Meshy.ai free tier noted as a fallback only. (Reaffirms + extends the 2026-06-13 asset-route decisions with the explicit 3D-Agent decline.)
- Why: The existing routes proved out the castaway + hatchet + full world-look this saga at zero/low marginal cost and WITH the stylization control a photoreal generator lacks; a paid tool that fights the art direction is not worth the spend (memory [[in-house-asset-routes-over-paid-tools]]).
- Reversibility: reversible (route preference; re-evaluable if a low-poly-capable tool appears)
- Affects: all asset creation, Devon + Drew + Uma, orchestrator R&D lane

## 2026-06-15 — Stacked-PR integration (#48 on #47) + re-reconcile each soak round

- Decided by: orchestrator (integration topology; validated against `team/orchestrator/integration-39-38-playbook.md`)
- Decision: The multi-round character (#47) + world-look (#48) work is integrated as a **linear stacked PR** — #48 is based on #47's branch (not main), so the combined build carries both feature sets in one regenerated `Boot.unity`. Each soak round that churns either side **RE-RECONCILES #48 onto the updated #47** and regenerates Boot.unity per the integration playbook; only `Boot.unity` + `BuildStamp.txt` ever conflict (code auto-merges clean). Consequence: CI does NOT auto-run on #48 (it fires only on PRs→main) — the local full suite + serve_soak stamp==HEAD are the authoritative soak evidence; at the big merge, land #47 first OR retarget #48→main and re-run CI (EPERM-aware).
- Why: A linear stack avoids a three-way Boot.unity reconcile and keeps one combined soak artifact; the regenerate-on-rebase playbook (proven #32/#36/#40) makes the per-round reconcile mechanical with a known conflict surface.
- Reversibility: reversible (branch topology; retarget-able to main at merge)
- Affects: orchestrator integration flow, all scene-baking stacked PRs, Devon + Drew + Tess

## 2026-06-15 — World-look LOOK-verdict still Sponsor-PENDING (technical/root-cause decisions above ARE settled)

- Decided by: orchestrator (status note, not a settled look-call; tickets `86ca8t9pq` / `86ca8rdkp`)
- Decision: The above 2026-06-15 entries capture the SETTLED technical + root-cause decisions of the saga. The final **world-look LOOK-approval remains Sponsor-pending** — across the saga the Sponsor APPROVED-IN-PART repeatedly (C5 walk-grounding accepted, shoreline position fixed, character identity/recolor good) while flagging fresh world-look issues each round (shoreline foam, sky/clouds, mountain detail). The combined soak (#48 stacked on #47) is being re-served; the look-verdict + THE BIG MERGE stay Sponsor-gated on the protected branch.
- Reversibility: n/a (a status flag; the look-verdict is the Sponsor's to give)
- Affects: roadmap sequencing, the big merge, orchestrator cadence, Devon + Drew

## 2026-06-16 — Organic seed-42 island is the world basis (supersedes the round disc + the strip)

- Decided by: Sponsor (soak picks 2026-06-16; "I love this island, commit this" → SEED 42 LOCKED)
- Decision: The world is a big ORGANIC/IRREGULAR procedural island — varied coast (beaches + cliffs), beach level with the grass, foam on all edges, water on all sides, mountains on separate islands — generated at `LowPolyZoneGen.IslandSeed = 42` (LOCKED; do NOT re-roll). Supersedes the earlier round disc and the beach-to-meadow strip.
- Why: The disc read artificial (square seabed edge + a "line"); the Sponsor wanted a real-island silhouette and picked seed 42 from 4 variant captures as the most "real island" (peninsula + bays). Shipped in the big merge (#50 → main `6aada8f`).
- Reversibility: reversible in principle (re-roll the seed) but Sponsor-locked — treat as one-way unless he reopens it.
- Affects: world gen (LowPolyZoneGen), NavMesh, camera, all future world content.

## 2026-06-16 — Sea renders Opaque + top-as-front-face (URP cull is by WINDING, not the normal)

- Decided by: orchestrator + Drew (root-cause, PR #50 `d944f6c`)
- Decision: The "sea reads identical to sky" defect was BACKFACE-CULLING, not fog — URP `Cull Back` culls by triangle WINDING, so a +Y-normal guard is a proxy a culled mesh satisfies. Fix = reverse the sea triangle winding so the TOP is the FRONT face; GUARD the winding direction (not the normal). Water stays Opaque-queue (avoids transparent overdraw on the large ocean) with a water-only fog cap → distinct teal + moving waves.
- Why: The gameplay cam saw the skybox THROUGH the culled sea (water==sky); the normal-guard false-greened. Same perceptual-vs-proxy cull family as the magenta / −Z-grid findings (unity-conventions.md).
- Reversibility: reversible (winding flip) but it is the correct render setup — do not revert.
- Affects: water rendering, the visual-pass SRP gate (`86ca9a3b3`), unity-conventions.

## 2026-06-16 — Held prop FOLLOWS the arm's natural swing (reverses the stabilizer)

- Decided by: Sponsor (soak 2026-06-16, "it works perfectly"; final F9 seat dialed)
- Decision: The held axe rides the RAW hand bone's natural swing during locomotion — `HeldAxeRig` removed the swing-stabilizer/grip-anchor AND the bounce-fix vertical-decouple, keeping only the facing fix (hand-local offset rotated by `hand.rotation`, never `hand.TransformPoint`) + a light damp. Final seat `HeldAxeWorldOffsetFromHand=(-0.1502,-0.1602,-0.0528)`, euler `(16,2,-82)`. This REVERSES the earlier stabilize-steady decisions (`86ca8rdkp` / `86ca9ykp0`).
- Why: "Steady-held" vs "natural swing" is a taste call; the Sponsor chose natural follow. Follow-the-arm is simpler and has no cumulative ratchet by construction. The choice carries to run/jump.
- Reversibility: reversible (re-add stabilization) but Sponsor-chosen — the stabilizer traps are the path not taken.
- Affects: HeldAxeRig, CastawayCharacter, the locomotion backlog (run/jump axe behavior).

## 2026-06-16 — Locomotion pivots to WASD + run + jump (supersedes click-to-move core feel)

- Decided by: Sponsor (2026-06-16; CLAUDE.md core-feel line updated 2026-06-17 per his "WASD is the core feel" pick)
- Decision: The movement model pivots from PoE-style click-to-move to WASD + run (Shift) + jump (Space). This REVERSES the "Sponsor-locked PoE-style click-to-move core feel" in CLAUDE.md Context. Backlog sequenced WASD `86ca9yq2x` → run `86ca9yq34` → jump `86ca9yq3q`; the live build keeps click-to-move until they land.
- Why: Sponsor preference — direct WASD control fits the survival-exploration feel better than click-to-move.
- Reversibility: reversible (the input layer is swappable), Sponsor-directed.
- Affects: input/locomotion (CastawayCharacter, MovementCameraScene), held-axe behavior, and jump touches the float system (`modelSoleGround` must suspend ground-snap airborne).

## 2026-06-16 — Unity-6/URP mastery is an always-on mandatory-read (Sponsor HIGH-PRIORITY)

- Decided by: Sponsor (2026-06-16, "cannot stress enough how important")
- Decision: `.claude/docs/unity6-mastery.md` (distilled Unity 6/URP always-on guardrails) is auto-loaded at SessionStart and a MANDATORY pre-read for Drew/Devon before ANY Unity code — wired into CLAUDE.md, the dispatch-template, and the persona files; full cited reference at `team/erik-consult/unity6-mastery-research.md`.
- Why: Repeated Unity/URP traps (serialization, culling, GC, lighting budget) cost soak rounds; a distilled always-on reference reduces them. Sponsor flagged it as high-priority. Shipped in orch-docs PR #56.
- Reversibility: reversible (docs) but a standing process gate.
- Affects: every Drew/Devon Unity dispatch, dispatch-template, SessionStart hook.

## 2026-06-17 — Locomotion sequence locked: WASD (merged) → run → jump → crouch

- Decided by: Sponsor (order is Sponsor-set; crouch added 2026-06-17)
- Decision: The locomotion family ships in a Sponsor-set order: **WASD MERGED** (`86ca9yq2x`, PR #63 squash `f34a829`, feel-approved) → **run-on-Shift** in flight (`86ca9yq34`) → **jump-on-Space** queued (`86ca9yq3q`) → **crouch-on-Ctrl** new (`86caa3kur`, queued). Run/jump/crouch each build on the merged WASD base; crouch is best sequenced AFTER run + jump land so its stance composes onto the finished Walk/Run/Jump Animator without blend-tree churn (but is independent enough to dispatch whenever the locomotion lane is free). Jump is the ONE ticket that touches the float system — it must SUSPEND `modelSoleGround` ground-snap airborne while leaving the grounded-state 8-attempt float fix unchanged.
- Why: WASD is the new core feel (supersedes click-to-move; see 2026-06-16 pivot). The Sponsor set the per-feature order; each feature is feel-soaked before merge. Serializing run→jump→crouch keeps the shared Animator + the held-axe/finger-curl/grounding wiring from churning under parallel edits.
- Reversibility: reversible (each feature is an additive input + Animator state; per-ticket revert in ≤1 PR).
- Affects: input/locomotion (CastawayCharacter, MovementCameraScene), the Animator, held-axe + finger-curl drivers, `modelSoleGround` (jump only), Devon + Drew + Tess.

## 2026-06-17 — Locomotion animation route: Sponsor-sourced Mixamo Without-Skin / In-Place clips

- Decided by: Sponsor (clip sourcing) + Devon (retarget execution)
- Decision: The locomotion clips (Running / Jump / Crouching-Idle / Sneak-Walk) are **sourced by the Sponsor from Mixamo** as **FBX-for-Unity / Without Skin / In Place / 30fps** and dropped into `Assets/Art/Character/Castaway/`; the implementing agent imports + retargets them to the castaway Humanoid like the existing Idle/Walk (Rig → Humanoid, Copy-From-Other-Avatar = the Idle avatar). **In-Place** because movement is driven by the locomotion system (NavMeshAgent-driven), not by root-motion in the clip. The Sponsor-downloaded crouch clips (`Crouching Idle.fbx`, `Sneak Walk.fbx`) are UNTRACKED in the main worktree — the agent copies them from `c:/Trunk/PRIVATE/Far-Horizon/Assets/Art/Character/Castaway/` into its own worktree AFTER its Step-0 `git clean` (else the clean wipes them). All clips carry the Mixamo MANGLED-FINGER note: the open-hand pose reads mangled holding the axe → the HasAxe-gated `CastawayFingerCurl` driver (curl axis MEASURED, not guessed) must cover every new clip.
- Why: Mixamo + the existing Hyper3D→Mixamo pipeline already produced Idle/Walk; reusing the route keeps the rig/retarget mechanics identical. Without-Skin clips retarget onto the existing castaway mesh; In-Place avoids fighting the code-driven locomotion with baked root motion.
- Reversibility: reversible (clip swap / re-import per feature in ≤1 PR).
- Affects: run/jump/crouch animation (Devon), `character-pipeline.md`, the finger-curl driver, the Animator.

## 2026-06-17 — Hit-reaction clips PARKED for a future damage/combat-feedback feature

- Decided by: Sponsor (2026-06-17)
- Decision: The hit-reaction clips (Head Hit / Rib Hit / Stomach Hit / Big Stomach Hit / Getting Up / Stunned) are **PARKED** — not wired now — for a FUTURE damage/combat-feedback feature. There is no damage source designed yet, so there is nothing for these reactions to respond to.
- Why: Wiring reaction animations with no damage system to trigger them would be dead content; the clips are deferred until a damage/combat-feedback feature gives them a trigger. Keeps the locomotion + gameplay waves thin and free of speculative animation state.
- Reversibility: reversible (the clips are parked, not deleted; pick them up when the damage feature is designed).
- Affects: animation backlog, a future damage/combat-feedback milestone, Devon + Priya (scope).

## 2026-06-17 — Gameplay wave: settings panel → inventory/belt → chop → stone (settings = extensible registry)

- Decided by: Sponsor (ticket-prompts 2026-06-17; sequence Sponsor-set)
- Decision: A new gameplay wave of four tickets ships in this strict order: **settings panel** (`86caa4bqp`) → **inventory + belt** (`86caa4bya`) → **chop trees for wood** (`86caa4c5c`) → **pick up small stones** (`86caa4c96`). The **settings panel is FIRST and FOUNDATIONAL** — it is an EXTENSIBLE registry (each setting a named, typed entry — float slider / int / min-max range — bound to a LIVE gameplay param, no restart) that the later three tickets REGISTER into (inventory registers belt-slot / inventory-slot / stack-size; chop registers tree-regrowth + tool-use-speed; stone registers stone-respawn). The inventory ticket defines the shared ITEM model — the **tool-vs-resource rule** (tools → belt-allowed + don't stack; resources → inventory-only + stack to a cap) — that chop (`chopped wood`) and stone (`picked up stones`) plug their resource items into. Inventory on Tab (20 slots), belt hotbar at the bottom (5 slots, select via 1–5 / scroll), axe = PoC tool auto-placed in belt slot 1 + shown in-hand only when selected.
- Why: The settings panel is the soak-tuning instrument (give-him-the-knob: the Sponsor dials values live, we bake the chosen defaults — the F9 axe-nudge pattern generalized to a registry). Building it first means each downstream feature registers its tweakables instead of hard-coding them; building the inventory item model second means chop + stone are thin add-ons onto a settled item/slot system rather than re-deriving it. The strict order is a hard blocked-by chain — the downstream tickets consume the upstream tickets' shared surfaces.
- Reversibility: reversible (additive feature systems; per-ticket revert in ≤1 PR) — but the SHARED contracts (settings-registry API + inventory item model) should be pinned before any PARALLEL dispatch (see `team/priya-pm/gameplay-wave-plan.md`).
- Affects: settings/inventory/chop/stone systems, UI Toolkit, world-gen scatter (chop/stone), `HeldAxeRig` (selected-slot show/hide), Devon + Drew + Tess.

## 2026-06-17 — M-U2 survival loop EXPANDED from one need (WARMTH) to three (warmth + hunger + thirst)

- Decided by: Sponsor (2026-06-17; vision doc `.claude/docs/vision-far-horizon-game-concept.md` + ticket-prompts)
- Decision: M-U2 — which shipped THIN with a single Sponsor-locked WARMTH need (DECISIONS 2026-06-12) — is **expanded to THREE needs**: **warmth** (existing, campfire), **hunger** (harvest berries from bushes → "small satisfaction to his hunger"), and **thirst** (drink-from-hand at a freshwater pond → "small amount of thirst with each scoop"). Three new tickets carry it: `86caamkp8` (HUNGER need — generalizes the `WarmthNeed` model, satisfied by the berry eat-action from bushes `86caa5zz3`), `86caamkv7` (THIRST need + a freshwater pond placed in the seed-42 world + a no-tool drink-scoop interaction), `86caamkxv` (need-meter HUD — generalizes the single-warmth `SurvivalHud` to three bars, to Uma's forthcoming direction). All three GENERALIZE the existing `WarmthNeed` surface (`Current01`/`Max`/`IsCritical`/`Changed`, Time.time-window decay, `TickSeconds` for EditMode) — no rebuild. Death/fail/starvation/dehydration states stay OUT of scope (a floor, not a fail). A cup/container to hold more water is explicitly deferred ("later").
- Why: The Sponsor's full survival-arc vision always included berries/hunger + fresh-water/thirst (the game-concept doc); the WARMTH-only loop was the deliberate thin START, and the single-need model was designed to generalize to N needs. Expanding now — after locomotion + the gameplay wave's inventory — lets berries be eaten from inventory and the pond plug into the settled item/interaction patterns. This reconciles the scope-mismatch flagged in the game-concept doc's index line.
- Reversibility: reversible (additive need systems on the proven `WarmthNeed` pattern; per-ticket revert in ≤1 PR). The three needs land close together — a shared abstract need base, if extracted, must agree its name + surface BEFORE both land (shared-concept naming discipline; coordination noted on `86caamkv7`).
- Affects: survival loop, HUD, world-gen (pond + bushes), settings registry (need tweakables), CLAUDE.md M-U2 scope (updated this PR), Devon + Drew + Uma + Tess.

## 2026-06-17 — Adopt Erik's procedural-mesh + URP-shader quality findings as standing dev guidance

- Decided by: Sponsor (2026-06-17, "apply Erik R&D findings to all developers")
- Decision: Erik's R&D note `team/erik-consult/procedural-shadergraph-quality-research.md` (ticket `86ca8x038`) is **distilled into a standing dev-guardrails doc `.claude/docs/lowpoly-quality.md`** — the `unity6-mastery.md` precedent — and made a MANDATORY pre-work read for all visual/mesh/shader work (auto-loads via the SessionStart hook + the existing "sub-agents Read every `.claude/docs/*.md` before work" rule; a CLAUDE.md Detailed-Documentation index line added). The seven adoptable patterns are filed as Unity tickets, sequenced for the single build slot: `86caamnhf` (apply the confirmed-bug `QuantizeFine` fix), `86caamnjb` (`_FlatShading` ddx/ddy toggle), `86caamnmb` (transparent depth-fade `LowPolyWater.shader` — fog-cap migration risk noted), `86caamnnj` (Fresnel/rim term), and `86caamnra` (a polish backlog rolling up chamfer highlight + vertex-AO bake + seeded scatter rotation). Toon hard-band ramp + screen-space outlines + flat-shading the welded terrain + transparent-water-without-fog-cap are explicitly RULED OUT (they fight the approved faceted-smooth look).
- Why: The Sponsor wants the in-house procedural-mesh + URP-shader route (paid AI-3D tools declined) levelled up; standing guidance + filed tickets operationalize the research into code rather than leaving it as a one-off note. The doc also pins the already-correct patterns NOT to regress (outward winding, per-face normals, up-biased foliage normals, SRP-Batcher compliance, the `_FogCap` floor) so a future change doesn't reopen a closed bug.
- Reversibility: reversible (doc + tickets; no code shipped in this PR — each code ticket reverts in ≤1 PR).
- Affects: all visual/mesh/shader work, Drew + Devon (+ Sponsor-Blender for chamfer geometry), Tess (the visual-UX gate already requires an SRP-Batcher check per `86ca9a3b3`).

## 2026-06-18 — Locomotion-first gate RELEASED; gameplay wave un-gated; crouch deferred

- Decided by: Sponsor (2026-06-18)
- Decision: The locomotion-first sequence gate is **released** — the gameplay/survival wave (settings panel → inventory/belt → chop → stone → bushes/berries → hunger → thirst → three-bar HUD) is now **un-gated** and dispatchable without waiting for run/jump to fully land. **Crouch (`86caa3kur`) is DEPRIORITIZED** ("it can wait") — set to low priority, to be picked up after the gameplay wave. The settings panel (`86caa4bqp`) remains the foundational first dispatch (extensible registry the downstream tickets register into); the inventory item model (`86caa4bya`) remains the second, gating the world-resource family (chop/stone/bushes).
- Why: the Sponsor judged the locomotion lane far enough along that the gameplay wave should not idle behind it; crouch is the lowest-value locomotion remainder and composes onto the finished Animator just as well later. Capacity discipline: the wave is constrained by the single-Unity-build slot (one Unity-build ticket in flight at a time per `single-unity-build-slot-serializes-orchestration`), so the wave serializes on the build-bearing tickets even though the dependency graph would allow more parallelism.
- Reversibility: reversible (priority + sequence calls; re-gate or re-prioritize crouch in a single board edit).
- Affects: dispatch order, the gameplay wave, crouch ticket priority, orchestrator + Devon + Drew + Tess.

## 2026-06-19 — Route A: unified hand-tool/weapon visual style via ONE in-house Blender pipeline

- Decided by: Sponsor (2026-06-19, "Route A")
- Decision: The hand-tool/weapon family (axe, knife, sword, spear, …) gets a **unified visual style** through **ONE in-house Blender (MCP) pipeline** sharing **ONE style spec** (`team/uma-ux/weapon-tool-style-spec.md`, Uma finalizes) + **ONE shared low-poly palette material** — NOT per-asset sourcing. Family cohesion is treated as a STYLE-SYSTEM decision (shared spec + shading model + palette + one pipeline + one shared grip pivot so a single `HeldTool` rig generalizes), not item-by-item asset acquisition. The currently-shipped CC-BY axe (`Assets/Art/Props/CastawayAxe/` — Viktor.G "One-handed stylized axe", Sketchfab CC-BY, baked photographic atlas) is a **PLACEHOLDER to be re-made in the family style — NOT the style anchor** (it is the outlier vs the flat-shaded Zone-D world). Two style parameters stay OPEN for Uma to lock against the LIVE build (not in the abstract): (a) shading model flat-vs-smooth — verify against how the world props are actually shaded in-engine; (b) palette hexes — EXTRACTED from the live world palette, never invented. Re-making the axe in-house RETIRES the CC-BY attribution obligation: once the in-house axe ships, remove `Assets/Art/Props/CastawayAxe/CastawayAxe_License_CC-Attribution.txt` + the in-game/about credit.
- Why: Sourcing each weapon separately gives a mismatched family (the current axe's baked atlas is exactly that outlier); one shared spec + palette + pipeline is the only route that makes every item read as "made by the same castaway" and lets one `HeldTool` rig seat any item. In-house also keeps the family CC-obligation-free. Attribution history: a procedural C# `HeroAxeMesh` was tried first (PR #21, abandoned — "didn't read as an axe"), replaced by the Viktor.G CC-BY asset (PR #29, ticket 86ca8ce6y) which carries an in-game-credit obligation until replaced. Tickets: Uma finalizes the spec (lock the 2 open params); Devon produces the matched SET + re-makes the hero axe (HARD-GATED on the spec; uses the single Unity-build slot; generalizes `HeldAxe.cs`/`HeldAxeRig.cs` → a shared `HeldTool` rig; shipped-build capture gate before merge). Rationale memory: `weapon-tool-unified-style-inhouse-blender-set`.
- Reversibility: reversible (style-system + pipeline convention; per-asset revert in ≤1 PR) — but re-making the axe + retiring the CC-BY file is effectively one-way once it lands and the license file is removed.
- Affects: all hand-tool/weapon assets, the shared palette material, `HeldTool` rig (generalized from `HeldAxeRig`), the CC-BY attribution obligation + about-screen credit, Uma (spec) + Devon (SET) + Tess (capture gate).

## 2026-06-19 — Shared survival-need base = Pattern A: hunger OWNS `SurvivalNeed`, thirst EXTENDS it

- Decided by: Priya (AC pin resolving a Tess wave-prep mergeability risk, PR #86; consistent with the parallel-shared-concept naming discipline + DECISIONS 2026-06-17 three-needs expansion)
- Decision: For the hunger (`86caamkp8`) / thirst (`86caamkv7`) pair that both generalize the `WarmthNeed` surface and both feed the three-bar need HUD (`86caamkxv`), the shared abstract need base is owned by **Pattern A** — the FIRST-to-land need (HUNGER) OWNS the shared base type `SurvivalNeed` (defined in `Assets/Scripts/Runtime/SurvivalNeed.cs`, surface byte-identical to WarmthNeed: `Current01`/`Current`/`Max`/`IsCritical`, `event Action<float> Changed`, `TickSeconds`, `decayPerSecond`/`floor01`/`criticalThreshold01`, a protected satisfaction primitive). THIRST EXTENDS the merged `SurvivalNeed` from main (adds `AddWater`) and does NOT re-declare a base; HUNGER extends it adding `AddFood`. WarmthNeed is NOT required to be refactored onto the base by these tickets (the base generalizes WarmthNeed's *shape*, not its file). Divergence between the base and either need's usage at review is **REQUEST_CHANGES (mergeability-blocking), NOT a NIT**. Pinned in both tickets' ACs (hunger AC1a / thirst AC1a). Recommendation accompanying this decision: an AC-level pin SUFFICES — a full need-base vocabulary-contract doc (à la Drew's item-model contract) is NOT warranted, because the shared surface is fully specified by the existing `WarmthNeed.cs` (a single concrete reference both tickets already mirror) and only ONE new type name (`SurvivalNeed`) + its export site need pinning; sequencing hunger before thirst (Pattern A) collapses the remaining ambiguity by construction.
- Why: hunger + thirst land close together and both bind the HUD via the identical read surface; per the parallel-shared-concept naming discipline, an unowned shared base produces divergent vocabulary (`SurvivalNeed` vs `NeedBase`, differing `Current01` shapes) and non-mergeable parallel PRs. Pattern A (first-to-land owns; sequence the dispatches) removes the divergence by construction at the cost of one merge cycle, which the wave's single-Unity-build-slot serialization already imposes.
- Reversibility: reversible (the base type + its extension are a refactor in ≤1 PR; the ownership rule is an AC pin, re-editable on the board).
- Affects: hunger (`86caamkp8`) + thirst (`86caamkv7`) + the need HUD (`86caamkxv`), the survival-need model, Devon (owner of both needs) + Drew (reviewer) + Tess (the vocabulary-grep review gate).

## 2026-06-24 — The 3 survival needs do NOT share a common base TYPE; the HUD binds by read-state value, not by `SurvivalNeed` param

- Decided by: Devon (implementation finding on the three-bar HUD, ticket `86caamkxv` / PR #129)
- Decision: The three survival needs do **NOT** all share a common base type. **`WarmthNeed` is a standalone `MonoBehaviour`** that PREDATES `SurvivalNeed` and does NOT extend it; only **Hunger + Thirst extend the `SurvivalNeed` base**. Consequently the HUD's generalized `DrawNeedBar` takes the need read-state **BY VALUE** (the `current01` fill fraction + the `isCritical` flag, duck-typed and null-guarded) rather than a `SurvivalNeed`-typed widget parameter. This **supersedes** the assumption in the #125 HUD spec / #127 QA-plan that a `SurvivalNeed`-typed widget param would be the bindable surface. The earlier Pattern-A decision (2026-06-19) still holds for Hunger↔Thirst (Thirst extends the base Hunger owns); this entry only corrects the cross-need HUD-binding assumption — WarmthNeed is NOT on the base, so a base-typed HUD param could not bind all three.
- Why: `WarmthNeed` shipped first (M-U2 warmth-only loop, PR #11) before `SurvivalNeed` existed, and was never refactored onto the base (the Pattern-A decision explicitly did not require it). A `SurvivalNeed`-typed widget param therefore cannot accept WarmthNeed; passing the read-state by value (fill fraction + critical flag) is the only surface all three needs share, and it keeps `DrawNeedBar` decoupled from the need class hierarchy.
- Reversibility: reversible (refactoring WarmthNeed onto `SurvivalNeed` and re-typing the HUD param is a ≤1 PR change if a base-typed binding is later wanted).
- Affects: the three-bar need HUD (`86caamkxv`), the survival-need model + WarmthNeed, the #125 HUD spec + #127 QA-plan assumptions, Devon + Uma (HUD spec) + Tess (QA plan).

## 2026-06-24 — Real-world anchor + silhouette gate for physical-world features (four standing rules)

- Decided by: Sponsor (2026-06-24, popup "bake all four in")
- Decision: Four standing rules now govern every physical-world feature task (pond / fire / hill / dune / terrain carve / water body / shaped prop whose up-vs-down read matters), so such features look RIGHT on the FIRST try: **(1) Real-world anchor** — every such task OPENS with one plain sentence naming what the thing IS in real life ("a pond is a HOLE in the ground the player steps DOWN into; water collects in it"); the build must satisfy that sentence, not just a numeric/color/byte/seed metric. **(2) Mandatory side-profile (silhouette) capture** — before any 3D-shape feature ships to QA/Sponsor, the AUTHOR captures and eyeballs a side-on shot themselves (up-vs-down is invisible from player-eye/top-down, obvious side-on). **(3) Fix the cause, not the symptom** — a fix that contradicts the real-world anchor (e.g. raising water above ground to dodge an occlusion bug) is a band-aid → rethink/escalate; carve the pond INTO the terrain instead. **(4) Reviewer + QA human-eye line** — "would a person call this a <pond/fire/hill>?" sits beside the seed-42/byte/metric checks. Woven into: dispatch-template (new "Real-world anchor + silhouette gate" situational block + pre-dispatch checklist item), `lowpoly-quality.md` §0, devon.md + drew.md (self-test one-liner), tess.md (QA human-eye gate).
- Why: the freshwater pond shipped as a raised MOUND **twice** (cautionary example: PR #130) because the team chased the `-verifyPond` color metric (green on a mound — a metric can't tell a hole from a hill) and "fixed" a water-hidden-under-terrain occlusion bug by LIFTING water above ground = a pond on a hill (nonsense; a pond is a hole). Metrics can't see nonsense; anchoring in the real-world thing + a one-shot side-profile catches up-vs-down errors on the first pass, cheaper than repeated soak-reject rounds.
- Reversibility: reversible (process convention woven into docs/templates/persona files; removable in ≤1 PR).
- Affects: every physical-world-feature dispatch, the dispatch-template, `lowpoly-quality.md`, Devon + Drew (authors) + Tess (reviewer/QA gate) + orchestrator (dispatch-side block selection).

## 2026-06-25 — Freshwater pond collar = FLAT terrain-painted vertex color, NOT a raised mesh

- Decided by: Priya (lesson captured from Devon's round-5 pond diagnostic, ticket `86cadj4g7` / PR #130 — **in-flight, not merged**: the terrain-paint approach is proven correct, only the verify-gate is in round-6 rework, so the decision itself stands ahead of the merge)
- Decision: Flush ground-level water/terrain features (the freshwater pond, and any future puddle/shore/inlet collar) get their shoreline ring as **flat terrain-painted vertex color on the ground mesh, NOT a raised collar/bank-ring mesh**. The persistent white-shoreline-ring artifact in the round-5 pond was isolated by Devon's `-verifyPondDiag` prover. Foam was NOT one of the prover's toggles — foam was already OFF as a round-5 precondition (foam had been a separately-proven white-ring cause in an EARLIER round: the stale committed `PondWaterMat.mat`, final fix `c2af204`, guarded by `CommittedPondMaterialAsset_ShipsFoamOff_NotStale`). The prover's four conditions were **baseline / bloom-off / collar-REMOVED / sea-plane-off** (`Assets/Scripts/Runtime/FreshwaterPondVerifyCapture.cs:~85-89`). With foam ALREADY off, the round-5 residual white shoreline ring was PROVEN by the collar-removed toggle to be the **raised `PondBank` collar mesh** — removing the collar made the ring vanish, while bloom-off and sea-plane-off each left it present (so the round-5 residual ring was the collar, NOT bloom and NOT the sea plane). Root cause: a raised collar/bank-ring mesh draping a recessed-bowl wall catches the warm Zone-D key light edge-on and reads pale/washed-white — a structural white-ring source independent of any post/shader effect. Standing guidance: prefer a terrain-painted vertex-color ring (flush with the ground) over a raised bank-ring mesh for any flush ground feature; if a raised lip is genuinely wanted, treat the pale-edge read as the expected failure mode and verify it in the shipped-build capture, not just the editor.
- Why: the round-5 residual ring resisted effect-side fixes (bloom/sea-plane) because the cause was geometric, not shader/post; the prover isolated it definitively once foam was already ruled out (and fixed) in the prior round. Encoding "flush feature → flat painted ring, not a raised mesh" as a decision prevents the next ground-water feature from re-introducing the same raised-collar white-ring class. Coheres with the existing low-poly vertex-color inline-materials pattern (DECISIONS 2026-06-12) and the "physical features: anchor real-world + side-profile capture, fix the cause not the metric" discipline.
- Reversibility: reversible (a per-feature mesh-vs-painted-ring choice; revert in ≤1 PR) — but re-introducing a raised collar reopens the proven white-ring class. Note #130 is still in review; if its round-6 rework changes the approach materially, revisit this entry.
- Affects: the freshwater pond + any future flush ground-water feature, world-gen ground/terrain vertex-color painting, Devon + Drew (visual/mesh work) + Tess (shipped-build capture gate on the shoreline read).

## 2026-07-01 — Combat / HP / Death system LOCKED (grill-first, 9 Sponsor decisions)

- Decided by: Sponsor (via /grill-me, 9 branches all Sponsor-picked; design ticket `86cabcdpn`)
- Decision: The combat/HP/death model is locked across 9 decisions: **(1)** HP is a **dedicated `Health` component, SEPARATE from `SurvivalNeed`** (needs rest at a floor and never hit zero; HP takes ACUTE damage and 0 HP = death — a different shape, do NOT fold into SurvivalNeed). **(2)** Death consequence is **tiered by difficulty — the 3 death behaviors ARE the 3 tiers:** Easy = faint & recover in place (no setback, enemy disengages, low damage, fast regen); Medium = respawn at last campfire (start beach if none), inventory KEPT, moderate; Hard = respawn at camp + inventory DROPS at the death spot (reclaimable), high damage, slow regen. **(3)** HP regen is **NEEDS-GATED** — regenerates only while warmth/hunger/thirst are above threshold; a critical need STALLS regen (or slow-drains HP), reading the SurvivalNeed surface (`Current01`/`IsCritical`) without adding a new need. **(4)** Fighting back is a **WEAPON SYSTEM** (not axe-only): a weapon carries damage/reach/attack-speed/own-animation/damage-type (pierce/slash/blunt)/optional on-hit-status; identity = type × material tier; reuses the left-click swing; acquisition = BOTH craft-at-station (wood→stone→bone/metal) AND find-in-world. **(5)** Weapon-vs-mob effectiveness is **HYBRID** — weapon attributes + a damage-TYPE tag vs mob size/behavior + resistances/weaknesses ("spear beats boar" emerges from long reach + boar weak-to-pierce); systemic + designer-tunable via type↔resistance tags, NO full O(weapon×mob) table. **(6)** Status effects are a **GENERAL data-driven framework (DoT/stun/slow-capable), shipping BLEED first**; works both ways (mobs→player and player→mobs). **(7)** Enemies have HP + resistances + behavior and die at 0 HP (mirror of the player HP model). **(8)** Difficulty exposure — HP-max/damage-taken/regen-rate/death-behavior are per-tier, dialed in the dev-tweak console (same pattern as per-need decay `86cabeqwf`) and baked into the difficulty presets. **(9)** All of the above compose into ONE integrated system, which the POC proves. **Broken into impl tickets:** POC `86cah7xxp` (lean vertical slice — player HP + tiered death + needs-gated regen + snake-as-damageable + axe-vs-spear + bleed + damage-type↔resistance + HP readout) and phased follow-ups `86cah7y5b` (find-in-world acquisition), `86cah7ydt` (wild boar 2nd enemy + matchup proof), `86cah7ym9` (weapon-roster expansion), `86cah7yuh` (poison/stun/slow), `86cah7z2q` (HP HUD polish + heal sources). Design ticket `86cabcdpn` is design-complete (records the design + spawns the impl tickets).
- Why: the game had no health/damage/death (SurvivalNeed has only a cosmetic critical flag + a no-fail floor; WarmthNeed explicitly scoped death OUT) and the snake POC (`86caaz4vn`) shipped a bite with no effect. The Sponsor directed (2026-06-19) "introduce a real HP/death system but grill me about it first"; the grill resolved every branch. Keeping HP separate from needs, gating regen ON needs, and making enemies damageable ties combat into the existing survival loop as one system rather than a bolt-on. The tiered-death-as-difficulty-tiers choice makes the game kid-friendly on easy and consequential on hard per the standing difficulty-settings directive (quality-bar #7).
- Reversibility: reversible in principle (each system is new code addable/removable in bounded PRs) — but the model is Sponsor-locked and unblocks the whole combat roadmap, so treat as directionally committed. The enemy-HP surface is SHARED with the snake POC `86caaz4vn` (whichever lands the enemy `Health` first OWNS it; the other extends it — per the parallel-shared-concept naming discipline).
- Affects: a new `Health` component + weapon system + status-effect framework + enemy-HP surface; the SurvivalNeed framework (regen reads its surface), the dev-tweak console + difficulty presets, the unified-weapon Blender set + procedural-animation-verbs (per-weapon swings), the snake POC `86caaz4vn`; Drew (POC owner) + Devon (reviewer / systems follow-ups) + Uma (HP HUD) + Tess (QA + shipped-build capture + soak).

## 2026-06-30 — Next-island/boat POC: DESTINATION-FIRST — prove the big-island terrain-gen now; boat/journey deferred

- Decided by: Sponsor (2026-06-30 `/grill-me` on the next-island/boat prompt; ticket `86caa9zpp`)
- Decision: The next-island/boat prompt is **split into two halves and sequenced destination-first**. **Half 1 — DESTINATION (ticket `86caa9zpp`):** the POC proves the **big-island terrain-gen** and is **build-ready now** (grill done, design locked). The locked design: (Q1 scope) destination-first — the POC proves the terrain-gen; the boat/journey is a separate follow-up half. (Q2 size+perf) a *feels-big* island, **~2-3 min to cross**, holding **60fps** on the EXISTING low-poly + GPU-Resident-Drawer approach **scaled up** — scale toward the eventual ~10-min target ONLY if perf holds; the **#1 POC finding is the PERF VERDICT** (single scaled mesh + LOD vs needing chunked/streamed terrain). (Q3 mountain) ONE dominant **snow-cap peak** = the island's hero landmark + future sea-beacon, snow rendered as a **height-threshold white material** on the faceted low-poly mesh (no snow texture). Shape: organic / non-round, like the seed-42 start island. (Q4 success bar) walkable + feels-big + organic + one snow-cap peak + 60fps, judged in the **shipped build** via a **Sponsor walk-soak**. The Sponsor **declined a separate Erik perf-benchmark — the build itself is the perf test.** **OOS of this POC:** the boat/sail + journey/reveal; survival systems on the new island; props/decoration/content; wiring to the seed-42 start island (the POC island loads **STAND-ALONE** for the soak). **Half 2 — JOURNEY (ticket `86caa9zju`, the boat):** **DEFERRED** — it gets its OWN `/grill-me` only AFTER the destination POC lands + soaks; kept `to do` + a `sponsor-gate`/grill-first note; the boat design is unsettled.
- Why: the "sail to a much-bigger island, walk ~10 min across it" vision only works if the terrain-gen scales without breaking framerate, so the Sponsor chose to answer the perf go/no-go on the big island BEFORE any boat work — the journey only matters if the destination is feasible. Trying the existing gen scaled (rather than a new gen or a separate benchmark) keeps the POC's question precise: does the *existing* approach scale? Sequencing destination-first also keeps the boat grill honest — it gets designed against a known-feasible island, not a hypothetical one.
- Reversibility: reversible (POC on an independent branch; the start island is untouched; the split + sequence are board state, re-editable in ≤1 PR). The perf VERDICT it produces is a finding, not a config.
- Affects: world-gen (a new stand-alone big-island POC scene + scaled terrain + height-threshold snow material), the next-island POC `86caa9zpp` (build-ready) + the deferred boat/journey `86caa9zju`, Devon/Drew (owner+reviewer) + Tess (shipped-build capture gate + side-profile silhouette) + the Sponsor (walk-soak), quality-bars Bar 1 (organic) + Bar 4 (real-world feature + side-profile), the single Unity-build slot.

## 2026-06-30 — Open-horizon = full open ocean (Option A); next-island reveal via natural fog-haze

- Decided by: Sponsor (2026-06-30 walkthrough on the open-horizon spec `86cafffe8` / PR #199)
- Decision: The horizon look is **Option A — full open ocean**: REMOVE the distant horizon mountains so the start island reads as "a little island lost in a huge ocean", open blue water dissolving into warm sky a full 360° around the player. **Option B (a faint rim hint on the horizon) is the pre-planned soak fallback** — adopt it only if an empty horizon reads cheap/flat in the soak. The future next-island reveal (the journey POC `86caa9zpp` follow-up) uses a **natural fog-haze reveal (Approach 1)** — the next island fades up out of the haze as the player nears it — NOT a scripted/authored cinematic reveal moment. Impl ticket: **`86cagfn8h`** (open-horizon look); soak-gated.
- Why: Sponsor vision call (subjective-feel, his domain). A full open ocean strengthens the big-endless-world / small-player north-star — the lone little island in a vast sea reads as the start of a real journey, and a natural haze-reveal keeps the eventual next-island moment diegetic rather than staged.
- Reversibility: reversible (the mountains are removable/re-addable world-gen state; Option B is the staged fallback if the soak rejects the empty horizon; revert in ≤1 PR).
- Affects: world-gen / skybox / horizon look (`86cagfn8h`), the next-island reveal approach for the journey POC follow-up, quality-bars (big-endless-world north-star), Devon/Drew (author) + Tess (shipped-build capture gate) + the Sponsor (soak).

## 2026-07-01 — Settings-panel SPLIT: player-facing Settings (F1) vs dev debug console (new key) — supersedes the unified-panel direction

- Decided by: Sponsor (2026-07-01 soak of the #218 F-key migration — "that was not the intention")
- Decision: The single unified settings/dev-tweak panel is **SPLIT into two panels**: **(a) a PLAYER-FACING Settings panel on F1** (the existing open key) carrying only the rows a player should touch — belt slots, inventory stack size, difficulty tier (if/when a row exists), and the three survival-need on/off toggles + decay-rate sliders (warmth/hunger/thirst) — WITH a conditional-visibility rule that shows a need's decay-rate slider ONLY when that need's decay toggle is ON; and **(b) a SEPARATE DEV DEBUG CONSOLE on a NEW key (proposed F3 — layout-agnostic function key, verified unused; F2 = legacy IMGUI overlays, F5/F6 = SneakIsolationTool)** carrying every dev-only visual/positional tuning row migrated in #218 (world-look sun/fog/clouds/mountains, arm-pose R/L, run-lower, cam-follow lerp/vertical/airborne/lead, held-weapon placement, ground-Y, air-control accel, tree/stone/berry/log timers + yields, plus the panel-chrome Console UI scale / UI text scale). This **REVERSES** the earlier "one unified panel that absorbs all F-key handles" direction (memory `[[sponsor-wants-unified-dev-tweak-console]]`, DECISIONS/dev-tweak-console-spec lineage) — the unification is retained ONLY within the dev console; the player must never see the dev nudges. Impl ticket: `86cah8ukr` (`feat/refactor`, Unity-build lane, Devon owner / Drew reviewer, `needs-soak`). #220 (`86cabeqwf`, the per-need on/off + decay-rate work) is **SUPERSEDED / folded into** the split ticket — its need rows land in the PLAYER panel with the new conditional-visibility fix rather than as unconditional rows on the unified panel.
- Why: The #218 migration correctly consolidated the standalone F7/F9/F10 dev nudges into the console, but it put them alongside genuine player settings, and the Sponsor's soak surfaced that a player opening Settings must NOT see arm-pose eulers / fog channels / follow-gains. The split keeps the dev-tuning unification (one console, all nudges) while giving the shipping player a clean, minimal Settings panel. The conditional-visibility fix (#220) removes the meaningless decay slider for a disabled need.
- Reversibility: reversible (a UI routing/key split + a per-need slider visibility rule; the underlying registry + bindings are unchanged, so revert is ≤1 PR — re-point both panels at one registry view + one key).
- Affects: `SettingsPanel` / `SettingsCatalog` / the panel scene wiring (`MovementCameraScene`/`Boot.unity`), the two open keys (F1 player + proposed F3 dev), the superseded #220 per-need work (`86cabeqwf`), memory `[[sponsor-wants-unified-dev-tweak-console]]` (now scoped to the dev console only), Devon (author) + Drew (reviewer) + Tess (shipped-build capture gate) + the Sponsor (soak).

## 2026-07-05 — Castaway v2 identity: bearded, rugged, friendly-neutral adult survivor
- Decided by: Sponsor
- Decision: The hero castaway's identity becomes a **bearded, rugged, friendly-neutral adult survivor**, deliberately REVERSING the earlier "young + happy" lock. (Sponsor chose the "Full reference look" against `inspiration/2026-06-12_21h00_32.png`, then iterated the concept to a friendly-neutral expression.)
- Why: Sponsor's direct in-session art call on the Castaway v2 concept — the full-reference rugged look reads as the intended hero over the earlier young+happy design sheet.
- Reversibility: reversible (asset/identity choice; the old castaway stays live until v2 passes the soak)
- Affects: character art, CLAUDE.md character identity, the Castaway v2 integration, downstream gear/customization

## 2026-07-05 — Hero-character route ratified: concept → Rodin → Mixamo
- Decided by: Sponsor
- Decision: The hero-character production route is ratified as **AI concept image (openai-image gpt-image-1, image-to-image from the inspiration board) → Hyper3D Rodin (web UI, Gen-2.5/High, de-light ON, Quad ~8000) → Mixamo auto-rig → Unity Humanoid**, proven end-to-end by shipping Castaway v2 through it (harvest PR #260).
- Why: proven in-session end-to-end (concept → Rodin → Mixamo → Blender-verified 41-bone rig); extends the existing character-pipeline.md route; the Rodin base is welded, so customization = gear modules + texture recolors (deeper hair/beard modularity = phase 2).
- Reversibility: reversible in principle (route choice); one-way in practice for v2
- Affects: character pipeline, character-pipeline.md, all future hero-character work, gear-module strategy

## 2026-07-06 — Sponsor walkthrough: #261 fold+merge, axe redo iter-1 approved, v2 owns grip, combined re-seat
- **#261 docs:** fold the 2 stale CLAUDE.md lines (line-3 identity, doc-index Humanoid->Generic; + the hero-route/v2-LIVE line, same error class) then auto-merge label. Folded as `d697960`.
- **Stone-axe redo (86cajkk7h):** Sponsor approved iteration 1 on the A/B — haft radial x0.70 (0.051–0.064 m), head uniform x0.88 about the mount point (X-width 0.312->0.2746 m). Baked to FBX + blend saved. Original rejected FBX preserved in session scratchpad only.
- **PR #239 / 86cahnmjv:** closed superseded-by-v2-rig; the combined v2 re-seat pass owns thumb/grip on the new hand; fresh fix only if v2 reproduces the defect.
- **Re-seat sequencing:** ONE combined Devon pass (new axe integration + all-4 stone weapons on the v2 hand) instead of two passes/two soaks.
- **Auto-status:** stays OFF during the interactive session; re-arm when dispatchable work exists.

## 2026-07-06 — Hero-character low-poly direction re-opened (Sponsor, feedback-driven)
Sponsor received feedback that the Hyper3D/Rodin castaway is "not low poly like the rest of the game" and wants to revisit the in-house low-poly path. Route decision deferred to evidence: Erik consult dispatched FIRST (ticket 86cak3r3k, note → team/erik-consult/lowpoly-hero-conversion-research.md) evaluating (1) low-polyfy the existing v2 mesh, (2) fresh hand-model burst (context: 7 bpy hero fails 2026-07-05), (3) Rodin re-gen from low-poly concept, (4) shader-only faceting. Castaway v2 STAYS the live default until a replacement passes the full soak gate — this decision re-opens direction, it does not revert #262.

## 2026-07-06 — Castaway v3 identity: hopeful-but-SCARED young adventurer (reverses the v2 bearded-rugged lock)
Sponsor (verbatim, route-pick popup): "I dont actually like the current v2. he is too strong and too happy. I would like another go at a hopeful but scared young man prepared for adventure." So the 2026-07-05 bearded-rugged-friendly identity is REJECTED for the next hero, partially returning to the original young+hopeful direction with a scared/brave nuance. Erik's Route-1 conversion mechanics (retopo ~1,500–3,000 tris + flat-shade + palette re-texture + Mixamo Generic; 86cak3r3k) are ADOPTED but applied to a NEW base mesh: concept → WEB-Rodin → retopo/low-polyfy → Mixamo → Unity Generic, staged v3 toggle. v2 stays the LIVE default until v3 passes the full soak gate. CLAUDE.md's character line stays describing the LIVE v2 (#261 unchanged); it flips when v3 activates.

## 2026-07-06 — Castaway v3 concept LOCKED (variant E)
Sponsor picked concept E after 2 rounds (5 candidates): man in his mid-30s, light stubble (adult, NOT v2-rugged), lean ordinary-guy build, scared-but-going-anyway expression (wide worried eyes, set mouth), torn-sleeve teal shirt, rope belt, rolled grey-brown trousers, barefoot, A-pose. Locked file: art-src/castaway_v3_concept_apose.png. Round-1 direction note: "torn shirt, but not a boy, a man in his 30s" (rejected the young-boy reads A/B/C). Next: Sponsor runs WEB-Rodin image-to-3D on the locked concept (ticket 86cak41d4).

## 2026-07-06 — Castaway v3 concept RE-LOCKED: variant G supersedes E (expression dialed back)
Sponsor re-judged after locking E: "he looks too scared" → "should be more neutral" → picked G from the F/G/H expression round: mid-30s, light stubble, lean, head-on gentle friendly smile (not scared, not neutral-blank), torn teal shirt, rope belt, rolled trousers, barefoot, A-pose. art-src/castaway_v3_concept_apose.png now = G (E superseded, kept in session scratchpad only). Identity nuance final: the "scared" part of the original brief is carried by ANIMATION/posture, not the sculpt — the face bakes a mild friendly-neutral read.

## 2026-07-06 — Castaway v3 facet density LOCKED: 3.0k tris (QuadriFlow 1509 quads)
Sponsor picked the 3.0k-tri conversion from the facet A/B (raw-Rodin vs 3.0k vs 2.1k): clearly faceted, keeps hands/belt/silhouette, better rig deformation headroom; top of Erik's 1,500-3,000 band. Source of truth: art-src/castaway_v3_lowpoly.blend (raw 8,370-quad Rodin import + the chosen v3_lp_2800tri mesh). Remaining before Mixamo: region-color cleanup (border noise), painted/geometry face features, final palette texture + UV placement.

## 2026-07-06 — Castaway v3 Route-1 conversion GESTALT-REJECTED ("no its terrible" — the whole converted character)
The retopo conversion (QuadriFlow 3.0k + flat semantic regions + geometry face decals) cleared every itemized defect (density Sponsor-picked from A/B, regions cleaned, face positions verified against the sculpt's measured sockets) and still failed the Sponsor's bar as a WHOLE. Per character-pipeline.md's ratified gestalt diagnostic: route switched, not iterated. Work preserved in art-src/castaway_v3_lowpoly.blend for reference. v2 stays LIVE. Next: Sponsor picks among remaining routes (Rodin Smart-Low-poly re-gen probe / Quaternius-class low-poly base customization toward concept G / exaggerated-chunky concept re-draw / pause).

## 2026-07-06 — v3 route after gestalt-fail: Rodin Smart-Low-poly(BETA) re-gen probe
Sponsor picked the probe: re-run locked concept G through Rodin choosing Smart-Low-poly topology at Confirm (instead of Quad-8000). Framed explicitly as a PROBE (Erik grade: unproven, vendor-claimed) — fallbacks remain low-poly-base customization (Quaternius-class, v1 precedent) and exaggerated-chunky concept re-draw. Export lands in a SEPARATE folder (art-src/castaway-v3-rodin-export-lowpoly/); the first Quad-8000 export stays untouched.

## 2026-07-06 — v3 hero route LOCKED: Rodin Smart-Low-poly + HSV-posterized diffuse
The Smart-Low-poly(BETA) probe SUCCEEDED where the retopo conversion gestalt-failed: Rodin re-gen of locked concept G with Smart-Low-poly topology (Quad type, Low density) produced REAL facet geometry at 3,605 quads / 7.4k tri-equiv (~half of Quad-8000's 16.7k), keeping the concept's painted cartoon face. Sponsor picked the HSV-POSTERIZED diffuse treatment (V 5-steps / S 4-steps, hue untouched — flattens cloth/skin shading to steps, keeps the face) over painted-as-is. Erik's 1.5–3k band is superseded by Sponsor verdict at 7.4k. Export: art-src/castaway-v3-rodin-export-lowpoly/ + texture_diffuse_posterized.png. Next: Mixamo auto-rig (A-pose ok) → Unity GENERIC → staged v3 toggle + capture-gate reconcile + held-weapon re-seat.

## 2026-07-06 — Double soak-PASS: v3 identity + #254 weapons; activation ordered
Sponsor (verbatim): "soak v3 identity approved. soak 254 approved lets move on with v3 char and dial in the weapons with that char."
- v3 identity soak (Build\soak-v3\, stamp bb715b1) PASSED -> activation ticket 86cak9kau un-gates (default flip + capture-gate reconcile + ALL-4-weapon re-seat on the v3 hand + finger-curl + Drew's #263 NIT).
- #254 weapons soak (Build\soak-254-v2\, stamp 10b8195) PASSED -> auto-merge labeled (MERGEABLE, sequenced after #263's 11:46Z merge). 86cajkk7h complete on merge; 86cahnmjv closed (thumb defect not reproduced; v3 grip owned by 86cak9kau item 4).
- Weapon "dial-in" on the v3 char = 86cak9kau scope items 3-4, dispatching once #254 lands on main (re-seat then covers all 4 stone weapons).

## 2026-07-06 — v3-live soak PASS (with follow-up): v3 activation ships
Sponsor (verbatim, walkthrough): "pass but weapons etc. need to be dialed in for the new v3 character." #264 auto-merge-labeled; 86cak9kau completes on merge; v3 = the shipped default hero. Follow-up: a weapon DIAL-IN pass on v3 (seat/scale/pose fine-tuning, Sponsor-driven) — per [[sponsor-prefers-direct-tweak-tools-for-fiddly-placement]], route through the F9 nudge handle (fix its broken head-resize keys 86cajuuz0 where still relevant) -> Sponsor dials -> bake. Ticket to be filed this walkthrough round.

## 2026-07-06 — Walkthrough Q2+Q3: #265 Sponsor-browser-merging now; island lane GREEN-LIT
- #265 (weaponset CI gate): Sponsor merging in browser now; orch flips 86caju052 complete on observed merge.
- Island lane: GO — dispatch C2 (86cakk4w8) when the build slot frees post-#264-merge; C2->C3->C4 serial; the v3 weapon dial-in follow-up runs in the non-build lane in parallel.

## 2026-07-06 — Iron progression Q1: MODEL A locked (mine ore + smelter)
Sponsor picked Model A over Erik's D recommendation: full mining chain — ore nodes, pickaxe (new 5th tool type per tier), smelter. Richest survival build, scope L. Consequences: pickaxe extends the Sponsor-locked weapon set (Blender burst needed for wpn_pickaxe_{stone,iron}_01); 86cah7y5b (find-in-world) stays standalone (not absorbed). Q2-Q5 refine the model below.

## 2026-07-06 — Iron progression Q2+Q3: WORK-LED earn feel; NEW forge/furnace structure
Q2: work-led — the crafting/smelting grind is the point (ore findable without heavy exploration; effort = mining volume + fuel + smelt time). Q3: a NEW buildable forge/furnace structure (distinct from the bonfire; extends the survival arc shipwreck->...->furnace->iron; crafting-table-class buildable).

## 2026-07-06 — Iron progression Q4+Q5: PEACEFUL gather/craft; BOTH difficulty dials
Q4: NO combat guard — mining is peaceful; the iron chain ships fully independent of the combat cluster (no hard dep). Q5: BOTH dials exposed — ore rarity + smelt cost (fuel/time/material), per-tier easy/med/hard presets, registered in SettingsCatalog per the existing tweakable pattern.

## 2026-07-06 — Iron progression BONUS: PICKAXE approved as the 5th tool type (both tiers)
wpn_pickaxe_stone_01 + wpn_pickaxe_iron_01 extend the locked weapon family (knapped stone / forged iron recipes per [[weapon-two-tier-style-stone-iron]]); authored via an interactive Sponsor-judged Blender burst (schedulable). IRON DESIGN NOW FULLY LOCKED: Model A (mine ore + smelter) · work-led · new forge/furnace buildable · peaceful (no combat dep) · both difficulty dials (ore rarity + smelt cost) · pickaxe 5th type. Erik's note must be committed to main before/with the citing implementation spec.

## 2026-07-07 — Fable = advisor-only; opus implements everything (supersedes the design-lane fable exception)
- Decided by: Sponsor
- Decision: Fable (the orchestrator session) only analyzes tasks, authors the plan/brief, and gives advisement; ALL implementation — including the creative/Blender/design-build lane — runs on opus agents; an agent that is unsure or hits a plan gap STOPS (commit+push WIP) and asks fable for advisement (`ADVISEMENT NEEDED:` report → SendMessage answer) instead of improvising.
- Why: fable token conservation (Sponsor verbatim 2026-07-07: "stop burning fable tokens and begin using fable only as an advisor…"). Supersedes the 2026-07-03 per-dispatch `model:"fable"` design-lane upgrade.
- Reversibility: reversible (re-enable per-dispatch upgrades on the Sponsor's word)
- Affects: orchestrator dispatch policy, all personas, R&D/creative lane, dispatch-template (new Advisor-escalation block)

## 2026-07-08 — STANDING AUTO-MERGE authorization (supersedes per-PR merge approval for the non-soak class)
- Decided by: Sponsor (popup, 2026-07-08 morning)
- Decision: the orchestrator auto-labels ANY PR for merge — present or away, no per-PR ask — once ALL machine gates are green (required CI SUCCESS + peer APPROVE verdict + Tess QA where the class requires it + Self-Test Report where UX-visible) AND the PR has no soak surface. Soak-surface (feel/visual) PRs still gate on the Sponsor's soak verdict. Workflow-file (.github) PRs remain manual until the scoped-PAT fix (86cafhehe) lands.
- Why: Sponsor verbatim: "look into why i have to do any manual merges, I want to avoid this." Investigation: the label path was already mechanical; only the per-PR-approval policy forced manual steps. The one-click staging class is retired.
- Reversibility: reversible — Sponsor revokes with a word; falls back to one-click staging.
- Affects: orchestrator merge flow, away-queue format (one-click class retired), all personas' merge expectations

## 2026-07-08 — Sponsor NEVER performs git/CLI operations (hard rule; supersedes all "you run this" handoffs)
- Decided by: Sponsor (verbatim, /drain-and-save popup: "I NEVER WANT TO COMMIT, PUSH OR ANYTHING YOU SHOULD DO IT")
- Decision: the Sponsor is never handed git/gh commands to run — no merges, commits, pushes, worktree cleanups, or label commands. The orchestrator/team performs ALL mechanical operations; where the classifier gates an action, the orchestrator obtains the Sponsor's in-context approval via popup and then executes it ITSELF. The Sponsor's role is verdicts and approvals only (soaks, dials, priorities, popup clicks).
- Why: repeated friction handing the Sponsor one-click commands (away-queue one-clicks, fh-261-fold cleanup, the #287 merge suggestion). Pairs with the 2026-07-08 STANDING AUTO-MERGE grant.
- Reversibility: reversible on the Sponsor's word
- Affects: orchestrator merge/cleanup flows, away-queue format (no command handoffs — approval-only items), memory [[explain-why-before-handing-sponsor-commands]]

## 2026-07-08 — Rule clarified: GitHub UI clicks count as commands too (Sponsor scope-check)
- Decided by: Sponsor (verbatim: "clicking in gh should count as command also then")
- Decision: the never-runs-commands rule includes GitHub's web UI — no browser merges, no UI operations. The Sponsor's surface is IN-CHAT ONLY (popups, soak verdicts, priorities) plus physical machine actions the orchestrator's sandbox genuinely cannot perform (e.g. launching the interactive runner window — attempted twice, OS-denied). Consequence: the browser-merge class is RETIRED — even .github workflow-file PRs are merged by the orchestrator via direct `gh pr merge --admin` after in-chat approval (proven live on PR #287, 2026-07-08 16:03Z; the workflow-token wall only constrains the Action's token, not the orchestrator's gh auth). The scoped-PAT ticket 86cafhehe is downgraded to optional (label-path completeness, no longer required for any merge).
- Reversibility: reversible on the Sponsor's word
- Affects: merge flows for .github PRs, away-queue item format, ticket 86cafhehe priority

## 2026-07-18 — Wood-tier weapon set PASSED (Sponsor walkthrough verdict)

- **Decision:** Sponsor PASSED all 5 wood-tier pieces as-is (axe/pickaxe/spear/knife/sword; whittled-wood, existing palette tones, 28-41 tris) from the 13 staged renders in art-src/wood-burst-renders/ — "PASS — export FBXs, integrate" via /sponsor-questions-walkthrough popup.
- **Consequence:** FBXs export from art-src/weapons_reauthor.blend (wood row y=-0.6) to Assets/Art/Props/WeaponPack/ and integration proceeds (ids *_wood already live in #294 catalogs; in-hand seating + verbs remain ②/art-burst scope per the #294 deferred flags).
- **Source:** away-queue item 0b (staged 2026-07-08) → resolved 2026-07-18.

## 2026-07-19 — Crafting redesign wave ①-④ CLOSED on sponsor soak PASS; C build menu is the single build entry point

- **④ chain soak = SPONSOR PASS** (walkthrough popup, "chain works, forge reads right"; soak-crafting-4 @ 75a9725): `86camz9uz` ① (shipped 07-18) · `86camz9v7` ② · `86camz9vh` ③ · `86camz9vq` ④ · ghost-obstruction fix `86catqxm0` — all complete. The Sponsor-locked wood→stone→iron progression from the 2026-07-08 grill is live end-to-end.
- **Sponsor design confirmation (mid-soak verbatim, ticket 86catpvpa comment 90150243183538):** C = build MENU for all placeable structures; the placed crafting TABLE's menu is ITEMS-only (tools/weapons); the interim forge key V retires. Shipped same-day as PR #311 (`IBuildPlaceable`/`BuildMenuUI.RegisterPlaceable` seam — ⑤ campfire and future placeables register rows, never fork a menu).
- **Merge-path policy shift (sponsor verbatim in-walkthrough: "Why do i have to merge anything? you can do it. yes merge now"):** fully-gated workflow-file PRs are orch-DIRECT-merged via `gh pr merge --admin` when the sponsor is present/delegating — the browser-click ritual was classifier-convention only, never token-required for the CLI (#299 `d757c2e`, #308 `fdb81df`, #309 `9a8687b`). Away-mode staging unchanged.
