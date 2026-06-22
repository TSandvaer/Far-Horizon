# Art Direction — Sponsor inspiration board

**Status:** Sponsor-set art-direction north-star — **REBASED 2026-06-12 evening**: the Sponsor
replaced the entire board ("throwing a lot of stuff in the inspiration folder, deleted the old
genre"). The 2026-06-08 lush-garden/courtyard references are DELETED; the board now reads
**chunky stylized cartoon low-poly** across characters, tools, and nature.
**Every session and sub-agent doing visual / level / tile / prop / palette work
must look at the actual images** in [`inspiration/`](../../inspiration/) before
proposing or implementing — the text below is a guide to what to SEE in them, not
a replacement for seeing them.

> **How to use:** `Read` the PNGs in `inspiration/` directly (they render as images).
> This doc captures the extracted direction so it survives in context; the images
> are the ground truth. When Sponsor drops a new reference into `inspiration/`,
> add a catalog entry here.

## The references (board v2 — 2026-06-12)

**The one-line direction:** toy-like, chunky, saturated low-poly — faceted flat-shaded meshes,
bold readable silhouettes, cheerful color. It spans ALL THREE surfaces (character, tools/props,
world/nature), which is why the old "realistic-lush" world references left the board.

### `inspiration/2026-06-12_21h00_32.png` — cartoonish castaway (CHARACTER pole)
The chat reference, now on disk (resolves the former PENDING entry). Chunky stylized proportions:
oversized head (~1:3 head:body), big expressive dark eyes, simplified blocky hands/feet, flat
smooth-shaded materials, warm skin, stone-axe prop, teal backdrop. **Sponsor-clicked scope: STYLE
ONLY** — proportions/stylization transfer to the LOCKED young/hopeful identity; the reference's
bearded rugged adult is NOT adopted (`_castaway_judge/` sheets stay the identity ground truth).
Ticket `86ca8ca1m`.

### Tools/props family — `21h06_54` (pickaxe), `21h07_20` (sword), `21h07_42` (curved blade), `21h08_08` (axe)
A coherent flat-shaded tool language: faceted heads/blades with a white edge-highlight plane,
chunky slightly-bent wooden hafts, segmented wrapped grips, mild asymmetry (nothing machined).
The **axe** is immediately actionable — the survival loop's hero tool should read like
`21h08_08` (red head, white edge, bent brown haft). Sword + curved blade signal future interest
(combat-ish props) but nothing is scheduled — style reference only until the Sponsor shapes M-U3+.

#### Hand-tool / weapon family — cohesion is a style-SYSTEM decision (Sponsor, 2026-06-19 "Route A")
The axe/knife/sword/spear family is produced via **ONE in-house Blender pipeline + one shared style spec + one shared flat-shaded low-poly palette material** — NOT per-asset sourcing. Cohesion across the family is a *style-system* call, and the deciding parameter is the **shading/texturing model**: a per-asset baked texture atlas (what the current axe uses) is the primary OUTLIER-maker against a flat-shaded palette world — it imports its own baked lighting and reads as a foreign, more-detailed object beside faceted flat geometry. So the current shipped axe (`Assets/Art/Props/CastawayAxe/`, Sketchfab CC-BY) is a **placeholder, NOT the style anchor** — hold `21h08_08` as the visual target and the shared flat-shaded palette material as the shading model; don't tune the placeholder's look as if it were final. Draft spec: `team/uma-ux/weapon-tool-style-spec.md`. Code implication: generalize `HeldAxe.cs` / `HeldAxeRig.cs` into a shared `HeldTool` rig so any family item slots in without per-weapon hold logic.

### Nature family — `21h10_44` (trees/clouds/rocks/grass set), `21h11_03` (four trees)
Blob-canopy low-poly trees (faceted polygonal clusters in varied greens on simple trunks),
bright teal cartoon clouds, faceted grey rocks, simple grass tufts. This is the WORLD direction:
the Zone-D environment's trees/props should migrate toward these silhouettes — saturated,
toy-like, readable at orbit distance. (The warm/lush FEELING carries; the rendering style shifts
from soft-realistic toward chunky-cartoon.)

### World/vista family — `21h12_49`, `21h13_31`, `21h16_13`, `21h16_52`, `21h21_30`, `21h22_05` (added later the same evening)
The world direction at every scale, all in the chunky cartoon style:
- **`21h12_49`** — Blender nature kit: pine + blob trees, faceted grey snow-capped mountain, log,
  stump, rocks, mushrooms, flowers, grass tufts (the prop vocabulary, and a literal Blender shot —
  the creation route the Sponsor flagged).
- **`21h13_31`** — rolling grassland FEEL shot: blob trees on gentle hills, soft sky, birds,
  depth — the small-elements-in-a-big-alive-world read in the new style.
- **`21h16_13`** — the VISTA composition: faceted snow-capped mountains behind pine forest, a
  winding blue river, clouds (one raining), and a campfire in the foreground — essentially the
  whole game in one frame: fire at your feet, the far horizon behind.
- **`21h16_52`** — human-scale LANDMARK: red cabin on stilts over a lake, footbridge, pine islands.
- **`21h21_30`** — ground-level traversal: sunlit forest path, pines, grass, rock cliffs, stream.
- **`21h22_05`** — low-poly VILLAGE triptych: houses, paths, bridges, walls, terraced hills —
  settlement-scale landmarks; journey destinations that read warm and lived-in.

Together these are the M-U5+ "journey out" arc made visible: regions, landmarks, and the horizon
as a destination — in the chunky style. Pine trees join blob trees in the tree vocabulary;
mountains are the vista backdrop; water (river/lake) recurs in every wide shot.

### Ground-detail pair — `21h22_33` (forest meadow), `21h22_52` (toy village path)
- **`21h22_33`** — dense forest meadow at player height: tall pines, RICH layered ground cover
  (grass blades, purple/white wildflowers, mushrooms, stumps, logs, rocks) along a worn dirt
  trail with a simple bench — the lush *purposeful* decoration carry-over rendered in the new
  style; ground reads full, never empty.
- **`21h22_52`** — top-down toy-like village path: chunky cut-stone path winding between a
  hobbit-style door, stone arch bridge, cone trees, scattered rocks; warm sunlit grass.
  Path-as-readable-ribbon at orbit distance — the path language for settlements.

> **Carry-overs that survive the rebase:** small-player/big-alive-world north-star; warm cohesive
> palette; human-scale landmarks; lush *purposeful* decoration. What changed is the STYLE those
> are rendered in — chunkier, more saturated, more toy-like.
> **Catalog freshness note:** the Sponsor adds references in bursts — before any visual work,
> `ls inspiration/` and view anything not yet catalogued here, then add its entry.
