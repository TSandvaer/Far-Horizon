#!/usr/bin/env python3
"""frames_differ.py — assert two capture frames are VISIBLY different (ticket 86caa4bqp re-QA).

The settings capture gate's job is to prove a tweak is VISIBLE in the shipped build —
settings_tweaked.png must show the changed readout, not be a byte-copy of settings_open.png.

THE bug class this guards (Tess re-QA of PR #83): the verify harness drove the tweak through
the entry setter directly (walk.SetValue), which changes the live param + PlayerPrefs but
BYPASSES the slider callback that repaints the row readout — so the panel frame never repainted
and settings_tweaked.png came out PIXEL-IDENTICAL to settings_open.png. The frame_check.py
content checks (not black/uniform/magenta) AND the changedLive=True log BOTH passed while the
captured tweaked frame showed the un-tweaked value. Only a human eye caught it. This script is
the machine guard: it FAILS when the two frames don't differ enough to be a real repaint.

A diff PASSES (the frames ARE different) only when the fraction of changed pixels exceeds a
small floor — a single repainted readout label is a small region of a 1280x720 frame, so the
floor is deliberately tiny (a few-hundred-pixel text change clears it) but strictly > 0 so a
byte-identical copy fails. Same dependency story as frame_check.py: Pillow if present, else the
proven stdlib PNG decoder it already ships (imported here, not duplicated).

Usage:
  frames_differ.py <frame_a.png> <frame_b.png> [--min-frac F]
Exit 0 only when both decode AND the changed-pixel fraction is >= --min-frac (default 0.0005).
Any decode error / missing file = fail loud (a check that can't read its input is a failure).
"""
import os
import sys

# Reuse frame_check.py's decoder (Pillow-or-stdlib) so we never duplicate the PNG plumbing and
# stay in lockstep with the format frame_check already proves it handles for these captures.
_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)
import frame_check  # noqa: E402

# A repainted readout label is a small region of a 1280x720 frame. The floor is small enough
# that a few-hundred-pixel text change passes, but strictly > 0 so a byte-identical copy fails.
DEFAULT_MIN_FRAC = 0.0005
# A per-channel delta above this counts a pixel as "changed" (ignores 1-bit dither/AA noise).
PIXEL_DELTA = 16


def _decode(path):
    dec = frame_check._decode_with_pillow(path)
    if dec is None:
        dec = frame_check._decode_stdlib(path)
    return dec  # (w, h, rgba_bytes)


def changed_fraction(path_a, path_b):
    """Fraction of sampled pixels that differ by > PIXEL_DELTA on any RGB channel."""
    wa, ha, a = _decode(path_a)
    wb, hb, b = _decode(path_b)
    if (wa, ha) != (wb, hb):
        # Different dimensions = unambiguously different frames (and undefined to diff per-pixel).
        return 1.0, (wa, ha), (wb, hb)
    w, h = wa, ha
    step_x = max(1, w // 200)
    step_y = max(1, h // 200)
    n = 0
    changed = 0
    for y in range(0, h, step_y):
        row = y * w * 4
        for x in range(0, w, step_x):
            i = row + x * 4
            if (abs(a[i] - b[i]) > PIXEL_DELTA or
                    abs(a[i + 1] - b[i + 1]) > PIXEL_DELTA or
                    abs(a[i + 2] - b[i + 2]) > PIXEL_DELTA):
                changed += 1
            n += 1
    return (changed / n if n else 0.0), (w, h), (w, h)


def main():
    args = [a for a in sys.argv[1:]]
    min_frac = DEFAULT_MIN_FRAC
    if "--min-frac" in args:
        idx = args.index("--min-frac")
        try:
            min_frac = float(args[idx + 1])
        except (IndexError, ValueError):
            print("[frames_differ] --min-frac needs a number", file=sys.stderr)
            return 2
        del args[idx:idx + 2]
    if len(args) != 2:
        print("usage: frames_differ.py <frame_a.png> <frame_b.png> [--min-frac F]",
              file=sys.stderr)
        return 2

    a, b = args
    for p in (a, b):
        if not os.path.isfile(p):
            print(f"[frames_differ] FAILED — frame not found: {p} (the capture step must "
                  f"have produced it)", file=sys.stderr)
            return 1
    try:
        frac, dim_a, dim_b = changed_fraction(a, b)
    except Exception as e:  # decode error = cannot verify = fail loud
        print(f"[frames_differ] ERROR decoding frames: {e}", file=sys.stderr)
        return 1

    na, nb = os.path.basename(a), os.path.basename(b)
    if frac < min_frac:
        print(f"[frames_differ] FAILED — {na} and {nb} are PIXEL-IDENTICAL "
              f"(changed_frac={frac:.5f} < {min_frac}). The tweak did NOT repaint the panel — "
              f"the captured tweaked frame shows the un-tweaked value (the slider readout never "
              f"refreshed). This is the PR #83 re-QA bug class.", file=sys.stderr)
        return 1
    print(f"[frames_differ] PASS — {na} vs {nb} differ (changed_frac={frac:.5f} >= {min_frac}); "
          f"the tweak is VISIBLE in the shipped frame")
    return 0


if __name__ == "__main__":
    sys.exit(main())
