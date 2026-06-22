# Berry bushes — content plan (ticket 86caa5zz3)

## What's already on main (build on, don't reinvent)
- `ItemCatalog.BerryId = "berry"` + `BerryCluster()` icon (PR #90) — berry item EXISTS.
- `InventoryModel.AddItem(def, n)` — the pickup seam; `RemoveItem(id, n)` all-or-nothing consume.
- `HungerNeed.TryEatBerry(Func<bool> consumeOneBerry)` (PR #93) — the atomic eat-seam. RESTORE side lives there.
- World scatter: `LowPolyZoneGen.ScatterIslandProps(root, seed, groundCol)` (trees/rocks/grass), additive per sub-seed.
- `GroundPoint(col, x, z)` — raycast-down grounding (scale-immune, like stones).
- Interaction idiom: `ChopTree` (proximity-poll + interact); BuildChopTree wires it into Boot.unity.

## Scope (this ticket)
- AC1/AC2: scatter bushes across seed-42 island, varied sizes + 2 types (plain + berry). ADDITIVE sub-seed (seed+777) → seed-42 island/terrain/scatter/NavMesh untouched.
- AC3: berry-bush variant; proximity+interact harvest (no tool) → `AddItem(berry, n)`.
- AC5/AC5a/AC5b: eat-action consumes one berry; graceful no-HungerNeed guard; THIS ticket tests CONSUME side only (the atomic test is in 86caamkp8).
- AC4 (regrowth): bush persists, berries deplete→regrow random within serialized [min,max]. Data side here.
- AC6: PlayMode tests — bushes present+grounded; harvest→inventory(stacks); regrow after timer; eat consumes one.

## Out of scope (file follow-up, do NOT bundle)
- AC4 settings-panel REGISTRATION: no settings/dev-tweak panel on main (gated on #83 per memory `sponsor-wants-unified-dev-tweak-console`). Regrowth min/max ship as serialized tweakable fields; panel-registration is a follow-up once the panel foundation lands. NOT a mid-PR scope expansion.
- Hunger NEED + what eating RESTORES (86caamkp8 owns it).

## Files
- NEW `Assets/Scripts/Runtime/BerryBush.cs` — harvest+regrow+eat-bridge, with `[bush-trace]` instrumentation from day one.
- `Assets/Scripts/Editor/LowPolyMeshes.cs` — `BushBlob` mesh (low rounded blob cluster) + berry spheres.
- `Assets/Scripts/Editor/LowPolyZoneGen.cs` — scatter bushes (separate sub-seed) in ScatterIslandProps.
- `Assets/Scripts/Editor/MovementCameraScene.cs` — author ONE berry bush near the loop centre, wired into Boot.unity (PlayMode/capture target).
- Tests: EditMode `BushSceneTests` (scene-presence + scatter), PlayMode `BerryBushPlayModeTests` (harvest/regrow/eat-consume).
