# World-Look Quality Rework — Diagnosis-via-trace (ticket 86ca8t9pq, PR #48)

Branch `drew/world-look-impl` @ `4457d47`. Sponsor soaked `4457d47` → "doesn't look good":
mountains render as FLOATING TRANSLUCENT shards; water = hard flat diagonal edge; sky/clouds
need to look better. Erik consult `team/erik-consult/world-look-quality-research.md`.

## Discipline: instrument/read before hypothesizing. Erik's two mountain hypotheses are BOTH REFUTED.

### MOUNTAINS — "floating translucent shards"

Erik H1: material Surface Type = Transparent. **REFUTED.** The mountains use
`FarHorizon/LowPolyVertexColor` (`MakeVertexColorMat`, `WorldBootstrap.cs:371`). That shader is
`Tags{"RenderType"="Opaque" "Queue"="Geometry"}`, frag returns `half4(finalCol, 1.0)` (alpha 1).
Fully opaque — no transparency at the shader level (`Assets/Shaders/LowPolyVertexColor.shader:31,121`).

Erik H2: inverted winding (same class as the −Z sea grid). **REFUTED.** `FacetedMountain`
(`LowPolyMeshes.cs:674`) enforces OUTWARD winding per-face in `EmitFace` (`:717-727`: flips the
tri + normal if `dot(faceNormal, centroid−center) < 0`). The existing EditMode guard
`FacetedMountain_AllFacesPointOutward_NotBackfaceCulled` (`WorldLookMeshTests.cs:184`) is GREEN
(branch shipped 167/167). The mesh is NOT backface-culled.

**REAL CAUSE = double-fade washes distant clusters to ghosts.** The far clusters get faded TWICE
toward the horizon stop `#DCE8E4`:
1. Per-cluster `_Tint` lerps the mesh colour toward `#DCE8E4` by `fadeK` (`WorldBootstrap.cs:258`,
   `FadeTint`). Far islands have `fadeK` 0.45–0.82 → mesh already 45–82% sky-coloured before lighting.
2. Exp² fog (density 0.0016, colour == `#DCE8E4`, `QualityPassGen.cs:93-101`) then blends the
   shaded result toward the SAME colour by distance. Computed fog blend at the cluster distances:
   - 430u → 38% fog;  470u → 43%;  540–560u → 53–55%;  **950u (`Vista_Far`) → 90% fog**.

Net: the far islands are ~70–95% horizon-coloured = faint, washed, see-through-looking silhouettes
floating against the sky with no grounding → exactly "floating translucent shards." This is a
LOOK/composition bug (over-fade + clusters too far + raised on thin +2–6u shelves over open sea),
NOT a winding/surface-type bug.

### WATER — "hard flat diagonal edge"

`BuildWaterEdge` (`LowPolyZoneGen.cs:531`). Current water is a WELDED grid w/ `RecalculateNormals`
(smooth), 2-band depth lerp keyed off world-Z (`WaterShallow→WaterDeep`), and a baked FoamEdge in
the SEAWARD-most rows (far out, `FoamEdge`). The "hard diagonal edge" the Sponsor sees is the water
mesh BOUNDARY where the rectangular grid meets the curving beach — there is NO depth-fade at the
SHORELINE intersection (foam is baked far out to sea, not at the land contact). Uma/Erik want:
flat-shaded UNWELDED facets + depth-fade shoreline foam at the land intersection + depth gradient.

### SKY — already fixed (no action)

`GradientSkybox.shader` already uses standard skybox render state (`Cull Off ZWrite Off`,
object-space dir, `Queue=Background`, NO `xyww`). The PR #48 wash bug is fixed + Tess QA confirmed
the shader ships correctly (`-wlDiag`). Sky step is DONE. Only re-verify it survives the rework.

### CLOUDS — fine; lighter pass

`CloudBlob` (`LowPolyMeshes.cs:508`) is a multi-blob faceted cyan cluster, guarded + green. Sponsor
issue (4) was clouds HIDDEN BEHIND the mountain wall — fixing the mountains (open sky) reveals them.

## Fix shapes

1. **Mountains:** kill the double-fade — cap/remove the per-cluster tint fade, pull the far clusters
   IN (so fog doesn't ghost them), drop `Vista_Far` (950u → 90% fog = pure waste) or pull it to a
   visible distance, and seat clusters on visibly RAISED land bases (taller `raise`/landmass shelf)
   so they read as grounded islands, not floating shards. ALL controllable live via the tweak tool.
2. **Water:** unwelded flat-shaded facets + a depth-fade shoreline foam band at the actual shore
   intersection + keep the depth gradient. Kill the hard rectangular boundary read.
3. **Sky:** no change; re-verify no regression.
4. **Clouds:** light pass — slightly more/larger so they read once the sky opens.
5. **Tweak tool:** F9 `WorldLookNudgeTool` — live sky gradient stops, fog distance+colour
   (seam-kill preserved), cloud count/scale/height, mountain count/scale/distance/fade. Sponsor
   dials the final look + reports values to bake.
