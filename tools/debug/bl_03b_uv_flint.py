import bpy, bmesh

obj = bpy.data.objects['wpn_axe_01']
me = obj.data
scene = bpy.context.scene
PU = list(scene['pal_u']); PV = float(scene['pal_v'])
# 10-slot palette
HAFT_WOOD, HAFT_SHADOW, HEAD_RED, RED_SHADOW, BLADE_STEEL, EDGE_BEVEL, BONE, GRIP_WRAP, FLINT, FLINT_DARK = range(10)

if not me.uv_layers:
    me.uv_layers.new(name='UVMap')

bm = bmesh.new(); bm.from_mesh(me)
bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
uvlay = bm.loops.layers.uv.active or bm.loops.layers.uv.new()

# --- haft island via flood-fill from lowest vert (haft + lashing are separate from head) ---
lowest = min(bm.verts, key=lambda v: v.co.z)
haft_verts=set(); stack=[lowest]
while stack:
    v=stack.pop()
    if v in haft_verts: continue
    haft_verts.add(v)
    for e in v.link_edges:
        ov=e.other_vert(v)
        if ov not in haft_verts: stack.append(ov)
def is_haft(f): return all(v in haft_verts for v in f.verts)

# --- lashing island: enumerate ALL connected components; the lashing is the
# component (excluding the haft) that is a small-radius cylinder centered on the
# haft axis at z~0.77 (NOT the big head). Pick by min radius + tight z-band. ---
def component(seed, seen):
    out=set(); stack=[seed]
    while stack:
        v=stack.pop()
        if v in out: continue
        out.add(v)
        for e in v.link_edges:
            ov=e.other_vert(v)
            if ov not in out and ov not in seen: stack.append(ov)
    return out
seen=set(haft_verts)
comps=[]
for v in bm.verts:
    if v in seen: continue
    c=component(v, seen)
    seen |= c
    comps.append(c)
def comp_score(c):
    zs=[v.co.z for v in c]; rs=[(v.co.x**2+v.co.y**2)**0.5 for v in c]
    import statistics
    zc=sum(zs)/len(zs); rmax=max(rs)
    # lashing: tight radius (<0.085) and z near 0.77; head has rmax up to ~0.4
    return (rmax, abs(zc-0.77))
lash_verts=set()
nonhaft=[c for c in comps]
if nonhaft:
    # lashing = the non-head component: smallest rmax
    cand=sorted(nonhaft, key=lambda c: comp_score(c))
    # only accept if it's genuinely small-radius (a cylinder), else there's no lash
    if cand and max((v.co.x**2+v.co.y**2)**0.5 for v in cand[0]) < 0.090:
        lash_verts=cand[0]
def is_lash(f): return len(f.verts)>0 and all(v in lash_verts for v in f.verts)

for f in bm.faces:
    n=f.normal; c=f.calc_center_median()
    if is_lash(f):
        slot = GRIP_WRAP                      # leather lashing binding
    elif is_haft(f):
        slot = HAFT_SHADOW if n.z < -0.4 else HAFT_WOOD
    else:
        # KNAPPED FLINT head: the flake-scars must read as a PATTERN, so adjacent facets
        # alternate between the two flint shades (a mottled knapped surface), biased so
        # down/back facets trend darker (depth) but neighbours still contrast. The 2-shade
        # mottle across the modeled facets IS the knapped pattern (geometry + palette only).
        c2 = f.calc_center_median()
        # deterministic per-facet hash from the face centroid -> stable mottle
        h = int(abs(c2.x*131.7 + c2.z*97.3 + c2.y*53.1)*1000) % 7
        depth_bias = 1 if (n.z < -0.05 or n.x > 0.25) else 0   # undersides/back lean dark
        slot = FLINT_DARK if ((h % 2) ^ 0) == 1 or depth_bias else FLINT
    u=PU[slot]
    for loop in f.loops:
        loop[uvlay].uv=(u,PV)

bm.to_mesh(me); bm.free(); me.update()
print('UV_FLINT haft=%d lash=%d head=%d' % (
    len(haft_verts), len(lash_verts), len(me.vertices)-len(haft_verts)-len(lash_verts)))
