# Enemy Body-Level Hit Feedback — `_HitFlash`, Pooled Dust-Puff, Flinch

> **Provenance:** re-verified against `origin/main` commit `993faee` (2026-07-27), read from a fresh
> checkout at `c:/Trunk/PRIVATE/Far-Horizon-erik2-wt`. The first pass was researched from the
> orchestrator's stale `orch/coordination` checkout and drew its flinch verdict from `SnakeAI`/
> `SnakeBodyChain` alone, having never seen the boar. This pass reads `BoarEnemy.cs`, `BoarAI.cs`, and
> `BoarBodyRig.cs` directly and re-confirms every other claim from the current tree. The original two
> "open questions" (whether a `BoarEnemy` class exists; whether the design brief exists) are REMOVED —
> both were artifacts of the stale tree, not real gaps: `BoarEnemy.cs`/`BoarAI.cs`/`BoarBodyRig.cs`/
> `BoarVerifyCapture.cs`/`verify_boar_gate.sh` and `team/uma-ux/combat-cluster-design-brief.md` all exist
> on `origin/main`.

## Question

Priya is filing a body-hit-feedback ticket (it blocks `86caxhfg2`, the enemy-HP pip-row — Sponsor
decision 2026-07-27, `team/STATE.md` line 682) covering three things: a per-enemy `_HitFlash`
material pulse, a flinch/hit-react, and a pooled faceted dust puff (the project's first
`ParticleSystem`). Before the dev dispatch goes out, does `_HitFlash` — inherently per-instance —
actually fit inside the "no `MaterialPropertyBlock` on juice VFX" / GPU Resident Drawer (GRD)
discipline this project has built its whole draw-call model on? What's the right particle-system
shape for the dust puff? Does the flinch owe anything to the `CastawayArmPose`→`HeldAxeRig`
additive-offset chain, or to the castaway's own Animator-clip-based hit-react states?

## Bottom line

**`_HitFlash` should be a per-instance, per-material `_HitFlashTime` float** — driven by
`renderer.material.SetFloat(...)` (auto-instantiated unique Material, **never**
`MaterialPropertyBlock`) — added as a fourth opt-in CBUFFER term on `LowPolyVertexColor.shader`,
alongside the three terms (`_RimIntensity`, `_AOStrength`, `_MeadowPatchAmp`) already shipped there.
**`_HitFlashTime` MUST default to a very-negative value (e.g. `-1000`), never `0`.** It is a
timestamp, not a magnitude, so unlike `_RimIntensity`/`_AOStrength` — which are genuinely inert at a
literal default of `0` — a `_HitFlashTime` default of `0` means every material reads as "hit at
`_Time.y` = 0" and flashes for ~0.08–0.18s the instant the scene loads. The next reader should not
assume `_HitFlash` follows the same default-0 rule as its precedents just because it mirrors their
CBUFFER shape; the very-negative default is the one part of the shape that must NOT be copied
verbatim. This satisfies the letter of the no-MPB rule and matches Uma's own §1.2 prescription
verbatim (a `_HitFlash` float inside the shader's `CBUFFER_START(UnityPerMaterial)` on a per-enemy
material instance, not an MPB, not a post-process Volume pulse). **The dust puff should be a
classic Shuriken `ParticleSystem`** (Mesh render mode, tiny faceted chunk mesh, `Unlit/Particle`-class
material, pooled via `ObjectPool<ParticleSystem>` + `OnParticleSystemStopped`) — `game-juice.md` T3
and Uma's §1.2 both prescribe this shape, and Shuriken particles sit outside the GRD/MPB disqualifier
concern entirely (their own renderer path, not `MeshRenderer`).

