"""
bl_10_knife_sword_spear.py — model wpn_knife_01 / wpn_sword_01 / wpn_spear_01 to
MATCH the look-locked wpn_axe_01 style (ticket 86cabh907, Route A weapon set).

Style contract (Uma weapon-tool-style-spec §1-4 + blender-asset-pipeline doc):
  - chunky low-poly, faceted: Shade Smooth on whole mesh + Mark Sharp EVERY edge
    (the axe is 220/220 sharp, 96/96 smooth — the faceted read without vert blow-up).
  - ONE shared material 'WeaponPalette'; UV islands scaled tiny + parked on the
    palette colour block for each part (palette blocks are a single row at v~0.031,
    16px-wide; block centre u's measured from the live PNG):
        haft-wood      W1  u=0.031
        haft-shadow    W2  u=0.156
        blade-steel    W5  u=0.406
        edge-bevel     W6  u=0.531   (off-white chamfer plane — family signet)
        bone-fitting   W7  u=0.594
        grip-wrap-red  W8  u=0.719
        flint-grey     W3  u=0.781
        dark-flint     W4/W9 u=0.906
  - origin at GRIP MIDPOINT, blade pointing +Z (Blender) so the HeldTool rig + axis
    conversion match the axe (grip at world (0,0,0); geometry extends +Z up, haft -Z).
  - haft ~0.045 radius (chunky vs 1.8m char ref), gentle hand-made bend.
  - edge-bevel = MODELLED thin plane on the hero cutting edge, UV'd to W6 (not a line).

Sponsor LOCKS (this ticket): spear-tip = STONE/flint (W3/W9); sword edge = full
cutting edge ONE side, tapering off before the crossguard.

Reproducible: python tools/debug/blender_mcp_send.py code tools/debug/bl_10_knife_sword_spear.py
"""
import bpy, bmesh
from mathutils import Vector

V = 0.031  # palette row v
U = {  # palette block centre u
    'haft':    0.031,
    'haftsh':  0.156,
    'steel':   0.406,
    'edge':    0.531,
    'bone':    0.594,
    'redwrap': 0.719,
    'flint':   0.781,
    'darkflint': 0.906,
}

MAT = bpy.data.materials.get('WeaponPalette')
assert MAT is not None, "WeaponPalette material must exist (run the axe pipeline first)"


def finalize(obj, uv_assign):
    """Shade Smooth + Mark Sharp ALL edges; assign WeaponPalette; UV every face to its
    palette block per uv_assign(face_index, centroid)->block_key. Recalc normals out."""
    me = obj.data
    # single material slot
    me.materials.clear()
    me.materials.append(MAT)
    # recalc normals consistent (outside) via bmesh FIRST (this rewrites the mesh)
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    # NOW (after the rewrite) ensure uv layer + smooth/sharp + UV-park.
    if not me.uv_layers:
        me.uv_layers.new(name='UVMap')
    uvl = me.uv_layers.active.data
    for p in me.polygons:
        p.use_smooth = True
    for e in me.edges:
        e.use_edge_sharp = True
    # UV: park each face's loops on its palette block centre (tiny island = a dot)
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


def new_mesh_obj(name):
    if name in bpy.data.objects:
        old = bpy.data.objects[name]
        bpy.data.objects.remove(old, do_unlink=True)
    me = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def box(bm, cx, cy, cz, sx, sy, sz):
    """add an axis-aligned box centred at (cx,cy,cz) with half-extents sx,sy,sz; return its 8 verts."""
    verts = []
    for dz in (-1, 1):
        for dy in (-1, 1):
            for dx in (-1, 1):
                verts.append(bm.verts.new((cx+dx*sx, cy+dy*sy, cz+dz*sz)))
    # faces (winding handled by recalc later)
    # bottom z-, top z+, etc via the 8 indexed verts: order is [ (dz-1: dy-1 dx-1, dx+1; dy+1 dx-1,dx+1), (dz+1 ...) ]
    v = verts
    quads = [
        (v[0], v[1], v[3], v[2]),  # bottom
        (v[4], v[6], v[7], v[5]),  # top
        (v[0], v[4], v[5], v[1]),  # -y
        (v[2], v[3], v[7], v[6]),  # +y
        (v[0], v[2], v[6], v[4]),  # -x
        (v[1], v[5], v[7], v[3]),  # +x
    ]
    for q in quads:
        try: bm.faces.new(q)
        except ValueError: pass
    return verts


