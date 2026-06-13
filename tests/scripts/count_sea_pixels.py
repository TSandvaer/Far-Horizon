#!/usr/bin/env python3
# count_sea_pixels.py — the MAGENTA-DIFF water-visibility evidence tool (ticket 86ca8fet0).
#
# WHY THIS EXISTS: the beach ocean was invisible in the seaward gameplay view for SIX shipped builds
# while every text-only check (and the Z-edge "not occluded" EditMode guard) said it was fine. The
# only thing that caught it was painting the water material bright MAGENTA, capturing the shipped
# -verifySea seaward frame, and COUNTING how many magenta pixels actually reached the frame. 0 px =
# the sea is not rendering / is occluded / is culled; a clear band = the sea is visible. This is the
# silhouette/pixel evidence the shipped-build capture gate needs for the sea — editor geometry can't
# prove on-frame visibility (the inverted-winding backface-cull bug rendered zero px with the geometry
# perfectly correct).
#
# HOW TO USE (the verification ritual a reviewer re-runs):
#   1. Temporarily set LowPolyZoneGen.WaterShallow + WaterDeep to bright magenta (0.95, 0.05, 0.95).
#   2. Bootstrap + BuildWindows, then run:
#        FarHorizon.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
#          -verifySea -captureDir <dir>
#   3. python3 tests/scripts/count_sea_pixels.py <dir>/sea_seaward.png
#   4. PASS = water px > 0 (a clear teal/sea band). The fix lands 55k-58k px (~6% of frame).
#   5. Revert the magenta to the teal albedo before committing.
#
# The committed EditMode guard (WaterFacesUpTests) catches the inverted-winding REGRESSION cheaply in
# every CI run; THIS tool is the heavier shipped-build VISIBILITY evidence for soak/QA.
import sys
from PIL import Image

def is_magenta(r, g, b):
    # bright magenta: high R, high B, low G (the painted water albedo, post-graded). The Zone-D warm
    # grade pushes R up / B down, so allow a generous band but keep G clearly the minority channel so
    # sky/sand/grass (all G-rich or warm) never qualify.
    return r > 110 and b > 90 and g < min(r, b) - 25

def main():
    if len(sys.argv) < 2:
        print("usage: count_sea_pixels.py <magenta_seaward.png>", file=sys.stderr)
        sys.exit(2)
    path = sys.argv[1]
    im = Image.open(path).convert("RGB")
    w, h = im.size
    px = im.load()
    total = w * h
    mag = 0
    minrow, maxrow = h, -1
    rows_with = {}
    for y in range(h):
        cnt = 0
        for x in range(w):
            r, g, b = px[x, y]
            if is_magenta(r, g, b):
                cnt += 1
        if cnt > 0:
            mag += cnt
            rows_with[y] = cnt
            if y < minrow: minrow = y
            if y > maxrow: maxrow = y
    frac = mag / total
    print(f"file        : {path}")
    print(f"size        : {w}x{h}  ({total} px)")
    print(f"water px    : {mag}  ({frac*100:.2f}% of frame)")
    if maxrow >= 0:
        print(f"water band  : rows {minrow}..{maxrow}  "
              f"(top {minrow/h*100:.1f}% .. {maxrow/h*100:.1f}% of height)")
        band_h = maxrow - minrow + 1
        print(f"band height : {band_h}px ({band_h/h*100:.1f}% of frame height)")
        peak_y = max(rows_with, key=rows_with.get)
        print(f"peak row    : y={peak_y} ({rows_with[peak_y]} water px, "
              f"{rows_with[peak_y]/w*100:.0f}% of width)")
        # Exit 0 (PASS) only when the sea actually contributes a real band.
        sys.exit(0 if mag > 0 else 1)
    else:
        print("water band  : NONE — zero magenta water pixels reached the frame "
              "(sea not rendering / occluded / backface-culled)")
        sys.exit(1)

if __name__ == "__main__":
    main()
