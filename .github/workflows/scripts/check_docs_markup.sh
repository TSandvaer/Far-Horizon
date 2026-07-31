#!/usr/bin/env bash
# check_docs_markup.sh — license-free CI guard (ticket 86cayxtw8).
#
# THE BUG CLASS: an authoring agent's own tool-call CLOSING TAGS get written into a
# doc's BODY instead of terminating the call, so the file ends with a bare tag alone
# on a line. Measured extent at origin/main b9abf7b: 5 committed *.md, exactly 2
# leaked lines each, always the LAST two lines. Four different personas' directories
# were affected, so the mechanism recurs — it is not one author's slip.
#
# WHY A MACHINE GUARD AND NOT "just delete them": every one of those five files
# PASSED a peer review with the markup in it. Reviewers read diff hunks; nobody reads
# the last two lines of a 200-line doc. The review blind spot is the defect this
# closes.
#
# PATTERN BREADTH — the trade-off taken (AC2), stated so a future reader can re-judge:
#   Chosen: a bare tag ALONE ON A LINE, anchored at COLUMN 0, tag vocabulary WIDE.
#     - Column-0 anchor (no leading-whitespace tolerance) is the discriminator. The
#       leak mechanism always emits at column 0; a legitimate fenced XML example in a
#       doc is almost always INDENTED. Allowing leading whitespace would have grown
#       the false-positive population (indented examples) without covering any
#       plausible instance of the real defect.
#     - Trailing [[:space:]]*$ IS tolerated — it absorbs a CRLF. .gitattributes pins
#       eol for *.sh/*.py/*.yml/*.yaml/*.ps1 and packages-lock.json but NOT *.md, so a
#       CRLF-in-blob *.md is possible; on a Linux runner a strict `>$` anchor would
#       then MISS it — a false GREEN, silent. (The 5 known offenders' blobs are LF.)
#     - Tag vocabulary is WIDE on purpose (both open and close forms, plus the
#       `antml:`-prefixed variants and `parameter`/`function_calls`/`function_results`,
#       none of which appear in the measured set): widening the MATCH set can only
#       produce a false RED — loud, someone looks at it. See team/TESTING_BAR.md
#       § "Doc-staleness greps" on the monotone match-vs-exclusion asymmetry.
#   RESIDUAL HOLE, disclosed: a legitimate fenced code block that puts one of these
#   tags alone on a line at COLUMN 0 will red. That is deliberate — the fix is to
#   indent the example by one space, and the failure direction is the loud one. There
#   is NO fenced-code-block EXCLUSION here on purpose: an exclusion grows the
#   exclusion set, and a false negative in this guard is silent forever.
#
# SCOPE (AC3): tracked *.md ONLY. NOT covered: *.txt, *.json, *.yml/*.yaml, *.uxml,
# *.uss, code comments (*.cs/*.py/*.sh), and untracked/ignored files. A sweep across
# ALL tracked file types at b9abf7b found offenders in *.md and nowhere else, so this
# scope loses nothing measured — but it is a scope, not a proof of absence elsewhere.
#
# EXIT-CODE TRAP (the #360 lesson, team/TESTING_BAR.md § Doc-staleness greps): a
# grep-based guard exits NON-ZERO when it finds NOTHING. The pass signal here is
# EMPTY OUTPUT, never $?. Every grep below is `|| true`-neutralised for exactly that
# reason; the verdict is taken from the collected hit text.
#
# Usage:  check_docs_markup.sh [<repo-root>]
#         Defaults to the enclosing git work tree. Zero Unity dependency — runs on
#         GitHub-hosted ubuntu in the licence-free `structure` job AND in the
#         docs-markup workflow (docs-only PRs never trigger ci.yml — see that file).
set -uo pipefail

ROOT="${1:-}"
if [ -z "$ROOT" ]; then
  ROOT="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
fi

# Open OR close form; optional antml: prefix; the tool-call tag family; alone on the
# line at column 0, trailing whitespace/CR tolerated.
PATTERN='^</?(antml:)?(content|invoke|parameter|function_calls|function_results)>[[:space:]]*$'

echo "=== docs tool-call-markup check (86cayxtw8) ==="
echo "  root:    $ROOT"
echo "  scope:   tracked *.md only (NOT .txt/.json/.yml/.uxml/.uss, NOT code comments, NOT untracked files)"
echo "  pattern: $PATTERN"

files="$(git -C "$ROOT" ls-files -- '*.md' 2>/dev/null || true)"
if [ -z "$files" ]; then
  echo "[ OK ] no tracked *.md files under $ROOT — nothing to scan"
  exit 0
fi

count=0
hits=""
while IFS= read -r f; do
  [ -n "$f" ] || continue
  count=$((count + 1))
  m="$(grep -n -E "$PATTERN" -- "$ROOT/$f" 2>/dev/null | tr -d '\r' || true)"
  [ -n "$m" ] || continue
  while IFS= read -r line; do
    [ -n "$line" ] || continue
    hits="${hits}${f}:${line}"$'\n'
  done <<< "$m"
done <<< "$files"

if [ -n "$hits" ]; then
  n="$(printf '%s' "$hits" | grep -c '' || true)"
  echo "[FAIL] leaked tool-call markup in committed *.md — ${n} line(s):"
  printf '%s' "$hits" | sed 's/^/    /'
  echo
  echo "  These are an authoring agent's own tool-call tags written into the document"
  echo "  BODY instead of terminating the call. Delete the offending line(s)."
  echo "  BEFORE deleting: check the document actually ENDS COHERENTLY (does the last"
  echo "  section conclude? does a numbered list stop mid-sequence? does the doc's own"
  echo "  cross-reference list name a section that is absent?). Stray closing tags at"
  echo "  a file's end are weak evidence the write was TRUNCATED — if content is"
  echo "  genuinely missing, deleting the tags HIDES the damage."
  echo "  If the hit is a legitimate fenced XML example, indent it by one space; do"
  echo "  NOT add an exclusion to this guard (see the header's asymmetry note)."
  exit 1
fi

echo "[ OK ] no leaked tool-call markup in ${count} tracked *.md file(s)"
exit 0
