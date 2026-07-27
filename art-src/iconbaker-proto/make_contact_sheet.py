#!/usr/bin/env python3
"""
Build the IconBaker candidate CONTACT SHEET for ticket 86camyvwn (Sponsor judges this live).

Input : the PNGs written by FarHorizon.EditorTools.IconBakerProto.BakeCandidates in this directory.
Output: contact-sheet.png in this directory.

Three sections:
  1. 64px bake, 4x NEAREST-NEIGHBOUR  — judge silhouette + facet read at the honest slot resolution.
  2. 128px reference bake, 2x NEAREST  — judge detail headroom (see the 52px note below).
  3. REAL Pack-slot simulation, 1:1    — each candidate drawn at its true on-screen size inside a
     faithful slot well, alongside the CURRENT shipped state (the "I" letter-chip) for both iron items.

Slot facts taken from source, not invented:
  Assets/UI/InventoryPanel.uss:77-78   .slot        width/height 64px
  Assets/UI/InventoryPanel.uss:89-92   .slot        border radius 8px
  Assets/UI/InventoryPanel.uss:142-148 .slot__icon  inset 6px on all sides -> the icon renders at 52x52
  Assets/UI/InventoryPalette.uss:12    --panel-walnut #2A2320
  Assets/UI/InventoryPalette.uss:13    --panel-edge   #5A4632
  Assets/UI/InventoryPalette.uss:14    --slot-empty   #3A302A
  Assets/UI/InventoryPalette.uss:17    --ink-cream    #EAD9B8
"""
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))

SUBJECTS = [
    ("iron_ore_pile",      "iron_ore  S1", "looted OrePile cluster, AS SHIPPED (grey rock mat)"),
    ("iron_ore_pile_rust", "iron_ore  S2", "same pile mesh + the shipped RUST vein mat"),
    ("iron_ore_veined",    "iron_ore  S3", "the in-world ore NODE: grey rock + 3 rust veins"),
    ("iron_ingot_proto",   "iron_ingot S4", "PROTOTYPE stand-in bar (no ingot mesh exists)"),
]
VARIANTS = [
    ("A_hero34_keyrim_bgNone", "A  3/4 hero, key+rim", "TRANSPARENT"),
    ("B_hero34_keyrim_bgWell", "B  3/4 hero, key+rim", "slot-well chip #3A302A"),
    ("C_side_flat_bgNone",     "C  SIDE PROFILE, flat", "TRANSPARENT (silhouette check)"),
    ("D_hero34_keyrim_bgWarm", "D  3/4 hero, key+rim", "warm chip #5A4632"),
]

WALNUT = (0x2A, 0x23, 0x20, 255)
EDGE = (0x5A, 0x46, 0x32, 255)
WELL = (0x3A, 0x30, 0x2A, 255)
CREAM = (0xEA, 0xD9, 0xB8, 255)
DIM = (0x9C, 0x90, 0x7A, 255)

CELL = 256
GUT = 20
LABEL_H = 46      # two lines of column header
ROWLBL_W = 400    # row-label gutter; subject label wraps onto two lines inside it


def font(size, bold=False):
    for name in (("segoeuib.ttf", "arialbd.ttf") if bold else ("segoeui.ttf", "arial.ttf")):
        try:
            return ImageFont.truetype("C:/Windows/Fonts/" + name, size)
        except OSError:
            pass
    return ImageFont.load_default()


F_TITLE = font(26, True)
F_SEC = font(20, True)
F_LBL = font(15)
F_SMALL = font(13)


def checker(size, a=(72, 72, 78), b=(96, 96, 102), step=16):
    """Neutral checkerboard so a TRANSPARENT candidate's cutout is visibly a cutout."""
    im = Image.new("RGBA", (size, size), a + (255,))
    d = ImageDraw.Draw(im)
    for y in range(0, size, step):
        for x in range(0, size, step):
            if ((x // step) + (y // step)) % 2:
                d.rectangle([x, y, x + step - 1, y + step - 1], fill=b + (255,))
    return im


def cell_image(path, scale):
    """Nearest-neighbour upscale onto a checkerboard so alpha reads honestly."""
    src = Image.open(path).convert("RGBA")
    up = src.resize((src.width * scale, src.height * scale), Image.NEAREST)
    base = checker(up.width)
    base.alpha_composite(up)
    return base


def rounded_mask(size, radius):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, size[0] - 1, size[1] - 1], radius=radius, fill=255)
    return m


def slot_well(icon=None, chip_text=None, selected=False):
    """A faithful 64x64 Pack slot: rounded well + 1px rim + the icon at its real 52x52 inner size."""
    w = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    d = ImageDraw.Draw(w)
    rim = (0xE8, 0xB2, 0x5C, 255) if selected else EDGE
    d.rounded_rectangle([0, 0, 63, 63], radius=8, fill=WELL, outline=rim, width=1)
    if icon is not None:
        inner = icon.resize((52, 52), Image.LANCZOS)   # scale-to-fit into the 6px-inset icon element
        w.alpha_composite(inner, (6, 6))
    if chip_text:
        f = font(30, True)
        bb = d.textbbox((0, 0), chip_text, font=f)
        d.text(((64 - (bb[2] - bb[0])) / 2 - bb[0], (64 - (bb[3] - bb[1])) / 2 - bb[1]),
               chip_text, font=f, fill=CREAM)
    return w


