# World-Look Polish — Visual Direction Brief (clouds + far-horizon vista + sky-tint)

**Ticket:** `86ca8rd6a` · **Author surface:** Uma (UX/Visual Direction) · **Status:** PROPOSAL for Sponsor approval — NOT implementation.

**This is a design brief, not code.** No engine/scene/palette-code changes; no merge to `main`. It is the **Pattern-A "author first" step** — Erik's URP-technique research, Priya's ticket decomposition, and Drew's implementation all sequence AFTER Sponsor sign-off on the *look* described here.

---

## Tonal anchor (read this first)

**The far horizon is a PROMISE the player can see but not yet touch.** Three surfaces — the clouds overhead, the distant landmasses on the rim of the world, and the sky-tint that binds them — exist to make one feeling true: *you are a small hopeful castaway standing inside a big, bright, alive diorama, and there is somewhere out there worth walking toward.* Every dial below serves that. If a beat decorates the sky but doesn't make the world feel BIGGER or the horizon feel like a destination, it's cut.

The world reads as a **toy diorama under open daylight** — chunky, faceted, saturated, cheerful (board v2). NOT atmospheric-moody, NOT photoreal, NOT hazy-warm. The reference frame is `2026-06-12_21h13_31.png` (the grassy tree-field — clean bright sky, crisp facets, gentle depth) and `2026-06-12_21h16_13.png` (the mountain-valley vista — the whole game in one frame: fire at your feet, the far horizon behind).

