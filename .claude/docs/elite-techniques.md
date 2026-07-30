# Elite techniques — reach-for-these references

The mandatory-read set already says "Read EVERY `.claude/docs/*.md`" — this file folds Erik's
2026-06-15 Unity-3D-mastery research (`team/erik-consult/unity-3d-mastery-path.md` +
`developer-accuracy-performance-research.md`, which agents do NOT auto-read) into the
guaranteed-read surface. **Pointers, not tutorials** — each entry says when to reach for the
technique + where the authoritative source lives. Read the linked source before implementing;
filing the technique here makes it discoverable, it does not replace the source.

> **maintain-docs append-target:** this doc holds ONLY external references + not-yet-adopted-technique
> pointers (imported-rig grounding, chunk-LOD terrain, URP flat-shading). Hard-won incident findings land in
> `unity-conventions.md`; daily-use guardrails in `unity6-mastery.md`; adoptable mesh/shader patterns in
> `lowpoly-quality.md` — NOT here. When a pointer here graduates into an adopted, incident-proven rule, MOVE it
> to its canonical home and leave a pointer, don't duplicate.

> **Scope note:** this OPERATIONALIZES the knowledge (so every dispatch picks it up). The
> ADOPTION of each technique is its own backlog ticket (`86ca9a340` Diagnose-Before-Fix ·
> `86ca9a36g` PlayMode locomotion-sampling · `86ca9a39q` Configurable Enter Play Mode ·
> `86ca9a3b3` SRP-batcher gate, + the foot-IK / chunk-LOD / shader adopt-tickets). Don't
> implement here; reach for these when the matching pain shows up in a dispatched ticket.

## Grounding imported-rig avatars (the float saga's domain)

