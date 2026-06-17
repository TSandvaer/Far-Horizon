# Far Horizon

A 3D survival game built in **Unity 6 (6000.4.10f1) / URP**, desktop-first (Windows). A young, hopeful castaway washes ashore and survives his way toward the far horizon. Visual direction: **low-poly smooth-shaded** world (faceted meshes, soft gradient lighting, warm/lush palette) with a quality pass (bloom / grading / fog / gradient skybox) — the Sponsor-approved "Zone D" look from the 2026-06 engine-eval spike.

## Context

- **Director / sole stakeholder ("Sponsor"):** Thomas. Single delegated decision-maker; orchestrator handles team coordination.
- **Heritage:** Successor to the Godot project *Embergrave/RandomGame* (archived; `c:/Trunk/PRIVATE/RandomGame`). Engine decision 2026-06-12: migrate to Unity (RandomGame `team/DECISIONS.md`, ticket 86ca7y46c). The Unity eval spike at `c:/Trunk/PRIVATE/EmbergraveUnitySlice` is a READ-ONLY reference for the M-U1 ports — never modify it.
- **Core feel (Sponsor-locked):** small character in a big alive world; **WASD movement + run (Shift) + jump (Space)** — pivoted 2026-06-16 from the original PoE-style click-to-move (implementation backlog `86ca9yq2x`→`yq34`→`yq3q`; the live build still uses click-to-move until those land); mouse-orbit camera + zoom; survival loop (M-U2 starts THIN: one need → craft axe → chop → campfire). North-star: world feels BIG and ENDLESS — a journey.
- **Player character:** low-poly 3D castaway (Quaternius CC0 base, warm castaway recolor; young + happy identity per the archived PixelLab design sheets in RandomGame `_castaway_judge/`).
- **Distribution:** Windows desktop build (`Build/Windows/FarHorizon.exe`). No HTML5/WebGL target.
- **Tracker:** ClickUp list **"Far Horizon"** (list id `901523878268`, space `90156932495`). The RandomGame list is the Godot-era archive.
- **PixelLab:** NOT a Far Horizon concern — Sponsor uses the subscription for other projects (verbatim 2026-06-12 evening: "im using pixellab for other projects, dont worry about it"); do not track its cost or revisit it as a project decision. Pixel-art-native, ruled out for this game's characters/world; the asset-creation route here is Blender + Blender MCP (see unity-conventions.md §Asset creation).

## Tech stack & project facts

- **Unity 6** `6000.4.10f1` at `C:/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe`; URP.
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
- **Testing bar.** Paired EditMode/PlayMode tests + green checks + a SHIPPED-BUILD verification (built exe runs; capture evidence for anything visual) + Tess sign-off before "complete". Sponsor will not debug. See `team/TESTING_BAR.md`.
- **Shipped-build capture gate** (successor to the HTML5 gate): anything UX/visually-visible needs evidence captured from the BUILT exe (not just the editor) before merge — editor-vs-runtime divergence is a proven failure class (spike iter6 "legs-up" incident).
- **Self-Test Report gate.** UX-visible PRs need an author-posted Self-Test Report comment before Tess reviews.
- **ClickUp status as hard gate.** Every dispatch / PR-open / merge pairs with a status move on list `901523878268` in the same tool round.
- **Orchestrator never codes** (R&D-lane exception for MCP-bound generation + Sponsor-interactive iteration; every R&D burst closes with a harvest PR + productionization tickets).
- **Always parallel dispatch** where dependencies allow; tickets aren't progress, dispatches are.
- **Agent liveness from probe, NEVER assumption.** Report in-flight state only from a fresh `SendMessage`-by-agentId probe + `git log` on the worktree + `gh pr view`. Enforced by the `agent-liveness-stop.sh` hook.
- **Tightened final-report contract.** Sub-agent reports ≤200 words: verdict + blockers + key paths + doc-updates; every claim cites verifiable evidence (run/commit/path). Detail goes in PR body / ticket comments.
- **Sponsor soak = direct artifact.** Any soak ask includes the exact exe path + the expected HUD build stamp.
- **Never fabricate, never guess, never extrapolate** (sub-agent inheritance surface). Concrete values — URLs, IDs, SHAs, file paths, command output, ticket/run IDs — must be fetched from a real source, never invented or pattern-extrapolated. Fetch, don't guess: PR URL via `gh pr view`, SHA via `git rev-parse`, ticket state via ClickUp MCP. Observed-symptom claims in tickets/PRs/reports need a verifiable source in the same paragraph; label hypotheses explicitly (`Hypothesis:` / `Likely:`). **The creating turn is never the referencing turn:** never batch a producer call (ticket create, Agent dispatch, `gh pr create`, `git commit`) with a consumer that writes the produced value; if a value hasn't been seen in a tool result, write the literal token `<pending>`.

