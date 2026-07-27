> # ⚠⚠ DRAFT — BASIS PARTIALLY INVALID, DO NOT CITE YET (orchestrator, 2026-07-27)
>
> This note was researched from the **orchestrator checkout** (`orch/coordination`), whose
> `Assets/` and `team/` trees are a STALE pinned snapshot — `git diff --stat origin/main --
> Assets/Scripts/Runtime/Combat/` reports 8 files / ~1018 deletions of drift. The author has no
> Bash and no current worktree, so he could not detect this.
>
> **Verified by the orchestrator against `origin/main` 2026-07-27:**
> - The two "open questions" in the report are ARTIFACTS OF THE STALE TREE, not findings.
>   `Assets/Scripts/Runtime/Combat/BoarEnemy.cs` (plus `BoarAI.cs`, `BoarBodyRig.cs`,
>   `BoarVerifyCapture.cs`, `verify_boar_gate.sh`) DO exist on `origin/main`, and so does
>   `team/uma-ux/combat-cluster-design-brief.md`. Neither is missing.
> - **CONSEQUENCE — the FLINCH verdict is unsound as written.** It concludes "enemies have no
>   Animator" from `SnakeAI`/`SnakeBodyChain` alone. The boar was invisible to this research,
>   so the enemy set behind that conclusion is INCOMPLETE. Re-verify against `BoarEnemy`/
>   `BoarAI`/`BoarBodyRig` before any dev acts on it.
> - The `_HitFlash` and particle-route verdicts are NOT invalidated by this — the shader
>   opt-in-term precedent (`_RimIntensity`, `_AOStrength`) was spot-checked on `origin/main`
>   and holds — but they were still reached from a stale tree and want a re-read.
>
> Re-verification dispatched. Until this banner is removed, treat every claim here as
> UNCONFIRMED and cite nothing from it in a ticket, spec or PR.

# Enemy Body-Level Hit Feedback — `_HitFlash`, Pooled Dust-Puff, Flinch

## Question

Priya is filing a body-hit-feedback ticket (it blocks `86caxhfg2`, the enemy-HP pip-row — Sponsor
decision 2026-07-27, `team/STATE.md` line 682) covering three things: a per-enemy `_HitFlash`
material pulse, a flinch/hit-react, and a pooled faceted dust puff (the project's first
`ParticleSystem`). Before the dev dispatch goes out, does `_HitFlash` — inherently per-instance —
actually fit inside the "no `MaterialPropertyBlock` on juice VFX" / GPU Resident Drawer (GRD)
discipline this project has built its whole draw-call model on? What's the right particle-system
shape for the dust puff? Does the flinch owe anything to the `CastawayArmPose`→`HeldAxeRig`
additive-offset chain?

## Bottom line

**`_HitFlash` should be a per-instance, per-material `_HitFlashTime` float** — driven by
`renderer.material.SetFloat(...)` (auto-instantiated unique Material, **never**
`MaterialPropertyBlock`) — added as a fourth default-0 CBUFFER term on `LowPolyVertexColor.shader`,
exactly mirroring the three terms (`_RimIntensity`, `_AOStrength`, `_MeadowPatchAmp`) already
shipped there. This is not a compromise: it satisfies the letter of the no-MPB rule, and the GRD
cost of pulling a handful of enemy renderers out of the instanced pool is negligible next to the
~4.18k-renderer population the rule actually protects. **The dust puff should be a classic Shuriken
`ParticleSystem`** (Mesh render mode, tiny faceted chunk mesh, `Unlit/Particle`-class material,
pooled via `ObjectPool<ParticleSystem>` + `OnParticleSystemStopped`) — this is what `game-juice.md`
T3 already prescribes verbatim, and Shuriken particles are explicitly OUTSIDE the GRD/MPB
disqualifier concern (they use the ParticleSystemRenderer batching path, not MeshRenderer/GRD).
**The flinch does NOT owe anything to `CastawayArmPose`/`HeldAxeRig`** — that chain is
Animator-clip-based and Castaway-rig-specific; enemies (confirmed: `SnakeAI`/`SnakeBodyChain`) have
no Animator at all and are posed by a plain per-frame `LateUpdate` script — the flinch should be a
small procedural perturbation added directly to that existing pose method, following the SAME
Time.time-anchored-phase idiom `SnakeAI`'s Telegraph/Lunge already use, not a new Animator state.

