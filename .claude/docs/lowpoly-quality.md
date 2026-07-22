# Low-Poly Quality — Procedural-Mesh + URP Shader Guardrails

**MANDATORY pre-work read for all Far Horizon visual/mesh/shader work (props, rocks, trees, water, terrain, hero props).**
This is the concise "adopt these patterns in OUR code" checklist distilled from Erik's R&D note.
Full evidence, citations, and per-file sketches: `team/erik-consult/procedural-shadergraph-quality-research.md` (ticket `86ca8x038`).
Cross-refs: `unity-conventions.md` §Low-poly mesh patterns / §Build stripping & shaders (SRP-Batcher, opaque-water tradeoff) and `unity6-mastery.md` §2 (batching) / §8 (mesh import).

> **maintain-docs append-target:** adoptable procedural-mesh + URP Shader Graph PATTERNS (flat-shading toggles, depth-fade water, Fresnel/rim, vertex-AO, seeded scatter, chamfer highlight, palette quantizers) + the already-correct patterns NOT to regress. A new reusable mesh-gen / shader pattern lands in §2; a shipped-build capture/percept lesson for a physical feature lands in §0. Runtime engine gotchas tied to a specific incident go to `unity-conventions.md` instead.

The Sponsor-approved look is **faceted SMOOTH-shaded low-poly** (chunky polygons, continuous diffuse gradient), warm/lush, with a quality pass (bloom / grading / fog / gradient skybox). These guardrails LEVEL UP that look without changing it.

---

## 0. Anchor in the real-world thing — metrics can't see nonsense

**Before any guardrail below, the feature has to actually BE the thing it represents.** A green metric does not mean the shape is right. Two rules, every physical-world feature (pond, fire, hill, dune, terrain carve, water body, shaped prop):

- **Open with the real-world anchor.** State in one plain sentence what the thing IS — what a person would mechanically call it. "A pond is a HOLE in the ground the player steps DOWN into; water collects in the bottom." The build must satisfy that sentence, not just a numeric / color / byte / seed gate.
- **Verify with a side-profile (silhouette) capture.** Up-vs-down is invisible from the player-eye and top-down angles and obvious side-on. The author eyeballs the side profile against the anchor sentence before the feature goes to QA/Sponsor — every time.
- **Fix the cause, never the symptom.** A fix that contradicts the anchor sentence is a band-aid; rethink or escalate.