**The flinch verdict — now confirmed for BOTH enemies, not extrapolated from one.** Reading
`BoarEnemy.cs`/`BoarAI.cs`/`BoarBodyRig.cs` this pass changes the EVIDENCE, not the CONCLUSION: the
boar is structurally identical to the snake on the one fact that matters — `BoarBodyRig`'s own doc
comment states *"There is NO Animator, NO rig, NO skinned mesh: the parts are plain baked meshes and
the pose IS this LateUpdate,"* and calls itself *"A SIBLING to `SnakeBodyChain` / CastawayArmPose /
HeldAxeRig"* (`BoarBodyRig.cs:13-15`) — the same self-description framing `SnakeBodyChain` uses of
itself, *"a SIBLING idiom to CastawayArmPose/HeldAxeRig"* (`SnakeBodyChain.cs:15-17`). So "enemies have
no Animator" was accidentally correct on the first pass but unsound as reasoned (N=1, boar unseen); it
is now confirmed on N=2 (both current enemy types), by direct read, not inference. The flinch should
be a small procedural perturbation added directly into each enemy's own pose method (`SnakeBodyChain.
LateUpdate` / `BoarBodyRig.LateUpdate`), following the SAME Time.time-anchored NormT idiom both
already use for their tells — concretely, `BoarAI.WindupNormT`/`ChargeNormT` (and `SnakeAI`'s
`TelegraphNormT`/`LungeNormT`) are exactly the right SEAM SHAPE to copy: add a `HitReactNormT`-style
public float, driven by a `Time.time`-stamped `_lastHitAt` set from a `Health.Changed` subscription,
and have the body-rig read it as one more additive term alongside the existing windup/charge terms —
not a new Animator state, because there is no Animator to add one to.

**Reconciliation flag on Uma's §2.5 wording (read this pass, not the first):** her brief calls the
boar's flinch *"the animal analog of the castaway hit-react states (`procedural-animation-verbs.md`)."*
That phrase is worth flagging because the castaway's hit-react is NOT a procedural perturbation — it
is 5 **Mixamo-clip-driven Animator overlay states** (`HitToBody`/`HeadHit`/`BigStomachHit`/
`StomachHit`/`RibHit`, `AnyState→<region>` on a `Hit` trigger + `HitRegion` int;
`procedural-animation-verbs.md` "Per-verb status" table, `86cackb3j`). Read literally, "the animal
analog of X" could be misread as "wire the same Animator-overlay mechanism for the boar." That's not
buildable — the boar/snake have no Animator to add a state to (confirmed this pass). The sound
reading is FEEL-analog, not MECHANISM-analog: the boar's procedural head-toss/recoil should read as
emotionally equivalent to the castaway's clip-driven flinch, achieved via the completely different
(and correct-for-this-rig) additive-perturbation route. Worth a one-line confirmation from Uma before
the dev dispatch locks the approach, but this is a wording-ambiguity flag, not a verdict
contradiction — nothing else in §1.2/§2.5 conflicts with the recommendation here.

## Evidence

- **`.claude/docs/game-juice.md`** (read in full, this session, current tree) — §2 "Hard don'ts":
  *"No `MaterialPropertyBlock` on juice VFX MeshRenderers. It disqualifies the renderer from the GPU
  Resident Drawer instanced path... Use particle systems (their own renderer path) or separate
  material instances. (Particles are exempt — they're not the MPB-disqualified MeshRenderer
  path.)"* §1 T3 prescribes the pooled-particle pattern verbatim: `UnityEngine.Pool.ObjectPool<T>` +
  `OnParticleSystemStopped` return, chunky/faceted/polygonal shapes, ≤12 particles/burst, bursts
  only. §3 confirms particles use a separate `Unlit/Particle` material, not `LowPolyVertexColor`.
  **Strong** — the project's own committed guardrail doc; re-read this session, unchanged from the
  first pass's citation.

- **`team/uma-ux/combat-cluster-design-brief.md`** (read in full THIS pass — the first pass only had
  this second-hand) — §1.2 "Hit-flash on the struck enemy": *"drive it via a `_HitFlash` float inside
  the shader's `CBUFFER_START(UnityPerMaterial)` on a **per-enemy material instance** — NOT a
  `MaterialPropertyBlock`... and NOT a full-screen post-process Volume pulse."* §1.2 also prescribes
  the impact puff (*"a pooled, chunky, warm-palette particle burst at the contact point — ≤12
  particles, pooled via `ObjectPool<T>` + `OnParticleSystemStopped`... on the boar = a small earthy
  dust/impact puff, never red"*) and §2.5 the boar's own flinch (*"the boar needs its own flinch (a
  brief recoil / head-toss) on taking a hit — the animal analog of the castaway hit-react states"*).
  **Strong** (Sponsor-released, Uma-authored spec) for what it prescribes; the §2.5 phrasing is
  flagged above as ambiguous on MECHANISM, not on FEEL-intent.

- **`.claude/docs/unity6-mastery.md`** §2 (read in full, current tree) — the GRD disqualifier list:
  *"MaterialPropertyBlocks on MeshRenderer; `sortingLayerID`/`sortingOrder` set; >128 materials per GO;
  `OnWillRenderObject`/`OnBecameVisible`/`OnBecameInvisible` callbacks; Realtime Enlighten GI; Light
  Probe Proxy Volumes. Keep world props as plain MeshRenderers without these to stay in the
  instanced path."* §4 names the Frame Debugger as the verification tool ("verify GPU Resident Drawer
  merged draw calls"). **Strong** for what it asserts about MPB; it does not itself spell out
  whether a *unique, non-shared* Material instance (no MPB call at all) is penalized differently from
  a *shared* Material — see the external-verification note below (unchanged from the first pass).

- **`docs.unity3d.com/6000.0/.../make-object-compatible-gpu-rendering.html`** (fetched previous
  session; not re-fetched this pass — content is a static manual page, not project state, so
  re-fetching adds nothing) — Unity's own GRD-compatibility manual page lists the disqualifiers
  per-GameObject/Renderer (*"Doesn't use the `MaterialPropertyBlock` API"*, proxy-volume probes,
  realtime GI, non-DOTS-instancing shaders, per-instance callbacks) and does **not** name "owns a
  unique Material instance" as a disqualifier at all. **Strong (official manual)** on the MPB point;
  **the manual does not explicitly resolve the shared-vs-unique-material scope question** — I could
  not find that granularity in the fetched page.
  [Unity Manual — Make a GameObject compatible with the GPU Resident Drawer in URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/make-object-compatible-gpu-rendering.html)

- **Third-party technical write-ups** (WebSearch, not independently re-verified against Unity source;
  unchanged from the first pass) — multiple 2026-era Unity-6 GRD deep-dives (Knights of U,
  gamedevllm.com) state the MPB disqualification is evaluated **per-Renderer** ("that Renderer is
  entirely excluded from GPU Resident Drawer — even changing a single property has this effect"), not
  per shared-Material group. **Moderate** — technical blog posts, not official docs; consistent with
  the official manual's per-GameObject phrasing and with how `BatchRendererGroup` is architected (it
  groups compatible renderers into batches; an incompatible renderer is simply excluded from its
  batch, it doesn't poison siblings that share its material asset). [Boost performance of your game in
  Unity 6 with GPU Resident Drawer](https://theknightsofu.com/boost-performance-of-your-game-in-unity-6-with-gpu-resident-drawer/),
  [GPU Resident Drawer Internals — How Unity 6 Cuts Draw Calls](https://gamedevllm.com/en/unity-6-gpu-resident-drawer-deep-dive-en/).
  **Actionable caveat, carried forward unchanged:** treat "per-renderer, not per-shared-material" as a
  well-supported inference, not a fully-confirmed fact — the dev dispatch should A/B the Frame
  Debugger's merged-draw-call count before/after adding the per-instance `_HitFlash` material (exactly
  the tool `unity6-mastery.md` §4 already names), rather than trust this note alone.

- **`Assets/Shaders/LowPolyVertexColor.shader`** (re-read in full THIS session, current tree) — the
  concrete, already-shipped precedent for exactly this idiom. Three separate opt-in terms —
  `_RimIntensity`/`_RimColor`/`_RimPower` (ticket `86caamnnj`, lines 65-79/160-162/310-323),
  `_AOStrength` (ticket `86caamnra`, lines 80-94/163/298-308), `_MeadowPatchAmp`/`_MeadowLime`/
  `_MeadowDeep` (ticket `86cahhfkc`, lines 95-111/164-166/242-253) — all live inside
  `CBUFFER_START(UnityPerMaterial)` (lines 150-167), all default to 0 so every non-opted-in material is
  byte-identical, and the shader's own comments state the invariant explicitly: *"NO
  MaterialPropertyBlock anywhere, so GPU Resident Drawer eligibility is preserved"* (lines 32-34, re:
  the canopy wind-sway uniform). `_MeadowPatchAmp` is confirmed the third such opt-in-default-0 CBUFFER
  term (not just claimed) — its own comment states *"When `_MeadowPatchAmp` = 0 (the DEFAULT on EVERY
  material) the term is a pure no-op → BYTE-IDENTICAL to before this existed."* `_HitFlashTime`/
  `_HitFlashColor` would be a fourth term of the identical shape. **Strong** — this is the actual file
  the ticket will touch, re-read directly this pass; claim fully re-grounded.

- **`Assets/Scripts/Runtime/Combat/BoarEnemy.cs`** (read in full THIS pass) — a pure data/consumer
  class mirroring `SnakeEnemy`: per-tier HP, a `BoarResistance` weak-to-pierce/slash-resistant tag, and
  a `Gore(Health)` method through the shared `Health.ApplyDamage` seam. **No Animator, no rig
  reference anywhere in the file.** **Strong** — read directly.

- **`Assets/Scripts/Runtime/Combat/BoarAI.cs`** (read in full THIS pass) — the boar's state machine
  (`Wander→Chase→Windup→Charge→Cooldown→Dead`), structurally the direct mirror of `SnakeAI`. Exposes
  `WindupNormT`/`ChargeNormT` as `Time.time`-anchored, `Mathf.Clamp01`-normalized public floats
  (lines 140-145) — the EXACT seam-shape (public NormT float, state-entry-timestamp-anchored) a hit-
  react driver should copy. No Animator reference anywhere in the file; movement is via
  `NavMeshAgent`/planar-transform fallback only. **Strong** — read directly; this is the concrete
  answer to the orchestrator's "check whether `WindupNormT` is the right seam" question — yes, it is
  the established pattern, and a `HitReactNormT` should be built the same way.

- **`Assets/Scripts/Runtime/Combat/BoarBodyRig.cs`** (read in full THIS pass) — poses the boar's 7
  baked-mesh parts (body/head/4 legs/tail) every `LateUpdate` from a terrain-snapped body origin +
  each part's captured HOME transform + an additive verb offset (breathing, gait swing, head-lower on
  windup/charge, charge-lean, tail wag). Its own doc comment: *"A SIBLING to `SnakeBodyChain` /
  CastawayArmPose / HeldAxeRig — it drives ONLY the boar's own part transforms and never touches the
  player's Animator → CastawayArmPose → HeldAxeRig chain. There is NO Animator, NO rig, NO skinned
  mesh: the parts are plain baked meshes and the pose IS this LateUpdate."* This is the DECISIVE file
  the first pass never saw. **Strong** — read directly, not inferred; confirms the flinch verdict for
  the boar independently of the snake, using the SAME additive-offset idiom already coded for
  windup-head-lower/charge-lean (lines 136-142/152-172) — a `HitReactNormT`-driven head-toss/recoil
  term slots in as one more line in this same per-part loop.

- **`Assets/Scripts/Runtime/Combat/SnakeAI.cs`** + **`SnakeBodyChain.cs`** (re-read in full THIS
  session) — confirm the snake side of the same pattern, unchanged from the first pass:
  `SnakeBodyChain`'s doc comment: *"This is a SIBLING idiom to CastawayArmPose/HeldAxeRig — it drives
  ONLY the snake's own segment transforms... There is no Animator here at all: the segments are plain
  baked meshes; the pose IS this LateUpdate"* (lines 14-17). `SnakeAI` uses the identical
  `TelegraphNormT`/`LungeNormT` idiom (lines 140-145). **Strong** — read directly.

- **`.claude/docs/procedural-animation-verbs.md`** (re-read in full THIS session) — TWO points, one
  carried and one new:
  1. (Carried, unchanged) The mandatory `CastawayArmPose`/`HeldAxeRig` additive-offset chain is titled
     and scoped explicitly to the "Castaway Generic Rig," cross-referencing `unity-conventions.md`'s
     note that "body is Y-yaw-only, no tilt/lean exists — a lean/tilt ask is new work, NOT an
     extension of this arm-pose idiom." Nothing in this doc claims jurisdiction over non-player rigs.
     **Strong** — grounds "does not apply to enemies."
  2. (NEW this pass) The "Per-verb status" table (line 45, ticket `86cackb3j`) confirms the castaway's
     OWN hit-react is a DIFFERENT idiom entirely from the arm-offset chain: *"Hit-react (Body / Head /
     BigStomach / Stomach / Rib) — `Hit To Body`/`Head Hit`/`Big Stomach Hit`/`Stomach Hit`/`Rib Hit`
     `.fbx` — YES, 5 hit-react states (`86cackb3j`) — Base-layer overlay states, `AnyState→<region>` on
     the `Hit` trigger, clip selected by the `HitRegion` int... The actual TRIGGERING from
     gameplay/damage systems is not yet wired (the params exist + are controller-test-covered; no
     system sets them yet)."* **Strong** — read directly; this is the "castaway hit-react states" Uma's
     §2.5 references, and it is Animator-clip-based, not a candidate template for a rig with no
     Animator. Also notable: even the CASTAWAY's own hit-react trigger isn't wired to a damage event
     yet — when it is, `Health.Changed` is the obvious hook, the same seam this note proposes for the
     enemy `_HitFlash`/flinch drivers, so one hook pattern would end up serving both sides eventually.

- **`Assets/Scripts/Runtime/Combat/Health.cs`** (re-read, current tree) — `Changed`/`Died` are the ONE
  existing damage-mutation seam (`ApplyDamage`) with a public `event Action<float> Changed` firing on
  every HP change. **Strong** — a ready-made hook for both the `_HitFlash` driver and the new
  `HitReactNormT` timestamp, on both enemy types and (per the note above) potentially the castaway too.

- **`Assets/Scripts/Editor/LowPolyZoneGen.cs`** (spot-read, current tree, unchanged from first pass) —
  confirms the scale the GRD rule actually protects: materials are created ONCE per cache-key and
  assigned via `mr.sharedMaterial = mat` to potentially thousands of scattered instances
  (rocks/canopy/bush/water). **Strong** — grounds the "population size" argument: a handful of enemy
  renderers were never going to join that batched pool regardless of MPB use, because each enemy needs
  independent per-instance timing data no shared-material scheme can carry.

- **`team/erik-consult/elite-techniques.md`** "C4 perf re-measure" (already-committed team doc, cited
  not re-derived) — the ~1200u populated island holds **~4.18k vegetation renderers** in the
  GRD-relevant instanced pool. **Strong** (Devon-authored, PR #278-cited) — the actual order of
  magnitude the no-MPB discipline defends; two enemy types (currently one snake + one boar) are orders
  of magnitude smaller and were never in scope for that batch.

- **`team/DECISIONS.md`** 2026-07-27 boar entry (re-read) — *"2nd-enemy meshes follow the snake's
  C#-editor-baked + procedurally-posed route (no rig — sidesteps the FBX-helicopter class)."*
  **Strong** (Sponsor-ratified decision text), and now DIRECTLY CONFIRMED by reading `BoarBodyRig.cs`
  itself this pass (not merely cited as a plan) — the boar shipped exactly per this decision.

- **Grep verification (this session, current tree):** zero hits for `ParticleSystem` under `Assets/`
  — confirms the "first ParticleSystem in the project" premise still holds. Zero hits for
  `MaterialPropertyBlock` on any Runtime enemy/combat script; the only Runtime MPB users are
  `ForgePlacement.cs`/`CraftingTablePlacement.cs`/`CampfirePlacement.cs` (ghost-placement preview
  highlighting — a one-off UX overlay, not a batched world-prop population, so it sets no
  contradictory precedent for enemies). **Strong** (direct tool output).

## Application to Far Horizon

**`_HitFlash` mechanism — recommend the per-instance-Material route, not MPB, not vertex-color-baked
(unchanged conclusion, now doubly grounded — matches both the shader precedent AND Uma's §1.2 spec
verbatim):**

1. Add a fourth opt-in term to `LowPolyVertexColor.shader`'s existing CBUFFER, same shape as
   `_RimIntensity`/`_AOStrength`/`_MeadowPatchAmp`: `_HitFlashColor` (warm-white, matching the
   existing `_RimColor` warm-white convention — Uma's §1.2 also calls for "a brief sub-1.0 warm-white
   tint pulse (~0.08s, eased out)"), `_HitFlashDuration` (~0.08-0.18s), and `_HitFlashTime` (the
   `_Time.y` timestamp of the last hit; a **very-negative default, never `0`,** so the term is inert
   at rest — see Bottom line for why `0` is unsafe here even though it's the right default for the
   other three terms). Frag adds `finalCol = lerp(finalCol, _HitFlashColor.rgb, saturate(1 -
   (_Time.y - _HitFlashTime) / max(_HitFlashDuration, 0.001)))` right before the return — no fourth
   `_HitFlashIntensity` term is needed, the `saturate(...)` expression alone is already the 0→1 decay
   factor. This is a pure no-op when `_HitFlashTime` is at its very-negative default (`_Time.y -
   _HitFlashTime` is then enormous, so `saturate(1 - huge)` clamps to `0`) — the same "inert-by-default
   = byte-identical" idiom this shader already uses three times, achieved here via a very-negative
   default rather than a `0` default. Keep every channel sub-1.0 per Uma's HDR-clamp rule (§1.2,
   `style-guide-v2.md` §5) so the flash doesn't bloom-blow-out.
2. Wire a small `EnemyHitFlash` MonoBehaviour on each enemy that: caches `GetComponent<Renderer>()` in
   `Awake` (per `unity6-mastery.md` §5), calls `renderer.material` ONCE at init (auto-instantiates the
   unique per-object copy Unity natively supports — **not** `sharedMaterial`, **not**
   `SetPropertyBlock`), subscribes to `Health.Changed` (already-built hook, no new event
   infrastructure), and on a detected HP drop calls `_mat.SetFloat("_HitFlashTime", Time.time)` once.
   Zero per-frame allocation, zero MPB anywhere.
3. **Why this satisfies the discipline rather than dodging it:** `game-juice.md` §2 and Uma's §1.2
   both name a per-enemy material instance as the sanctioned MPB alternative. The cost is that the
   enemy's own renderer sits outside GRD's instanced path; per the `LowPolyZoneGen.cs` evidence above,
   that pool is built for populations in the thousands (~4.18k), not the 1-2 enemies onscreen at once.
   Verify this is truly cost-free with the Frame Debugger (before/after merged-draw-call count) per
   `unity6-mastery.md` §4 — the "negligible cost" claim is strongly-supported-but-not-yet-measured on
   this build until that A/B is run.
4. **Ruled out:** a vertex-color/UV-baked flash (baked channels are static; "time since this specific
   hit" is inherently runtime). **Ruled out for now:** `Graphics.RenderMeshInstanced` with a manual
   per-instance data array (technically GRD-safe but requires abandoning `MeshRenderer` entirely for
   enemies — disproportionate for two enemy types).

**Pooled dust puff — classic Shuriken `ParticleSystem`, not VFX Graph, not hand-rolled (unchanged,
now matched against Uma's §1.2/§2.5 boar-specific "dust-brown, never red" call):**

`game-juice.md` T3 and Uma's §1.2 both prescribe this shape verbatim (pooled via `ObjectPool<T>` +
`OnParticleSystemStopped`, chunky/faceted/polygonal, ≤12 particles/burst, bursts only, warm palette,
separate `Unlit/Particle` material). Concretely: `ParticleSystemRenderer.renderMode = Mesh`, fed a
tiny low-poly chunk mesh (reuse a scaled-down `FacetedRock`-style tri-chunk from `LowPolyMeshes.cs`
rather than authoring a new one), tinted via Start Color range for per-particle variety (mirrors
`lowpoly-quality.md` §2 Rec 7's seeded-variation pattern). On wood = existing wood-chip read; on the
boar = dust-brown per Uma's §1.2/§2.5, never red at any tier (§2.3's kid-safe "no blood, no gore"
rule is absolute across all three difficulty tiers). VFX Graph and a hand-rolled mesh-instance burst
remain ruled out for the same reasons as the first pass (disproportionate for a ≤12-particle burst;
Shuriken already provides lifetime/velocity/size-curve for free). This is precedent-setting — first
`ParticleSystem` in the project (grep-confirmed this pass too) — whatever pooling wrapper gets built
should be reusable by the next juice burst (berry-pop, water-droplet, per `game-juice.md` T3's list).

**Flinch/hit-react — does NOT extend `CastawayArmPose`→`HeldAxeRig`, and does NOT extend the
castaway's clip-driven hit-react states either — confirmed for BOTH current enemy types:**

Neither Castaway idiom applies: the arm-offset chain is explicitly Castaway-Generic-rig-scoped
(`procedural-animation-verbs.md`'s own title/cross-refs), and the castaway's 5 clip-driven hit-react
Animator states (`HitToBody`/`HeadHit`/`BigStomachHit`/`StomachHit`/`RibHit`) require an Animator +
Mixamo clips that neither `SnakeAI`/`SnakeBodyChain` nor `BoarAI`/`BoarBodyRig` have — confirmed by
direct read of all four files this pass, not inferred from one. The flinch should be authored as a
small procedural perturbation added directly into each enemy's own per-frame pose method, using the
SAME seam shape already coded there:

- Add a `Time.time`-anchored `HitReactNormT` public float to `SnakeAI`/`BoarAI`, built exactly like
  `WindupNormT`/`TelegraphNormT` (a `_lastHitAt` timestamp, `Mathf.Clamp01((Time.time - _lastHitAt) /
  hitReactSeconds)`), set from a `Health.Changed` subscription (the same hook the `_HitFlash` driver
  uses — one hit-detection seam, two consumers, plus the castaway's own not-yet-wired hit-react could
  eventually reuse the identical pattern per the `procedural-animation-verbs.md` finding above).
- `SnakeBodyChain.LateUpdate` / `BoarBodyRig.LateUpdate` read `HitReactNormT` and add one more additive
  term to the existing per-part loop — e.g. a brief amplitude bump on the slither/breathe terms, or a
  head-toss/recoil kick on the head part — composed the SAME way `windupEase`/`charge`/`lean` already
  compose in `BoarBodyRig` (lines 136-172) or the telegraph/lunge terms compose in `SnakeBodyChain`.
  No new Animator state, clip, or layer — there is no Animator to add one to, on either enemy.
- Since enemy parts are plain baked-mesh transforms (not `SkinnedMeshRenderer`), `game-juice.md` §2's
  "no squash/stretch on the rig" prohibition — which exists specifically because non-uniform scale
  desyncs the castaway's skin weights and `HeldAxeRig` bone seating — does not mechanically apply the
  same way; a small scale-pulse per part is not ruled out by that reasoning, though it should stay
  inside the calm-tone amplitude cap (`game-juice.md` §0, Uma's §0 tonal anchor) and read as "flinch,"
  never "impact explosion."
- **Reconciliation flag (see Bottom line):** confirm with Uma whether her §2.5 "animal analog of the
  castaway hit-react states" phrasing meant feel-equivalence (this note's reading, and the only
  buildable one) or mechanism-equivalence (not buildable — no Animator on either enemy) before the dev
  dispatch locks the implementation shape.
