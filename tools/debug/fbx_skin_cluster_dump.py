# fbx_skin_cluster_dump.py — RAW (no-importer) FBX skin-cluster / per-bone weight dumper.
#
# WHY: the sibling of fbx_rest_convention_diff.py. That tool reads bone REST orientations;
# this one reads the SKIN — which bone (Cluster/SubDeformer) owns which vertices and with
# what weight — straight out of the binary FBX via io_scene_fbx.parse_fbx (no Blender import,
# no axis compensation, no scene). Use it to ground-truth a skin-weight defect (e.g. "the
# right thumb geometry is bound to the index chain, not the thumb chain") WITHOUT importing
# into Unity or trusting a re-import.
#
# WHAT IT REPORTS: FBX binary version; Skin/Cluster counts; per-Cluster bone name (resolved
# via the OO Connections, since Cluster nodes are named generically), vertex count and weight
# sum; and — for any --token you pass — a union/overlap analysis (which verts a chain owns,
# and how many are shared between two chains). Bone tokens are the lowercased tail after the
# last ':' (e.g. 'righthandthumb1' from 'mixamorig:RightHandThumb1').
#
# RUN (headless):
#   "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background \
#       --python tools/debug/fbx_skin_cluster_dump.py -- <FILE.fbx> [token1 token2 ...]
# Example (the 86cau4za2 right-thumb defect):
#   ... fbx_skin_cluster_dump.py -- castaway_v4_rigged.fbx righthandthumb righthandindex
#
# NOTE ON SCOPE: this tool enumerates skin CLUSTER deformers only. It can prove a cluster is
# ABSENT, but it says NOTHING about the bone hierarchy — a zero-weight bone has no cluster yet
# still exists. Never read a missing cluster as a missing bone (the 86cavcy4u/86cau4za2 trap).
#
# Empirical precedent 86cavcy4u (Option-C FBX binary weight-edit spike): named the v4 right
# hand's SKIN as structurally asymmetric — right thumb has only thumb1+thumb2 clusters (NO
# righthandthumb3 CLUSTER; the thumb3 BONE exists on both sides with zero skin influence on the
# right — per 86cau4za2 QA-verified correction, comment 90150243345817), all 18 right-thumb
# verts co-owned by the right index chain, right index1 carrying 56 verts vs the left's 14
# (+42 index-only). That SKIN asymmetry is why the fix needs a cluster-MEMBERSHIP change, not a
# weight-VALUE overwrite.
import sys
try:
    import io_scene_fbx.parse_fbx as pf
except Exception:
    import bpy
    bpy.ops.preferences.addon_enable(module='io_scene_fbx')
    import io_scene_fbx.parse_fbx as pf

args = [a for a in sys.argv[sys.argv.index('--') + 1:]] if '--' in sys.argv else sys.argv[1:]
PATH = args[0]
TOKENS = args[1:]

def eid(e): return e.id.decode() if isinstance(e.id, bytes) else e.id
def find(elem, name): return [c for c in elem.elems if eid(c) == name]
def ps(p): return p.decode('utf-8', 'replace') if isinstance(p, bytes) else p

root, ver = pf.parse(PATH)
print(f"=== FBX binary version: {ver}  ({PATH})")

objs = find(root, 'Objects')[0]
conns = find(root, 'Connections')
conns = conns[0] if conns else None

model_name = {m.props[0]: ps(m.props[1]).split('\x00')[0] for m in find(objs, 'Model')}
deformers = [(d.props[0], ps(d.props[2]) if len(d.props) > 2 else '?', d) for d in find(objs, 'Deformer')]
skins = [d for d in deformers if d[1] == 'Skin']
clusters = [d for d in deformers if d[1] == 'Cluster']
print(f"=== Deformers: {len(skins)} Skin, {len(clusters)} Cluster")

c2p, p2c = {}, {}
if conns:
    for c in find(conns, 'C'):
        if ps(c.props[0]) == 'OO':
            c2p.setdefault(c.props[1], []).append(c.props[2])
            p2c.setdefault(c.props[2], []).append(c.props[1])

def cbone(cid):
    for par in c2p.get(cid, []):
        if par in model_name: return model_name[par]
    for ch in p2c.get(cid, []):
        if ch in model_name: return model_name[ch]
    return ''

def arr(elem, name):
    hits = find(elem, name)
    return hits[0].props[0] if hits and hits[0].props else None

bone_verts = {}
for cid, sub, d in clusters:
    tok = (cbone(cid) or ps(d.props[1])).lower().split(':')[-1].split('\x00')[0]
    idxs, wts = arr(d, 'Indexes'), arr(d, 'Weights')
    if idxs is not None and wts is not None:
        vd = {int(i): float(w) for i, w in zip(idxs, wts)}
        bone_verts[tok] = (set(vd), vd)
    else:
        bone_verts[tok] = (set(), {})

print(f"\n=== {len(bone_verts)} weighted clusters (bone : verts : weightsum)")
for tok in sorted(bone_verts):
    s, vd = bone_verts[tok]
    print(f"    {tok:30s} verts={len(s):5d} weightsum={sum(vd.values()):9.3f}")

def union(token):
    u = set()
    for k in bone_verts:
        if token in k:
            u |= bone_verts[k][0]
    return u

if len(TOKENS) >= 2:
    a, b = TOKENS[0], TOKENS[1]
    ua, ub = union(a), union(b)
    print(f"\n=== overlap  '{a}' vs '{b}'")
    print(f"    {a}: {len(ua)} verts   {b}: {len(ub)} verts")
    print(f"    shared (in BOTH): {len(ua & ub)}")
    print(f"    {a}-only: {len(ua - ub)}   {b}-only: {len(ub - ua)}")
