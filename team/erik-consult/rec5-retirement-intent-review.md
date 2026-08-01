# Rec 5 Retirement — Intent Review

## Question
PR #382 retires `lowpoly-quality.md` Rec 5 (traced to my `procedural-shadergraph-quality-research.md` §D). Was Rec 5 ever aimed at weapon-pack assets, does retirement (vs. scope-limiting) read that intent correctly, do Rec 2 (`_FlatShading`) / Rec 6 (`_AOStrength`) need the same scrutiny, and was the specific `#EBE6D6` value load-bearing?

**Epistemic note:** per the brief, I have not re-read the source doc or repo state for this review — I'm reasoning from the pasted, already-verified facts plus my recollection of what that class of recommendation was written to solve. Where I'm reconstructing intent rather than restating a fact, I've labeled it.

## Bottom line
Retirement is the right call, not scope-limiting — Rec 5's mechanism was written for exactly the weapon-class assets the enumeration exhausts, and the world that assumption depended on (per-asset materials) was overtaken two days later by the shared-palette contract. Rec 6 is the sharper problem: it's not just misapplied, it's structurally unreachable on `URP/Unlit`, and its own "before FBX export" wording places it on the same Blender/weapon surface Rec 5 was written for — it needs a corrective scope note, not silence. Rec 2 has no such tell and is clean. `#EBE6D6` was illustrative, not a calibrated value — no Sponsor time owed on it.

## Answers

### Q1 — Was the distinct-material-slot mechanism ever meant for weapon-pack assets?
**Likely: yes, under an assumption the project no longer holds.** The dating is the tell: Rec 5 landed 2026-06-17, two days *before* the shared-palette contract (2026-06-19). At the time I wrote §D, the working assumption for weapon-class props was still per-asset procedural generation with per-asset material tuning — the Sponsor's Blender-pipeline + one-shared-material decision hadn't happened yet. `AxeAssetGen.cs` not existing repo-wide is consistent with this: it reads as a prototype/planning-stage name for a procedural axe generator that was superseded by `WeaponPackAssetGen.cs` once the pipeline consolidated to Blender + one palette material. I don't have a different, still-live asset class in mind that Rec 5 was actually protecting — this is the "I meant it generally, and the contract now overrides it" case the brief flagged as a good answer. It just happens the "generally" was already weapon-scoped.

### Q2 — Does retirement (not scope-limiting) read this correctly?
**Agree — retirement is right.** Scope-limiting only makes sense if there's a live asset class the recommendation still fits. The enumeration exhausts the candidates: weapon-pack (axe/pickaxe/sword) is now explicitly one-material-many-meshes by design (`WeaponPackAssetGen.cs:33,98`); campfire/stump are procedural-only with vertex colour as their equivalent knob (a material-slot recommendation doesn't transfer to a class that was never going to get a material slot); the "chest" never existed, so it's not a real third leg. There's no fourth asset class I was writing about. Scope-limiting a recommendation to an empty set is retirement with extra words — Uma's call is correct.

### Q3 — Do Rec 2 / Rec 6 need the same treatment?
**Different verdicts for each — this is the finding worth flagging harder than the ticket's actual subject.**

- **Rec 2 (`_FlatShading`)** — clean. Nothing in the pasted facts ties it to the weapon/Blender surface; it's a Shader Graph custom-function toggle for procedurally-generated, ddx/ddy-faceted Lit materials (world/terrain/rock/prop family). It still holds where it was always meant to hold. No action.

- **Rec 6 (`_AOStrength`)** — needs a correction, not a shrug. Two independent things are true at once: (1) the underlying pattern — bake AO into vertex colour, read it back via a Lit Shader Graph vertex-colour node — is real and is one of the seven adoptable patterns `lowpoly-quality.md` already documents correctly elsewhere for procedural world geometry; (2) the specific clause "bake in Blender before FBX export" was never going to apply to that world-geometry use, because procedural `LowPolyMeshes`/`FacetedRock` meshes are built directly as Unity meshes at editor/runtime — there IS no FBX export step in that path. FBX export only exists on the Blender-authored side, i.e. weapons/props. So that one clause was misfiled against the weapon surface from the day it was written, same category error as Rec 5, just not caught at the time because nobody chased it against `WeaponPackAssetGen.cs`. The fix is narrow: strike or re-home the "before FBX export" wording to make explicit it does not apply to `Mat_WeaponPalette.mat` / the Blender weapon route, leaving the vertex-AO-on-Lit-procedural-geometry guidance intact where it's actually used.

- **Is the vertex-colour half ever reachable on the weapon material?** No — never, independent of timing. This is stronger than Rec 5's problem. Rec 5 was a *design* mismatch that a later contract happened to make worse; Rec 6's vertex-colour path is *physically* unreachable on `URP/Unlit`, because that shader's `Attributes` struct never declared a `COLOR` semantic. There was no window in which setting vertex colour on a weapon FBX and expecting `_AOStrength`-style modulation would have worked. That's worth a one-line addendum in the doc so a future developer doesn't try it and silently get nothing — silent no-ops are worse than a hard error because nothing tells you to stop.

### Q4 — Was `#EBE6D6` load-bearing?
**Illustrative, not load-bearing.** My research pattern in this doc class is "here's a technique + why it works," not "here's the exact production hex your palette should use" — that class of number is normally a worked example accompanying the mechanism (here: "a separate material slot lets you nudge edge-highlight warmth per asset tier"), not a value derived from a colour-grading pass, a reference-image sample, or Sponsor sign-off. Once the mechanism (distinct material slots) is retired, the example value that rode along with it has no independent standing — it was never validated against `art-direction.md`'s board or soaked by the Sponsor. Uma declining to push it into 15 shipped FBXs and calling it a Sponsor call is the right instinct, but I'd go slightly further: it isn't even reaching the bar of "a Sponsor call" — there's nothing behind the number to make it worth asking about. It can be dropped with the rest of Rec 5, not carried forward as an open question.

## Application to Far Horizon
- No doc edits from me (out of scope per the brief) — this is input for whoever finalizes #382.
- Recommend the PR's actual change (retire Rec 5 wholesale) proceed unmodified.
- Recommend a follow-up (small, mechanical) narrow Rec 6's FBX-export clause to exclude the weapon/Unlit material explicitly, distinct from and lower-urgency than #382's Rec-5 retirement — it's a correctness note on a still-valid pattern, not a retirement.
- No Sponsor-facing question needed on `#EBE6D6` — it dies with Rec 5.