## Evidence

- **`.claude/docs/game-juice.md`** (read in full, this session) — §2 "Hard don'ts": *"No
  `MaterialPropertyBlock` on juice VFX MeshRenderers. It disqualifies the renderer from the GPU
  Resident Drawer instanced path... Use particle systems (their own renderer path) or separate
  material instances. (Particles are exempt — they're not the MPB-disqualified MeshRenderer
  path.)"* §1 T3 prescribes the pooled-particle pattern verbatim: `UnityEngine.Pool.ObjectPool<T>` +
  `OnParticleSystemStopped` return, chunky/faceted/polygonal shapes, ≤12 particles/burst, bursts
  only. §3 confirms particles use a separate `Unlit/Particle` material, not `LowPolyVertexColor`.
  **Strong** — this is the project's own committed guardrail doc, and it already names "separate
  material instances" as the sanctioned alternative to MPB, in so many words.

- **`.claude/docs/unity6-mastery.md`** §2 (read in full) — the GRD disqualifier list: *"MaterialPropertyBlocks
  on MeshRenderer; `sortingLayerID`/`sortingOrder` set; >128 materials per GO;
  `OnWillRenderObject`/`OnBecameVisible`/`OnBecameInvisible` callbacks; Realtime Enlighten GI; Light
  Probe Proxy Volumes. Keep world props as plain MeshRenderers without these to stay in the
  instanced path." §4 names the Frame Debugger as the verification tool ("verify GPU Resident Drawer
  merged draw calls"). **Strong** for what it asserts about MPB; it does not itself spell out
  whether a *unique, non-shared* Material instance (no MPB call at all) is penalized differently from
  a *shared* Material — see the external-verification note below.

- **`docs.unity3d.com/6000.0/.../make-object-compatible-gpu-rendering.html`** (fetched this session)
  — Unity's own GRD-compatibility manual page lists the disqualifiers per-GameObject/Renderer
  (*"Doesn't use the `MaterialPropertyBlock` API"*, proxy-volume probes, realtime GI, non-DOTS-instancing
  shaders, per-instance callbacks) and does **not** name "owns a unique Material instance" as a
  disqualifier at all. **Strong (official manual)** on the MPB point; **the manual does not
  explicitly resolve the shared-vs-unique-material scope question** — I could not find that
  granularity in the fetched page.
  [Unity Manual — Make a GameObject compatible with the GPU Resident Drawer in URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/make-object-compatible-gpu-rendering.html)