## Detailed Documentation

Auto-loaded at session start via `.claude/hooks/session-start-read-docs.sh`. **Sub-agents do NOT inherit the auto-load — Read every `.claude/docs/*.md` before starting work.** **`unity6-mastery.md` is the MANDATORY pre-work read for ALL Unity code (Sponsor-stressed 2026-06-16) — Drew/Devon read it before every task, every action.** The `maintain-docs` Stop hook captures new findings each turn.

- [Art Direction](.claude/docs/art-direction.md) — Sponsor's inspiration board (`inspiration/*.png`): warm/lush, human-scale landmarks, small-player/big-alive-world; **look at the actual images before any visual work** (engine-agnostic carry from RandomGame)
- [Unity Conventions](.claude/docs/unity-conventions.md) — hard-won Unity/URP findings from the eval spike + bootstrap: headless rituals, editor-vs-runtime serialization traps, FBX/rig gotchas, low-poly mesh/normals patterns
- [Character Pipeline](.claude/docs/character-pipeline.md) — generate a chunky-low-poly character via Hyper3D Rodin Image-to-3D → Mixamo auto-rig → Unity Humanoid; non-obvious gotchas (pose is driven by the reference image, Quad-not-Tri, de-light, with-skin/without-skin Mixamo split)
- [Unity 6 Mastery](.claude/docs/unity6-mastery.md) — **MANDATORY Unity 6/URP daily-use guardrails** (rendering path/Forward+, GPU Resident Drawer, draw-call batching, lighting budget, GC/scripting rules, ScriptableObject architecture, UI Toolkit, texture/mesh import, IL2CPP build) — read before ANY Unity code, every action. Full cited reference: `team/erik-consult/unity6-mastery-research.md` (Sponsor-commissioned 2026-06-16)
- [Far Horizon — game concept](.claude/docs/vision-far-horizon-game-concept.md) — Sponsor's full survival-arc vision (shipwreck → branches/stones → crafting table → axe → chop wood → bonfire → berries/hunger → fresh-water/thirst); difficulty/scariness adjustable for kids + adults. ⚠ introduces hunger+thirst needs beyond the current WARMTH-only M-U2 loop — reconcile scope before building those

## Key references

- **Team / process docs:** [`team/`](team/) — TESTING_BAR.md (Unity testing bar), GIT_PROTOCOL.md, ROLES.md, STATE.md (live coordination), DECISIONS.md (append-only log; see its header protocol), RESUME.md, per-role subdirs, `team/orchestrator/dispatch-template.md`.
- **Godot-era archive:** `c:/Trunk/PRIVATE/RandomGame` (repo + ClickUp list "RandomGame") — full history, decisions, and the `.claude/docs` Godot doc set. Cite it for history; never resume development there.
- **Eval spike (read-only):** `c:/Trunk/PRIVATE/EmbergraveUnitySlice` — working reference for the M-U1 ports (click-move, orbit camera, Zone-D look, castaway, FINDINGS.txt).
