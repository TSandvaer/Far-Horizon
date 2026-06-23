import bpy, os, struct, zlib

# Locked palette — write TRUE sRGB byte values to the PNG so Unity (and any
# sRGB-sampling pipeline) reads the exact hexes. The previous version wrote
# linear floats under an sRGB tag -> darkened on disk. This writes the hex bytes.
PAL = [
    (0x7A,0x52,0x30),  # 0 haft_wood
    (0x5A,0x3B,0x22),  # 1 haft_shadow
    (0xA3,0x3B,0x30),  # 2 head_red
    (0x7E,0x2C,0x24),  # 3 red_shadow
    (0x8C,0x93,0xA8),  # 4 blade_steel
    (0xE4,0xE2,0xDC),  # 5 edge_bevel
    (0xCF,0xC6,0xAD),  # 6 bone_fitting
    (0x7E,0x3A,0x3A),  # 7 grip_wrap
    (0x8E,0x8A,0x82),  # 8 rock_grey
]
W = H = 128
BLOCK_W = 14
# build RGBA byte grid, default = wood
grid = [[list(PAL[0])+[255] for _ in range(W)] for _ in range(H)]
centers = {}
for idx,rgb in enumerate(PAL):
    x0 = idx*BLOCK_W + 1
    x1 = x0 + BLOCK_W - 2
    for y in range(H):
        for x in range(x0,x1):
            grid[y][x] = [rgb[0],rgb[1],rgb[2],255]
    centers[idx] = (x0+x1)/2.0

# 1) write a real sRGB PNG to disk via manual encoder (no PIL dependency)
def write_png(path, grid, w, h):
    def chunk(typ, data):
        c = typ + data
        return struct.pack('>I', len(data)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)
    raw = bytearray()
    for y in range(h):
        raw.append(0)  # filter type 0
        for x in range(w):
            raw += bytes(grid[y][x])
    sig = b'\x89PNG\r\n\x1a\n'
    ihdr = struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)  # 8-bit RGBA
    png = sig + chunk(b'IHDR', ihdr) + chunk(b'IDAT', zlib.compress(bytes(raw),9)) + chunk(b'IEND', b'')
    with open(path,'wb') as f: f.write(png)

OUT = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/weapon_palette.png'
write_png(OUT, grid, W, H)

# 2) reload it into the Blender image datablock + re-pack so the .blend/material match disk
img = bpy.data.images.get('weapon_palette')
if img:
    img.filepath = OUT
    img.source = 'FILE'
    img.reload()
    img.colorspace_settings.name = 'sRGB'
    img.pack()

print('PALETTE_FIXED wrote sRGB png to', OUT)
print('centers_u', [round(centers[i]/W,4) for i in range(9)])
