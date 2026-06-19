# tools/debug — instrument registry

One-line index of durable debug/build instruments. Reuse before rebuilding.

| Tool | Purpose |
|---|---|
| `blender_mcp_send.py` | Send a command to the running BlenderMCP (ahujasid) addon over its TCP socket (localhost:9876). Modes: `code <file>` (execute_code), `scene` (get_scene_info), `shot <out.png>` (viewport screenshot). The transport for all Far Horizon Blender-MCP modeling when the `mcp__blender__*` tools aren't wired into the agent session. |
| `bl_01_setup.py` | Weapon-pack scene setup: clean scene, metric units, 1.8m char reference, writes the locked 9-hex `weapon_palette.png`, builds the shared `WeaponPalette` material. Run once per modeling session. |
| `bl_02_axe.py` | Builds the hero-axe geometry (haft + faceted red head matching inspiration `21h08_08`): hawk-beak poll w/ top notch, fanned cutting edge, grip-through-head. Shade Smooth + all-edges Mark Sharp. |
| `bl_03_uv.py` | Assigns palette-block UVs by island (haft flood-fill) + geometric edge-strip detection. **Head color knob:** `scene['head_variant']` in {`red`,`steel`} switches the head main/shadow palette slots (Sponsor reconsidering red vs steel, 2026-06-19). |
| `bl_04_finalize_export.py` | Merge-by-distance, recalc normals, set grip-midpoint origin (z=0.45), apply rot/scale (NOT location), export `wpn_axe_01.fbx` with spec-exact settings (-Y fwd / Z up / Normals Only / Apply-Transform OFF / triangulated). |
| `bl_05_hero_render.py` | Eevee hero 3/4 render of the axe (blade-left / beak-right framing) -> `axe_hero_render.png`. |
| `bl_06_save.py` | Saves the working `.blend` to `Assets/Art/Props/WeaponPack/weapon_set_src.blend`. |
| `bl_07_fix_palette.py` | Rewrites `weapon_palette.png` with TRUE sRGB hex bytes (manual PNG encoder, no PIL) so Unity reads the exact locked hexes; reloads + re-packs the Blender image datablock. |
| `bl_10_knife_sword_spear.py` | Builds the matched `wpn_knife_01` / `wpn_sword_01` / `wpn_spear_01` to the look-locked axe style (faceted, Shade Smooth + Mark-Sharp-all, shared `WeaponPalette`, UVs parked on palette blocks). Honors the Sponsor locks: spear-tip STONE/flint, sword edge full-length one-side tapering before the crossguard. Grip-origin at (0,0,0), blade +Z. |
| `bl_11_export_set.py` | Merge-by-distance + recalc-normals + zero-location + export `wpn_knife_01/wpn_sword_01/wpn_spear_01.fbx` to `Assets/Art/Props/WeaponPack/` with spec-exact FBX settings (-Y fwd / Z up / FBX-unit-scale / Apply-Transform OFF / Normals Only). |

Render outputs (`axe_*.png`, `_*.png`, `_*.py`, `*.blend1`) are gitignored build/throwaway artifacts.