**Cautionary case — the pond lift→mound saga (PR #130).** The freshwater pond shipped as a raised MOUND *twice*. The team chased the `-verifyPond` color metric (green on a mound — the metric can't tell a hole from a hill) and "fixed" a water-hidden-under-terrain occlusion bug by LIFTING the water above the ground. Result: a pond on a hill — nonsense, because a pond is a hole. The cause-level fix was to CARVE the pond INTO the terrain so the water sits in the depression; a single side-profile shot would have caught the mound on the first pass. Anchor + silhouette is cheaper than two soak-reject rounds. (Sponsor directive 2026-06-24; the dispatch-template "Real-world anchor + silhouette gate" block is the dispatch-side enforcement.)

---

## 1. Already correct — DO NOT regress

The codebase implements several hard-won patterns correctly. Touching any of these reopens a closed bug:

- **Outward-winding enforcement** on flat-shaded meshes (`FacetedRock`, `CloudBlob`, `FacetedMountain`, `FacetedLandmass` in `LowPolyMeshes.cs`) — keep the `Vector3.Dot(fn, faceCentre) < 0` flip; inward winding → `Cull Back` culls the mesh.
- **Explicit per-face normals** on flat-shaded meshes — do NOT call `RecalculateNormals` on them (it self-smooths; you'd lose the facets).
- **Up-biased foliage normals** (`GrassClump`: `nUp = up*0.85 + outward*0.15`) + **distinct verts per face** for double-sided foliage — removing either reverts the iter-8 dark-shard bug.
- **Per-blob vertex-color value blend** on canopy (`BlobCanopy`, a directional-light proxy, NOT AO).
- **Seeded shape variation per instance** (`FacetedRock`, `MessyHairCap`, `BlobCanopy`, `FacetedMountain` all accept `seed`).
- **SRP-Batcher compliance** — every shader property lives inside `CBUFFER_START(UnityPerMaterial)` (`LowPolyVertexColor.shader` lines 60-66); no live `MaterialPropertyBlock` anywhere in the shipped path.
- **`_FogCap` fog-floor** (teal water at the horizon, `LowPolyVertexColor.shader`) + **seam-kill** (fog colour == `WorldLookPalette.SkyHorizon`, `QualityPassGen.EnableGlobalFog()`) — never drift the fog colour off that single constant.

**Test before you "simplify" any of the above: "could removing this re-open a culled / dark / pink / floating-shard bug?" If unsure, leave it.**

---

## 2. The seven adoptable patterns (ranked impact-vs-effort)

Each maps to a code rec in Erik's note. Tickets are filed for the actionable ones — see §4.

### Rec 1 — `QuantizeFine` for near-neutral props (confirmed BUG, fix authored)
The coarse 12-step palette quantizer (`LowPolyZoneGen.cs Quantize()`) snaps a near-grey warm ramp (R≈G≈B, chroma < 0.10) to `(0.667, 0.583, 0.583)` = R>G=B = an unmistakable **pink cast**. Fix: route any prop with `max(R,G,B) − min(R,G,B) < 0.10` through the 24-step `QuantizeFine`. Already authored + tested on `drew/rocks-sourced` (ticket `86ca8m5zu`); confirm where the fix currently lives and land it. Affects every near-neutral rock, trunk, stump.

### Rec 2 — `_FlatShading` ddx/ddy toggle on `LowPolyVertexColor.shader`
A fragment-shader derivative trick — `normalize(cross(ddy(positionWS), ddx(positionWS)))` — computes the true per-face normal at runtime. Expose as `_FlatShading (Toggle) = 0`; `positionWS` is already in the Varyings (`TEXCOORD1`). When ON, props/rocks get the faceted look WITHOUT unwelded verts or manual outward-winding enforcement (a winding-inverted face is simply culled, never shaded wrong) — it **eliminates the winding-inversion bug CLASS for any prop that opts in**. ~2 ALU ops/fragment; the keyword keeps the off-path free.
- Default OFF → terrain / canopy / water (welded, smooth normals) are unaffected.
- Does NOT remove the need to fix actual inverted windings (a culled face is still invisible) — it removes the *editor-passes-but-shipped-build-shades-dark* danger.
- **Do NOT flat-shade the welded terrain** — the smooth-normal roll IS the Zone-D dune look; flat-shading it reads as a spike polyhedron. Props/rocks only.

### Rec 3 — Depth-fade intersection foam (new `LowPolyWater.shader`)
Sample `SampleSceneDepth` → `LinearEyeDepth`, subtract the fragment's own eye depth, divide by `_FoamDistance` (~1.5u), saturate → a 0→1 mask that lerps toward a near-white warm foam colour where water meets any object (beach, rock, stump, future pier). The biggest single read-quality gap in the current ocean.
- **Hard prerequisite — Transparent queue.** Opaque shaders can't sample the depth texture they're writing; this needs a NEW `LowPolyWater.shader` (fork the existing shader, `"Queue"="Transparent"` + `Blend SrcAlpha OneMinusSrcAlpha` + `ZWrite Off`). Enable `Depth Texture` + `Opaque Texture` in the URP Asset.
- ⚠ **Fog-cap migration risk.** Moving water to Transparent reopens the sea↔sky horizon problem (`unity-conventions.md`: opaque water was the FPS-protecting + fog-composing choice). MITIGATION: port the `_FogCap` fog-floor logic into the new transparent water frag (apply it manually). Verify the teal-at-horizon read with the `-seaDiag` horizon-pixel sampler — never trust the normal attribute or a metric, sample actual water PIXELS.
- ⚠ **Overdraw cost** on the ~600u ocean extent — this is a DELIBERATE later PR with its own perf budget, not a free upgrade. Complements (does not replace) the static baked `FoamEdge` band.

### Rec 4 — Fresnel/rim additive term on `LowPolyVertexColor.shader`
`rim = pow(1 − saturate(dot(normalWS, viewDirWS)), _RimPower)`; `finalCol += _RimColor.rgb * rim * _RimIntensity`. At `_RimPower ~2-3` a soft silhouette highlight; ~6-8 a thin outline. One property block + one line after the fog-cap block.
- `_RimIntensity` defaults to 0 → zero cost on all current materials; per-prop instances opt in.
- It is a FALLBACK for props that never get a Blender pass — NOT an exact substitute for the chamfer plane (Rec 5).

### Rec 5 — Chamfer-highlight geometry in Blender for hero props
The white "caught-sun" edge on the board's axe/pickaxe/sword (`inspiration/2026-06-12_21h08_08.png` etc.) is a DISCRETE bright polygon face on the bevel — geometry, not a shader Fresnel wrap. Author it in Blender MCP: a bevel/chamfer face on the top edge with a distinct material slot carrying `Color(0.92, 0.90, 0.84)`. Authoring-time, per hero prop (axe, campfire, stump, chest). Fresnel (Rec 4) is the cheap fallback; the chamfer is the exact board match.

### Rec 6 — Vertex-color AO alpha baking for rocks/props
Bake Blender geometric AO into vertex-color ALPHA before FBX export; add `_AOStrength (Float) = 0` + `finalCol *= lerp(1.0, IN.color.a, _AOStrength)` (one frag line). Zero runtime cost (baked constant). Default OFF → no regression on terrain/canopy/water (they carry no AO in alpha). Rock/stump instances set `_AOStrength ~0.5` for contact-shadow depth at crevices. Additive to the existing per-facet value step (which is a light proxy, not AO).

### Rec 7 — Seeded rotation + lean/height variation on scatter
The seed-per-instance pattern is the right approach (already in `LowPolyMeshes.cs`). Extend it in `LowPolyZoneGen.cs` scatter loops: a seeded Y-rotation (`go.transform.Rotate(0, rnd.Next(360), 0)`) so rocks never align identically, plus a seeded ±20% height scale + small apex lean on pine trunks. Costs nothing; raises scene diversity.

---

## 3. Explicitly ruled OUT (fight the approved look)

| Tempted to... | Why NOT |
|---|---|
| Toon **hard-band ramp** (Step node on ndotl) | The board is faceted SMOOTH, not cel-shaded. Keep the `ndotl * mainLight + SampleSH` path. |
| **Screen-space outlines** (Sobel depth/normals Renderer Feature) | Expensive at desktop res on a large island; the board's "edge highlight" is a per-face chamfer, not a full-silhouette outline. Reserve for an explicit future decision. |
| **Flat-shade the welded terrain** (unwelded) | The smooth-normal roll IS the Zone-D dune look; flat-shading reads as a spike polyhedron. Props/rocks only. |
| **Transparent water without porting `_FogCap`** | Reopens the sea↔sky teal-at-horizon problem the opaque path was built to solve. |

---

## 4. Filed tickets (Unity work — single build slot, sequenced)

| Ticket | Rec | Scope |
|---|---|---|
| `86caamnhf` | Rec 1 | Apply the `QuantizeFine` fix (confirmed pink-cast bug) |
| `86caamnjb` | Rec 2 | `_FlatShading` ddx/ddy toggle on `LowPolyVertexColor.shader` |
| `86caamnmb` | Rec 3 | Transparent depth-fade `LowPolyWater.shader` (fog-cap migration + `-seaDiag`) |
| `86caamnnj` | Rec 4 | Fresnel/rim additive term on `LowPolyVertexColor.shader` |
| `86caamnra` | Recs 5-7 | Quality-polish backlog: chamfer highlight, vertex-AO bake, seeded rotation |

(See Erik's note for the per-file implementation sketches each ticket references. The Unity build slot is single — these are sequenced, not parallel: run them roughly in the order above, lowest-effort first.)