**Hard-carries this brief honors** (from `style-guide-v2.md` §5–6 + `art-direction.md`):
- **Small-player / big-alive-world** — stylization must never shrink the sense of scale.
- **Warm cohesive palette, saturation UP, warm hue bias stays.** Sky stays warm-bright, never cold-clear.
- **Sub-1.0 HDR-clamp-safe** — *every channel < 1.0 on every color in this brief.* Saturated ≠ blown-out. (Carries from the Zone-D look + the U2-5 HUD spec; verified per-swatch in the tables below.)
- **Keep the post-processing STACK, re-tune its DIALS.** The VolumeProfile is already correctly serialized (`VolumeProfile.Add<T>` fix, U5/PR #4) — these are intensities to turn, not systems to rip out or re-add. Do not re-break that serialization (`unity-conventions.md` §Editor-vs-runtime).

> **Palette inheritance:** this brief EXTENDS `style-guide-v2.md` §6's master swatch table; it does not redefine it. Where this brief adds new surfaces (cloud body, mountain band, sky stops, fog tint) it appends new anchor swatches in the same sub-1.0 convention. Existing swatches (canopy greens, rock grey, water blue) are cited, not changed.

---

## 1. CLOUDS

### Tonal intent
The clouds are the **toy-diorama ceiling** — chunky cyan blobs that float far overhead and quietly say "this is a cartoon world, and it is a nice day." They reinforce the cheerful read and give the orbit camera something alive in the upper third of the frame without ever competing with the player. They are decoration that serves the anchor (a bright, hopeful sky); they are NOT weather, NOT mood, NOT volumetric.

### Reference images (ground truth)
- **`inspiration/2026-06-12_21h10_44.png`** (top row) — the cloud asset sheet: 6 clustered faceted cyan/teal blobs. THIS is the cloud language: same blob vocabulary as the tree canopies (`21h11_03`) but in light cyan. Chunky, hard-faceted, varied silhouettes.
- **`inspiration/2026-06-12_21h16_13.png`** — clouds in-scene at vista scale: a few small puffs high in the sky, one darker grey "rain" cloud (the rain streak is a *future* mood beat — see Sponsor-gate flag below; NOT in this pass).
- **`inspiration/2026-06-12_21h13_31.png`** — clouds soft and distant against the bright sky at the orbit-distance read we actually ship.

### Form — chunky low-poly, NOT volumetric
- **Clustered faceted blobs**, identical construction language to the blob canopies (`style-guide-v2.md` §4 trees): each cloud = 3–6 overlapping low-poly spheroids welded into one chunky mass with **hard normals (smoothing angle 0°)** so the facets read. Solid volumes — this sidesteps the thin-foliage near-black-shard normal bug (`unity-conventions.md` §Low-poly mesh patterns) the same way the canopies do.
- **3–5 distinct silhouettes** (per the 6 on the asset sheet) so they don't read as stamped clones. Mild asymmetry = the hand-made toy charm.
- **NO volumetric / billboard / soft-particle clouds.** Volumetric fog-clouds break the faceted toy read AND carry a perf/HDR-bloom risk. If a soft cloud is ever wanted it's a separate Sponsor call — default here is mesh blobs.

### Count / scale / placement / drift
| Property | Direction | Rationale |
|---|---|---|
| **Count** | **5–9** clouds across the visible sky dome | `21h16_13` reads as "a few" — sparse, purposeful (the no-clutter carry-over). Enough to feel alive, few enough to never crowd. |
| **Scale** | Each cloud spans **~8–18 world-units** in its long axis; player is ~1u. So a cloud is **~8–18× the player** — big enough to read as "up there and large," small enough not to be a ceiling. | Reinforces small-player/big-world: the sky furniture is many times the player's size. |
| **Altitude** | High — sit them at **~30–60u above ground**, biased toward the upper third of the orbit frame at the default pitch. | They should live in the sky band the orbit cam catches, not hang in the player's eye-line. |
| **Drift** | **Very slow lateral drift, ~0.2–0.5 u/s**, single shared wind direction, slight per-cloud speed variance. Looping/wrapping at the dome edge. | Slow enough to feel calm-day, not stormy; motion proves the world is alive without pulling focus. |
| **Rotation** | None / negligible. Clouds translate, they don't tumble. | A tumbling cloud reads as debris, not sky. |

### Palette (new anchor swatches — sub-1.0 verified)
The board clouds are a **warm-leaning cyan** (green-biased, belongs to the warm-cohesive family — NOT a cold steel blue), with a brighter top-lit facet and a cooler shadow facet, exactly mirroring the canopy 3-value treatment.

| Cloud facet | Color | RGB (0–1) | Note |
|---|---|---|---|
| Body (mid) | light cyan `#8FD8E0` | 0.56, 0.85, 0.88 | the dominant blob value; warm-leaning cyan, sub-1.0 |
| Top-lit facet | bright pale cyan `#C4ECEF` | 0.77, 0.93, 0.94 | facets catching the key light; near-white but each channel < 1.0 (HDR-safe) |
| Shadow facet | soft teal `#6BBAC6` | 0.42, 0.73, 0.78 | underside / away-from-key planes; gives the blob depth |

> **HDR-clamp discipline:** the brightest cloud value (`#C4ECEF` = 0.94 max channel) is deliberately held below 1.0. A pure-white cloud + the (reduced but present) bloom would bloom-clip into a glowing blob and break the crisp facet read. Keep the cap sub-0.95.

### How the clouds read at the human-scale orbit camera
At the default PoE-style orbit pitch the player sees clouds in the **upper third** of the frame, drifting slowly, never occluding the player or the play space. From a low orbit pitch (orbiting toward the horizon) clouds sit just above the distant landmass silhouettes (§2) and reinforce the depth stack. They must **read as clearly faceted** at orbit distance — if they read as smooth blobs, the facet count is too high or smoothing isn't hard-0°.

### How we judge it (shipped-build capture gate)
Shipped-exe (`serve_soak.sh`) orbit-cam capture at the default gameplay pitch AND a low horizon-ward pitch: clouds read as chunky faceted cyan blobs (not smooth, not volumetric), 5–9 visible, slow drift confirmed across two frames, none occluding the player. Editor RenderTexture evidence is NOT sufficient (`unity-conventions.md` §Editor-vs-runtime — editor `Camera.Render` mis-renders multi-submesh URP + the no-post false-green class).

---

## 2. FAR-HORIZON VISTA

### Tonal intent
**This is the north-star surface.** The vista is where the game says *the world is bigger than where you stand, and it goes on.* When the player orbits toward the horizon they should see layered distant landmasses fading into the bright sky — a destination, not a wall. The feeling is the held breath of `21h16_13`: mountains behind the forest, the eye pulled past the campfire at your feet, out and up to the far peaks. **If the horizon reads as a flat backdrop or a hard skybox seam, this surface has failed its only job.**

### Reference images (ground truth)
- **`inspiration/2026-06-12_21h16_13.png`** — THE vista composition. Faceted grey-to-snow peaks layered behind a conifer forest, a winding water ribbon leading the eye in, depth from foreground to far peaks. This is the target silhouette + layering.
- **`inspiration/2026-06-12_21h22_05.png`** (village triptych) — shows **atmospheric fade** done right: distant hills wash toward a warm pale haze while the foreground stays crisp and saturated. This is the fade gradient to emulate (warm, distance-only).
- **`inspiration/2026-06-12_21h13_31.png`** — the gentle-depth read at orbit distance; faint hills barely visible on the rim behind the tree-field.
- **`inspiration/2026-06-12_21h16_52.png`** (cabin-on-lake) — human-scale landmark sitting BETWEEN the player and the far peaks; the mid-ground layer that makes the depth stack legible.

### The depth stack — where the eye travels
The vista must build in **readable layers** (foreground → far rim) so the eye ladders outward. The eye should travel: **fire/player at your feet → the wooded mid-ground → a mid-distance landmark (a hill, a cabin, a bend in the water) → the far landmass silhouettes → the bright sky they dissolve into.**

| Layer | Distance band | Content | Treatment |
|---|---|---|---|
| **Foreground** | 0–~40u | player, fire, trees, craft spot | full saturation, crisp facets, no fade |
| **Mid-ground** | ~40–150u | blob/pine tree masses, a landmark or water bend | near-full saturation, very light fade beginning at the far end |
| **Far landmass** | ~150–400u | faceted mountain/hill SILHOUETTES (the `21h16_13` grey-to-snow peaks) | strong atmospheric fade toward sky-tint; silhouette + facet still legible but desaturated and lightened |
| **Rim / sky dissolve** | ~400u+ | where the farthest peaks meet the gradient sky | landmasses dissolve INTO the sky-tint (§3) — no hard horizon line, no skybox seam |

### Distant landmass silhouettes — form
- **Faceted grey-to-snow mountains** per `21h16_13`: big confident planes, a snow-white cap facet, warm-grey body — the SAME hard-faceted language as foreground terrain/rocks (`style-guide-v2.md` §4), just scaled up and atmosphere-faded. They are part of the diorama, not a painted backdrop.
- **2–3 overlapping silhouette ranges** at different far-distances → parallax depth and an "endless" read (range behind range behind range). Overlap is what sells distance.
- **Lower, rolling hill silhouettes** can sit between mountain ranges for the gentler `21h13_31` regions — the landmass language isn't only peaks.
- **Implementation note for Drew (not a lock):** distant ranges can be far low-poly mesh OR a faceted painted-into-the-gradient band — but the SILHOUETTE must stay faceted/chunky and the FADE must be real atmospheric depth, not a flat decal. Erik's URP research (sequenced after this brief) should weigh mesh-at-distance vs. a skybox-baked horizon band for perf + the seamless-dissolve requirement. **The look requirement is fixed; the technique is Erik's to research.**

### Palette (new anchor swatches — sub-1.0 verified)
Distant landmasses are the foreground rock/mountain palette **shifted toward the sky-tint** (the atmospheric-fade effect): lighter, lower-saturation, warmer as they recede.

| Vista element | Color | RGB (0–1) | Note |
|---|---|---|---|
| Far mountain body (faded) | hazy warm grey-blue `#9AA6AE` | 0.60, 0.65, 0.68 | the foreground rock `#8E8A82` lightened + cooled toward sky; still warm-leaning, not steel |
| Far mountain snow cap (faded) | pale warm white `#E8ECEC` | 0.91, 0.93, 0.93 | sub-0.95 cap, won't bloom-clip |
| Farthest rim range | sky-blended `#B6C4CC` | 0.71, 0.77, 0.80 | nearly dissolved into the sky horizon stop (§3 horizon color) — the most-faded range |

> The farthest range's color is intentionally **a half-step from the §3 sky-horizon stop** so the dissolve is seamless. Drew: tune the farthest-range tint to read as "almost sky" against the shipped gradient.

### How we judge it (shipped-build capture gate)
Shipped-exe orbit-to-horizon capture (low pitch, oriented toward the far landmass): the eye ladders through ≥3 depth layers; ≥2 overlapping faceted mountain/hill silhouette ranges are visible; the farthest range dissolves into the sky with NO hard horizon seam; foreground stays crisp+saturated while the far rim is faded. The frame must FEEL big — Sponsor-judged. (Gameplay-orbit cam + real scene lighting/post, per the false-green-capture lesson `unity-conventions.md` 86ca8ca1m soak — an isolated hero frame lies.)

---

## 3. SKY-TINT

### Tonal intent
The sky is the **warm bright daylight that holds the whole diorama together** — the open-sky read of `21h13_31`, not a hazy-warm mood and not a cold clear blue. It is the surface the clouds float in and the landmasses dissolve into, so it must bind §1 and §2 into one coherent frame. The grading and fog tie-in keep the toy colors clean and saturated while letting the far horizon fade gently for depth. **Warm-bright, clean, cheerful — a nice day in a toy world.**

### Reference images (ground truth)
- **`inspiration/2026-06-12_21h13_31.png`** — THE sky-tint target: open bright daylight sky, soft pale-blue-to-warm gradient, clean (not hazy), crisp foreground. This is "clearer, brighter sky" from the §4 Zone-D delta made specific.
- **`inspiration/2026-06-12_21h16_13.png`** — the gradient behind the vista: pale warm blue up top easing to a near-white warm horizon where the peaks dissolve.
- **`inspiration/2026-06-12_21h22_05.png`** — the warm grading + light distance-haze done right (warm, distance-only, foreground stays saturated).

### Gradient skybox direction — the vertical gradient
A **3-stop vertical gradient** (zenith → mid → horizon), warm-bright, clean. This re-tunes the Zone-D gradient skybox toward "clearer, brighter, lower-haze" (`style-guide-v2.md` §4 delta table) while keeping the warm hue bias (§6 — "the sky stays warm-bright, not cold-clear").

| Sky stop | Position | Color | RGB (0–1) | Note |
|---|---|---|---|---|
| **Zenith** | top of dome | soft warm blue `#7FB4D6` | 0.50, 0.71, 0.84 | the bright open-day blue; warm-leaning (not steel/navy), sub-1.0 |
| **Mid** | ~horizon+30° | pale warm blue `#AAD0E2` | 0.67, 0.82, 0.89 | the easing band; lighter, warmer |
| **Horizon** | dome base | warm pale cream-blue `#DCE8E4` | 0.86, 0.91, 0.89 | near-white WARM horizon — this is where landmasses dissolve (§2 farthest range `#B6C4CC` blends INTO this); sub-0.95, won't bloom-clip |

> **Why warm at the horizon, not cold:** a cold-white horizon reads as overcast/winter and fights the cheerful anchor. The slight green-cream warmth (`#DCE8E4`) keeps it sunny. **This is the load-bearing warm-carry-over** — if the horizon goes cold the whole frame goes from "nice day" to "grey day."

### Grading / fog tie-in (re-tune the existing serialized stack — do NOT re-add it)
Per `style-guide-v2.md` §4 delta + `unity-conventions.md` §Editor-vs-runtime (`VolumeProfile.Add<T>` serialization — the stack is already in the profile asset; these are intensity DIALS):

| Post dial | Direction | Why |
|---|---|---|
| **Bloom** | **DOWN** from Zone-D. A touch on the brightest highlights (cloud caps, snow, water glints) is fine; heavy bloom is OUT. | Board objects have crisp facet edges, not glowy ones — heavy bloom softens facets + bloom-clips the sub-1.0 brights. |
| **Color grading** | **Lighter, neutral-warm.** Pull any heavy filmic/contrast grade DOWN; let the saturated toy greens/reds/cyans speak. Slight warm-temperature nudge only. | The board is saturated-but-clean; a heavy grade muddies the toy colors. |
| **Fog** | **Much lighter, DISTANCE-ONLY.** Crisp to mid-distance; fog engages only in the far band (~150u+) to fade the landmasses into the horizon stop. **Fog color = the §3 horizon stop `#DCE8E4`** so the fade and the sky agree. | Near-field haze kills the crisp-bright read; far-field fog IS the §2 atmospheric fade that serves the big-endless north-star. The fog tint MUST equal the horizon sky stop or the dissolve seams. |
| **Ambient / exposure** | Bright open-daylight key; do not crush shadows to mood-dark. Soft even key + gentle AO (the board's studio-soft light, `style-guide-v2.md` §1.5). | The diorama sits in cheerful daylight, not dramatic chiaroscuro. |

> **The one tie-in that must not drift:** **fog color == horizon sky stop == farthest-vista-range tint family.** These three are one dissolve. If Drew tunes one, tune all three together, or the horizon seams (a hard line between faded mountain / fog / sky is the most common way this whole look breaks).

### How we judge it (shipped-build capture gate)
Shipped-exe orbit captures at high AND low pitch: the sky reads as a clean warm-bright vertical gradient (warm horizon, NOT cold-white, NOT hazy); facets stay crisp (bloom not over-soft); foreground saturation is clean (grade not muddy); far landmasses fade into a fog tint that MATCHES the horizon sky stop with no seam. Sponsor-judged for the "nice warm day" feel. (Gameplay-orbit cam + real post stack — `unity-conventions.md` 86ca8ca1m false-green-capture lesson.)

---

## Sponsor subjective-approval gates (flag before Drew implements)

Three of these are **subjective-feel calls** — per the orchestrator-autonomy never-auto-decide list, visual-feel is the Sponsor's, not the team's. They must clear a Sponsor gate (this brief's approval, or a soak sign-off) before Drew's impl is judged "done":

1. **GATE — the whole-frame warm-vs-cold balance of the sky-tint.** Warm-bright is the locked direction, but the exact horizon warmth (`#DCE8E4` vs. a touch warmer/cooler) is a taste call only the Sponsor's eye settles. Recommend an A/B in soak (two horizon tints) if the first read isn't an instant yes — Sponsor prefers direct-tweak over blind iteration (memory: `sponsor-prefers-direct-tweak-tools-for-fiddly-placement`).
2. **GATE — bloom intensity (how glowy is too glowy).** "Pull bloom down" is directional; the exact stop where facets stay crisp but brights still sparkle is a Sponsor-eye call. Soak A/B candidate.
3. **GATE — does the vista FEEL big.** The §2 north-star payoff is purely subjective. The capture gate proves the layers exist; only the Sponsor confirms it feels like a destination. This is the single most important sign-off in the brief.
4. **FLAG (scope, not feel) — the rain-cloud / weather beat.** `21h16_13` shows a darker rain cloud with a rain streak. That is a MOOD/weather beat that fights the "nice bright day" anchor of THIS pass. **Recommend: explicitly OUT of this pass** — clouds ship as cheerful cyan puffs only. If the Sponsor wants weather, it's a separate future ticket with its own tonal call (rain changes the whole frame's mood). Cut from this pass unless Sponsor pulls it in.
5. **FLAG (technique, not feel) — far-landmass implementation route is Erik's to research.** This brief LOCKS the look (faceted overlapping silhouettes, real atmospheric fade, seamless sky dissolve) but deliberately does NOT pick mesh-at-distance vs. baked-horizon-band. Erik's URP research sequences after this brief and feeds Priya's decomposition. No Sponsor gate needed — just don't let impl start on the technique before Erik's pass.

---

## Cross-references

- **Ticket** `86ca8rd6a` (this brief). Sequencing: Pattern-A author-first → Erik URP research → Priya decomposition → Drew impl, all AFTER Sponsor approval.
- **`team/uma-ux/style-guide-v2.md`** — the master board-v2 style guide this brief EXTENDS: §4 (nature/world + Zone-D delta table — clouds/terrain/sky high-level), §5 (carry-overs), §6 (master sub-1.0 palette table — this brief appends cloud/vista/sky swatches in the same convention).
- **`team/uma-ux/beach-water-direction.md`** — sibling world-surface brief; shares the orbit-to-horizon capture-gate convention + the "can't see X is a camera-reach bug" lesson.
- **`.claude/docs/art-direction.md`** — board-v2 catalog + carry-overs. *(Note: the three scene refs this brief leans on — `21h12_49` / `21h13_31` / `21h16_13` — are in `inspiration/` but not yet in the catalog; the catalog amendment is already proposed in `style-guide-v2.md` §8 for Priya to route. Not re-filed here.)*
- **`.claude/docs/unity-conventions.md`** — load-bearing traps for Drew's eventual impl: §Editor-vs-runtime (`VolumeProfile.Add<T>` serialization — re-tune dials, don't re-add the stack; the no-post / isolated-hero false-green capture classes; gameplay-orbit-cam judgment), §Build stripping (`AlwaysIncludedShaders` for any new sky/cloud shader), §Low-poly mesh patterns (hard-0° normals + solid blob volumes for clouds = sidesteps the thin-foliage shard bug), §Headless rituals (serve_soak / build-stamp gate).
- **`inspiration/`** board v2 (ground truth — viewed for this brief): `21h10_44` (cloud asset sheet), `21h13_31` (sky-tint + orbit-depth target), `21h16_13` (THE vista composition), `21h22_05` (atmospheric-fade reference), `21h16_52` (mid-ground landmark layer), `21h11_03` (blob-construction language clouds share).