# =========================================================================
# KNIFE — short single blade + stubby grip. blade-steel + edge-bevel + haft grip.
# overall ~0.42 tall; grip at z=0, blade up.
# =========================================================================
def build_knife():
    obj = new_mesh_obj('wpn_knife_01')
    bm = bmesh.new()
    # grip: stubby box from z=-0.15..0 (haft-wood), chunky
    box(bm, 0,0,-0.075, 0.024, 0.020, 0.075)
    # bolster (small bone fitting between grip + blade)
    box(bm, 0,0,0.014, 0.028, 0.022, 0.016)
    # blade body: a tapering wedge, z=0.026..0.30, widening then tapering to a point.
    # build as a strip of quads narrowing in +y (back/spine to edge) — single-edge knife.
    # spine on +y side, cutting edge on -y side. tip near z=0.30.
    bm.verts.ensure_lookup_table()
    # blade silhouette in YZ, thin in X (faceted slab). define profile rings.
    # ring: (y_spine, y_edge) at each z; thickness +-x
    rings = [
        (0.026, 0.030, -0.018),
        (0.030, 0.045, -0.030),
        (0.300, 0.030, -0.010),  # tip converges
    ]
    # simpler: model the blade as two faceted slabs (body + edge-bevel plane).
    tx = 0.013  # blade half-thickness (chunkier)
    def quad(a,b,c,d):
        va=bm.verts.new(a); vb=bm.verts.new(b); vc=bm.verts.new(c); vd=bm.verts.new(d)
        try: bm.faces.new((va,vb,vc,vd))
        except ValueError: pass
        return va,vb,vc,vd
    zr, zm, zt = 0.030, 0.150, 0.310   # root, mid, tip
    # blade as a faceted prism: two large side faces + spine + edge bevel + tip.
    # side +x
    quad(( tx, 0.032,zr),( tx,-0.026,zr),( tx,-0.034,zm),( tx, 0.038,zm))
    quad(( tx, 0.038,zm),( tx,-0.034,zm),( tx,-0.004,zt),( tx,-0.004,zt))
    # side -x
    quad((-tx, 0.032,zr),(-tx, 0.038,zm),(-tx,-0.034,zm),(-tx,-0.026,zr))
    quad((-tx, 0.038,zm),(-tx,-0.004,zt),(-tx,-0.034,zm),(-tx,-0.004,zt))
    # spine (+y) rim
    quad(( tx, 0.032,zr),( tx, 0.038,zm),(-tx, 0.038,zm),(-tx, 0.032,zr))
    quad(( tx, 0.038,zm),( tx,-0.004,zt),(-tx,-0.004,zt),(-tx, 0.038,zm))
    # EDGE-BEVEL plane on the cutting (-y) side (its own faces -> UV to edge-bevel)
    quad(( tx,-0.026,zr),(-tx,-0.026,zr),(-tx,-0.034,zm),( tx,-0.034,zm))
    quad(( tx,-0.034,zm),(-tx,-0.034,zm),(-tx,-0.004,zt),( tx,-0.004,zt))
    bm.to_mesh(obj.data); bm.free()

    def uv_assign(i, c):
        if c.z < 0.0:        # grip
            return 'haft'
        if c.z < 0.030:      # bolster
            return 'bone'
        # blade: edge-bevel faces are the ones whose centroid y is most negative (cutting side)
        if c.y < -0.020:
            return 'edge'
        return 'steel'
    finalize(obj, uv_assign)
    return obj


