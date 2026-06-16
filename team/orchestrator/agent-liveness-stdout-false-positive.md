# Handoff note — `agent-liveness-stop.sh` false-positive on echoed agentIds

**Owner:** whoever maintains the orchestration hooks (this note written from a
non-orchestrating side session, 2026-06-14).
**Severity:** low (nuisance block, no data risk). **Fix size:** one-line grep change.

## Symptom

`agent-liveness-stop.sh` blocked turn-end and demanded liveness probes for ~20
agentIds (`a0707e6bea8de5739`, `a09af1cbd61ea9430`, …) that were **never
dispatched by the session**. The session had made **zero `Agent` tool calls**.

## Root cause

The detector greps the transcript for the bare string `agentId: <hex>`:

```sh
# agent-liveness-stop.sh, lines ~71-74
dispatched=$(head -n "$slice_end" "$transcript_path" 2>/dev/null \
  | grep -oE 'agentId: [0-9a-f]{12,}' \
  | sed -E 's/agentId: //' \
  | sort -u || true)
```

The header assumes `agentId: <hex>` only appears in genuine Agent **spawn
results**. But the string also lands in the transcript whenever it's printed as
**Bash stdout** — e.g. a verification/debug command that greps *other* sessions'
transcripts (`grep -oE 'agentId: [0-9a-f]+' …`), a pasted ID, or a test fixture.
The detector can't tell those apart and flags them all as unverified dispatches.

This was triggered live while *verifying the hooks* — a `uniq -c` over real
agentIds printed lines like `      1 agentId: a0707e6bea8de5739` straight into
the transcript.

## The discriminator (verified against real transcripts)

A **genuine** spawn-result always embeds the hex with a fixed suffix:

```
"type":"text","text":"Async agent launched successfully.\nagentId: a2381517cad9d8594 (internal ID - do not mention to user. …
```

i.e. `agentId: <hex> (internal ID`. Echoed/stdout occurrences never carry the
` (internal ID` suffix (nor the `Async agent launched successfully.` prefix).

## Recommended fix (primary)

Anchor the dispatch grep to the genuine suffix, then extract the hex:

```sh
dispatched=$(head -n "$slice_end" "$transcript_path" 2>/dev/null \
  | grep -oE 'agentId: [0-9a-f]{12,} \(internal ID' \
  | grep -oE '[0-9a-f]{12,}' \
  | sort -u || true)
```

- Keeps every true positive (all real spawn-results have ` (internal ID`).
- Drops Bash-stdout / pasted / fixture occurrences.
- `Async agent launched successfully` as a prefix anchor is an equivalent option.

**Empirical result (applied + measured 2026-06-14):** on the session that
triggered this, the anchor cut flagged ids **21 → 3**. The residual 3 are NOT
real dispatches — they're verbatim spawn-result lines this very note + its test
fixtures quoted into the transcript. Takeaway: the suffix anchor fully fixes
normal operation (each session has its own transcript; nothing quotes full
spawn-result lines except hook-documentation work like this), but it canNOT
reach 0 in a transcript that quotes genuine spawn-result text. If you want
true-zero even in hook-doc/test contexts, the **structural parse** below is the
only complete fix — anchor on the JSONL entry being a genuine `Agent` tool
result, not arbitrary text/`tool_result` content.

## Secondary (optional)

The resolution greps (lines ~84-88) have the symmetric pollution: a `"to":"<hex>"`
or `task-id><hex>` string echoed to stdout could falsely mark an id *resolved*.
That only **suppresses** a block (never causes one), so it's lower priority — but
if hardened, scope those to genuine `SendMessage` tool_use / completion
notifications the same way.

## Heavier alternative

Parse the JSONL structurally (Python, like `dispatch-sentinel-stop.py`): only
count `agentId:` inside an assistant `text` block that is the result of an
`Agent` tool_use, ignoring `Bash` tool_results entirely. More robust, more code;
the one-line suffix anchor above is likely sufficient.

## Quick test after patching

```sh
# Should NOT flag (stdout-style echo, no suffix):
printf '%s\n' '{"type":"user","message":{"role":"user","content":[{"type":"text","text":"x"}]}}' \
  '{"type":"user","message":{"role":"user","content":[{"type":"tool_result","content":"      1 agentId: a0707e6bea8de5739"}]}}' \
  '{"type":"user","message":{"role":"user","content":[{"type":"text","text":"status?"}]}}' > /tmp/echo.jsonl
printf '{"transcript_path":"/tmp/echo.jsonl"}' | bash .claude/hooks/agent-liveness-stop.sh   # expect: silent

# Should STILL flag (genuine spawn-result, has suffix, unprobed):
printf '%s\n' '{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"Async agent launched successfully.\nagentId: a2381517cad9d8594 (internal ID - do not mention to user."}]}}' \
  '{"type":"user","message":{"role":"user","content":[{"type":"text","text":"status?"}]}}' > /tmp/real.jsonl
printf '{"transcript_path":"/tmp/real.jsonl"}' | bash .claude/hooks/agent-liveness-stop.sh   # expect: block
```
