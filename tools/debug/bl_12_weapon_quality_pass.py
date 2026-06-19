"""
bl_12_weapon_quality_pass.py — QUALITY PASS on wpn_knife_01 / wpn_sword_01 /
wpn_spear_01 to bring them up to the look-locked wpn_axe_01 standard (236 tris,
modeled knapped facets). Sponsor soak of #100 flagged the trio as (a) too SMALL
and (b) UNDER-DETAILED vs the axe. (ticket 86cabh907 follow-up.)

THE GAP (live scene, 2026-06-19): axe=236 tris chunky hero; knife/sword/spear =
37/51/40 tris flat prisms reading as toothpicks beside it.

THIS PASS — raise all three to the axe's bar:
  1. SCALE UP — sword ~ real-sword length, spear long, knife a solid knife (not
     tiny). Judged against the axe (1.186 tall) + char_ref (0.9).
  2. DETAIL/FACET — match the board language (inspiration 21h07_20 sword,
     21h07_42 curved blade, 21h08_08 axe):
       - faceted blade with a raised CENTRAL SPINE RIDGE (lens/diamond cross-
         section) — NOT a flat slab; the centerline catches light, edge planes
         angle away.
       - full white EDGE-HIGHLIGHT RIM plane (modeled thin bevel strip UV'd to
         the 'edge' block) — the family signet.
       - chunky beveled CROSSGUARD with up-swept tips.
       - segmented wrapped GRIP (4-6 chunky faceted red blocks).
       - faceted POMMEL.
       - spear point = knapped STONE leaf with modeled flake-scar facets (like
         the axe head), thin edge rim ONLY at the tip.

SPONSOR LOCKS (held): knife+sword = STEEL blades; axe+spear = STONE/flint.
  sword = full one-side cutting edge tapering OFF before the crossguard.
  spear-tip = STONE/flint (flint-grey + dark-flint facets), NOT a white point.

Shared 'WeaponPalette' material; UV islands parked on palette blocks (one row at
v=0.031). Shade Smooth on whole mesh + Mark Sharp EVERY edge = faceted read
without vert blow-up (the axe is 220/220 sharp).

Reproducible: python tools/debug/blender_mcp_send.py code tools/debug/bl_12_weapon_quality_pass.py
"""
import bpy, bmesh, math, random
from mathutils import Vector

V = 0.031
U = {
    'haft':      0.031,
    'haftsh':    0.156,
    'steel':     0.406,
    'edge':      0.531,   # off-white chamfer plane — family signet
    'bone':      0.594,
    'redwrap':   0.719,
    'flint':     0.781,
    'darkflint': 0.906,
}

MAT = bpy.data.materials.get('WeaponPalette')
assert MAT is not None, "WeaponPalette material must exist (run the axe pipeline first)"


def new_mesh_obj(name):
    if name in bpy.data.objects:
        bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
    me = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def box(bm, cx, cy, cz, sx, sy, sz, taper_top=1.0):
    """axis-aligned box centred at (cx,cy,cz), half-extents sx,sy,sz; top ring scaled
    by taper_top (for chunky tapered grip segments / pommels). Returns nothing; faces
    are added straight to bm (winding fixed by recalc later)."""
    b = []
    for dz in (-1, 1):
        sc = taper_top if dz > 0 else 1.0
        for dy in (-1, 1):
            for dx in (-1, 1):
                b.append(bm.verts.new((cx + dx * sx * sc, cy + dy * sy * sc, cz + dz * sz)))
    quads = [
        (b[0], b[1], b[3], b[2]), (b[4], b[6], b[7], b[5]),
        (b[0], b[4], b[5], b[1]), (b[2], b[3], b[7], b[6]),
        (b[0], b[2], b[6], b[4]), (b[1], b[5], b[7], b[3]),
    ]
    for q in quads:
        try: bm.faces.new(q)
        except ValueError: pass


