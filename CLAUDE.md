# Far Horizon

A 3D survival game built in **Unity 6 (6000.4.11f1) / URP**, desktop-first (Windows). A bearded, rugged, friendly castaway washes ashore and survives his way toward the far horizon. Visual direction: **low-poly smooth-shaded** world (faceted meshes, soft gradient lighting, warm/lush palette) with a quality pass (bloom / grading / fog / gradient skybox) — the Sponsor-approved "Zone D" look from the 2026-06 engine-eval spike.

## Context

- **Director / sole stakeholder ("Sponsor"):** Thomas. Single delegated decision-maker; orchestrator handles team coordination.
- **Heritage:** Successor to the Godot project *Embergrave/RandomGame* (archived; `c:/Trunk/PRIVATE/RandomGame`). Engine decision 2026-06-12: migrate to Unity (RandomGame `team/DECISIONS.md`, ticket 86ca7y46c). The Unity eval spike at `c:/Trunk/PRIVATE/EmbergraveUnitySlice` is a READ-ONLY reference for the M-U1 ports — never modify it.
- **Core feel (Sponsor-locked):** small character in a big alive world; **WASD movement + run (Shift) + jump (Space)** — pivoted 2026-06-16 from the original PoE-style click-to-move (implementation backlog `86ca9yq2x`→`yq34`→`yq3q`; the live build still uses click-to-move until those land); mouse-orbit camera + zoom; survival loop (M-U2: started THIN with one need — WARMTH/campfire — then EXPANDED 2026-06-17 to THREE needs: **warmth** (campfire) + **hunger** (harvest berries from bushes) + **thirst** (drink-from-hand at a freshwater pond); tickets `86caamkp8` hunger / `86caamkv7` thirst / `86caamkxv` three-bar need-meter HUD). North-star: world feels BIG and ENDLESS — a journey.
- **Player character:** low-poly 3D castaway — **bearded, rugged, friendly-neutral adult-survivor** identity (Sponsor decision 2026-07-05, deliberately reversing the earlier "young + happy" lock; see `team/DECISIONS.md`). Hero-character route: AI concept (openai-image) → Hyper3D Rodin → Mixamo → Unity **Generic** (NOT Humanoid — Humanoid explodes this mesh, `86ca8rdkp`). **Castaway v2 is the LIVE default** since 2026-07-05 (#262); the old base is retained behind the `CharacterAssetGen.UseCastawayV2Default` toggle (env `FARHORIZON_CASTAWAY_V2`) for rollback.
- **Distribution:** Windows desktop build (`Build/Windows/FarHorizon.exe`). No HTML5/WebGL target.
- **Tracker:** ClickUp list **"Far Horizon"** (list id `901523878268`, space `90156932495`). The RandomGame list is the Godot-era archive.
- **PixelLab:** NOT a Far Horizon concern — Sponsor uses the subscription for other projects (verbatim 2026-06-12 evening: "im using pixellab for other projects, dont worry about it"); do not track its cost or revisit it as a project decision. Pixel-art-native, ruled out for this game's characters/world; the asset-creation route here is Blender + Blender MCP (see unity-conventions.md §Asset creation).

## Tech stack & project facts

- **Unity 6** `6000.4.11f1` at `C:/Program Files/Unity/Hub/Editor/6000.4.11f1/Editor/Unity.exe`; URP.
- **Namespaces / asmdefs:** `FarHorizon` (runtime), `FarHorizon.EditorTools` (editor); asmdefs `FarHorizon.Runtime` / `FarHorizon.Editor` / `FarHorizon.EditTests` / `FarHorizon.PlayTests`.
- **Headless entry points:** scene/bootstrap `-executeMethod FarHorizon.EditorTools.BootstrapProject.Run`; build `-executeMethod FarHorizon.EditorTools.FarHorizonBuilder.BuildWindows` → `Build/Windows/FarHorizon.exe` (exits non-zero on failure). Tests: `-runTests -testPlatform EditMode|PlayMode`.
- **Build-stamp ritual (carried from the spike — it earned its keep):** HUD shows `BUILD <tag> | <UTC> | <sha>`; every soak request verifies the stamp before judging.
- **`.gitignore` note:** `*.log`, `test-results*.xml`, `Captures/`, `Build/` are ignored — CI must upload artifacts before cleanup; new throwaway-dir conventions must be added there.
- **Empty dirs carry `.meta` files** so the Assets layout survives commits — preserve them.

## Architecture

**Orchestrator + named-agent team model** (carried from RandomGame). The Claude Code main session is the orchestrator; named personas (Priya PL / Uma UX / Devon Dev1 / Drew Dev2 / Tess QA, + Erik consult) work dispatched tasks in per-role git worktrees (`../Far-Horizon-<role>-wt`). The orchestrator never codes — it briefs, dispatches, gates, merges. Sponsor talks only to the orchestrator.

> **Persona-file note:** `.claude/agents/*.md` are carried from the Godot project. Read their Godot-era specifics through Unity equivalents: GUT → EditMode/PlayMode (NUnit); HTML5 visual gate → shipped-build capture gate; `.tscn`/`.tres` → scenes/prefabs; Playwright E2E → PlayMode + shipped-exe capture evidence. Craft scope and team conventions carry unchanged.

## Hard rules (orchestrator + team)

- **`main` is protected.** PR-flow + `gh pr merge --admin --squash --delete-branch` only. (Bootstrap exception: U1/U2 root scaffolding landed direct, recorded on their tickets.)
- **Testing bar.** Paired EditMode/PlayMode tests + green checks + a SHIPPED-BUILD verification (built exe runs; capture evidence for anything visual) + Tess sign-off before "complete". Sponsor will not debug. Soak-gated/feel PRs additionally carry a **Predict-Before-Soak** line (a falsifiable pre-soak prediction graded against the soak) + a **bounded convergence claim** (name the bar tested + the bars NOT tested). See `team/TESTING_BAR.md`.
- **Shipped-build capture gate** (successor to the HTML5 gate): anything UX/visually-visible needs evidence captured from the BUILT exe (not just the editor) before merge — editor-vs-runtime divergence is a proven failure class (spike iter6 "legs-up" incident).
- **Self-Test Report gate.** UX-visible PRs need an author-posted Self-Test Report comment before Tess reviews.
- **ClickUp status as hard gate.** Every dispatch / PR-open / merge pairs with a status move on list `901523878268` in the same tool round.
- **Orchestrator never codes** (R&D-lane exception for MCP-bound generation + Sponsor-interactive iteration; every R&D burst closes with a harvest PR + productionization tickets). **The exception is about the SESSION, not the model:** MCP-bound generation runs wherever the MCP connection lives — normally here — **on opus**, never on fable; fable stays advisor-only (2026-08-18 CORRECTION entry in `team/DECISIONS.md`, amending the 2026-07-07 advisor-only entry). No persona holds a Blender MCP tool, so "dispatch it to an opus agent" is not currently an option.
- **Always parallel dispatch** where dependencies allow; tickets aren't progress, dispatches are.
- **Agent liveness from probe, NEVER assumption.** Report in-flight state only from a fresh `SendMessage`-by-agentId probe + `git log` on the worktree + `gh pr view`. Enforced by the `agent-liveness-stop.sh` hook.
- **Tightened final-report contract.** Sub-agent reports ≤200 words: verdict + blockers + key paths + doc-updates; every claim cites verifiable evidence (run/commit/path). Detail goes in PR body / ticket comments.
- **Sponsor soak = direct artifact.** Any soak ask includes the exact exe path + the expected HUD build stamp.
- **Never fabricate, never guess, never extrapolate** (sub-agent inheritance surface). Concrete values — URLs, IDs, SHAs, file paths, command output, ticket/run IDs — must be fetched from a real source, never invented or pattern-extrapolated. Fetch, don't guess: PR URL via `gh pr view`, SHA via `git rev-parse`, ticket state via ClickUp MCP. Observed-symptom claims in tickets/PRs/reports need a verifiable source in the same paragraph; label hypotheses explicitly (`Hypothesis:` / `Likely:`). **The creating turn is never the referencing turn:** never batch a producer call (ticket create, Agent dispatch, `gh pr create`, `git commit`) with a consumer that writes the produced value; if a value hasn't been seen in a tool result, write the literal token `<pending>`.

## Orchestration doctrine (rewritten 2026-08-02 — Sponsor decision, 12 rulings)

> **What this section is for.** The team exists to produce **results the Sponsor can see in a
> build**. It does not exist to test exhaustively, document thoroughly, or keep everyone busy.
> If the shape below stops producing visible results, it gets retired (see § Kill switch) —
> a single hands-on session beats an orchestration that generates its own work.

### The measured failure this replaces

Last `feat` on `origin/main`: **2026-07-22** (`0dc4844`, wild boar). **79 commits since —
47 docs, 12 chore, 10 fix, 8 test, 1 spike, 1 ci, ZERO feat** (`git log 0dc4844..origin/main`,
measured 2026-08-02). Nine of ten open PRs were non-gameplay. An unattended loop burned four
rate-limit windows and then the weekly account cap producing documentation.

The cause was not the build cap and not laziness. It was a **demand engine** (an anti-idle
hook that forbade the orchestrator from ending a tick without dispatching) feeding on
**supply engines** that manufacture work from work. The anti-idle hook is removed. The supply
engines are named and killed below.

### The rules

- **Idle is free; an unjustified dispatch is the bug.** Rank the dispatchable set by
  **player-visible value**, never by readiness. A bug in the shipped build outranks every doc
  ticket. **Prefer leaving a slot idle to manufacturing work.** Scan the whole board so you
  never wrongly conclude "all gated" (the 2026-06-28 idle-3h failure) — but having scanned,
  dispatch only what earns its cost. [[orchestrator-fill-nongated-slots-scan-whole-board]],
  [[token-waste-hard-line]].
- **Hard team ceiling: ONE developer + ONE reviewer + at most ONE support.**
  - **Developer** — Devon or Drew, in the build slot, on the gameplay ticket.
  - **Reviewer/QA** — Tess on dev PRs, Devon/Drew peer on Tess's. Dispatched **when a PR
    exists**, never before, never speculatively.
  - **Support (optional, needs a named concrete need)** — Priya for a batched once-daily board
    pass; Uma only when a feature ticket needs a spec this week; Erik only against a consumer
    ticket a developer is actually blocked on.
  Five-agents-out is retired. Nobody is "cut" — personas are prompts, not salaries — but
  standing dispatch is.
- **Unity-build cap = 1 — the mechanism, and the ONLY authoritative statement of it.** ⛔ **The number is a Sponsor decision. Nothing in this bullet authorises changing it; a docs or CI PR is never where it moves.** ⚠ With the one-developer ceiling above, this cap is **no longer the binding constraint** — do not spend effort trying to lift it. Two independent things hold it, both machine-checkable in one command each — **measured 2026-07-31 on `origin/main` @ `721701d`; re-measure before citing, do not carry these figures forward on trust**:
  - **(a) An absolute concurrency group.** `.github/workflows/ci.yml` puts every `build` job in `concurrency: group: unity-build` with **NO ref suffix** and `cancel-in-progress: false`. All `build` jobs repo-wide therefore QUEUE into ONE lane — **regardless of how many runners exist or how they are labelled**. (Why absolute rather than ref-scoped: the `86caammpq` orphan-hold fix, rationale in-line in `ci.yml`.)
  - **(b) One registered runner.** `gh api repos/TSandvaer/Far-Horizon/actions/runners` → **`total_count: 1`**: `far-horizon-local`, `status: online`, labels `[self-hosted, Windows, X64, unity, capture]`.
  - **To lift the cap, BOTH (a) and (b) must change** — a 2nd runner must be registered **AND** the `unity-build` group widened or removed. A 2nd runner ALONE buys nothing: the group would still serialize the builds. ⚠ A third question rides along — `build`'s label set (`[self-hosted, windows, unity]`) is a strict SUBSET of the one runner's, so a `build` job is *eligible for the capture-pinned runner*; protecting captures under two runners needs `build` given a disjoint label.
  - **⛔ Three stale reasons are still in circulation, each with its own believer. NONE of them is why the cap is 1:** (1) *"a CI-split of headless-build from captures is the prerequisite"* — **that split SHIPPED** (`86cafz9tg`); `ci.yml` now has separate `build` and `capture` jobs and **no `unity` job exists at all**. Anyone told to go build it would be rebuilding something already merged. (2) *"PackageCache EPERM / shared-cache contention forces serialization"* — the `unity-build` group is **cache-independent**, so cache work cannot move the cap; the `86cabkhjg` spike (PR **#387**) additionally found EPERM **absent** in its resolving legs. (3) *"there are two runners / the 2-runner topology"* — `far-horizon-local-2` is **history, not current state**; `total_count: 1` today.
  - **Still true and still load-bearing — do NOT delete or act against:** a 2nd runner's PRESENCE breaks WINDOWED captures (**A/B-CONFIRMED 2026-06-29**: 4/4 clean single-runner vs 3/3 flaked with runner-2 online — the D3D12 first-frame present loop wedges under contention). That is why the `capture` job is pinned via the extra `capture` label and serialized by an absolute `unity-capture` group. **Do not unpin it.** Memory: [[single-unity-build-slot-serializes-orchestration]]; deeper CI shape in `.claude/docs/unity-conventions.md` § CI architecture.
- **⛔ Reviews may NEVER create a ticket.** `APPROVE_WITH_NITS` is deleted. Two verdicts:
  `APPROVE` (merge) or `REQUEST_CHANGES` (fixed **in this PR**, reviewer re-checks the diff
  once, done). Nits are fixed now or dropped; dropping them is an accepted cost.
  **Docs-only and test-only PRs get NO reviewer at all** — CI green, merge. **Code PRs get one
  reviewer, one round**; a would-be third round escalates to the Sponsor with the
  ship-with-documented-defect option instead ([[offer-ship-with-documented-defect-escape]]).
  Verified generator this kills: #383 → #394 ("#383 NITs") → #401 ("#394 NITs").
- **⛔ Documentation requires a paid-for incident.** A doc entry may be written only by naming
  **the incident it would have prevented and what that incident cost** (a rebuild, a soak
  round, a dead agent-hour). No named incident with a cost → no doc. "Useful", "non-obvious",
  and "future Claude would benefit" are not incidents — that bar was already written down and
  it did not hold. `maintain-docs` is **manual-only**; its Stop hook is removed from
  `.claude/settings.json` and **must not be re-added**. No line-anchor audits — cite by
  section heading or stable slug, never `file:line`.
- **⛔ Agents may not create tickets**, except for a bug **reproduced in a built exe**. Every
  other ticket — features, refactors, research, hygiene, follow-ups — needs the Sponsor's yes
  first. An unbounded ticket source plus any board scan guarantees the team never runs out of
  non-gameplay work.
- **⛔ Do not test the test infrastructure.** No tests guarding capture components, no guards
  on guards. A verify-capture component ships **CI-wired in its own PR or it does not ship**;
  of 37 built, only 13 were ever wired. When a feature PR next touches an unwired one, wire it
  or delete it in that PR — do not file tickets for the rest.
- **Scoped pre-reads.** The blanket "read every `.claude/docs/*.md`" rule is retired; briefs
  name the 1–3 docs the task class needs (table in `team/orchestrator/dispatch-template.md`).
- **Away/unattended mode is OFF.** Orchestration runs Sponsor-attended only, until **three
  gameplay `feat`s have shipped** under these rules. Do not re-arm `auto-status away`. Gate
  decisions surface to the present Sponsor via popups rather than being auto-decided.
- **Dependency-aware pivot.** A ticket gated on the Sponsor blocks ONLY its hard-dependents,
  never the whole board. Tag it `sponsor-gate` (or `needs-soak` for feel/look) and move on.
  Priority orders dispatchable tickets; it is never a reason to idle — nor a reason to invent
  work.
- **Merging.** `main` is protected and the auto-mode classifier blocks admin-merge-to-main
  (it tags it "Production Deploy"). Verify the live merge mechanism before acting; never
  assume a label name. [[classifier-blocks-merge-to-protected-main]].
- **Board statuses** (list `901523878268`): `to do` → `in progress` → `in review` →
  `ready for qa test` → `complete`. No "blocked" status exists — gated tickets keep their
  functional status + a `sponsor-gate` tag.
- **Coordination docs stay small.** `team/STATE.md` is a resume header, not a log.
  `team/DECISIONS.md` is append-only history. `.claude/away-queue.md` and
  `.claude/decisions-while-away.md` are archived under `team/log/` — away mode is off, so they
  have no consumer. Do not grow them back.

### Kill switch (automatic — not a judgement call)

**Any calendar week with zero `feat` merges retires the standing team.** Check:

```
git log origin/main --since="7 days ago" --pretty=%s | grep -c "^feat"
```

`0` → collapse to a single hands-on session + an on-demand QA agent, and stop dispatching
personas. No debate, no appeal. This exists because the last drought ran ten days before
anyone named it, and it took an independent audit to surface.

### Current destination (2026-08-02)

**Close out the weapon/combat line.** PR #351 — find `sword_iron` in a stump, E-loot it — is
the only open gameplay PR and is one merge from visible. Finish that thread (find a weapon →
fight the boar with it), then the Sponsor picks the next milestone deliberately.

⚠ Note for whoever reads `team/survival-roadmap.md`: its plan of record **stops at M-U2**
(one need → axe → chop → campfire) while the team has shipped combat, enemies and weapons well
past it. The roadmap is stale as a plan. Do not treat it as the destination; ask the Sponsor.

## Detailed Documentation

**The docs are INDEXED, not preloaded** (changed 2026-08-14). `.claude/hooks/session-start-read-docs.sh`
injects the ~2KB index below's mechanical half — filename, size, title — and nothing else. It
used to `cat` all twelve bodies into context: 451,785 bytes, which **exceeded the harness
injection limit**, so the payload was silently persisted to a file and only a ~2KB preview
arrived — while the hook's own text claimed the docs were loaded and told the session **not to
Read them**. Every session in that window ran blind on suppressed reads. **Read the doc your
task routes to.** Nothing below is in context until you do.

**Sub-agents do NOT inherit the index — and nobody reads all twelve.** The blanket
read-everything rule was retired 2026-08-02 (Sponsor decision): ~1,855 lines of context on
every dispatch including trivial ones, paid in full by ~13 agents that died mid-task in a
single week. **Dispatch briefs NAME the 1–3 docs the task class requires** — the routing table
lives in `team/orchestrator/dispatch-template.md` § "Read BEFORE any code". Reading a doc
outside your list is fine when you have a reason; reading all of them by default is not.

**These docs do not grow on their own any more.** `maintain-docs` is manual-only and gated on
a named incident with a named cost — see § Orchestration doctrine.

| Doc (`.claude/docs/`) | Read when |
|---|---|
| `asset-routing.md` | **FIRST on any "model / create a new X" task** — maps asset class → route (Procedural / Blender / Hyper3D-Mixamo / action-verb-animation) → that route's mandatory doc, plus the "source when procedural fights the style" route-switch rule |
| `unity6-mastery.md` | **MANDATORY before ANY Unity code, every action** (Sponsor-stressed 2026-06-16) — rendering path/Forward+, GPU Resident Drawer, batching, lighting budget, GC/scripting, ScriptableObject architecture, UI Toolkit, IL2CPP |
| `blender-asset-pipeline.md` | **MANDATORY before ANY Blender / weapon / tool / prop / asset task** (Sponsor-directed 2026-06-19) — style contract, scene/units, `wpn_`/`prop_`/`env_` naming, faceted-chunky modeling + FBX export |
| `procedural-animation-verbs.md` | **MANDATORY before ANY action-verb animation** — chop / pick-up / drink / throw, or any `CastawayArmPose` / `HeldAxeRig` / held-prop-seating change |
| `lowpoly-quality.md` | **MANDATORY for all visual / mesh / shader work** — props, rocks, trees, water, terrain, hero props |
| `game-juice.md` | **MANDATORY for any feel / polish / feedback dispatch** — chop impact, pickup feel, need-bar transitions, world liveness, jump feel |
| `art-direction.md` | Before any visual work — and **look at the actual `inspiration/*.png` images**, not just the doc |
| `unity-conventions.md` | Unity/URP incident findings: headless rituals, editor-vs-runtime serialization traps, FBX/rig gotchas. **241KB — search it, never read it whole** |
| `character-pipeline.md` | Hero-character work: Hyper3D Rodin Image-to-3D → Mixamo auto-rig → Unity **Generic** |
| `elite-techniques.md` | Reaching for an external reference or a not-yet-adopted technique (pointers, not tutorials) |
| `agent-file-crlf-colon-registration-trap.md` | **Before editing ANY `.claude/agents/*.md` frontmatter** |
| `vision-far-horizon-game-concept.md` | Scoping a survival-arc feature against the Sponsor's full vision |

Three carve-outs kept resident because they stop a wasted search before it starts:

- **Weapon/prop style anchor:** the live in-house `Assets/Art/Props/WeaponPack/` set **IS** the anchor. The CC-BY `CastawayAxe/` placeholder that predated the pipeline was never the anchor and no longer exists — deleted with its licence file in PR #100 (`031d43a`, `86cabh907`). Do not go looking for that path.
- **A `.claude/` fix is only real once merged to `main`.** A worktree edit has a half-life of one dispatch (`git checkout -B ... origin/main` wipes it); a coordination-branch commit reaches nobody.
- **Doc ≠ full research.** Each doc's fully-cited source lives under `team/erik-consult/`; the `.claude/docs/` file is the distilled guardrail.

## Key references

- **Team / process docs:** [`team/`](team/) — TESTING_BAR.md (Unity testing bar; incl. Predict-Before-Soak + bounded silence), GIT_PROTOCOL.md, ROLES.md, STATE.md (live coordination), DECISIONS.md (append-only log; see its header protocol), RESUME.md, quality-bars.md (Sponsor-confirmed standing quality bars; maintained via the `/name-the-bar` skill), per-role subdirs, `team/orchestrator/dispatch-template.md`.
- **Godot-era archive:** `c:/Trunk/PRIVATE/RandomGame` (repo + ClickUp list "RandomGame") — full history, decisions, and the `.claude/docs` Godot doc set. Cite it for history; never resume development there.
- **Eval spike (read-only):** `c:/Trunk/PRIVATE/EmbergraveUnitySlice` — working reference for the M-U1 ports (click-move, orbit camera, Zone-D look, castaway, FINDINGS.txt).

# Orchestration rules (imported)

Orchestrator disciplines moved from the user-global CLAUDE.md (2026-08-07, token-burn audit R3) — they load only in orchestrated projects now:

@~/.claude/orchestration-rules.md
