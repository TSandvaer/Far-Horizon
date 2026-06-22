# Hand-tool / weapon style spec — DRAFT seed

**Status:** DRAFT seed (authored 2026-06-19 in a hands-on session) for **Uma** to
finalize. Captures the Sponsor's **Route A — in-house Blender matched set** decision for
a unified hand-tool/weapon family (axe, knife, sword, spear, …). This spec is the
load-bearing contract: every item produced through it will read as one family.

**Route decision:** cohesion across a weapon family is a *style-system* decision (shared
spec + shading model + palette + one pipeline), NOT per-asset sourcing. All items go
through **one Blender MCP pipeline** sharing **one low-poly palette material**. The
currently-shipped axe (`Assets/Art/Props/CastawayAxe/` — Viktor.G "One-handed stylized
axe", Sketchfab **CC-BY**, baked atlas) is the OUTLIER vs the flat-shaded Zone-D world:
treat it as a **placeholder to be re-made**, not the anchor. Re-making in-house also
retires the CC-BY attribution obligation.

---

## ⚠️ DECIDE THESE FIRST (open — Uma to lock)

These two parameters decide whether the family reads as cohesive. Lock before any modeling.

1. **Shading model.** Recommend **flat-shaded / faceted** (matches the art-board "faceted
   flat-shaded, chunky" direction in `.claude/docs/art-direction.md`).
   **VERIFY against how the current WORLD props are actually shaded** — weapons must match
   the world in-engine, not the board on paper. (CLAUDE.md frames the world as "low-poly
   smooth-shaded"; the art board says "faceted flat-shaded" — reconcile against the live
   build before locking.)
2. **Shared palette.** ONE shared URP material driven by a small palette. Extract the exact
   hexes from the **live world palette** (do NOT invent them). Minimum swatches:
   - wood tone (handle/shaft)
   - metal/stone tone (head/blade)
   - binding / wrap accent (grip)
   Optional: a darker shade-step per swatch if the shading model needs it.

---

## Locked principles (Route A)

- **One shared material, no per-asset baked atlases.** Vertex color or a tiny shared
  palette texture. (The baked photographic atlas is exactly what makes the current axe an
  outlier — do not repeat it.)
- **Poly budget:** chunky low-poly, ~150–500 tris per item, single mesh. Silhouette over
  surface detail.
- **Silhouette language:** bold, readable at distance; exaggerated heads/blades; chunky
  proportions (toy-like); NO thin/spindly forms. Each item's function reads instantly.
- **Shared handle/grip motif:** same wood handle treatment + same wrap/binding accent +
  proportional grip length across ALL items, so they read as "made by the same castaway."
- **In-hand scale:** normalized hand-size on the castaway's right-hand bone. The current
  axe already establishes the reference scale — match it.
- **Consistent grip-point pivot + forward axis** across every item, so ONE `HeldTool` rig
  generalizes from today's `HeldAxeRig` and any item slots in without per-item tuning.
- **In-house only — no CC assets** (no attribution obligations).

## Per-item silhouette notes (starting point — Uma refines)

| Item  | Read | Notes |
|-------|------|-------|
| Axe   | wedge head on a stout handle | re-make of the current hero axe; the family's scale + grip reference |
| Knife | short single blade, stubby | shortest grip; smallest silhouette |
| Sword | long blade + crossguard | longest blade; crossguard is the family's only "extra" detail beat |
| Spear | long shaft + compact point | longest overall; thin-but-not-spindly shaft within the chunky rule |

## Production / rig notes (for Devon)

- Produce the family **as a SET in one Blender MCP pass** against the locked spec — same
  material, same palette, same grip pivot — not item-by-item.
- **Re-make the hero axe** in the same pipeline; retire `CastawayAxe` (Viktor.G CC-BY) +
  its license file once the in-house axe ships.
- **Generalize** `Assets/Scripts/Runtime/HeldAxe.cs` / `HeldAxeRig.cs` → a `HeldTool`
  rig so every item in the family uses one hold/seat system (the held-axe soak-tuning
  already solved the hard part; don't redo it per item).
- 1 Unity-build slot (single-runner cap) — this is the Unity-heavy lane; sequence it.

## Acceptance (proposed)

- All family items in-engine share one material + palette; no per-asset atlas.
- Lined up side by side in the gameplay cam, they read as ONE family (shading + silhouette
  + grip motif consistent).
- Each held in-hand at correct scale via the shared `HeldTool` rig.
- Hero axe re-made; Viktor.G CC-BY asset + license retired.
- Shipped-build capture evidence (per the capture gate) before merge.