# =========================================================================
# SWORD — long blade + crossguard + wrapped grip + pommel. ~0.95 tall.
# blade-steel + edge-bevel (full cutting edge ONE side tapering before guard),
# bone-fitting guard/pommel, grip-wrap-red grip.
# =========================================================================
def build_sword():
    obj = new_mesh_obj('wpn_sword_01')
    bm = bmesh.new()
    tx = 0.012
    # pommel (bone) at very bottom
    box(bm, 0,0,-0.20, 0.026,0.022,0.022)
    # grip-wrap (red) from z=-0.175..-0.03
    box(bm, 0,0,-0.10, 0.020,0.018,0.075)
    # crossguard (bone) — wide in y, at z=-0.02
    box(bm, 0,0,-0.015, 0.075,0.024,0.018)
    bm.to_mesh(obj.data); bm.free()
    # blade as faceted prism from z=0.005..0.78, single cutting edge on -y, spine +y.
    bm = bmesh.new(); bm.from_mesh(obj.data)
    def quad(a,b,c,d):
        va=bm.verts.new(a); vb=bm.verts.new(b); vc=bm.verts.new(c); vd=bm.verts.new(d)
        try: bm.faces.new((va,vb,vc,vd))
        except ValueError: pass
    z0, z1, ztip = 0.005, 0.62, 0.78
    sp0, ed0 = 0.040, -0.040   # near guard: full width
    sp1, ed1 = 0.030, -0.030   # mid
    # side +x  (two segments + tip)
    quad(( tx,sp0,z0),( tx,ed0,z0),( tx,ed1,z1),( tx,sp1,z1))
    quad(( tx,sp1,z1),( tx,ed1,z1),( tx,0.0,ztip),( tx,0.0,ztip))
    # side -x
    quad((-tx,sp0,z0),(-tx,sp1,z1),(-tx,ed1,z1),(-tx,ed0,z0))
    quad((-tx,sp1,z1),(-tx,0.0,ztip),(-tx,ed1,z1),(-tx,0.0,ztip))
    # spine (+y)
    quad(( tx,sp0,z0),( tx,sp1,z1),(-tx,sp1,z1),(-tx,sp0,z0))
    quad(( tx,sp1,z1),( tx,0.0,ztip),(-tx,0.0,ztip),(-tx,sp1,z1))
    # EDGE-BEVEL (-y cutting side) — tapers OFF before the guard: start it a touch above z0
    ze = 0.10  # bevel starts above the guard (taper off before crossguard, Sponsor lock)
    quad(( tx,ed0+0.006,ze),(-tx,ed0+0.006,ze),(-tx,ed1,z1),( tx,ed1,z1))
    quad(( tx,ed1,z1),(-tx,ed1,z1),(-tx,0.0,ztip),( tx,0.0,ztip))
    # fill the small no-bevel root region of the edge with steel side rim
    quad(( tx,ed0,z0),(-tx,ed0,z0),(-tx,ed0+0.006,ze),( tx,ed0+0.006,ze))
    bm.to_mesh(obj.data); bm.free()

    def uv_assign(i, c):
        if c.z < -0.18:  return 'bone'      # pommel
        if c.z < -0.03:  return 'redwrap'   # grip wrap
        if c.z < 0.005:  return 'bone'      # crossguard
        if c.y < -0.030 and c.z > 0.09:  return 'edge'  # cutting-edge bevel (above taper-off)
        return 'steel'
    finalize(obj, uv_assign)
    return obj


