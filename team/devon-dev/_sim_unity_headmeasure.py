"""Simulate MeasureImportedModelHeadHeight (the C# logic) on the FINAL FBX to confirm it
returns headH ~= 0.4453 -> globalScale = 0.4710 / headH ~= 1.0578 (head-preserving).
Mirrors the C# exactly: long axis = widest bounds axis, find blade TIP by wider off-axis
spread near an end, junction = tip -/+ 0.47267, head = verts on the tip side of junction."""
import bpy
FINAL = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/wpn_axe_01.fbx'
HEAD_FROM_TIP = 0.47267
TARGET = 0.4710
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FINAL)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
v = [tuple(x.co) for x in obj.data.vertices]
n=len(v)
def comp(p,a): return p[a]
bmin=[min(comp(p,a) for p in v) for a in range(3)]
bmax=[max(comp(p,a) for p in v) for a in range(3)]
ext=[bmax[a]-bmin[a] for a in range(3)]
la = 0 if (ext[0]>=ext[1] and ext[0]>=ext[2]) else (1 if ext[1]>=ext[2] else 2)
o0=(la+1)%3; o1=(la+2)%3
loEnd=bmin[la]; hiEnd=bmax[la]; span=max(1e-4,hiEnd-loEnd); band=span*0.15
def spread(endc):
    sel=[p for p in v if abs(comp(p,la)-endc)<=band]
    if not sel: return 0
    return max(max(comp(p,o0) for p in sel)-min(comp(p,o0) for p in sel),
               max(comp(p,o1) for p in sel)-min(comp(p,o1) for p in sel))
tip = hiEnd if spread(hiEnd)>=spread(loEnd) else loEnd
dirn = -1 if tip==hiEnd else 1
junc = tip + dirn*HEAD_FROM_TIP
head=[p for p in v if (comp(p,la)>=junc if dirn==-1 else comp(p,la)<=junc)]
hlo=min(comp(p,la) for p in head); hhi=max(comp(p,la) for p in head)
headH=hhi-hlo
gs = TARGET/headH if headH>1e-4 else 0
print("SIM_HEADMEASURE_START")
print("long_axis=%s tip=%.5f dir=%d junctionCoord=%.5f  head verts=%d/%d  headH=%.5f"
      % ("XYZ"[la], tip, dirn, junc, len(head), n, headH))
print("=> globalScale = %.5f / %.5f = %.5f   (head-preserving target 1.05781)" % (TARGET, headH, gs))
print("=> resulting total long-axis = %.5f * %.5f = %.5f u" % (span, gs, span*gs))
print("SIM_HEADMEASURE_DONE")
