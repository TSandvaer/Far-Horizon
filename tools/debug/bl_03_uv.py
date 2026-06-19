import bpy, bmesh

obj = bpy.data.objects['wpn_axe_01']
me = obj.data
scene = bpy.context.scene
PU = list(scene['pal_u']); PV = float(scene['pal_v'])
HAFT_WOOD, HAFT_SHADOW, HEAD_RED, RED_SHADOW, BLADE_STEEL, EDGE_BEVEL, BONE, GRIP_WRAP, ROCK = range(9)

# ---- HEAD COLOR KNOB (Sponsor reconsidering red vs steel, 2026-06-19) ----------
# One switch. 'red' = locked spec (#A33B30 main / #7E2C24 shadow);
# 'steel' = #8C93A8 blade-steel main, #5A3B22 too dark -> reuse rock_grey for the
# steel shadow facets. Set via scene['head_variant'] before running; default red.
variant = scene.get('head_variant', 'red')
if variant == 'steel':
    HEAD_MAIN, HEAD_SHADOW = BLADE_STEEL, ROCK   # steel head, rock-grey shadow facets
else:
    HEAD_MAIN, HEAD_SHADOW = HEAD_RED, RED_SHADOW

if not me.uv_layers:
    me.uv_layers.new(name='UVMap')

bm = bmesh.new(); bm.from_mesh(me)
bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
uvlay = bm.loops.layers.uv.active or bm.loops.layers.uv.new()

# --- haft island via flood-fill from the lowest vertex ---
lowest = min(bm.verts, key=lambda v: v.co.z)
haft_verts = set(); stack=[lowest]
while stack:
    v = stack.pop()
    if v in haft_verts: continue
    haft_verts.add(v)
    for e in v.link_edges:
        ov = e.other_vert(v)
        if ov not in haft_verts: stack.append(ov)

def is_haft(f): return all(v in haft_verts for v in f.verts)

# Pre-identify the cutting-edge bevel strip: the rim (4-vert, |n.y|~0) head faces
# with the most-negative X centers. The reference shows the off-white strip along
# the whole cutting edge (4 facets), so take rim head-faces with cx below a cutoff.
head_rim = [f for f in bm.faces if not is_haft(f) and len(f.verts) == 4
            and abs(f.normal.y) < 0.5]
edge_strip = set(f for f in head_rim if f.calc_center_median().x < -0.30)

for f in bm.faces:
    n = f.normal; c = f.calc_center_median()
    if is_haft(f):
        slot = HAFT_SHADOW if n.z < -0.4 else HAFT_WOOD
    elif f in edge_strip:
        slot = EDGE_BEVEL
    elif n.z < -0.15 or (c.x > 0.18 and n.x > 0.3):
        slot = HEAD_SHADOW
    else:
        slot = HEAD_MAIN
    u = PU[slot]
    for loop in f.loops:
        loop[uvlay].uv = (u, PV)

bm.to_mesh(me); bm.free(); me.update()
print('UV_OK haft_verts=%d total=%d' % (len(haft_verts), len(me.vertices)))