def faceted_blade(bm, z0, z1, ztip, sp0, ed0, sp1, ed1, tx, spine_bulge,
                  bevel_from_z, single_edge=True):
    """Build a faceted blade with a RAISED CENTRAL SPINE RIDGE (lens cross-section)
    and a modeled white EDGE-HIGHLIGHT RIM on the cutting edge.

    ORIENTATION (load-bearing): the blade's BROAD dimension (spine<->edge) runs
    along X so the broad face presents flat-on to the gameplay/render camera (which
    sits on -Y). THICKNESS runs along Y, with the central spine ridge bulging toward
    +Y/-Y (the lens crest catches light). The CUTTING EDGE is the -X side; the
    SPINE/back is +X. (Earlier version had width along Y -> blade rendered edge-on
    as a needle; this is the fix.)

      z0..z1 = blade body (two z-segments: root->mid->tip), ztip = point.
      sp* = spine(+X back) x at root/mid; ed* = edge(-X cutting) x at root/mid
            (ed* are negative; sp* positive).
      tx = broad-face half-thickness (Y) at the spine/edge rims; the ridge crest is
           tx+spine_bulge (the lens peak along Y).
      bevel_from_z = z where the white edge-rim begins (taper-off below it).
    """
    def quad(a, b, c, d):
        va = bm.verts.new(a); vb = bm.verts.new(b); vc = bm.verts.new(c); vd = bm.verts.new(d)
        try: bm.faces.new((va, vb, vc, vd))
        except ValueError: pass

    crest = tx + spine_bulge   # spine-ridge crest half-thickness in Y (the lens peak)

    # Two broad faces (±Y), each split spine->ridge and ridge->edge so the raised
    # centerline reads. Per z-segment: spine-rim quad, two plates per side, edge-rim.
    segs = [(z0, sp0, ed0, z1, sp1, ed1), (z1, sp1, ed1, ztip, 0.0, 0.0)]
    for (za, spa, eda, zb, spb, edb) in segs:
        # ridge centerline x (between spine & edge), biased toward spine slightly
        rxa = (spa * 0.55 + eda * 0.45)
        rxb = (spb * 0.55 + edb * 0.45)
        for sgn in (1, -1):           # ±Y broad faces
            T = sgn * tx
            C = sgn * crest
            # spine plate: spine-rim(+X) -> ridge crest
            quad((spa, T, za), (rxa, C, za), (rxb, C, zb), (spb, T, zb))
            # edge plate: ridge crest -> edge(-X)
            quad((rxa, C, za), (eda, T, za), (edb, T, zb), (rxb, C, zb))
        # spine rim band (+X back), between the two side spine verts
        quad((spa, tx, za), (spb, tx, zb), (spb, -tx, zb), (spa, -tx, za))
        # EDGE-HIGHLIGHT RIM (-X cutting side) — the white bevel plane.
        # tapers OFF below bevel_from_z (Sponsor lock for the sword): below that z
        # the edge rim is plain steel rim (same geometry, picked as steel by uv).
        quad((eda, tx, za), (eda, -tx, za), (edb, -tx, zb), (edb, tx, zb))
    # tip cap implied by converging quads (sp/ed -> 0 at ztip)
    return bevel_from_z


def finalize(obj, uv_assign, grip_to_origin_z=0.0):
    """weld dup corners, recalc normals out, Shade Smooth + Mark Sharp ALL, single
    WeaponPalette slot, UV-park each face on its palette block. grip_to_origin_z
    shifts geometry so the grip midpoint sits at z=0 (origin = grip)."""
    me = obj.data
    me.materials.clear(); me.materials.append(MAT)
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0008)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if abs(grip_to_origin_z) > 1e-9:
        for v in bm.verts:
            v.co.z -= grip_to_origin_z
    bm.to_mesh(me); bm.free()
    if not me.uv_layers:
        me.uv_layers.new(name='UVMap')
    uvl = me.uv_layers.active.data
    for p in me.polygons:
        p.use_smooth = True
    for e in me.edges:
        e.use_edge_sharp = True
    for p in me.polygons:
        c = Vector((0, 0, 0))
        for vi in p.vertices:
            c += me.vertices[vi].co
        c /= len(p.vertices)
        key = uv_assign(p.index, c)
        u = U[key]
        for li in p.loop_indices:
            uvl[li].uv = (u, V)
    me.update()


