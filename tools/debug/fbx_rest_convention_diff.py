# fbx_rest_convention_diff.py — RAW (no-importer) FBX bone rest-orientation diff.
#
# WHY: measuring a rigged FBX's bone facing/rest via a Blender RE-IMPORT LIES — the importer
# axis-COMPENSATES per the file's declared GlobalSettings, so a wrong-convention export can still
# "read 0.000deg rest delta" inside Blender (character-pipeline.md §Step 3; the exact trap that let
# PR #330's helicopter regression pass its own verification). This reads the RAW FBX node tree via
# io_scene_fbx.parse_fbx — no importer, no axis compensation — and diffs each Model node's local rest
# TRS (Lcl Translation/Rotation/Scaling) OLD vs NEW, plus GlobalSettings + the FBX binary version.
#
# THE BUG CLASS IT CATCHES: a Mixamo-rigged character (all bone Lcl Rotation == 0, the zero-rest
# Generic convention the without-skin clips bind against; FBX binary version 7700) round-tripped
# through Blender's FBX exporter comes back with per-bone rest orientations re-derived from Blender's
# armature head/tail (limbs go non-zero: shoulders ~±120deg, feet ~±59deg, uplegs ~±180deg; version
# downgrades to 7400). The shared clips then apply absolute local rotations against the wrong rest =
# "helicopter over a T-pose". NEVER re-export a rigged character from Blender — re-rig via Mixamo.
#
# RUN (headless):
#   "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background \
#       --python tools/debug/fbx_rest_convention_diff.py -- <OLD.fbx> <NEW.fbx>
# Extract a committed FBX at a past SHA to a temp file with:  git show <sha>:<path> > OLD.fbx
#
# Empirical precedent: 86cau4za2 / PR #330 round-2 — this diff named the changed set as 33 of 42
# bones (whole skeleton), NOT the 9 RightHand bones the fix was scoped to, in one pass.
import sys
try:
    import io_scene_fbx.parse_fbx as pf
except Exception:
    import bpy
    bpy.ops.preferences.addon_enable(module='io_scene_fbx')
    import io_scene_fbx.parse_fbx as pf

OLD, NEW = sys.argv[-2], sys.argv[-1]

def eid(e):
    return e.id.decode() if isinstance(e.id, bytes) else e.id
def find(elem, name):
    return [c for c in elem.elems if eid(c) == name]
def ps(p):
    return p.decode() if isinstance(p, bytes) else p

def globalsettings(root):
    gs = {}
    for g in find(root, 'GlobalSettings'):
        for p70 in find(g, 'Properties70'):
            for P in find(p70, 'P'):
                gs[ps(P.props[0])] = P.props[4:]
    return gs

def model_locals(root):
    out = {}
    for objs in find(root, 'Objects'):
        for m in find(objs, 'Model'):
            name = ps(m.props[1]).split('\x00')[0]
            token = name.lower().split(':')[-1]
            T, R, S = [0.0]*3, [0.0]*3, [1.0]*3
            for p70 in find(m, 'Properties70'):
                for P in find(p70, 'P'):
                    k = ps(P.props[0])
                    if k == 'Lcl Translation': T = [float(x) for x in P.props[4:7]]
                    elif k == 'Lcl Rotation': R = [float(x) for x in P.props[4:7]]
                    elif k == 'Lcl Scaling': S = [float(x) for x in P.props[4:7]]
            out[token] = (tuple(T), tuple(R), tuple(S))
    return out

def load(p):
    return pf.parse(p)

for label, path in [('OLD', OLD), ('NEW', NEW)]:
    root, ver = load(path)
    gs = globalsettings(root)
    print(f"=== {label} FBX binary version {ver}  ({path})")
    for k in ['UpAxis','FrontAxis','CoordAxis','UnitScaleFactor']:
        v = gs.get(k)
        print(f"    {k} = {v[0] if v else None}")

mo = model_locals(load(OLD)[0]); mn = model_locals(load(NEW)[0])
print(f"\n=== MODEL COUNT  OLD {len(mo)}  NEW {len(mn)}")
if set(mo) ^ set(mn):
    print("  set diff — only OLD:", sorted(set(mo)-set(mn)), " only NEW:", sorted(set(mn)-set(mo)))

def close(a, b, tol=1e-3):
    return all(abs(x-y) <= tol for x, y in zip(a, b))

changed = 0
for tok in sorted(set(mo) & set(mn)):
    to, ro, so = mo[tok]; tn, rn, sn = mn[tok]
    dR, dT, dS = not close(ro, rn), not close(to, tn, 1e-2), not close(so, sn)
    if dR or dT or dS:
        changed += 1
        flags = ''.join(f for f, on in [('R', dR), ('T', dT), ('S', dS)] if on)
        print(f"  [{flags:3}] {tok}")
        if dR: print(f"        LclR OLD {tuple(round(x,2) for x in ro)}  NEW {tuple(round(x,2) for x in rn)}")
        if dT: print(f"        LclT OLD {tuple(round(x,3) for x in to)}  NEW {tuple(round(x,3) for x in tn)}")
        if dS: print(f"        LclS OLD {tuple(round(x,2) for x in so)}  NEW {tuple(round(x,2) for x in sn)}")
print(f"\n=== BONES/MODELS CHANGED: {changed} of {len(set(mo)&set(mn))}")
