#!/usr/bin/env python3
"""parse_test_results.py — authoritative pass/fail gate for a Unity NUnit XML.

unity-conventions.md: "Always grep the XML's <test-run ... result= total= passed=
failed= line — exit code alone lies on some failure classes." This parser is that
gate, made robust: by default it fails the job unless result==Passed AND failed==0
AND inconclusive==0 AND total>0 (an empty run is a failure — it means the platform
compiled nothing / the asmdef didn't load).

--allow-skips (the PlayMode gate, ticket 86camz787): treat SKIPPED/IGNORED tests as
PASS. Unity's NUnit rolls a run that contains [Ignore]-skipped tests up to a
run-level result="Skipped:Ignored" (a compound "Status:Label"), which trips the
strict result==Passed check even when failed==0 (proven: main run 29683732044,
playmode job read `failure` on result=Skipped:Ignored total=302 passed=289 failed=0
skipped=13). With --allow-skips the gate is GREEN when failed==0 AND inconclusive==0
AND total>0 AND the run-level STATUS (the token before the ':') is Passed or Skipped
— so pre-existing [Ignore] quarantines no longer red the job, while any REAL failure
still fails RED: failed>0, inconclusive>0, an empty run, or a Failed/Error run-level
status. Timeouts red independently — a hung/timed-out run writes no XML, so the
caller reds before this parser runs, and the job's hard timeout cap still applies.

The EditMode (required) gate calls this WITHOUT --allow-skips, so its strict
behavior is unchanged.

Usage: parse_test_results.py [--allow-skips] <results.xml> <EditMode|PlayMode>
Exit 0 only when the run is genuinely green.
"""
import sys
import xml.etree.ElementTree as ET


def main() -> int:
    args = [a for a in sys.argv[1:] if a != "--allow-skips"]
    allow_skips = "--allow-skips" in sys.argv[1:]
    if len(args) != 2:
        print("usage: parse_test_results.py [--allow-skips] <results.xml> <label>",
              file=sys.stderr)
        return 2
    path, label = args[0], args[1]
    try:
        root = ET.parse(path).getroot()
    except (ET.ParseError, FileNotFoundError) as e:
        print(f"[{label}] CANNOT READ result XML '{path}': {e}", file=sys.stderr)
        return 1

    # NUnit3 emits <test-run> as the document root.
    tr = root if root.tag == "test-run" else root.find(".//test-run")
    if tr is None:
        print(f"[{label}] no <test-run> element in '{path}' — run produced no results",
              file=sys.stderr)
        return 1

    def attr_int(name: str) -> int:
        try:
            return int(tr.get(name, "0"))
        except ValueError:
            return 0

    result = tr.get("result", "<missing>")
    # Unity/NUnit rolls the run-level result up as a compound "Status:Label"
    # (e.g. "Skipped:Ignored", "Failed:Error"); the STATUS before the ':' is what gates.
    status = result.split(":", 1)[0]
    total = attr_int("total")
    passed = attr_int("passed")
    failed = attr_int("failed")
    skipped = attr_int("skipped")
    inconclusive = attr_int("inconclusive")

    print(f"[{label}] result={result} total={total} passed={passed} "
          f"failed={failed} skipped={skipped} inconclusive={inconclusive}"
          f"{' (allow-skips)' if allow_skips else ''}")

    if allow_skips:
        # Skipped/ignored tests are NOT failures. Green when nothing FAILED and the
        # run-level status is Passed or Skipped; total>0 still rejects an empty run,
        # and a Failed/Error run-level status still reds even if failed==0.
        green = (failed == 0 and inconclusive == 0 and total > 0
                 and status in ("Passed", "Skipped"))
    else:
        # Strict (EditMode / required gate) — behavior UNCHANGED: every discovered
        # test must have passed.
        green = result == "Passed" and failed == 0 and inconclusive == 0 and total > 0

    if not green:
        print(f"[{label}] TEST GATE FAILED", file=sys.stderr)
        # Surface the failing test names for fast triage.
        for tc in tr.iter("test-case"):
            if tc.get("result") not in ("Passed", "Skipped", "Ignored", None):
                name = tc.get("fullname") or tc.get("name")
                print(f"[{label}]   FAILED: {name}", file=sys.stderr)
        return 1

    print(f"[{label}] TEST GATE PASSED")
    return 0


if __name__ == "__main__":
    sys.exit(main())
