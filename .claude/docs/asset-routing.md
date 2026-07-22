# Asset-Class Routing Index — Which Pipeline for Which Asset

**Read this FIRST when a task says "model / create / make a new X."** It tells you WHICH of Far Horizon's
asset-creation routes applies to a given asset class, and points you at the one MANDATORY doc for that route.
It does NOT duplicate per-route content — each route's full rules live in its own doc (linked below); this is
the index above them.

Far Horizon has **four** distinct asset-creation routes. Picking the wrong one wastes a whole pass (a
procedural humanoid fights organic shapes; a Blender atlas-textured prop reads foreign beside faceted
geometry; a per-prop Blender model where procedural scatter would do is over-work). Route first, then go
read the route's MANDATORY doc before touching anything.

---

## The routing table

| Asset class | Route | MANDATORY doc | Reach for this when… / Don't use when… |
|---|---|---|---|
| **World / terrain / rocks / water / trees / scattered props** | **Procedural** — `LowPolyMeshes` / `FacetedRock` mesh-gen + URP Shader Graph (`LowPolyVertexColor` / `LowPolyWater`) | [`lowpoly-quality.md`](lowpoly-quality.md) (+ [`unity-conventions.md`](unity-conventions.md) §Asset creation) | **When:** the shape is generatable from code + seed (faceted rock, blob canopy, water plane, terrain, mass-scattered ground props) and benefits from per-instance seeded variation. **Don't when:** the shape is an organic humanoid, or a hero/held object whose silhouette is hand-authored (→ Character or Blender route). |
| **Weapons / tools / hero props** | **Blender / Blender-MCP** — faceted-chunky modeling, shared palette material (`weapon_palette.png` + one URP/Unlit mat), headless FBX export | [`blender-asset-pipeline.md`](blender-asset-pipeline.md) | **When:** a player-held or featured object whose silhouette is hand-authored (axe, knife, sword, spear, crafting table, campfire, chest) and must read crisp in-hand. **Don't when:** it's a mass-scattered background mesh (→ Procedural) or you're tempted to bake a per-asset texture atlas (BANNED — shared palette only). |
| **Characters** | **Hyper3D Rodin → Mixamo → Unity** — Image-to-3D mesh, auto-rig, **Generic** rig in Unity | [`character-pipeline.md`](character-pipeline.md) | **When:** a rigged/animatable humanoid or creature (the castaway, future NPCs/enemies). **Don't when:** it's a static prop (→ Blender/Procedural), or you reach for the Mixamo **Humanoid** rig under a scaled scene hierarchy (it explodes the mesh — use **Generic**). <br>*Clip authoring/repair is done on the Mixamo `mixamorig` deform skeleton in Blender (`bpy`), NOT via a rig swap. Rigify was evaluated 2026-07-21 and declined: it produces no clips, exports poorly to game engines (deform-chain split; manual bake-down), and re-rigging would break the Generic clip-bind + Animator + held-prop seat wiring. See `team/erik-consult/rigify-vs-mixamo-research.md`.* <br>*2nd-enemy CREATURE meshes have a ratified secondary route (C#-editor-baked + procedurally-posed, NO rig — snake/boar precedent, PR #332): see the note directly below the table. Blocky-stylized enemies only — the HERO stays Rodin→Mixamo.* |
| **Action-verb animation** (animating an EXISTING rig, **not** creating geometry) | **Procedural additive-offset** — `CastawayArmPose` `LateUpdate` additive bone-rotation idiom; NO new Animator clip / state / layer / AvatarMask | [`procedural-animation-verbs.md`](procedural-animation-verbs.md) | **When:** authoring a verb (chop / pick-up / drink / throw) or any change to `CastawayArmPose` / `HeldAxeRig` / a held-prop seating driver. **Don't when:** you're creating new geometry or a new mesh (that's one of the three routes above) — this route only MOVES an existing rig. |

Each route's doc is a **MANDATORY pre-work read** for that route's tasks (per CLAUDE.md §Detailed Documentation
+ the "sub-agents Read every `.claude/docs/*.md` before work" rule). Read the route doc fully before starting —
this index is a routing slip, not a substitute for the route's rules.

> **Ratified secondary CREATURE route — C#-editor-baked + procedurally-posed (2nd-enemy meshes; Sponsor-ratified 2026-07-22, DECISIONS 2026-07-22; precedent snake + boar PR #332).** The Characters row routes a rigged creature to Hyper3D Rodin→Mixamo, and the Procedural row rules out organic humanoid/creature silhouettes — **that phrasing stays correct for the HERO character and any ORGANIC-fidelity creature.** But a **blocky-stylized 2nd-enemy creature** (snake, wild boar) has a ratified alternative: **C#-editor-baked geometry (`LowPolyMeshes.*`) + procedural body posing (`*BodyRig` / `*BodyChain`), NO rig, NO FBX** — sidesteps the FBX-helicopter / back-facing-auto-rig class entirely (`character-pipeline.md` §Step 3) and mirrors the shared enemy-AI seam the POC established. Reach for it when the enemy reads fine as faceted blocky geometry and the matchup/HP/AI proof is mesh-agnostic; a Blender-authored or Rodin→Mixamo silhouette stays an **optional swap-ticket** (the swap-hatch is mesh-only — `BoarEnemy`/`BoarAI`/tests reference only the shared combat seams + `transform`; Devon-verified drop-in, PR #332). **Scope guard — CREATURES/enemies only:** this does NOT reopen procedural for the organic-fidelity HERO character (still Rodin→Mixamo per `character-pipeline.md`) or for any humanoid whose silhouette needs organic fidelity. Route on the TARGET STYLE, not the asset class alone (the same discipline as the castaway-v4 refinement below).

---

## The decision rule when procedural fights the style

Mirrors `unity-conventions.md` §Asset creation ("**SOURCE a purpose-built base when the existing mesh fights
the target style**", `86ca8ca1m`):

> If **2+ mesh-edit / procedural attempts** at a style change fail, **STOP editing and switch routes** — source
> or author a base built in the target style rather than grinding more iterations against a route that fights
> the shape. Procedural is first-choice for generatable faceted geometry, but it fights organic humanoid shapes
> (→ Character route) and hand-authored hero silhouettes (→ Blender route). The whole point of having distinct
> routes is to switch instead of forcing one route to do another route's job.

(See `character-pipeline.md` for the same rule from the character side, and `blender-asset-pipeline.md` §3
for "source a purpose-built base" on the hero-prop side.)

> **Sharpened precedent (castaway v2, 2026-07-05):** seven procedural Blender attempts at a hero-character rebuild each failed the Sponsor's bar on GESTALT — the whole didn't land — not on any itemized/fixable defect; switching to the Character route (concept→web-Rodin→Mixamo) succeeded in under an hour. This refines the "2+ attempts fail" rule above: **a GESTALT failure (no punch-list, just "doesn't look right") is a switch-route signal on its own** — when a round clears every itemized defect and still doesn't land, switch immediately rather than running a further round. Full account: `character-pipeline.md`'s route-ratification note.

> **Scope refinement (castaway v4, 2026-07-18):** the gestalt-failure precedent above is scoped to ORGANIC-fidelity hero rebuilds — it does not mean hand/procedural modeling is closed for every character-shaped asset. A deliberately GEOMETRIC/segmented target (chamfered-blocky "wooden toy" style, 40 segmented boxes) passed the Sponsor's look-dev gate first-try via hand-modeled Blender/Blender MCP — a different problem class from the organic-humanoid rebuild that failed seven times (`character-pipeline.md`'s scope-nuance note). It has NOT yet cleared rig/soak, so it does not reopen the Character-route default for organic humanoids. Route on the TARGET STYLE, not the asset class alone: organic/realistic silhouette → stay on Hyper3D Rodin (Character route); geometric/stylized/toy-like silhouette → hand-modeled Blender is a viable candidate route worth considering, not automatically ruled out.

---

## Cross-cutting rules that apply across ALL routes

These are NOT route-specific — they govern any asset regardless of how it was made (do not duplicate the
detail here; the cited doc owns it):

- **Shipped-build capture gate** — anything visually-visible needs evidence from the BUILT exe, not the editor
  (`unity-conventions.md` §Editor-vs-runtime; CLAUDE.md Hard rules). Editor-vs-runtime divergence is a proven
  failure class.
- **Committed-asset staleness** — bootstrap-generated `.unity`/`.mat`/`.asset` files are committed; a build ships
  the committed snapshot, so a fix in regen-code alone never reaches the build unless the asset is regenerated +
  committed ([[unity-procedural-committed-assets-go-stale]]).
- **Inverse of committed-asset staleness: a regen can run correctly and still ship corrupted committed data** if a
  local EditMode test mutates live global engine state (e.g. `RenderSettings.skybox`) without restoring it between
  the bake and the commit — CI self-heals (it always re-bakes before build/tests) so every mechanical gate stays
  green against generator-correct data while the COMMITTED snapshot is wrong; only a reviewer diffing committed
  values against the generator's source constants catches it. Full mechanism + fix: `unity-conventions.md`
  §Headless/CLI rituals (PR #231, ticket `86cahvntg`).
- **Material-honest + pattern-via-geometry** — a surface reads as its material (stone→flint, metal→steel; no
  arbitrary colors); surface pattern is modeled low-poly facets, NOT a detail-texture ([[weapon-asset-material-honest-pattern-via-geometry]]).
- **Single Unity build slot** — any route that ends in a Unity build is serialized to one at a time; fan out the
  non-build work (modeling, research, doc) in parallel ([[single-unity-build-slot-serializes-orchestration]]).
