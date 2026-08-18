#!/usr/bin/env bash
# CI gate: team/DECISIONS.md is PREPEND-ONLY.
#
# The invariant: the ENTRY REGION of the file as it exists on the base ref must
# survive VERBATIM as the TAIL of the entry region on this branch. New entries
# are prepended above it; nothing below is edited, reordered or deleted.
#
# Why a tail-match and not "zero deleted lines": the correction protocol in
# team/DECISIONS.md bans annotating a merged entry precisely because "an
# insertion into a historical entry still passes the 0-deletions invariant
# check and so erodes the record without tripping the guard." A tail-match
# catches the insertion too — the inserted line shifts the block and the
# comparison fails. This gate is therefore strictly stronger than the check the
# protocol was written against (which, until 2026-08-18, did not exist at all).
#
# The HEADER — everything above the first `## <year>-` entry — is exempt. It
# holds the format spec and the correction protocol, which are meant to be
# edited as the process evolves.
#
# Usage:
#   verify_decisions_prepend_only.sh [BASE_REF] [HEAD_FILE]
#     BASE_REF   git ref holding the baseline copy   (default: origin/main)
#     HEAD_FILE  working-tree file to check          (default: team/DECISIONS.md)
#                — override exists so the RED half can be demonstrated against a
#                  mutated copy without touching the real file.
set -euo pipefail

FILE_IN_GIT="team/DECISIONS.md"
BASE_REF="${1:-origin/main}"
HEAD_FILE="${2:-$FILE_IN_GIT}"

# Entry region = from the first dated entry heading to EOF.
entries_region() { awk '/^## 20[0-9][0-9]-/{f=1} f'; }

if ! git rev-parse --verify --quiet "$BASE_REF" >/dev/null 2>&1; then
  echo "SKIP: base ref '$BASE_REF' not available in this checkout."
  exit 0
fi

if ! git cat-file -e "$BASE_REF:$FILE_IN_GIT" 2>/dev/null; then
  echo "SKIP: $FILE_IN_GIT does not exist at $BASE_REF."
  exit 0
fi

if [ ! -f "$HEAD_FILE" ]; then
  echo "FAIL: $HEAD_FILE is missing from the working tree."
  exit 1
fi

base=$(mktemp)
head=$(mktemp)
tailslice=$(mktemp)
trap 'rm -f "$base" "$head" "$tailslice"' EXIT

git show "$BASE_REF:$FILE_IN_GIT" | entries_region > "$base"
entries_region < "$HEAD_FILE" > "$head"

n=$(wc -l < "$base" | tr -d ' ')

if [ "$n" -eq 0 ]; then
  echo "OK: no entries at $BASE_REF — nothing to protect yet."
  exit 0
fi

tail -n "$n" "$head" > "$tailslice"

if cmp -s "$tailslice" "$base"; then
  echo "OK: all $n baseline entry lines survive verbatim as the tail of $FILE_IN_GIT."
  exit 0
fi

cat <<EOF
FAIL: $FILE_IN_GIT is PREPEND-ONLY and this branch changed history.

  The $n entry lines present on $BASE_REF must still appear, byte for byte, as
  the LAST $n lines of the entry region. They do not.

  Legal:   add a new entry at the TOP of the entry list; edit the header block
           above the first entry; edit an entry that is still unmerged on this
           branch (that is a draft revision, not a correction).
  Illegal: editing, annotating, reordering or deleting any entry that is already
           on $BASE_REF — including inserting a forward-pointer into one. Amend a
           merged entry with a new CORRECTION: entry instead (protocol at the top
           of $FILE_IN_GIT).

  First differences (left = this branch's tail, right = $BASE_REF):
EOF
diff "$tailslice" "$base" | head -40 || true
exit 1
