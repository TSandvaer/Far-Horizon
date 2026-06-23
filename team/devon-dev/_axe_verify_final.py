"""Verify the EXPORTED final FBX (round-tripped) against the original 14d5a41:
  - head verts byte-identical (z > head_base in the ORIGINAL classification)
  - haft straight (residual bend ~0)
  - ratio ~2.12
  - 128 verts, single material slot, origin (0,0,0), long axis Z
Takes the original (pre-bake) FBX path as the 1st `--` arg."""
import bpy, sys, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ORIG = argv[0]
FINAL = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'
HEAD_BASE_Z = 0.022674

def load(fbx):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    return [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]

# original (14d5a41) — classify head verts by ORIGINAL z
o = load(ORIG)
oco = [v.co.copy() for v in o.data.vertices]
on = len(oco)
omat = len(o.data.materials)
# head verts in original, sorted by a stable key for matching
ohead = sorted([c for c in oco if c.z > HEAD_BASE_Z], key=lambda c: (round(c.z,5), round(c.x,5), round(c.y,5)))

# final
f = load(FINAL)
fco = [v.co.copy() for v in f.data.vertices]
fn = len(fco)
fmat = len(f.data.materials)
forigin = [round(x,6) for x in f.matrix_world.translation]
fhead = sorted([c for c in fco if c.z > HEAD_BASE_Z], key=lambda c: (round(c.z,5), round(c.x,5), round(c.y,5)))

print("VERIFY_FINAL_START")
print("orig verts=%d  final verts=%d  (%s)" % (on, fn, "MATCH" if on==fn else "DIFFER"))
print("orig head verts=%d  final head verts=%d" % (len(ohead), len(fhead)))
# byte-compare head (matched by sorted order)
maxd = 0.0
if len(ohead) == len(fhead):
    for a, b in zip(ohead, fhead):
        maxd = max(maxd, (a-b).length)
print("HEAD max vert delta (orig vs final, round-tripped) = %.8f  -> %s"
      % (maxd, "BYTE-IDENTICAL" if maxd < 1e-5 else ("near (jitter)" if maxd < 1e-4 else "CHANGED!")))
head_top_f = max(c.z for c in fhead)
print("HEAD top Z final = %.6f (expect 0.495347)" % head_top_f)

# ratio + bend on final
grip = min(c.z for c in fco)
haft_len = HEAD_BASE_Z - grip
head_lo = min(c.z for c in fhead); head_hi = max(c.z for c in fhead)
head_h = head_hi - head_lo
ratio = haft_len / head_h
haft = [c for c in fco if c.z <= HEAD_BASE_Z]
rings = {}
for c in haft: rings.setdefault(round(c.z,3), []).append(c)
hk = sorted(rings.keys())
def cent(k):
    p = rings[k]; return (sum(q.x for q in p)/len(p), sum(q.y for q in p)/len(p), k)
g = cent(hk[0]); t = cent(hk[-1])
seg = Vector((t[0]-g[0], t[1]-g[1], t[2]-g[2]))
bend = math.degrees(math.atan2(math.hypot(seg.x, seg.y), abs(seg.z)))
print("FINAL HAFT_LEN=%.4f HEAD_H=%.4f RATIO=%.4f  residual_bend=%.4f deg" % (haft_len, head_h, ratio, bend))
print("FINAL material slots=%d (expect 1)  origin=%s (expect [0,0,0])  long-axis-grip_z=%.6f head_top_z=%.6f"
      % (fmat, forigin, grip, head_hi))
print("VERIFY_FINAL_DONE")