- **Third-party technical write-ups** (WebSearch, not independently re-verified against Unity source)
  — multiple 2026-era Unity-6 GRD deep-dives (Knights of U, gamedevllm.com) state the MPB
  disqualification is evaluated **per-Renderer** ("that Renderer is entirely excluded from GPU
  Resident Drawer — even changing a single property has this effect"), not per shared-Material
  group. **Moderate** — technical blog posts, not official docs; consistent with the official
  manual's per-GameObject phrasing and with how `BatchRendererGroup` is architected (it groups
  compatible renderers into batches; an incompatible renderer is simply excluded from its batch,
  it doesn't poison siblings that share its material asset). [Boost performance of your game in
  Unity 6 with GPU Resident Drawer](https://theknightsofu.com/boost-performance-of-your-game-in-unity-6-with-gpu-resident-drawer/),
  [GPU Resident Drawer Internals — How Unity 6 Cuts Draw Calls](https://gamedevllm.com/en/unity-6-gpu-resident-drawer-deep-dive-en/).
  **Actionable caveat:** treat "per-renderer, not per-shared-material" as a well-supported inference,
  not a fully-confirmed fact — the dev dispatch should A/B the Frame Debugger's merged-draw-call
  count before/after adding the per-instance `_HitFlash` material (exactly the tool `unity6-mastery.md`
  §4 already names), rather than trust this note alone.

- **`Assets/Shaders/LowPolyVertexColor.shader`** (read in full, this session) — the concrete,
  already-shipped precedent for exactly this idiom. Three separate opt-in terms —
  `_RimIntensity`/`_RimColor`/`_RimPower` (ticket `86caamnnj`), `_AOStrength` (ticket `86caamnra`),
  `_MeadowPatchAmp`/`_MeadowLime`/`_MeadowDeep` (ticket `86cahhfkc`) — all live inside
  `CBUFFER_START(UnityPerMaterial)` (lines 150-167), all default to 0 so every non-opted-in material
  is byte-identical, and the shader's own comments state the invariant explicitly: *"NO
  MaterialPropertyBlock anywhere, so GPU Resident Drawer eligibility is preserved"* (lines 32-34, re:
  the canopy wind-sway uniform). `_HitFlashTime`/`_HitFlashColor` would be a fourth term of the
  identical shape. **Strong** — this is the actual file the ticket will touch, read directly.

- **`Assets/Scripts/Editor/LowPolyZoneGen.cs`** (grepped + spot-read, this session) — confirms the
  scale the GRD rule actually protects: materials are created ONCE per cache-key (`new
  Material(vc) { name = key }`, e.g. lines 1370/1824/1845/1886/1921/1999/2043/2110) and assigned via
  `mr.sharedMaterial = mat` to potentially thousands of scattered instances (rocks/canopy/bush/water).
  **Strong** — grounds the "population size" argument: this is the many-thousand-instance pool GRD
  batches; a handful of enemy renderers were never going to join it regardless of MPB use, because
  each enemy needs independent per-instance timing data no shared-material scheme can carry.

- **`team/erik-consult/` cross-ref via `elite-techniques.md`** "C4 perf re-measure" (already-committed
  team doc, cited not re-derived) — the ~1200u populated island holds **~4.18k vegetation renderers**
  in the GRD-relevant instanced pool, with shadow-casting-off as "the deliberate first perf lever."
  **Strong** (Devon-authored, PR #278-cited, already on `main`) — this is the actual order of
  magnitude the no-MPB discipline defends. Enemy count (currently 1 snake type, a boar referenced but
  its class not found in `Assets/` at read-time — see caveat below) is orders of magnitude smaller.

- **`Assets/Scripts/Runtime/Combat/Health.cs`** (read in full) — `Changed`/`Died` are the ONE existing
  damage-mutation seam (`ApplyDamage`, lines 146-161) with a public `event Action<float> Changed`
  that fires on every HP change. **Strong** — this is a ready-made, already-built hook: a `_HitFlash`
  driver can subscribe to its OWN `Health.Changed` and compare against the previous value (a drop =
  a hit), needing zero new SO event-channel plumbing.

- **`Assets/Scripts/Runtime/Combat/SnakeAI.cs`** + **`SnakeBodyChain.cs`** (read in full) — confirm
  enemies have **no Animator**. `SnakeBodyChain`'s own doc comment: *"This is a SIBLING idiom to
  CastawayArmPose/HeldAxeRig — it drives ONLY the snake's own segment transforms... There is no
  Animator here at all: the segments are plain baked meshes; the pose IS this LateUpdate"* (lines
  14-17). `SnakeAI` already uses the exact Time.time-anchored NormT-phase idiom
  (`TelegraphNormT`/`LungeNormT`, lines 140-145) that `procedural-animation-verbs.md` prescribes for
  the castaway's verb drivers — it's the same *pattern*, applied to a *different* (non-Animator)
  posing mechanism. **Strong** — read directly, not inferred.

- **`team/DECISIONS.md`** 2026-07-27 boar entry (read) — *"2nd-enemy meshes follow the snake's
  C#-editor-baked + procedurally-posed route (no rig — sidesteps the FBX-helicopter class)."*
  **Strong** (it's the Sponsor-ratified decision text) for "the next enemy will also be non-Animator"
  — **but I could not find a `BoarEnemy`-, `Boar`-, or similarly-named class anywhere under
  `Assets/Scripts` at read-time** (targeted `\bboar\b` grep across the whole repo hits only team docs;
  the earlier broad `-i` "boar" grep hits were false positives on "onboard"/"keyboard" substrings).
  **Flagging as an open question, not asserting an answer** — either the boar's code lives on an
  unmerged branch/worktree I can't see from here, or it's folded into the generic `SnakeEnemy`/`SnakeAI`
  classes as data rather than a distinct type. Whoever picks up the dispatch should verify current
  `main` state before assuming a `BoarEnemy` class exists to attach a flinch to.

- **`.claude/docs/procedural-animation-verbs.md`** (read in full) — the mandatory chain
  (`Animator → CastawayArmPose[50] → HeldAxeRig[100]`) is titled and scoped explicitly to the
  "Castaway Generic Rig" and cross-references `unity-conventions.md`'s note that "body is Y-yaw-only,
  no tilt/lean exists — a lean/tilt ask is new work, NOT an extension of this arm-pose idiom." There
  is nothing in this doc that claims jurisdiction over non-player rigs. **Strong** (read directly) —
  grounds the "does not apply to enemies" verdict.

- **Grep verification (this session):** zero hits for `ParticleSystem` under `Assets/` — confirms the
  "first ParticleSystem in the project" premise. Zero hits for `MaterialPropertyBlock` on any
  Runtime enemy/combat script; the only Runtime MPB users found are `ForgePlacement.cs` /
  `CraftingTablePlacement.cs` / `CampfirePlacement.cs` (ghost-placement preview highlighting — a UX
  overlay on a single, one-off placement-ghost object, not a batched world-prop population, so it
  doesn't set a contradictory precedent for enemies). **Strong** (direct tool output).

## Application to Far Horizon

**`_HitFlash` mechanism — recommend the per-instance-Material route, not MPB, not vertex-color-baked:**

1. Add a fourth opt-in term to `LowPolyVertexColor.shader`'s existing CBUFFER, same shape as
   `_RimIntensity`/`_AOStrength`/`_MeadowPatchAmp`: `_HitFlashColor` (warm-white, matching the
   existing `_RimColor` warm-white convention), `_HitFlashDuration` (~0.12-0.18s — short, a "flinch,"
   not a strobe), and `_HitFlashTime` (the `Time.y` timestamp of the last hit; a very-negative default
   so the term is inert at rest). Frag adds
   `finalCol = lerp(finalCol, _HitFlashColor.rgb, saturate(1 - (_Time.y - _HitFlashTime) / max(_HitFlashDuration, 0.001)) * _HitFlashIntensity)`
   right before the return — a pure no-op when `_HitFlashTime` is in the deep past, exactly the
   established "default-0-or-inert = byte-identical" idiom this shader already uses three times.
2. Wire a small `EnemyHitFlash` MonoBehaviour on each enemy that: caches `GetComponent<Renderer>()`
   in `Awake` (per `unity6-mastery.md` §5), calls `renderer.material` ONCE at init (this auto-instantiates
   the unique per-object copy Unity already supports natively — **not** `sharedMaterial`, **not**
   `SetPropertyBlock`), subscribes to `Health.Changed` (already-built hook, no new event
   infrastructure), and on a detected HP drop calls `_mat.SetFloat("_HitFlashTime", Time.time)` once.
   Zero per-frame allocation, zero MPB anywhere.
3. **Why this satisfies the discipline rather than dodging it:** `game-juice.md` §2 itself names
   "separate material instances" as the sanctioned MPB alternative. The cost is that the enemy's
   renderer (and only that renderer — a per-instance Material, not a shared one, so it was never
   going to ride in the world's batched pool anyway) sits outside GRD's instanced path; per the
   `LowPolyZoneGen.cs` evidence above, that pool is built for populations in the thousands (the
   ~4.18k-renderer figure from `elite-techniques.md`), not the handful of enemies onscreen at once.
   Losing GRD eligibility on 1-5 renderers that each independently need a live per-instance timestamp
   is not a batching regression in any measurable sense — those renderers could never have shared a
   batch with each other in the first place (their `_HitFlashTime` values necessarily differ). Verify
   this is truly cost-free with the Frame Debugger (before/after merged-draw-call count) per
   `unity6-mastery.md` §4 — I could not find Unity's manual spelling out the shared-vs-unique-material
   scope distinction explicitly, so treat the "negligible cost" claim as strongly-supported-but-not-yet
   measured-on-this-build until that A/B is run.
4. **Ruled out:** a vertex-color/UV-baked flash — baked channels are static (authored at mesh-gen
   time); "time since this specific hit" is inherently a runtime, per-event value no baked channel can
   carry. **Ruled out for now:** `Graphics.RenderMeshInstanced` with a manual per-instance data array —
   technically GRD-safe but requires bypassing the normal MeshRenderer/prefab pipeline entirely
   (enemies would need to stop being `MeshRenderer` GameObjects); disproportionate complexity for a
   handful of enemies. Revisit only if enemy density ever scales into a swarm mechanic.

**Pooled dust puff — classic Shuriken `ParticleSystem`, not VFX Graph, not hand-rolled:**

`game-juice.md` T3 already prescribes this shape verbatim (pooled via `ObjectPool<T>` +
`OnParticleSystemStopped`, chunky/faceted/polygonal, ≤12 particles/burst, bursts only, warm palette,
separate `Unlit/Particle` material). Concretely: `ParticleSystemRenderer.renderMode = Mesh`, fed a
tiny low-poly chunk mesh (reuse a scaled-down `FacetedRock`-style tri-chunk from `LowPolyMeshes.cs`
rather than authoring a new one), tinted via Start Color range across the warm palette for
per-particle variety (mirrors the seeded-variation pattern `lowpoly-quality.md` §2 Rec 7 already
endorses for scatter). VFX Graph is ruled out here: it's a heavier, compute-shader-driven authoring
tool that doesn't slot into the codebase's established `ObjectPool<T>` MonoBehaviour-pooling idiom,
and is disproportionate for a ≤12-particle burst — reserve it for a future higher-density VFX need.
A hand-rolled mesh-instance burst is ruled out too: it would reinvent lifetime/velocity/size-curve
features Shuriken already provides for free, for no batching benefit (per the note above, particle
systems are already outside the GRD/MeshRenderer path regardless). This is precedent-setting (first
`ParticleSystem` in the project, confirmed via grep) — whatever pooling wrapper gets built here
should be named/structured so the NEXT juice burst (berry-pop, water-droplet, per `game-juice.md`
T3's list) reuses it rather than each verb inventing its own pool.

**Flinch/hit-react — does NOT extend the `CastawayArmPose`→`HeldAxeRig` idiom:**

That chain is explicitly Castaway-Generic-rig-scoped (`procedural-animation-verbs.md`'s own title and
cross-refs). Enemies are not that rig, and — confirmed by reading `SnakeAI.cs`/`SnakeBodyChain.cs` —
have no Animator at all; `SnakeBodyChain` is a plain per-frame `LateUpdate` procedural pose ("There
is no Animator here at all: the segments are plain baked meshes; the pose IS this LateUpdate"),
explicitly self-described as "A SIBLING idiom to CastawayArmPose/HeldAxeRig," not an instance of it.
The flinch should be authored as a small procedural perturbation added directly into that same
per-frame pose method — e.g., a brief amplitude bump on the existing slither/idle-sway terms, or a
short head/segment kick — keyed by a `Time.time`-anchored phase exactly like `TelegraphNormT`/
`LungeNormT` already are, driven off the same `Health.Changed` hook proposed for `_HitFlash` above
(one hit-detection seam, two consumers). No new Animator state, clip, or layer is needed because
there is no Animator to add one to. Since enemy body segments are NOT skinned meshes (they're
discrete baked-mesh transforms posed by script, unlike the castaway's `SkinnedMeshRenderer`), the
"no squash/stretch on the rig" prohibition in `game-juice.md` §2 — which exists specifically because
non-uniform scale desyncs the castaway's skin weights and `HeldAxeRig` bone seating — does not
mechanically apply to enemy segments the same way; a small scale-pulse per segment is not ruled out
by the same reasoning, though it should still stay inside the calm-tone amplitude cap (§0) and read
as "flinch," not "impact explosion." **Open item:** confirm on current `main` whether a `BoarEnemy`
(or equivalent 2nd-enemy) class actually exists yet before assuming this applies to two enemy types —
at read-time only `SnakeEnemy`/`SnakeAI` were found in `Assets/Scripts`.
