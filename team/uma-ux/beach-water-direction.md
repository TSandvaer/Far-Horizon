# Beach Water Direction — finishing the shore scene with stylized ocean

**Sponsor directive (2026-06-13, verbatim):** *"finish the scene, i want water at the beach."*
**Owner:** Uma · **Reviewer:** Tess (consistency vs board + HDR-clamp pins) · **Status:** DIRECTION — docs only, no implementation here.
**Source of truth:** [`inspiration/`](../../inspiration/) board v2 (esp. `21h16_52` lake-cabin, `21h16_13` mountain-valley river, `21h13_31` world-feel) + [`.claude/docs/art-direction.md`](../../.claude/docs/art-direction.md) + [`team/uma-ux/style-guide-v2.md`](style-guide-v2.md). **Look at the PNGs before implementing — this doc is the translation, the images are ground truth.**

---

## 0. The tonal anchor (read this first)

**The castaway washed ashore — so the sea has to be the thing at his back, the place he came FROM, and the first edge of the big world he's now inside.** Right now the shore scene is a castaway with no coast: flat warm ground, blob trees, no water (soak captures `capture_00..05`, stamp `2026-06-13T08:23`). That reads as "a clearing," not "a beach." Adding water doesn't just decorate the scene — it *completes the premise*. The water is the horizon-ward edge that makes the inland journey legible: turn one way and there's the endless calm sea you survived; turn the other and there's the warm wooded land you're about to walk into. That contrast IS the north-star ("small hopeful player, big alive world, a journey toward the horizon") made visible in one frame.

**Feel target:** *calm, bright, inviting — not menacing.* This is a hopeful castaway story, not a shipwreck-horror one. The sea is a friendly toy-bright teal that catches the sun, gently lapping a warm-golden shore. No churning surf, no dark deep, no cold. If a water beat makes the scene feel cold, dangerous, or slick-realistic, it's wrong even if it's technically prettier — cut it. **Toy-warm and cheerful is the gate** (carries the style-guide-v2 §0 anchor verbatim).

**Same-material rule:** the water must read as carved from the same faceted, saturated, soft-lit toy material as the trees, terrain, and axe. A realistic reflective shader-ocean under a chunky-cartoon castaway breaks the toy exactly the way a realistic terrain would. The board proves this — `21h16_52`'s lake is a flat bright teal plane with a faceted polygonal shoreline, not a Gerstner-wave ocean.

---

## 1. The stylized low-poly OCEAN — the look

### What the board shows (ground truth)
- **`21h16_52` (lake-cabin):** the canonical water read. A **flat, bright saturated-teal plane**, slightly glossy (catches a soft sky sheen, no mirror reflections), meeting the land along a **faceted polygonal shoreline** — the land's own triangles define the coast; the water is a simple plane sitting just below them. Pine islands poke through. This is the look.
- **`21h16_13` (valley river):** the same material as a **bright-blue faceted ribbon** winding through the valley — confirms water is always flat, bright, low-detail, toy-like, never a realistic shader surface.
- **`21h13_31` (world-feel):** no water, but pins the warm-bright daylight + soft sky the water must sit under and reflect.

### The recommended look — **flat gradient teal plane, gently animated, smooth-shaded (not hard-faceted)**

