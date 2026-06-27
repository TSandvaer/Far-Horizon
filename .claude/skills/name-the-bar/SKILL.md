---
name: name-the-bar
description: Surface the orchestrator's OWN cited guesses about the Sponsor's implicit quality bar / direction as one-at-a-time confirmable popups BEFORE a taste-sensitive dispatch or a long away-run — so the real bar is NAMED up front instead of discovered after a soak-reject. Persists confirmed bars to team/quality-bars.md and injects the relevant bar into the dispatch brief. Use when about to dispatch feel / visual / first-of-class-visual work, before an away-run that will drift, when the destination feels thin or stale, or when you catch yourself guessing what the Sponsor wants. Trigger phrases: "name the bar", "/name-the-bar", "what's the bar here", "articulate the quality bar", "destination check", "what does the sponsor actually want", "am I guessing the bar".
---

# Name the Bar — proactive quality-bar articulation

Far Horizon is a **taste-driven** project: the Sponsor knows the right answer when he sees
it, and most of `team/quality-bars.md` + the project memory was learned *reactively, after a
soak-reject* (organic pond, no red axe-head, lively motion, in-hand-not-bare-render). Each of
those rejects cost a build + a round of Sponsor attention. This skill moves that cost
**upstream**: surface the bar the agent *already suspects* as a cheap, confirmable question
BEFORE the work starts.

It is the local adaptation of the reference earned-autonomy suite's `destination` skill — the
mechanism is the same (sourced, falsifiable inferences confirmed one at a time); the plumbing
is Far Horizon's (AskUserQuestion popups, ClickUp tickets, the soak economy, project memory as
the citation source).

> **The asymmetry that justifies guessing:** the cost the agent pays for guessing wrong is one
> click of correction. The cost the Sponsor pays for the agent never guessing is another
> soak-reject round. Guess — but cite, and make it falsifiable.

## When to run

- **Before a taste-sensitive dispatch** — any `feat`/`fix` whose acceptance is subjective feel
  or first-of-class visuals (the same class that triggers a Sponsor soak in `team/TESTING_BAR.md`).
- **Before a long away-run** that will drift if the direction is unclear.
- **Mid-task, when you catch yourself guessing** what the Sponsor wants rather than citing a
  confirmed bar — pause and run this instead of guessing silently.
- **Compose with `/unstick`:** `/unstick` fires at *attempt 2* (the precision gap surfaced
  reactively); `name-the-bar` fires at *attempt 0* (name the bar before the first build).
  Together they bracket the whole iteration.

**Do NOT run** for mechanical work (`chore`/`docs`/`test`, refactors, CI) — there is no
subjective bar to name. If you cannot form an honest cited inference, say so and stop; a run
that produces zero inferences is a valid outcome (the bar is already clear, or the trail is
too thin to infer from).

## Procedure

### 1. Gather signal
Read, in this order: `team/quality-bars.md` (already-confirmed bars), the relevant project
memory entries (`MEMORY.md` index → the taste/look entries), the ClickUp ticket(s) about to be
dispatched, and the recent conversation. Notice what the Sponsor has emphasized, pushed back
on, or re-routed — including what he has NOT said directly.

### 2. Form 2–5 sourced inferences
Each inference is one of five shapes:
- **Direction** — "I think you're heading toward X."
- **Priority** — "I think X matters more than Y here."
- **Constraint** — "I think you'd reject Z."
- **Question-being-asked** — "the question you're really answering is W, not V."
- **Quality-bar** — "the bar you're actually holding this to is Q." ← *the highest-leverage shape for this project.*

Each inference must be **specific enough to be wrong** ("you care about quality" is not an
inference; "you'd rather ship one tuned weapon than three rough ones" is) and **cited** to a
quoted phrase, a memory slug (`[[pond-organic-not-round]]`), or a concrete soak exchange. State
it as "I think…" / "the trail suggests…", never "it's clear that…".

### 3. Turn each kept inference into a falsifiable question
Answerable in one sentence; a wrong reading should be cheap for the Sponsor to correct.

### 4. Surface ONE AT A TIME via AskUserQuestion, in priority order
Use `AskUserQuestion` (the Sponsor prefers clicking — `[[askquestion-always-mark-recommended]]`).
One popup per inference, the recommended option first and labelled "(Recommended)" when the
inference is foundation-defensible. **Never batch** — the answer to Q1 often reshapes or
obsoletes Q3–5. Present the question first, then the 1–2-sentence cited hunch behind it so the
Sponsor can correct the *source-reading*, not just the conclusion.

### 5. Persist confirmed bars
Append/update `team/quality-bars.md` with what the Sponsor confirmed, corrected, or rejected
(format in that file's header). A confirmed bar becomes **input to the next dispatch** — it does
not become the dispatch itself.

### 6. Inject into the dispatch brief
When the dispatch fires, paste the relevant confirmed bar into the brief and into the
Self-Test Report's **Prediction & convergence** block (`team/TESTING_BAR.md` § Predict-Before-Soak)
so the author predicts against a *confirmed* bar, not a guessed one.

## Boundaries
- This skill only CLARIFIES the bar; it never starts the work. A confirmed bar is input, not action.
- It does not score the Sponsor's clarity — "still exploring" is a legitimate answer; record it and stop.
- It does not replace a Sponsor soak — it makes the soak's verdict cheaper by naming the bar the soak will judge against.
- Orchestrator-run only (it asks the Sponsor directly). In a passive / non-orchestration session, draft the inferences but do not fire the popups unless the user asks.
