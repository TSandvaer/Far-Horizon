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
