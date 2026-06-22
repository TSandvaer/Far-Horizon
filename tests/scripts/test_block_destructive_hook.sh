#!/usr/bin/env bash
# Regression harness for .claude/hooks/block-destructive-bash.sh (ticket 86cabe3gx).
#
# Proves the hook scopes destructive-pattern matching to the parsed
# `.tool_input.command` field ONLY — closing both failure classes from
# memory destructive-bash-hook-matches-whole-input:
#   - FALSE POSITIVE: a destructive phrase in description / commit-message /
#     PR-body text must NOT block a harmless command.
#   - FALSE NEGATIVE: a leading recursive delete in the command must still block.
# And — critically — real protection is UNCHANGED: rm -rf / force-push /
# reset --hard / repo delete / branch -D in the COMMAND still BLOCK.
#
# Usage: bash tests/scripts/test_block_destructive_hook.sh   (exit 0 = all pass)

set -u
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
HOOK="$REPO_ROOT/.claude/hooks/block-destructive-bash.sh"

fail=0
pass=0

# verdict: returns "DENY" if the hook emits a deny decision for the given JSON, else "ALLOW".
verdict() {
  local out
  out="$(printf '%s' "$1" | bash "$HOOK" 2>/dev/null)"
  if printf '%s' "$out" | grep -q '"permissionDecision":"deny"'; then
    echo "DENY"
  else
    echo "ALLOW"
  fi
}

# check <expected> <label> <json>
check() {
  local expected="$1" label="$2" json="$3"
  local got
  got="$(verdict "$json")"
  if [ "$got" = "$expected" ]; then
    printf 'PASS  [%s] %s -> %s\n' "$expected" "$label" "$got"
    pass=$((pass+1))
  else
    printf 'FAIL  [want %s got %s] %s\n' "$expected" "$got" "$label"
    fail=$((fail+1))
  fi
}

echo "=== block-destructive-bash.sh — field-scoping regression harness ==="

# --- The 4 brief-mandated cases ---
check ALLOW "(a) single-file git rm \"x\" -> ALLOW" \
  '{"tool_name":"Bash","tool_input":{"command":"git rm \"inspiration/hyper3d-castaway/abc.zip\"","description":"drop redundant zip"}}'

check ALLOW "(b) git commit whose MESSAGE contains 'rm -rf' (command harmless) -> ALLOW" \
  '{"tool_name":"Bash","tool_input":{"command":"git commit -m \"docs: explain why rm -rf is dangerous\"","description":"commit note"}}'

check DENY  "(c) real rm -rf <path> in the COMMAND -> BLOCK" \
  '{"tool_name":"Bash","tool_input":{"command":"rm -rf /c/Trunk/PRIVATE/Far-Horizon-devon-wt/Assets","description":"cleanup"}}'

check DENY  "(d) git push --force -> BLOCK" \
  '{"tool_name":"Bash","tool_input":{"command":"git push --force origin main","description":"push"}}'

# --- Protection-intact cases (the fix must NOT weaken these) ---
check DENY  "git push --force-with-lease -> BLOCK" \
  '{"tool_name":"Bash","tool_input":{"command":"git push --force-with-lease origin feat","description":"x"}}'
check DENY  "git push -f (short flag) -> BLOCK" \
  '{"tool_name":"Bash","tool_input":{"command":"git push -f origin main","description":"x"}}'
check DENY  "git push origin main --force (flag last) -> BLOCK" \
  '{"tool_name":"Bash","tool_input":{"command":"git push origin main --force","description":"x"}}'
check DENY  "git reset --hard HEAD~1 -> BLOCK" \
  '{"tool_name":"Bash","tool_input":{"command":"git reset --hard HEAD~1","description":"x"}}'
check DENY  "rm -rf mid-command (after &&) -> BLOCK" \
  '{"tool_name":"Bash","tool_input":{"command":"cd /tmp && rm -rf build","description":"x"}}'
check DENY  "rm -fr (flag order) -> BLOCK" \
  '{"tool_name":"Bash","tool_input":{"command":"rm -fr /tmp/x","description":"x"}}'
check DENY  "Remove-Item -Recurse -> BLOCK" \
  '{"tool_name":"PowerShell","tool_input":{"command":"Remove-Item -Recurse -Force C:/x"}}'
check DENY  "gh repo delete -> BLOCK" \
  '{"tool_name":"Bash","tool_input":{"command":"gh repo delete owner/repo --yes","description":"x"}}'
check DENY  "git branch -D feat -> BLOCK" \
  '{"tool_name":"Bash","tool_input":{"command":"git branch -D feat/old","description":"x"}}'

# --- False-positive guards (destructive phrase only in NON-command text) ---
check ALLOW "git rm with 'force' in description -> ALLOW" \
  '{"tool_name":"Bash","tool_input":{"command":"git rm \"x.zip\"","description":"force-drop the redundant zip"}}'
check ALLOW "PR body mentioning 'git push --force' (gh pr create) -> ALLOW" \
  '{"tool_name":"Bash","tool_input":{"command":"gh pr create --title t --body \"explains git push --force pitfalls\"","description":"open PR"}}'
check ALLOW "commit msg mentioning 'reset --hard' -> ALLOW" \
  '{"tool_name":"Bash","tool_input":{"command":"git commit -m \"note: avoid reset --hard on shared branches\"","description":"x"}}'
check ALLOW "commit msg mentioning 'branch -D' -> ALLOW" \
  '{"tool_name":"Bash","tool_input":{"command":"git commit -m \"docs: when branch -D is safe\"","description":"x"}}'

# --- Benign command guards ---
check ALLOW "plain git push (no force) -> ALLOW" \
  '{"tool_name":"Bash","tool_input":{"command":"git push origin feat/x","description":"x"}}'
check ALLOW "git reset --soft -> ALLOW" \
  '{"tool_name":"Bash","tool_input":{"command":"git reset --soft HEAD~1","description":"x"}}'
check ALLOW "single-file rm (no -r/-f) -> ALLOW" \
  '{"tool_name":"Bash","tool_input":{"command":"rm /tmp/onefile.txt","description":"x"}}'

echo "---"
printf 'RESULT: %d passed, %d failed\n' "$pass" "$fail"
[ "$fail" -eq 0 ]