- **Ground the avatar ROOT to `terrainHit + K` (a FIXED constant K), NEVER a per-frame
  `BakeMesh` world-Y.** Mixamo / Hyper3D FBX bake a 100× cm→m node scale onto the
  `SkinnedMeshRenderer` transform that `localToWorldMatrix` then DOUBLE-applies — the source
  of the 8-attempt float/sink saga (fixed PR #47, `e1289ef`). Per-frame `BakeMesh`
  sole-chasing DIVERGES under that scale (a transient wrong-scale sole at walk-onset over-corrects
  the snap → the character sinks to `avatarRootY ≈ −68`); a stable terrain-raycast + a fixed
  offset does not.
- **World-space sole/measurement on a scaled skinned mesh → use a unit-scale
  `Matrix4x4.TRS(pos, rot, Vector3.one)`, never `localToWorldMatrix`** (it re-applies the 100× scale → garbage
  sole-Y). *De-duped — the full mechanism (the 100× cm→m double-apply + the walk-float saga's 8 false-greens)
  is owned by `unity-conventions.md` §FBX / rigs / characters (Bug B). Reach there; don't re-explain it here.*
- **Two-bone IK is now ADOPTED — but HAND-ROLLED, not `com.unity.animation.rigging` (86cay4282 round 4, PR #354).**
  The package is still NOT in `Packages/manifest.json` (checked), and the adoption deliberately did not add it.
  The shipped implementation is `Assets/Scripts/Runtime/TwoBoneIkSolver.cs` (a ~120-line pure static analytic
  solve) driven by `CastawayLeftArmHaftIk` (`DefaultExecutionOrder` 110). **The deciding reason is ORDER, and it
  generalizes to every held-prop IK this project will want:** Animation Rigging constraints evaluate inside the
  Animator's `PlayableGraph`, i.e. during `Animator.Update`, which runs BEFORE every MonoBehaviour `LateUpdate` —
  so a constraint cannot see anything produced by the order-50/60/65/100 driver chain, and its own output is
  overwritten by that chain immediately afterwards. Any IK whose TARGET depends on a held prop (a hand on a haft,
  a hand on a bow, a foot on a moving platform posed in LateUpdate) is therefore in the same position. Secondary
  reasons: the additive-`LateUpdate`-offset idiom forbids inserting an Animator layer
  (`procedural-animation-verbs.md`), a pure static is EditMode-testable with no PlayableGraph, and adding a package
  re-triggers cold-cache package resolution on the single self-hosted runner — a documented CI flake class
  (`unity-conventions.md` §Headless, the #103 stale-scene incident). **Reach for Animation Rigging only when the
  target is available BEFORE the Animator ticks** (pure terrain foot-planting is the plausible case); reach for
  the in-tree solver whenever the target comes out of the LateUpdate chain.
  - **Two gotchas the adoption paid for, both measured (`AttackClipPoseDiag` `[left-ik]`/`[left-span]`,
    `TwoBoneIkSolverTests`):**
    - **PASS THE CHAIN'S OWN JOINT AS THE POLE, never a fixed world point.** The bend plane comes from the pole's
      component PERPENDICULAR to the root→target axis, so as a pole nears parallel with that axis the perpendicular
      shrinks and its direction becomes hypersensitive: the plane swings by ~(parallel/perp) × the axis rotation and
      the elbow moves proportionally. Measured: a fixed straight-down pole with a target sweeping through
      straight-down turns a 4 mm target step into a **2.1 cm elbow step** (~5× amplification) — it renders as the
      limb twitching. Using the chain's own mid joint makes the components `a·cos(A)` and `a·sin(A)`, so the factor
      is `cot(A) ≤ 1` for any root angle ≥ 45° — de-amplifying. A named fallback direction is a LAST RESORT (0
      frames of use measured across the shipped clip), not a co-equal option.
    - **"Clamp + blend out on over-reach" can make the whole driver INERT — measure the reach envelope before
      choosing it.** The obvious design (pin at a fixed target, ease the IK out when unreachable) was REFUTED here:
      the target was beyond the arm's 54.0 cm extension on ~64% of judged frames at *every* candidate position, so
      blending out would have handed the pose back to the clip for most of the swing. The shipped strategy instead
      CLAMPS the target into the reachable part of the geometry (segment ∩ shell-sphere) and, where nothing is
      reachable, aims at the nearest point — plus a hard ceiling strictly below full extension so the limb never
      locks straight. Keep the blend-out, but size its falloff so it guards absurd targets rather than normal ones.
  - **Cross-refs:** the chain-order contract + the post-seat IK stage live in `procedural-animation-verbs.md`;
    `unity-conventions.md` §FBX / rigs / characters owns the scaled-skinned-mesh measurement traps.
- **Cross-ref:** `unity-conventions.md` §FBX / rigs / characters (the ground-snap / sole-vs-root
  finding + the intrinsic-height-normalization rule live there).

## Procedural terrain at scale (the big-island + journey-out arc)

- **Sebastian Lague "Procedural Landmass Generation" chunk-LOD architecture** is the reference
  for the big-island redesign and the M-U5+ expansion (`github.com/SebLague/Procedural-Landmass-Generation`).
  Reach for it when the world stops fitting in one hand-built scene — chunked terrain with
  distance-based LOD is how the "BIG and ENDLESS" north-star scales without tanking frame time.
- **Empirical scaling checkpoint (Devon, ticket `86caa9zpp`, PR #226, merged 2026-07-02): a single scaled mesh + the existing low-poly gen + STATIC batching held 60fps @ ~800u in the shipped exe** — static 60.0/59.9 fps, traversal 59.8 fps, NavMesh pathing 192/192, `gainedY=53u` (snow-cap hero mountain climbable end-to-end). **Chunked/streamed terrain (the Lague architecture above) is NOT yet needed at this size.** Bounds: vSync-capped (not an uncapped-GPU number), GPU Resident Drawer NOT tested on this build, and scoped to ~800u — re-measure before the next size tier. Read this as "Lague chunk-LOD not yet justified at ~800u," not "scaling solved." **Reconciliation flag:** the POC used Static Batching, which `unity6-mastery.md` §2's guidance calls INCOMPATIBLE with GPU Resident Drawer — before scaling this into production world-gen, either A/B it against GRD per §2 or re-affirm Static Batching and update §2 accordingly; don't silently carry the combination forward.
- **C4 perf re-measure (Devon, ticket `86cakk4xf`, PR #278, merged 2026-07-07): the #226 checkpoint EXTENDS to the ~1200u fully-populated island** (dense multi-species forest + climbable hero mountain + rock scatter/walls/slabs). Shipped release exe pegged at the 60 fps vSync cap (static + traversal/climb); the dev build UNCAPPED measures 2.05 ms/frame avg = **488 fps ≈ 8× headroom** over the 16.67 ms/60 fps budget. In-view shadow-caster draws held to **35 avg / 42 max** (rock-dominated) because all ~4.18k vegetation renderers are `castShadows:false` — the deliberate first perf lever — keeping the shadow pass at **2.7%** of the CPU frame. No chunk-LOD and no density cut needed at this size. Full trace + bounds: `team/analysis/2026-07-07-island2-c4-perf.md`.

## URP stylized shaders (closes two recurring bug classes)

- **`ddx`/`ddy` fragment-normal flat-shading (Hextant Studios) kills the winding-inversion class by
  construction** — computing the face normal from screen-space derivatives removes the per-vertex-normal /
  triangle-winding dependency, so the coincident-opposite-winding "black shards" class can't recur. Reach for
  it on any new flat-shaded surface. *De-duped — the shippable `_FlatShading` toggle spec (ticket `86caamnjb`)
  is owned by `lowpoly-quality.md` §2 Rec 2. Reach there for the implementation.*
- **keijiro `UnitySkyboxShaders`** — gradient skybox reference (the Zone-D look's sky).
- **Cyanilux** — depth / shoreline / water shader references (the shoreline-foam + waterline
  tuning the world-look pass repeatedly chased).
