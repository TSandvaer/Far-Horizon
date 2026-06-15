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
