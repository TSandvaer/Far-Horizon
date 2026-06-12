#!/usr/bin/env python3
"""parse_test_results.py — authoritative pass/fail gate for a Unity NUnit XML.

unity-conventions.md: "Always grep the XML's <test-run ... result= total= passed=
failed= line — exit code alone lies on some failure classes." This parser is that
gate, made robust: it reads the root <test-run> element's attributes and fails the
job unless result==Passed AND failed==0 AND total>0 (an empty run is a failure —
it means the platform compiled nothing / the asmdef didn't load).

Usage: parse_test_results.py <results.xml> <EditMode|PlayMode>
Exit 0 only when the run is genuinely green.
"""
import sys
import xml.etree.ElementTree as ET


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: parse_test_results.py <results.xml> <label>", file=sys.stderr)
        return 2
    path, label = sys.argv[1], sys.argv[2]
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
    total = attr_int("total")
    passed = attr_int("passed")
    failed = attr_int("failed")
    skipped = attr_int("skipped")
    inconclusive = attr_int("inconclusive")

    print(f"[{label}] result={result} total={total} passed={passed} "
          f"failed={failed} skipped={skipped} inconclusive={inconclusive}")

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
