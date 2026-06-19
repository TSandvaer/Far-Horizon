import bpy, os, struct, zlib

# Palette v2 — adds a darker FLINT shade for the knapped-stone axe head depth.
# Slot order is APPEND-ONLY vs v1 so existing UVs stay valid; flint_dark is slot 9.
# flint_dark = #6E6A63 is a WORKING darker grey (desaturated, ~20% darker than
# rock_grey #8E8A82) — FLAGGED for Uma to finalize the exact hex (palette is her call).
PAL = [
    (0x7A,0x52,0x30),  # 0 haft_wood
    (0x5A,0x3B,0x22),  # 1 haft_shadow
    (0xA3,0x3B,0x30),  # 2 head_red        (kept in palette; axe no longer uses it)
    (0x7E,0x2C,0x24),  # 3 red_shadow
    (0x8C,0x93,0xA8),  # 4 blade_steel
    (0xE4,0xE2,0xDC),  # 5 edge_bevel
    (0xCF,0xC6,0xAD),  # 6 bone_fitting    (lashing option B)
    (0x7E,0x3A,0x3A),  # 7 grip_wrap       (lashing option A)
    (0x8E,0x8A,0x82),  # 8 rock_grey  = FLINT BASE
    (0x5C,0x58,0x53),  # 9 flint_dark = WORKING darker flint (~35% darker; clear knap
                       #               contrast). UMA: finalize the exact hex.
]
W = H = 128
BLOCK_W = 12   # 10 slots * 12 = 120 < 128
grid = [[list(PAL[0])+[255] for _ in range(W)] for _ in range(H)]
centers = {}
for idx,rgb in enumerate(PAL):
    x0 = idx*BLOCK_W + 1
    x1 = x0 + BLOCK_W - 2
    for y in range(H):
        for x in range(x0,x1):
            grid[y][x] = [rgb[0],rgb[1],rgb[2],255]
    centers[idx] = (x0+x1)/2.0

def write_png(path, grid, w, h):
    def chunk(typ, data):
        c = typ + data
        return struct.pack('>I', len(data)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for x in range(w):
            raw += bytes(grid[y][x])
    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(bytes(raw),9))
           + chunk(b'IEND', b''))
    open(path,'wb').write(png)

OUT = r'C:/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets/Art/Props/WeaponPack/weapon_palette.png'
write_png(OUT, grid, W, H)

img = bpy.data.images.get('weapon_palette')
if img is None:
    img = bpy.data.images.new('weapon_palette', width=W, height=H, alpha=True)
img.filepath = OUT; img.source = 'FILE'; img.reload()
img.colorspace_settings.name = 'sRGB'; img.pack()

scene = bpy.context.scene
scene['pal_u'] = [centers[i]/W for i in range(len(PAL))]
scene['pal_v'] = 0.5
print('PALETTE_V2_OK slots=%d centers_u=%s' % (len(PAL), [round(centers[i]/W,4) for i in range(len(PAL))]))
