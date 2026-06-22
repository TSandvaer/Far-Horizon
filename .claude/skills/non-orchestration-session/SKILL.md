---
name: non-orchestration-session
description: Declare the CURRENT session a passive, hands-on conversation that must NOT orchestrate — because another session is the active orchestrator. Use when the user says "non-orchestration session", "/non-orchestration-session", "don't orchestrate", "I have another session orchestrating", "this is a normal session", "make this session passive", "don't dispatch / don't act as orchestrator here", or otherwise signals that a different session owns orchestration and this one should behave as an ordinary assistant. Suppresses this session's auto-status pulse and forbids the orchestrator behaviors (board scan-and-fill, sub-agent dispatch, PR staging/merge, STATE/DECISIONS upkeep) for the rest of the session — WITHOUT disturbing the persisted auto-status intent the other session depends on.
---

# Non-orchestration session

Declare **this** session a normal, hands-on conversation. Another session is the
active orchestrator; this one must stay passive so two orchestrators don't compete
on the same board / worktrees / coordination docs.

When invoked, do the following in order.

## 1. Suppress this session's auto-status pulse — but DON'T flip the persisted intent

The `auto-status` cron is **session-scoped** (dies on session end), but
`.claude/auto-status.state` (`enabled`/`mode`) is **project-scoped** and is what
re-arms the *orchestrating* session on its restarts. So:

- **Do NOT run `auto-status off`** and **do NOT edit `.claude/auto-status.state`.**
  That would flip the project-wide intent to disabled and silence the OTHER
  (orchestrating) session on its next tick/restart. Leave the state file exactly
  as it is.
- **Do NOT re-arm.** If the SessionStart hook injected an "Auto-status re-arm
  (SessionStart)" context block asking you to invoke `auto-status` as your first
  action, **ignore that instruction for this session** — this skill is the
  explicit override.
- **If this session already armed a pulse** (the skill was invoked *after* the
  SessionStart re-arm fired): load `CronList` + `CronDelete` via `ToolSearch`
  (`select:CronList,CronDelete`), find the auto-status cron belonging to **this**
  session, and delete it directly. This silences only this session's pulse and
  leaves the persisted intent untouched. (Crons from the other session are not
  visible here, so there's no risk of killing theirs.)

## 2. Announce passive mode

Reply in prose with the declaration — short, no preamble. Use this shape:

> Not orchestrating — you have another session that is the active orchestrator.
> This session is a normal hands-on conversation. I won't re-arm auto-status here
> (left the persisted intent alone so your orchestrating session keeps it).

Then ask what they need, or proceed with whatever request they already gave.

## 3. Behavioral guardrails for the REST of this session

For the remainder of the session, behave as an ordinary assistant — **never** as
the orchestrator. Specifically, do NOT:

- run the board **scan-and-fill** loop (compute the dispatchable set, fill free
  slots) — that's the other session's job;
- **dispatch sub-agent personas** (Priya / Uma / Devon / Drew / Tess / Erik) to do
  orchestrated work, or run `Workflow`/away-mode dispatch flows;
- **stage or merge PRs**, flip ClickUp statuses as an orchestrator, or run the
  soak-or-complete merge round;
- write orchestrator coordination state — `team/STATE.md` Resume header,
  `team/DECISIONS.md`, `.claude/away-queue.md`, `.claude/decisions-while-away.md`
  — as part of an orchestration tick.

You MAY still do ordinary hands-on work the user directly asks for: read/answer
questions, edit files they point you at, run read-only inspection, author a doc
or skill, etc. If the user explicitly asks you to spawn a one-off research agent
for a direct task, that's fine — it's the *orchestration role* that's off, not the
`Agent` tool itself. If the user asks you to start orchestrating, confirm they want
to switch this session into the orchestrator role (and coordinate with their other
session) before doing so.

## Notes

- This is a **per-session declaration**. It does not persist across restarts — on a
  fresh session the SessionStart hook will again suggest re-arming auto-status;
  re-invoke `/non-orchestration-session` if this session should stay passive.
- The skill changes nothing on disk. It touches no project state, no board, no git.
