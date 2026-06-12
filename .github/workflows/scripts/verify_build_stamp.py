#!/usr/bin/env python3
"""verify_build_stamp.py — soak-build stamp-vs-HEAD guard (ticket 86ca86gde).

THE stale-stamp trap (unity-conventions.md §Headless/CLI): `FarHorizonBuilder.
BuildWindows` does NOT regenerate `Assets/Resources/BuildStamp.txt` — only
`BootstrapProject.Run` (WriteBuildStamp) writes it. The stamp is also COMMITTED,
so it is ALWAYS one-or-more SHAs stale relative to a later HEAD. A soak that
skips bootstrap and just rebuilds ships the COMMITTED (old) stamp — exactly the
incident that bit today's first soak (served manually off main 28d9de7, stamp
`zoned | ... | 28d9de7` while HEAD had already moved). The Sponsor then can't
tell which build they're actually running — the precise build-identity ambiguity
the stamp ritual exists to kill.

This guard reads the stamp the soak build will SHIP and FAILS LOUD unless its
git-sha field equals the current HEAD short sha. serve_soak.sh runs it AFTER
bootstrap (which freshly stamps HEAD) and BEFORE handing the exe over — so a
stale or missing stamp can never reach the Sponsor.

Stamp format (BootstrapProject.WriteBuildStamp):  "<tag> | <UTC ISO> | <git-sha>"
e.g.  "zoned | 2026-06-12T14:43:12Z | a3edf04"

Usage:
  verify_build_stamp.py <BuildStamp.txt> <expected-short-sha>
Exit 0 iff the stamp's third field == <expected-short-sha>. Exit 1 on mismatch,
malformed stamp, or unreadable file (fail loud — a verification that can't read
its input is a failure, not a pass).
"""
import sys


def parse_stamp(text):
    """Return the (tag, utc, sha) triple from a stamp line, or raise ValueError.

    The stamp is a single ` | `-separated line; anything else is malformed and
    must fail loud rather than silently accepting a half-written stamp.
    """
    parts = [p.strip() for p in text.strip().split("|")]
    if len(parts) != 3 or not all(parts):
        raise ValueError(
            f"malformed stamp (expected '<tag> | <UTC> | <sha>', got {text.strip()!r})")
    return parts[0], parts[1], parts[2]


def main():
    if len(sys.argv) != 3:
        print("usage: verify_build_stamp.py <BuildStamp.txt> <expected-short-sha>",
              file=sys.stderr)
        return 2
    stamp_path, expected = sys.argv[1], sys.argv[2].strip()

    try:
        with open(stamp_path, "r", encoding="utf-8") as f:
            raw = f.read()
    except OSError as e:
        print(f"[verify-stamp] FAILED — cannot read {stamp_path}: {e}",
              file=sys.stderr)
        return 1

    try:
        tag, utc, sha = parse_stamp(raw)
    except ValueError as e:
        print(f"[verify-stamp] FAILED — {e}", file=sys.stderr)
        return 1

    if sha != expected:
        print(f"[verify-stamp] FAILED — STALE STAMP: build would ship sha={sha!r} "
              f"but HEAD is {expected!r}.", file=sys.stderr)
        print(f"[verify-stamp]   stamp = {tag} | {utc} | {sha}", file=sys.stderr)
        print("[verify-stamp]   This is the stale-stamp trap: BuildWindows does NOT "
              "regenerate BuildStamp.txt — bootstrap must run first so the stamp "
              "matches HEAD. Re-run the soak through serve_soak.sh (bootstrap -> "
              "build -> verify), never a bare BuildWindows.", file=sys.stderr)
        return 1

    print(f"[verify-stamp] OK — build ships stamp matching HEAD: {tag} | {utc} | {sha}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