# =========================================================================
# KNIFE — a SOLID knife (was a tiny 0.46 stick @ 37 tris). Real bowie-ish:
# chunky faceted blade w/ central spine + full white edge rim, crossguard,
# 3-segment wrapped grip, faceted pommel. STEEL blade. ~0.62 tall built;
# grip midpoint -> origin in finalize.
# =========================================================================
def build_knife():
    obj = new_mesh_obj('wpn_knife_01')
    bm = bmesh.new()
    # pommel (bone) bottom
    box(bm, 0, 0, -0.020, 0.028, 0.026, 0.022, taper_top=0.7)
    # grip: 3 chunky wrapped segments (redwrap), stacked z = 0.0..0.135
    for zc in (0.025, 0.070, 0.115):
        box(bm, 0, 0, zc, 0.028, 0.026, 0.024, taper_top=1.0)
    # crossguard (bone) — wide in X (across the blade), beveled, at z~0.155
    box(bm, 0, 0, 0.155, 0.078, 0.024, 0.018, taper_top=0.85)
    bm.to_mesh(obj.data); bm.free()
    # blade: BROAD faceted blade w/ central spine, z=0.175..0.60. spine +X, edge -X.
    bm = bmesh.new(); bm.from_mesh(obj.data)
    faceted_blade(bm, z0=0.175, z1=0.45, ztip=0.60,
                  sp0=0.058, ed0=-0.058, sp1=0.044, ed1=-0.044,
                  tx=0.013, spine_bulge=0.014, bevel_from_z=0.18)
    bm.to_mesh(obj.data); bm.free()

    def uv_assign(i, c):
        if c.z < 0.005:                       return 'bone'     # pommel
        if c.z < 0.14:                        return 'redwrap'  # grip
        if c.z < 0.175:                       return 'bone'     # crossguard
        # blade: edge-rim faces have the most-negative X centroid (cutting side)
        if c.x < -0.040:                      return 'edge'
        return 'steel'
    finalize(obj, uv_assign, grip_to_origin_z=0.070)
    return obj


# =========================================================================
# SWORD — a real sword (was a thin 1.002 line @ 51 tris). Broad faceted blade
# w/ central fuller/spine ridge (diamond x-section) + full white edge rim ONE
# side tapering OFF before the guard, up-swept beveled crossguard, 5-segment
# wrapped grip, faceted pommel. STEEL blade. ~1.10 tall built.
# =========================================================================
def build_sword():
    obj = new_mesh_obj('wpn_sword_01')
    bm = bmesh.new()
    # pommel (bone) — chunky faceted block
    box(bm, 0, 0, -0.030, 0.034, 0.030, 0.030, taper_top=0.65)
    # grip: 5 wrapped segments z=0.0..0.225
    for zc in (0.020, 0.065, 0.110, 0.155, 0.200):
        box(bm, 0, 0, zc, 0.026, 0.024, 0.024)
    # crossguard (bone) — wide in X (across blade), up-swept flared tips, z~0.255
    box(bm, 0, 0, 0.255, 0.110, 0.022, 0.020, taper_top=1.28)
    bm.to_mesh(obj.data); bm.free()
    bm = bmesh.new(); bm.from_mesh(obj.data)
    # blade z=0.28..1.12, broad at root, gentle taper to tip. spine +X, edge -X.
    # Edge rim (white) tapers off below z=0.40 (Sponsor lock: full cutting edge
    # tapering off before the crossguard).
    faceted_blade(bm, z0=0.28, z1=0.94, ztip=1.12,
                  sp0=0.066, ed0=-0.066, sp1=0.048, ed1=-0.048,
                  tx=0.015, spine_bulge=0.020, bevel_from_z=0.40)
    bm.to_mesh(obj.data); bm.free()

    def uv_assign(i, c):
        if c.z < 0.0:                         return 'bone'     # pommel
        if c.z < 0.235:                       return 'redwrap'  # grip
        if c.z < 0.285:                       return 'bone'     # crossguard
        # full cutting edge ONE side (-X), tapering off before the guard:
        # edge-rim block only above z=0.40 reads 'edge'; below = steel rim.
        if c.x < -0.040 and c.z > 0.40:       return 'edge'
        return 'steel'
    finalize(obj, uv_assign, grip_to_origin_z=0.110)
    return obj


