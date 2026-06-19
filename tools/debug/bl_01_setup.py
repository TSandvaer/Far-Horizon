import bpy, os

# --- 0. Clean scene -------------------------------------------------------
for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
for blk in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
    for b in list(blk):
        try: blk.remove(b)
        except: pass

# --- 1. Units -------------------------------------------------------------
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0

# --- 2. 1.8m character reference (a thin cube) ----------------------------
bpy.ops.mesh.primitive_cube_add(size=1.0, location=(1.2, 0, 0.9))
ref = bpy.context.active_object
ref.name = 'char_ref_1m8'
ref.scale = (0.18, 0.18, 0.9)   # 0.36 x 0.36 x 1.8 m
ref.display_type = 'WIRE'

# --- 3. Palette PNG (locked 9-slot hexes) ---------------------------------
# layout: a row of color blocks across a 128x128 image. Each block 12px wide.
PAL = [
    ('haft_wood',   (0x7A,0x52,0x30)),  # 0
    ('haft_shadow', (0x5A,0x3B,0x22)),  # 1
    ('head_red',    (0xA3,0x3B,0x30)),  # 2
    ('red_shadow',  (0x7E,0x2C,0x24)),  # 3
    ('blade_steel', (0x8C,0x93,0xA8)),  # 4
    ('edge_bevel',  (0xE4,0xE2,0xDC)),  # 5
    ('bone_fitting',(0xCF,0xC6,0xAD)),  # 6
    ('grip_wrap',   (0x7E,0x3A,0x3A)),  # 7
    ('rock_grey',   (0x8E,0x8A,0x82)),  # 8
]
def srgb_to_lin(c):
    c = c/255.0
    return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4

W = H = 128
img = bpy.data.images.new('weapon_palette', width=W, height=H, alpha=True)
px = [0.0]*(W*H*4)
# fill background with slot 0 so any stray UV still lands on wood
def putblock(x0, x1, rgb):
    lr, lg, lb = (srgb_to_lin(rgb[0]), srgb_to_lin(rgb[1]), srgb_to_lin(rgb[2]))
    for y in range(H):
        for x in range(x0, x1):
            i = (y*W + x)*4
            px[i]=lr; px[i+1]=lg; px[i+2]=lb; px[i+3]=1.0
# default whole image to wood
putblock(0, W, PAL[0][1])
# 9 blocks, each 14px wide starting at x=1 -> centers known
BLOCK_W = 14
centers_px = {}
for idx,(name,rgb) in enumerate(PAL):
    x0 = idx*BLOCK_W + 1
    x1 = x0 + BLOCK_W - 2
    putblock(x0, x1, rgb)
    centers_px[idx] = ((x0 + x1)/2.0)  # x pixel center; y center = H/2
img.pixels = px
img.pack()  # keep in .blend
img.colorspace_settings.name = 'sRGB'

# export PNG to disk (will copy into Unity WeaponPack later)
OUT_DIR = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack'
os.makedirs(OUT_DIR, exist_ok=True)
img.filepath_raw = os.path.join(OUT_DIR, 'weapon_palette.png')
img.file_format = 'PNG'
img.save()

# --- 4. Shared WeaponPalette material (flat: emission-ish via low spec) ----
mat = bpy.data.materials.new('WeaponPalette')
mat.use_nodes = True
nt = mat.node_tree
nt.nodes.clear()
out = nt.nodes.new('ShaderNodeOutputMaterial'); out.location=(400,0)
bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled'); bsdf.location=(100,0)
tex  = nt.nodes.new('ShaderNodeTexImage'); tex.location=(-300,0)
tex.image = img
tex.interpolation = 'Closest'   # crisp palette blocks, no bleed
bsdf.inputs['Roughness'].default_value = 1.0
bsdf.inputs['Metallic'].default_value = 0.0
nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])

# stash palette UV centers (normalized) as a custom scene prop for later scripts
scene['pal_u'] = [centers_px[i]/W for i in range(9)]
scene['pal_v'] = 0.5

print('SETUP_OK palette_centers_u=%s' % [round(centers_px[i]/W,4) for i in range(9)])
print('palette saved to', os.path.join(OUT_DIR, 'weapon_palette.png'))