# =========================================================================
# SPEAR — long shaft + compact STONE/flint point (Sponsor lock). ~1.15 tall.
# haft-wood shaft + haft-shadow + flint-grey point (W3) + dark-flint facets (W9)
# + edge-bevel on the point's hero edges + a small red-wrap lashing band.
# =========================================================================
def build_spear():
    obj = new_mesh_obj('wpn_spear_01')
    bm = bmesh.new()
    # shaft: 6-sided chunky cylinder, z=-0.32..0.66 (grip mid at 0), radius ~0.024
    import math
    r = 0.024; n = 6
    z_bot, z_top = -0.32, 0.66
    ring_b = [bm.verts.new((r*math.cos(2*math.pi*k/n), r*math.sin(2*math.pi*k/n), z_bot)) for k in range(n)]
    ring_t = [bm.verts.new((r*math.cos(2*math.pi*k/n), r*math.sin(2*math.pi*k/n), z_top)) for k in range(n)]
    for k in range(n):
        a,b = ring_b[k], ring_b[(k+1)%n]
        c,d = ring_t[(k+1)%n], ring_t[k]
        try: bm.faces.new((a,b,c,d))
        except ValueError: pass
    # cap bottom
    try: bm.faces.new(list(reversed(ring_b)))
    except ValueError: pass
    bm.to_mesh(obj.data); bm.free()
    # lashing band (red wrap) where point binds to shaft: a slightly fatter ring box z~0.63..0.70
    bm = bmesh.new(); bm.from_mesh(obj.data)
    box(bm, 0,0,0.665, 0.030,0.030,0.034)
    # flint point: a faceted leaf-shaped STONE tip (Sponsor lock) z=0.69..0.98, widest mid,
    # tapering to a point. Reads STONE (flint-grey body + dark-flint struck facets) with only a
    # THIN edge-bevel rim on the very tip — NOT a white point.
    def quad(a,b,c,d):
        va=bm.verts.new(a); vb=bm.verts.new(b); vc=bm.verts.new(c); vd=bm.verts.new(d)
        try: bm.faces.new((va,vb,vc,vd))
        except ValueError: pass
    tx = 0.016
    zb, zm, zt = 0.69, 0.83, 0.98
    wm = 0.050  # mid half-width (in y)
    # +x plate split into two facets (lower wide -> upper taper) for a knapped read
    quad(( tx,-wm,zm),( tx,0,zb),( tx,wm,zm),( tx,wm,zm))   # lower +x (triangle-ish)
    quad(( tx,-wm,zm),( tx,wm,zm),( tx,0,zt),( tx,0,zt))    # upper +x taper to tip
    # -x plate
    quad((-tx,0,zb),(-tx,-wm,zm),(-tx,wm,zm),(-tx,wm,zm))
    quad((-tx,wm,zm),(-tx,-wm,zm),(-tx,0,zt),(-tx,0,zt))
    # +y edge rim
    quad(( tx,0,zb),( tx,wm,zm),(-tx,wm,zm),(-tx,0,zb))
    quad(( tx,wm,zm),( tx,0,zt),(-tx,0,zt),(-tx,wm,zm))
    # -y edge rim
    quad(( tx,0,zb),(-tx,0,zb),(-tx,-wm,zm),( tx,-wm,zm))
    quad(( tx,-wm,zm),(-tx,-wm,zm),(-tx,0,zt),( tx,0,zt))
    bm.to_mesh(obj.data); bm.free()

    def uv_assign(i, c):
        # lashing band box (centred ~0.665, fatter in x/y than the shaft)
        if 0.62 < c.z < 0.71 and (abs(c.x) > 0.026 or abs(c.y) > 0.026 or 0.63 < c.z < 0.70):
            return 'redwrap'
        if c.z < 0.69:
            return 'haft'
        # STONE point (flint): only the upper-taper rim facets (near the tip, z>0.90) read edge;
        # the broad mid plates read flint-grey; the spine/edge rims at mid read dark-flint (knapped).
        if c.z > 0.93 and abs(c.y) < 0.02:
            return 'edge'        # the very tip catches a thin highlight
        if abs(c.y) > 0.035:
            return 'darkflint'   # struck-flake facet shadow on the rims
        return 'flint'           # broad stone plates
    finalize(obj, uv_assign)
    return obj


k = build_knife()
s = build_sword()
sp = build_spear()

# lay them out beside the axe for the family check (axe at x=0; knife/sword/spear to the right)
bpy.data.objects['wpn_axe_01'].location = (0,0,0)
k.location  = (0.9, 0, 0)
s.location  = (1.7, 0, 0)
sp.location = (2.5, 0, 0)

def stats(o):
    me=o.data; tris=sum(len(p.vertices)-2 for p in me.polygons)
    return f"{o.name}: verts={len(me.vertices)} faces={len(me.polygons)} tris={tris} dims={[round(v,3) for v in o.dimensions]}"
for o in (k,s,sp):
    print(stats(o))
print("BUILT_SET")