| Decision | Call | Why |
|---|---|---|
| **Flat plane vs animated** | **Flat plane + a VERY gentle large-wavelength bob** (a slow vertical sine on the plane's verts, amplitude ≈ 0.04–0.08u, period ≈ 4–6s). NOT scrolling normal-map waves. | A dead-still plane reads as ice/glass and kills the "alive world" feel; a big slow swell reads "calm sea, alive, friendly" — exactly the tonal anchor. Keep it subtle: this is a breath, not surf. |
| **Faceted vs smooth** | **Smooth-shaded** (welded grid + averaged normals, the ~60° character idiom — NOT the terrain's hard 0° facets). | Water is the ONE world surface that should read smooth, not faceted — `21h16_52`'s water is a smooth bright sheet against the *land's* facets. The contrast (smooth water / faceted shore) is what makes the coast pop. Per `unity-conventions.md` §Low-poly mesh patterns: welded verts + `RecalculateNormals`. |
| **Color** | **Gradient teal → deeper teal-blue with distance** (near-shore brighter/lighter, seaward slightly deeper). Bake the gradient into **vertex color** through the existing `FarHorizon/LowPolyVertexColor` shader, exactly as the canopy/terrain do. | Vivid, warm-leaning teal = saturated-but-warm (style-guide-v2 §6). The near→far gradient gives depth cheaply and points the eye toward the horizon. Riding the existing vertex-color shader avoids a second water shader (see §4) and the asset-churn / `AlwaysIncludedShaders` traps it already solves. |
| **Gloss** | **Moderate** — `_Smoothness` ≈ 0.55–0.65, `_Metallic` 0 (the current `MakeWaterMaterial` ships 0.88, too mirror-glossy for the toy read; pull it DOWN). | Water should *catch* the warm sky, not mirror it. High gloss reads slick-realistic and breaks the toy; a soft sheen reads "bright friendly sea." |

### Color anchors (sub-1.0 every channel — HDR-clamp discipline, carries from style-guide-v2 §6)

> **HARD RULE (style-guide-v2 §5, art-direction.md carry):** every channel < 1.0. Saturated ≠ blown-out. The Zone-D post stack (bloom + warm grade + postExposure) compounds bright values into a wash — the pale-shore first-frame incident (`LowPolyZoneGen.cs` line 31–35) is the proof. Water teal must stay sub-1.0 AND survive the post stack without blooming to white.

| Surface | Color | RGB (0–1) | Notes |
|---|---|---|---|
| Water — near-shore (bright) | bright shallow teal `#3FA6B0` | 0.25, 0.65, 0.69 | warm-leaning teal, sunlit shallows; the eye-catching value |
| Water — seaward (deeper) | deep teal-blue `#2E7E96` | 0.18, 0.49, 0.59 | gradient target with distance; cooler/deeper but still sub-0.6 |
| Water — sky-sheen highlight | pale warm cyan `#BFE6E0` | 0.75, 0.90, 0.88 | the gentle gloss catch; sub-1.0 so post doesn't blow it to white |
| Wet/damp shore band | damp sand `#9A8056` | 0.60, 0.50, 0.34 | **already exists** as `SandDamp` in `LowPolyZoneGen.cs` — reuse verbatim, the transition surface |
| Foam edge line | warm off-white `#E8E2D0` | 0.91, 0.89, 0.82 | sub-1.0 foam (NOT pure white `1,1,1` — would bloom); see §2 |

> These are direction anchors derived from the PNGs by eye — the QA-pin baseline for Tess. Tune in-build against shipped captures (post stack will shift them); treat as starting values, not locks. The teal is warm-leaning (green-biased, not a cold navy) so it belongs to the warm-cohesive palette per style-guide-v2 §6.

### How "chunky cartoon low-poly" reads for water specifically
The water's chunkiness comes from **the shoreline geometry and the color blocking, not from faceting the water surface.** The land's faceted triangles cut the coast into big honest planes; the water is a clean bright sheet that those facets sit in. The "low-poly" read is: few big color zones (bright shallows, deep seaward, foam line), a smooth slow swell, zero high-frequency detail (no ripples, no caustics, no foam particles). It's the *simplicity* that makes it toy-like — same principle as the blob canopy (a few flat greens, not a leaf texture).

---

## 2. SHORELINE / sand-to-water transition

**The shore is where the whole "washed ashore" story lives — get this band right and the premise sells itself.**

### Where water meets beach
The existing terrain already ramps **damp-sand → dry-sand → grass** by height+Z (`LowPolyZoneGen.cs` `VertexColorForZ`, with `SandDamp`/`SandLo`/`SandHi`/grass). The water plane sits just seaward of the damp-sand band, at a Y slightly below the lowest sand verts so the land's faceted edge dips INTO the water (no hard seam — the coast is defined by where the sloping beach mesh passes below the water plane's Y). **Reuse the existing sand ramp verbatim** — the wet `SandDamp` band IS the shoreline transition surface; we're adding the water below it, not re-authoring the sand.

### Foam / edge treatment — **simple stylized, geometry not particles**
- **A thin foam ribbon** along the waterline: a low strip of the warm off-white foam color (`#E8E2D0`, sub-1.0) baked as a **vertex-color band** on the seaward-most rows of the *beach mesh* (where it meets the water), OR as a narrow separate strip mesh tracking the coast. Recommend baking it into the beach mesh's vertex colors — no new object, rides the existing terrain shader.
- **Gentle alpha/width pulse (optional, defer):** the foam ribbon could subtly widen/narrow with the water's slow swell to suggest lapping. Flag as **optional polish** — the static foam band already reads "shore." Don't block the core deliverable on it.
- **NO foam particles, NO spray, NO animated surf texture.** That's realistic-ocean vocabulary; it breaks the toy. The board shows a clean faceted coast (`21h16_52`) — a single calm foam line is the entire treatment.

### How the castaway's spawn reads as "washed ashore"
The spawn narrative is **Sponsor-locked** (`MovementCameraScene.cs` line 677–684: "the castaway still washes ashore — spawn near the shore"). Today the spawn sits at Z+6 on flat ground with the shore/water "just behind" but OFF-CAMERA (the orbit camera frames inland). The water-completion must make that backstory *visible*:
- **Spawn stays at the damp-sand → grass edge** (Z+6, unchanged — it's locked and on-NavMesh). The water goes in just seaward (behind/below the spawn).
- **Orbit-camera default framing should let the player SEE the sea by orbiting toward it.** The inland default view is correct for the loop (trees/craft/fire), but the player must be able to orbit ~180° and find the bright sea + their landing point behind them. **This is a framing check, not a spawn move** — verify in soak that an orbit-to-seaward shows water filling the frame.
- **Optional landing read (see §3):** a short drag-furrow in the wet sand + scattered debris at the spawn point sells "I just crawled out of that water." Flag optional.

---

## 3. Scene-completion beats — what makes the shore read as a FINISHED beach

Ordered by how much each reinforces the tonal anchor. Everything past the first beat is flagged optional — **decoration serves the anchor; if a beat doesn't reinforce "calm bright sea, hopeful landing," cut it.**

1. **The ocean + foam line + visible-on-orbit framing** (§1–2) — **core, not optional.** This is the directive. Without it the scene isn't finished.
2. **Horizon framing toward the "far horizon" north-star** — **strongly recommended.** The sea should extend to a clean bright horizon where it meets the (re-tuned, brighter) gradient skybox. Lean on the existing distance fog (style-guide-v2 §4: "fog only for far-horizon depth") so the sea fades into a soft warm haze at the limit — that haze IS the far horizon, the destination the whole game points at. The water plane must extend far enough seaward that its edge is lost in fog, never a visible plane-edge (today's plane is `scale.z = 4` ≈ 40u deep and tucked at `shoreZ-14` — **too small and too near; it would show a hard far edge.** Extend it well past the fog distance).
3. **A couple of small faceted rocks / a tiny islet in the shallows** — **optional polish.** `21h16_52` has pine islands; a single small rock cluster breaking the water surface adds depth-cue and toy-charm cheaply (reuse the existing scatter rock prop). Keep it sparse — purposeful decoration, not clutter (carry-over §5).
4. **Wreckage / debris that sells the castaway story** — **OPTIONAL, flag for Sponsor.** A few planks, a half-buried crate, or a broken-mast silhouette near the spawn would directly narrate "washed ashore from a wreck." This is *evocative and on-premise* BUT it's a new prop family + a subjective-story call (is the wreck part of the fiction, or did he arrive another way?). **Do not build speculatively** — surface to Sponsor as a "want me to add wreckage?" beat. If yes, it's a small Blender-MCP scatter prop in the warm-brown wood family (matches the axe haft / trunk palette). Listed as a Sponsor-input item in the PR.

---

## 4. Drew-ready implementation spec

The scene **already has water infrastructure** — this is a *completion + re-tune + relocation* job, not a from-scratch water system. Drew's starting point:

### What already exists (don't re-invent — extend)
- `LowPolyZoneGen.BuildWaterEdge()` — builds a Unity primitive `Plane`, collider removed (not walkable/not NavMesh), at `(cx, -0.25, shoreZ-14)`, scale `(width/10*1.1, 1, 4)`, assigned `waterMat`. **This is the water plane.** It's editor-time authored (good — avoids the Awake-no-serialize "legs-up" trap).
- `LowPolyZoneGen.MakeWaterMaterial()` — currently URP/Lit, `_BaseColor` `(0.20,0.46,0.52)`, `_Smoothness` **0.88** (too glossy), `_Metallic` 0. Persisted as a `.mat` asset.
- The terrain sand→grass ramp (`VertexColorForZ`, `SandDamp`/`SandLo`/`SandHi`) — **the shoreline transition; reuse verbatim.**
- The `FarHorizon/LowPolyVertexColor` shader — already in `AlwaysIncludedShaders` (registered by `WorldBootstrap`), already used for canopy + terrain gradient. **Reuse it for the water gradient** (vertex-color teal near→far) rather than authoring a new water shader.

### The gap to close (why no water shows today)
The full environment (`WorldBootstrap.cs`) builds the zone + water, but **the shipped M-U2 soak scene is `MovementCameraScene` / Boot.unity, which builds only a FLAT minimal test ground (`BuildFlatGround`) — no terrain mesh, no shore, no water** (`MovementCameraScene.cs` §25–26: "minimal FLAT walkable test ground only; the real environment is U5's surface"). So in the shipped scene there is *no ocean at all*; in `WorldBootstrap` the water exists but is small (`scale.z=4`), dim, over-glossy, and tucked behind the spawn off-camera. **"Finish the scene" = bring a properly-sized, re-tuned, visible ocean into the SHIPPED soak scene at the beach edge.**

### Implementation tasks (Drew)

| # | Task | Type | Detail |
|---|---|---|---|
| **A** | **Re-tune `MakeWaterMaterial`** | material (editor-time) | Swap to the `FarHorizon/LowPolyVertexColor` shader (or keep URP/Lit if the gradient is baked per-vertex and the shader multiplies it — see note); `_Smoothness` **0.55–0.65** (down from 0.88); bake the near→far teal gradient (`#3FA6B0`→`#2E7E96`) into the plane's **vertex colors**; sky-sheen via the moderate smoothness, not a cubemap. Sub-1.0 every channel. |
| **B** | **Enlarge + reposition the water plane** | mesh/transform (editor-time) | Extend seaward FAR past the fog distance so the far edge is lost in haze (not a hard plane-edge). Build it as a **welded subdivided grid** (not the 10×10 Unity primitive) so (1) the near→far vertex-color gradient has verts to interpolate across, and (2) the gentle swell animation has verts to displace. Smooth normals (`RecalculateNormals`, ~60° idiom). |
| **C** | **Gentle swell animation** | runtime shader OR runtime script | A slow large-wavelength vertical sine on the water verts (amp ≈0.04–0.08u, period ≈4–6s). **Preferred: do it in the vertex shader** (`LowPolyVertexColor.shader` could gain a tiny `_WaveAmp`/`_WaveSpeed` vertex offset) so nothing runs per-frame on the CPU and it serializes cleanly. If done in a runtime `MonoBehaviour`, that script only ANIMATES a mesh that's already serialized into the scene (never *builds* it at runtime — the Awake-no-serialize trap). |
| **D** | **Foam band** | vertex color (editor-time) | Bake the warm off-white foam (`#E8E2D0`, sub-1.0) into the seaward-most rows of the **beach mesh** vertex colors (rides the existing terrain vertex-color shader — no new object). |
| **E** | **Bring the shore+water into the shipped scene** | editor-time serialize | The shipped soak scene must carry the ocean. Either (a) `MovementCameraScene` grows a shore+water band at its seaward edge, or (b) the soak migrates toward the `WorldBootstrap` environment. **Orchestrator/Drew call which** — but the binding contract: the water must be SERIALIZED into the scene the soak actually ships, provable by an EditMode scene-presence test (see traps). |
| **F** | **Orbit-to-sea framing check** | soak verification | Confirm the player can orbit toward the sea and see bright water + their landing point filling the frame (§2). |

> **Vertex-color note for A:** `LowPolyVertexColor.shader` exists specifically because URP/Lit ignores vertex color. If the water gradient is baked per-vertex, it MUST render through `FarHorizon/LowPolyVertexColor` (or a sibling), not URP/Lit — else the gradient is silently dropped and the water ships single-tone. Drew: confirm the shader path the way the canopy/terrain already do.

### Trap classes Drew MUST honor (from `unity-conventions.md`)

1. **Custom shaders → `AlwaysIncludedShaders` or the build strips them** (§Build stripping; the spike's magenta lesson). `FarHorizon/LowPolyVertexColor` is *already registered* by `WorldBootstrap` — but if the shipped scene is `MovementCameraScene` and it doesn't go through `WorldBootstrap`'s registration, **verify the shader is registered for whatever scene ships the water.** If Drew adds a `_WaveAmp` vertex-offset variant or a new water shader, register it too.
2. **Editor-time serialize, never Awake-build** (§Editor-vs-runtime divergence, the "legs-up" class). The water mesh + material assignment is authored by `executeMethod` and saved into the scene. Runtime code only *animates* serialized geometry (task C) — it never *constructs* the water in `Awake`.
3. **Component-in-source-but-not-serialized-into-scene** (§Editor-vs-runtime, the `CaptureGate` class). If a water-animation `MonoBehaviour` is used, it must be baked into the shipped scene — add an **EditMode scene-presence test** (`WaterSceneTests`: load the shipped scene, assert the water object + material + component exist) the way `CaptureGateSceneTests`/`WarmthNeedSceneTests` do. Binary scenes can't be GUID-grepped — the EditMode test is the only authoritative reader.
4. **`VolumeProfile.Add<T>` post-stack serialization** (§Editor-vs-runtime). If the brighter-skybox / lighter-fog re-tune (§3 horizon) touches the post stack, do NOT re-break the U5 `AddObjectToAsset`-per-component guard — re-tune intensities on the *serialized* profile, don't re-add overrides in memory.
5. **Shipped-build capture gate** (CLAUDE.md hard rule + §Editor-vs-runtime): the water is visual → evidence comes from the **SHIPPED exe** (`serve_soak.sh`), not an editor RenderTexture. Editor `Camera.Render()` mis-renders (hero-axe PR #21 flat-brown-in-editor / barn-red-in-exe). The Self-Test Report cites a shipped capture whose HUD stamp == PR HEAD == attached artifact (§Capture-evidence freshness). The judgment-grade frame is an **orbit-to-seaward** view showing the ocean (the default inland view won't show it).
6. **Stale-stamp / serve-soak ritual** (§Headless rituals): serve the soak via `serve_soak.sh` (bootstrap fresh-stamps HEAD → BuildWindows → verify_build_stamp → capture_gate), never a bare `BuildWindows`.

### Serialization summary (what's mesh / shader / editor-time)

- **Mesh:** the enlarged welded-grid water plane — built editor-time, saved into the scene.
- **Material:** the re-tuned water material (vertex-color teal gradient, smoothness ~0.6) — assigned to `sharedMaterial`, serialized inline / as the existing `.mat` per the established pattern.
- **Shader:** `FarHorizon/LowPolyVertexColor` (existing, reused) for the water gradient + foam band; if a swell-vertex-offset variant is added, it's a new shader needing `AlwaysIncludedShaders` registration.
- **Editor-time-serialized:** water mesh, water material assignment, foam vertex colors on the beach mesh, any animation component — ALL authored by `executeMethod` and saved into the shipped scene. EditMode scene-presence test guards it.
- **Runtime-only:** at most a swell-animation `MonoBehaviour` that displaces already-serialized verts (preferred: do it in-shader and have zero runtime code).

---

## 5. Open questions / Sponsor-input items

1. **Wreckage/debris (§3 beat 4)** — add planks/crate/mast to narrate the shipwreck, or keep the landing implied? Subjective-story call → Sponsor. Default if unanswered: **no wreckage** (the clean bright shore already reads "washed ashore"); add later if Sponsor wants the wreck-narrative beat.
2. **Shipped-scene strategy (§4 task E)** — does the M-U2 soak scene grow a shore+water band, or does the soak migrate to the full `WorldBootstrap` environment? This is an orchestrator/Drew sequencing call (touches the live milestone scene — honor the style-guide-v2 §7 "don't destabilize M-U2 mid-milestone" constraint; likely a post-M-U2-close wave item).
3. **Swell animation in-shader vs runtime-script (§4 task C)** — Drew's implementation call; in-shader is the recommended default (zero per-frame CPU, clean serialization).

---

## Cross-references

- **Sponsor directive** 2026-06-13 ("finish the scene, i want water at the beach").
- [`inspiration/`](../../inspiration/) — board v2 PNGs; water ground-truth: `21h16_52` (lake-cabin), `21h16_13` (valley river), `21h13_31` (world-feel/daylight).
- [`.claude/docs/art-direction.md`](../../.claude/docs/art-direction.md) — board-v2 catalog + carry-overs.
- [`team/uma-ux/style-guide-v2.md`](style-guide-v2.md) — §0 tonal anchor, §1 shared grammar, §4 Water + Zone-D post re-tune, §5 carry-overs, §6 palette + HDR-clamp + water-ribbon anchor.
- [`.claude/docs/unity-conventions.md`](../../.claude/docs/unity-conventions.md) — §Build stripping (`AlwaysIncludedShaders`), §Editor-vs-runtime divergence (Awake-no-serialize / component-not-serialized / `VolumeProfile.Add<T>`), §Low-poly mesh patterns (welded + smooth normals), §Headless rituals (serve_soak / stamp), shipped-build capture gate.
- `team/DECISIONS.md` — 2026-06-12 vertex-color inline-materials pattern (the water gradient rides this); 2026-06-12 art-board rebase; 2026-06-13 castaway base swap (the chibi who stands on this shore).
- **Code:** `Assets/Scripts/Editor/LowPolyZoneGen.cs` (`BuildWaterEdge`, `MakeWaterMaterial`, `VertexColorForZ`, sand-ramp colors); `Assets/Scripts/Editor/WorldBootstrap.cs` (zone build + shader registration); `Assets/Scripts/Editor/MovementCameraScene.cs` (the shipped M-U2 soak scene — flat ground, locked spawn narrative); `Assets/Shaders/LowPolyVertexColor.shader`.
