#!/usr/bin/env bash
# SessionStart hook — emits an INDEX of .claude/docs/*.md.
#
# It does NOT inline the doc bodies. It used to: the hook `cat`-ed all twelve
# files into `additionalContext`, which as of 2026-08-14 was 451,785 bytes
# (~113k est. tokens, `unity-conventions.md` alone 246KB). That exceeded the
# harness injection limit, so the payload was persisted to a tool-results file
# and only a ~2KB preview reached the session — while the hook's own text
# asserted "These reference docs are already in context. Do NOT call Read on
# them again." Every session since the docs crossed the limit ran on that false
# premise, with reads of the real content actively suppressed. Surfaced by
# /doctor 2026-08-14.
#
# The contract now: this hook tells you WHAT EXISTS and HOW BIG it is. The
# routing — which doc is mandatory before which task class — is the single
# source of truth in CLAUDE.md § Detailed Documentation. Read a doc when your
# task matches its routing rule.
#
# Always exits 0; never blocks.

set -eu

DOCS_DIR="$CLAUDE_PROJECT_DIR/.claude/docs"
if [ ! -d "$DOCS_DIR" ]; then
  DOCS_DIR="$CLAUDE_PROJECT_DIR/../.claude/docs"
fi
if [ ! -d "$DOCS_DIR" ]; then
  exit 0
fi

shopt -s nullglob
docs=("$DOCS_DIR"/*.md)
[ ${#docs[@]} -gt 0 ] || exit 0

TMP="$(mktemp)"
trap 'rm -f "$TMP"' EXIT

total_bytes=0

{
  echo "# Project documentation INDEX (.claude/docs)"
  echo
  echo "These docs are **NOT loaded into context** — only this index is. Read a file with the"
  echo "Read tool when your task matches its routing rule. The routing rules (which doc is"
  echo "MANDATORY before which task class) live in CLAUDE.md § Detailed Documentation, which"
  echo "IS loaded. Do not read all of them by default; that habit cost ~13 agents mid-task in"
  echo "a single week (Sponsor decision 2026-08-02)."
  echo
  echo "## First-response confirmation (mandatory)"
  echo
  echo "Begin your VERY FIRST response of this session with this exact line, on its own:"
  echo
  echo "> Doc index loaded (${#docs[@]} docs, bodies NOT in context). Ready."
  echo
  echo "Then a blank line, then answer the user's request normally. This is non-negotiable —"
  echo "always include the confirmation line, even if the user's prompt is short (e.g. 'yes',"
  echo "'hi'). It is the user's only visible signal that the SessionStart hook fired. Do NOT"
  echo "repeat it on subsequent turns; only the first response of the session."
  echo
  echo "| Doc | Size | Title |"
  echo "|---|---|---|"

  for f in "${docs[@]}"; do
    base="$(basename "$f")"
    bytes=$(wc -c < "$f")
    total_bytes=$((total_bytes + bytes))
    kb=$(( (bytes + 1023) / 1024 ))

    # Prefer the H1; fall back to the first non-empty, non-heading line.
    title="$(grep -m1 '^# ' "$f" 2>/dev/null | sed 's/^# //' || true)"
    if [ -z "$title" ]; then
      title="$(grep -m1 -v '^[[:space:]]*$' "$f" 2>/dev/null | sed 's/^#\+[[:space:]]*//' || true)"
    fi
    # Keep the table one row per doc: strip pipes/newlines, cap the length.
    title="$(printf '%s' "$title" | tr -d '\r|' | cut -c1-110)"

    printf '| `%s` | %sKB | %s |\n' "$base" "$kb" "${title:-(untitled)}"
  done

  echo
  printf 'Total on disk: %sKB across %s files — reading all of them would cost roughly %sk est. tokens.\n' \
    "$(( (total_bytes + 1023) / 1024 ))" "${#docs[@]}" "$(( total_bytes / 4000 ))"
} > "$TMP"

DOC_COUNT=${#docs[@]}

node -e '
const fs = require("fs");
const data = fs.readFileSync(process.argv[1], "utf8");
const docCount = Number(process.argv[2]);
const kb = Math.round(data.length / 1024);
process.stdout.write(JSON.stringify({
  systemMessage: `Project doc INDEX loaded (${docCount} docs indexed, ${kb}KB — bodies not inlined).`,
  hookSpecificOutput: {
    hookEventName: "SessionStart",
    additionalContext: data,
  },
}));
' "$TMP" "$DOC_COUNT"
