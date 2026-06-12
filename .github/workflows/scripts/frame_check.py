#!/usr/bin/env python3
"""frame_check.py — shipped-build capture gate (ticket 86ca86g7k).

The editor-vs-runtime divergence class (unity-conventions.md) is the #1 trap: a
scene that looks right in the editor can ship BLACK / unrendered / all-magenta
in the standalone player (Awake-no-serialize, shader stripping, swapchain not
present). The CaptureGate component renders N real frames from the BUILT exe
(windowed) and writes capture_NN.png; THIS script is the authoritative gate that
fails the build when those frames are black/empty/uniform/magenta — the exact
failure the editor-only evidence can never catch.

A frame PASSES only when ALL hold (it has real, varied, non-error content):
  * mean luminance is inside a sane band (not ~black, not ~white blowout),
  * pixel variance exceeds a floor (a uniform fill = a dead frame: black screen,
    clear-colour-only with nothing drawn, or a solid error screen),
  * the magenta fraction is below a ceiling (Unity renders missing/stripped
    shaders as bright magenta — a shader-strip regression paints the frame pink).

Decoding: uses Pillow when present (CI may have it); otherwise falls back to a
dependency-free stdlib decoder for the 8-bit non-interlaced RGB/RGBA PNGs that
Unity's ScreenCapture.CaptureScreenshot emits. No third-party install required.

Usage:
  frame_check.py <dir-or-png> [<dir-or-png> ...] [--min-frames N]
Exit 0 only when EVERY inspected frame passes AND at least --min-frames (default 1)
frames were found (zero frames = the capture step never ran = a failure, the same
silent-killer shape as parse_test_results.py's total>0 rule).
"""
import os
import sys
import struct
import zlib

# --- thresholds (0..255 luminance scale; fractions are 0..1) ---------------
MIN_MEAN_LUMA = 6.0      # below this the frame is ~black (nothing rendered)
MAX_MEAN_LUMA = 250.0    # above this the frame is a white blowout (no content)
MIN_VARIANCE = 8.0       # variance floor; a uniform fill (dead frame) is below it
MAX_MAGENTA_FRAC = 0.30  # > this fraction of bright-magenta pixels = shader strip


def _iter_pngs(paths):
    """Yield every .png under the given files/dirs, sorted for determinism."""
    found = []
    for p in paths:
        if os.path.isdir(p):
            for name in sorted(os.listdir(p)):
                if name.lower().endswith(".png"):
                    found.append(os.path.join(p, name))
        elif os.path.isfile(p) and p.lower().endswith(".png"):
            found.append(p)
    return found


def _decode_with_pillow(path):
    try:
        from PIL import Image  # type: ignore
    except Exception:
        return None
    with Image.open(path) as im:
        im = im.convert("RGBA")
        w, h = im.size
        return w, h, im.tobytes()


def _decode_stdlib(path):
    """Minimal stdlib PNG decoder: 8-bit, non-interlaced, RGB(3)/RGBA(4).

    Returns (w, h, rgba_bytes) or raises ValueError on an unsupported shape.
    Enough for Unity ScreenCapture output; raises (not silently passes) otherwise.
    """
    with open(path, "rb") as f:
        data = f.read()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("not a PNG")
    pos = 8
    width = height = bit_depth = color_type = interlace = None
    idat = bytearray()
    while pos < len(data):
        (length,) = struct.unpack(">I", data[pos:pos + 4])
        ctype = data[pos + 4:pos + 8]
        cdata = data[pos + 8:pos + 8 + length]
        pos += 12 + length  # 4 len + 4 type + data + 4 crc
        if ctype == b"IHDR":
            width, height, bit_depth, color_type, _, _, interlace = struct.unpack(
                ">IIBBBBB", cdata)
        elif ctype == b"IDAT":
            idat += cdata
        elif ctype == b"IEND":
            break
    if bit_depth != 8 or interlace != 0 or color_type not in (2, 6):
        raise ValueError(
            f"unsupported PNG (bit_depth={bit_depth} color_type={color_type} "
            f"interlace={interlace}); install Pillow to inspect it")
    channels = 3 if color_type == 2 else 4
    raw = zlib.decompress(bytes(idat))
    stride = width * channels
    out = bytearray(width * height * 4)
    prev = bytearray(stride)
    rp = 0
    for y in range(height):
        ftype = raw[rp]; rp += 1
        line = bytearray(raw[rp:rp + stride]); rp += stride
        _unfilter(ftype, line, prev, channels, stride)
        prev = line
        # expand to RGBA
        op = y * width * 4
        for x in range(width):
            si = x * channels
            out[op] = line[si]
            out[op + 1] = line[si + 1]
            out[op + 2] = line[si + 2]
            out[op + 3] = line[si + 3] if channels == 4 else 255
            op += 4
    return width, height, bytes(out)