# =========================================================================
# SPEAR — long (was 1.30 thin pole @ 40 tris). Chunky 6-sided shaft + lashing
# band + a SUBSTANTIAL knapped STONE/flint leaf point (modeled flake-scar
# facets like the axe head; flint-grey + dark-flint), thin edge rim ONLY at the
# very tip. ~1.78 tall built. STONE tip (Sponsor lock).
# =========================================================================
def build_spear():
    obj = new_mesh_obj('wpn_spear_01')
    random.seed(21)
    bm = bmesh.new()
    # shaft: 6-sided chunky cylinder z=-0.55..1.05, radius ~0.028 (chunkier)
    r = 0.028; n = 6
    z_bot, z_top = -0.55, 1.05
    NSEG = 4
    rings = []
    for s in range(NSEG + 1):
        t = s / NSEG
        z = z_bot + t * (z_top - z_bot)
        bend = math.sin(t * math.pi) * 0.008   # gentle hand-made bend
        ring = [bm.verts.new((bend + r * math.cos(2 * math.pi * k / n + math.pi / n),
                              r * math.sin(2 * math.pi * k / n + math.pi / n), z))
                for k in range(n)]
        rings.append(ring)
    for s in range(NSEG):
        for k in range(n):
            a, b = rings[s][k], rings[s][(k + 1) % n]
            c, d = rings[s + 1][(k + 1) % n], rings[s + 1][k]
            try: bm.faces.new((a, b, c, d))
            except ValueError: pass
    try: bm.faces.new(list(reversed(rings[0])))
    except ValueError: pass
    bm.to_mesh(obj.data); bm.free()
    # lashing band (red wrap) where point binds to shaft z~1.02..1.13
    bm = bmesh.new(); bm.from_mesh(obj.data)
    box(bm, 0, 0, 1.075, 0.036, 0.036, 0.050)

    # KNAPPED STONE LEAF POINT — built like a mini axe head: broad faceted faces
    # w/ a central spine ridge + flake-scar relief, leaf silhouette, z=1.12..1.62.
    def quad(a, b, c, d):
        va = bm.verts.new(a); vb = bm.verts.new(b); vc = bm.verts.new(c); vd = bm.verts.new(d)
        try: bm.faces.new((va, vb, vc, vd))
        except ValueError: pass

    # ORIENTATION: leaf WIDTH (across the broad face) runs along X so the broad
    # face presents to the camera; THICKNESS + spine ridge along Y. (mirror of the
    # blade fix.)
    ty = 0.018          # broad-face half-thickness (Y) at rim
    crest = ty + 0.016  # spine ridge crest (Y)
    zb, zm1, zm2, zt = 1.12, 1.27, 1.45, 1.62
    w_base, w_mid, w_up = 0.034, 0.072, 0.044   # half-widths in X (leaf widens then tapers)
    prof = [(zb, w_base), (zm1, w_mid), (zm2, w_up), (zt, 0.0)]  # (z, half_width_X)
    for si in range(len(prof) - 1):
        za, wa = prof[si]; zc, wc = prof[si + 1]
        for sgn in (1, -1):           # ±Y broad faces
            T = sgn * ty; C = sgn * crest
            # +X side -> ridge crest plate
            quad((wa, T, za), (0.0, C, za), (0.0, C, zc), (wc, T, zc))
            # ridge crest -> -X side plate
            quad((0.0, C, za), (-wa, T, za), (-wc, T, zc), (0.0, C, zc))
        # +X rim
        quad((wa, ty, za), (wc, ty, zc), (wc, -ty, zc), (wa, -ty, za))
        # -X rim
        quad((-wa, ty, za), (-wa, -ty, za), (-wc, -ty, zc), (-wc, ty, zc))
    bm.to_mesh(obj.data); bm.free()

    # knapped flake-scar relief on the point's broad verts (like the axe head)
    me = obj.data
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0008)
    for v in bm.verts:
        if v.co.z > 1.14 and abs(v.co.x) > 0.020 and v.co.z < 1.58:
            s = 1.0 if v.co.y >= 0 else -1.0
            v.co.y += s * random.uniform(0.003, 0.011)
            v.co.z += random.uniform(-0.008, 0.008)
    bm.to_mesh(me); bm.free()

    def uv_assign(i, c):
        if 1.01 < c.z < 1.14 and (abs(c.x) > 0.030 or abs(c.y) > 0.030):
            return 'redwrap'   # lashing band
        if c.z < 1.12:
            return 'haft'      # shaft
        # STONE point: thin edge highlight ONLY at the very tip; broad plates
        # flint-grey; the X rim flake facets dark-flint (knapped read).
        if c.z > 1.55 and abs(c.x) < 0.022:
            return 'edge'
        if abs(c.x) > 0.044:
            return 'darkflint'
        return 'flint'
    finalize(obj, uv_assign, grip_to_origin_z=0.25)
    return obj


k = build_knife()
s = build_sword()
sp = build_spear()

# lineup beside the axe (axe at x=0). Space them by their widths.
bpy.data.objects['wpn_axe_01'].location = (0, 0, 0)
k.location  = (0.75, 0, 0)
s.location  = (1.45, 0, 0)
sp.location = (2.25, 0, 0)

def stats(o):
    me = o.data; tris = sum(len(p.vertices) - 2 for p in me.polygons)
    return f"{o.name}: verts={len(me.vertices)} faces={len(me.polygons)} tris={tris} dims={[round(v,4) for v in o.dimensions]}"
print("=== AXE (reference) ===")
print(stats(bpy.data.objects['wpn_axe_01']))
print("=== QUALITY PASS ===")
for o in (k, s, sp):
    print(stats(o))
print("BUILT_QUALITY_PASS")
