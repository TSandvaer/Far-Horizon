"""Measure the FINAL FBX's head height via the SAME radial-width method WeaponPackAssetGen
uses (verts with off-long-axis radial > 0.10 mesh-u). The head is byte-locked, so this is a
constant. Print headH so we can set HeroAxeTargetHeadHeightU = 1.05781 * headH (which makes
the import globalScale come out to the head-PRESERVING 1.05781 = the old approved scale)."""
import bpy, math
FINAL = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FINAL)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
co = [v.co.copy() for v in obj.data.vertices]
n = len(co)
xs=[c.x for c in co]; ys=[c.y for c in co]; zs=[c.z for c in co]
ext=(max(xs)-min(xs), max(ys)-min(ys), max(zs)-min(zs))
la = 0 if (ext[0]>=ext[1] and ext[0]>=ext[2]) else (1 if ext[1]>=ext[2] else 2)
o0=(la+1)%3; o1=(la+2)%3
def comp(v,a): return (v.x,v.y,v.z)[a]
c0=(min(comp(c,o0) for c in co)+max(comp(c,o0) for c in co))*0.5
c1=(min(comp(c,o1) for c in co)+max(comp(c,o1) for c in co))*0.5
TH=0.10
lo=1e9; hi=-1e9
for c in co:
    d0=comp(c,o0)-c0; d1=comp(c,o1)-c1
    if math.hypot(d0,d1)>TH:
        lc=comp(c,la); lo=min(lo,lc); hi=max(hi,lc)
headH = hi-lo
print("HEAD_RADIAL_H_START")
print("long_axis=%s  radial>%.2f head span: lo=%.5f hi=%.5f  headH=%.5f" % ("XYZ"[la],TH,lo,hi,headH))
print("=> HeroAxeTargetHeadHeightU should be 1.05781 * %.5f = %.5f" % (headH, 1.05781*headH))
print("HEAD_RADIAL_H_DONE")