SLOT_PITCH = 84


def build():
    W = ROWLBL_W + len(VARIANTS) * (CELL + GUT) + GUT
    sec_grid_h = LABEL_H + len(SUBJECTS) * (CELL + GUT) + GUT
    strip_h = 52 + len(SUBJECTS) * (64 + 30) + 40
    H = 84 + 2 * (34 + sec_grid_h + 30) + 34 + strip_h + 40

    sheet = Image.new("RGBA", (W, H), (26, 24, 24, 255))
    d = ImageDraw.Draw(sheet)

    d.text((GUT, 14), "IconBaker PROTOTYPE candidates - ticket 86camyvwn (iron_ore vs iron_ingot)",
           font=F_TITLE, fill=CREAM)
    d.text((GUT, 48), "Baked headless from the ACTUAL props: Unity -batchmode (no -nographics), "
                      "device=Direct3D12, URP SubmitRenderRequest -> offscreen RT readback. "
                      "No windowed capture. Grey checker = transparent alpha.",
           font=F_SMALL, fill=DIM)
    y = 84

    for section, (size, scale) in enumerate([(64, 4), (128, 2)]):
        d.text((GUT, y), f"SECTION {section + 1}  -  {size}px bake shown at {scale}x nearest-neighbour",
               font=F_SEC, fill=CREAM)
        yy = y + 34 + LABEL_H
        for ci, (vid, l1, l2) in enumerate(VARIANTS):
            x = ROWLBL_W + ci * (CELL + GUT)
            d.text((x, yy - LABEL_H + 2), l1, font=F_LBL, fill=CREAM)
            d.text((x, yy - LABEL_H + 22), l2, font=F_SMALL, fill=DIM)
        for sid, stag, sdesc in SUBJECTS:
            d.text((GUT, yy + CELL // 2 - 20), stag, font=F_LBL, fill=CREAM)
            d.text((GUT, yy + CELL // 2 + 2), sdesc, font=F_SMALL, fill=DIM)
            for ci, (vid, _, _) in enumerate(VARIANTS):
                cell = cell_image(os.path.join(HERE, f"{sid}__{vid}__{size}.png"), scale)
                x = ROWLBL_W + ci * (CELL + GUT)
                sheet.alpha_composite(cell, (x, yy))
                d.rectangle([x, yy, x + CELL - 1, yy + CELL - 1], outline=(60, 54, 50, 255))
            yy += CELL + GUT
        y = yy + 30

    # ---- Section 3: real Pack-slot simulation, 1:1 ----
    d.text((GUT, y), "SECTION 3  -  REAL Pack-slot simulation at 1:1  "
                     "(64px well, icon at its true 52x52, walnut panel behind)", font=F_SEC, fill=CREAM)
    yy = y + 52
    panel_x = ROWLBL_W
    cols = len(VARIANTS) + 1
    panel_w = cols * SLOT_PITCH + 16
    panel_h = len(SUBJECTS) * (64 + 30) + 8
    d.rounded_rectangle([panel_x - 14, yy - 14, panel_x + panel_w, yy + panel_h],
                        radius=10, fill=WALNUT, outline=EDGE, width=1)
    for ci, (vid, l1, _) in enumerate(VARIANTS):
        d.text((panel_x + ci * SLOT_PITCH, yy - 34), l1.split()[0], font=F_LBL, fill=DIM)
    d.text((panel_x + len(VARIANTS) * SLOT_PITCH, yy - 34), "NOW", font=F_LBL, fill=(0xB5, 0x56, 0x3C, 255))
    for sid, stag, sdesc in SUBJECTS:
        d.text((GUT, yy + 12), stag, font=F_LBL, fill=CREAM)
        d.text((GUT, yy + 34), sdesc, font=F_SMALL, fill=DIM)
        for ci, (vid, _, _) in enumerate(VARIANTS):
            icon = Image.open(os.path.join(HERE, f"{sid}__{vid}__128.png")).convert("RGBA")
            sheet.alpha_composite(slot_well(icon=icon), (panel_x + ci * SLOT_PITCH, yy))
        # The CURRENT shipped state for both iron items: a bare "I" letter-chip (icon: null).
        sheet.alpha_composite(slot_well(chip_text="I"), (panel_x + len(VARIANTS) * SLOT_PITCH, yy))
        yy += 64 + 30

    d.text((GUT, H - 30), "NOW column = the CURRENT shipped Pack read for BOTH iron items: ItemDef.icon is "
                          "null -> the SAME 'I' letter-chip on both. That identical letter is the defect.",
           font=F_SMALL, fill=DIM)

    out = os.path.join(HERE, "contact-sheet.png")
    sheet.convert("RGB").save(out)
    print("wrote", out, sheet.size)


if __name__ == "__main__":
    build()