def _unfilter(ftype, line, prev, bpp, stride):
    if ftype == 0:
        return
    for i in range(stride):
        a = line[i - bpp] if i >= bpp else 0
        b = prev[i]
        c = prev[i - bpp] if i >= bpp else 0
        x = line[i]
        if ftype == 1:
            line[i] = (x + a) & 0xFF
        elif ftype == 2:
            line[i] = (x + b) & 0xFF
        elif ftype == 3:
            line[i] = (x + ((a + b) >> 1)) & 0xFF
        elif ftype == 4:
            p = a + b - c
            pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
            pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
            line[i] = (x + pr) & 0xFF
        else:
            raise ValueError(f"bad PNG filter type {ftype}")


def _stats(w, h, rgba):
    """Mean luma, luma variance, and bright-magenta fraction over a sampled grid.

    Samples a capped grid (≤ ~200x200 points) so multi-megapixel frames stay fast
    while remaining representative — a black/uniform/magenta frame is detectable
    from any dense sample.
    """
    step_x = max(1, w // 200)
    step_y = max(1, h // 200)
    n = 0
    s = 0.0
    s2 = 0.0
    magenta = 0
    for y in range(0, h, step_y):
        row = y * w * 4
        for x in range(0, w, step_x):
            i = row + x * 4
            r, g, b = rgba[i], rgba[i + 1], rgba[i + 2]
            luma = 0.2126 * r + 0.7152 * g + 0.0722 * b
            s += luma
            s2 += luma * luma
            # bright magenta: high R + high B, low G (Unity's missing-shader colour)
            if r > 200 and b > 200 and g < 80:
                magenta += 1
            n += 1
    if n == 0:
        return 0.0, 0.0, 1.0
    mean = s / n
    var = max(0.0, s2 / n - mean * mean)
    return mean, var, magenta / n


def _judge(path):
    dec = _decode_with_pillow(path)
    if dec is None:
        dec = _decode_stdlib(path)
    w, h, rgba = dec
    mean, var, mag = _stats(w, h, rgba)
    reasons = []
    if mean < MIN_MEAN_LUMA:
        reasons.append(f"black/empty (mean_luma={mean:.1f} < {MIN_MEAN_LUMA})")
    if mean > MAX_MEAN_LUMA:
        reasons.append(f"white blowout (mean_luma={mean:.1f} > {MAX_MEAN_LUMA})")
    if var < MIN_VARIANCE:
        reasons.append(f"uniform/dead (variance={var:.1f} < {MIN_VARIANCE})")
    if mag > MAX_MAGENTA_FRAC:
        reasons.append(f"shader-strip magenta (frac={mag:.2f} > {MAX_MAGENTA_FRAC})")
    ok = not reasons
    detail = (f"{w}x{h} mean_luma={mean:.1f} variance={var:.1f} magenta={mag:.2f}")
    return ok, detail, reasons


def main():
    args = [a for a in sys.argv[1:]]
    min_frames = 1
    if "--min-frames" in args:
        idx = args.index("--min-frames")
        try:
            min_frames = int(args[idx + 1])
        except (IndexError, ValueError):
            print("[frame_check] --min-frames needs an integer", file=sys.stderr)
            return 2
        del args[idx:idx + 2]
    if not args:
        print("usage: frame_check.py <dir-or-png> [...] [--min-frames N]",
              file=sys.stderr)
        return 2

    pngs = _iter_pngs(args)
    print(f"[frame_check] inspecting {len(pngs)} frame(s); need >= {min_frames}")
    if len(pngs) < min_frames:
        print(f"[frame_check] FAILED — found {len(pngs)} frame(s), expected >= "
              f"{min_frames}. The capture step never produced frames (the editor-"
              f"vs-runtime backstop only works if the shipped exe actually ran + "
              f"rendered).", file=sys.stderr)
        return 1

    failed = 0
    for p in pngs:
        try:
            ok, detail, reasons = _judge(p)
        except Exception as e:  # decode error = cannot verify = fail loud
            print(f"[frame_check]   ERROR {os.path.basename(p)}: {e}",
                  file=sys.stderr)
            failed += 1
            continue
        tag = "PASS" if ok else "FAIL"
        print(f"[frame_check]   {tag} {os.path.basename(p)} :: {detail}")
        if not ok:
            for r in reasons:
                print(f"[frame_check]        - {r}", file=sys.stderr)
            failed += 1

    if failed:
        print(f"[frame_check] CAPTURE GATE FAILED — {failed}/{len(pngs)} frame(s) "
              f"black/empty/uniform/magenta", file=sys.stderr)
        return 1
    print(f"[frame_check] CAPTURE GATE PASSED — {len(pngs)} frame(s) have real content")
    return 0


if __name__ == "__main__":
    sys.exit(main())
