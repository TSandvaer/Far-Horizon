# Rigify vs Mixamo — is Rigify a better solution for Far Horizon's castaway animation?

## Question
Sponsor (verbatim, 2026-07-21): *"you should look into the 'Rigify' plugin for blender, maybe
its a better solution than mixamo."* Far Horizon's hero (castaway v4) is a blocky hand-modeled
mesh, **Mixamo auto-rigged to a Generic 41-bone `mixamorig:*` skeleton** (NOT Humanoid), animated
from a **library of downloaded Mixamo clips** (5 attack clips + walk/run/idle/jump/crouch/hit-react
just shipping). Known pains: the `pickaxe_mine` clip poses the body contorted and Mixamo clips
**cannot be edited**; every clip is a **manual web download** (no API). Is Rigify a better route?

## Bottom line
**No — Rigify does not replace Mixamo here, and adopting it as the character's rig would be a net
loss.** Rigify and Mixamo solve *different* problems: Mixamo gives you a **skeleton + a mocap clip
library** with a clean game export; Rigify gives you an **animator's control rig for hand-posing** —
it produces **zero animation clips** and is a *documented pain to export to game engines*. The
project's real gap is a **bad/uneditable clip**, not a bad rig, so the fix lives in the *clip layer*,
not the *rig layer*. **Recommendation: STAY on the Mixamo Generic rig (path c).** Fix `pickaxe_mine`
by re-sourcing (the existing follow-up) or, if that fails, by **surgically repairing the clip's bad
bone curves on the existing `mixamorig` skeleton in Blender via headless `bpy`** and re-exporting a
Without-Skin FBX — no re-rig, no retarget, no Animator re-wire. Keep Rigify **on the shelf as a
conditional future tool** only if the Sponsor commits a human animator to hand-author a bespoke clip
library — and even then, bake down to the `mixamorig`-named deform skeleton to preserve the wiring.
**Asset routing is unchanged.**

## Evidence

### What Rigify actually is (Q1)
- **Blender Manual — Rigify** ([docs.blender.org/manual/.../addons/rigging/rigify](https://docs.blender.org/manual/en/latest/addons/rigging/rigify/index.html); official doc — **STRONG**, though the live page 403'd to my fetcher so the summary below is corroborated from the manual's indexed text + the Extensions page). Rigify stores rig-generation info in a simple armature called a **meta-rig**; you position its bones to your character, then **Armature tab → Generate Rig** builds a full **control rig** (IK/FK arms + legs, spine, fingers, face) plus a hierarchy of internal bones: `CTRL-`/`MCH-`/`ORG-` (controls, mechanisms, originals) and **`DEF-` deform bones** (the ones the mesh actually skins to). It ships **meta-rig sample templates**: human (biped), quadruped, bird, cat, wolf, horse, plus **"basic" single-bone / limb samples** you assemble for a custom creature.
- **Rigify is a first-party, GPL, zero-cost add-on that ships with Blender** — enable at *Preferences → Add-ons → Rigging → Rigify* ([Rigify — Blender Extensions version history](https://extensions.blender.org/add-ons/rigify/versions/); **STRONG**). Note: since Blender 4.2 LTS the add-on set was reorganized around the Extensions Platform; Rigify remains bundled/first-party and free in the 5.1 the project runs — verify it's enabled in Preferences rather than assuming it's already on.
- **Blocky/non-humanoid fit:** Rigify is **proportion-agnostic** — it rigs from *bone positions*, not mesh thickness, so castaway v4 (a chunky but still **bipedal** humanoid: head/arms/legs) fits the **Human meta-rig fine**; you just drag the meta-rig bones to the blocky joints. Blockiness is **not** the obstacle. (Reasoned from the manual workflow — **MODERATE**.)

### Animation authoring + the game-export problem (Q2)
- **Rigify produces no clips.** It is a *posing* rig; someone (a human animator, or `bpy` keyframing) still has to author every motion. This is the crux: Mixamo hands you *motion*; Rigify hands you *controls to make motion*.
- **Rigify rigs export badly to game engines — documented, not opinion.** Blender's own tracker: [*"rigify: rigs don't export well to game engines"* (T57536)](https://developer.blender.org/T57536) (maintainer tracker — **STRONG**): Rigify **splits deform bones into separate chains per module**, and once the non-deform bones are removed the baked animation is prone to breaking. To ship a Rigify character you must **bake the animation down onto only the `DEF-`/deform bones** and export those — the control rig itself never goes to the engine.
- **The shipped-studio workflow is "quite manual."** [Toyful Games — *Rigify to Unity*](https://www.toyfulgames.com/blog/rigify-to-unity-tutorial) (writeup by the studio that shipped *Very Very Valet* — **MODERATE-STRONG**): they maintain **three rigs** — metarig, the generated control rig, and a hand-built **`MeshDeformRig`** whose bones `Copy Transform`-constrain to the control rig's `ORG-` bones; they pose on the fancy rig, lay all poses on one NLA track, **bake** into the MeshDeformRig, then FBX-export with *Resample Curves off*. They call the process **"quite manual"** and **"unfriendly to rig changes."** Community bridges exist for the same gap ([trynyty/Rigify_DeformBones](https://github.com/trynyty/Rigify_DeformBones), a script to make DEF bones Mecanim-compatible — community tool, **WEAK-MODERATE**), and Unity historically documented the workflow ([Unity Manual — *Using Blender and Rigify*](https://docs.unity3d.com/560/Documentation/Manual/BlenderAndRigify.html); official but **Unity 5.6-era / dated — MODERATE**).
- **`bpy`-scriptability:** Rigify has a Python API, but *driving a control rig well from script* is not how it's meant to be used — its value is a **human** dragging IK handles. Our clip authoring is `bpy`-scripted/headless (per `blender-asset-pipeline.md` §10 dispatched-persona route), so Rigify's core benefit (interactive hand-posing) is largely **wasted** in our pipeline.

### Retargeting the shipped clips (Q3)
- **Vanilla Blender ships no first-party clip retargeter.** Moving a clip between two differently-named skeletons (Mixamo `mixamorig:*` ↔ Rigify `DEF-*`) needs a **third-party add-on** — Auto-Rig Pro (**paid**), Rokoko (free, requires account), or Expy Kit (free/GPL) — or a hand-built constraint bake. (**MODERATE** — well-established; flag to re-verify against Blender 5.1's animation-module state before relying on "no built-in.")
- **The shipped clips do not need retargeting at all if we keep the Mixamo skeleton** — they already bind and play. Introducing Rigify would *create* a retarget requirement that does not exist today, plus fidelity risk on every clip and (likely) a **paid add-on**, which cuts against the in-house/free posture ([[in-house-asset-routes-over-paid-tools]]).

### Unity import + blast radius if the rig source changes (Q4)
- **On-disk ground truth (Read this session — STRONG):** every Castaway rig/clip `.fbx.meta` carries **`animationType: 2` (Generic)** — `Attack_Axe/Dagger/Pickaxe/Spear/Sword`, `Melee_Attack`, `Idle`, `Walking`, `Running`, `Jump*`, `Crouching Idle`, `Sneak Walk`, `Getting Up`, `Stunned`, all 5 hit-reacts, `Breathing Idle`. Generic binds clips **by transform path/name** under the `mixamorig:*` hierarchy (`character-pipeline.md` §Step 4).
- **A Rigify re-rig breaks that binding by construction.** Rigify's deform bones are named `DEF-upper_arm.L` etc., **not** `mixamorig:RightArm`. Under Generic, a rename = no bind. So adopting Rigify as the *skeleton* forces: (1) **re-export/retarget every shipped clip**; (2) **re-wire the Animator** int-selector attack states + all overlay states; (3) **re-seat every held prop** — `HeldAxeRig`/`CastawayArmPose` look bones up by `mixamorig:*` name and carry **per-identity dialed eulers + measured seats** (`procedural-animation-verbs.md`: v3→v4 already proved dialed eulers are identity-specific and must be re-measured); (4) **re-run the full rig-symmetry + skin-weight acceptance gate** (`character-pipeline.md` §Rig-symmetry gate). This is a **full character re-adoption** — precisely the high-blast-radius event the staged-toggle machinery exists to contain. Mixamo, by contrast, already gives the clean **Generic-safe** export (and Mixamo's Humanoid rig is separately *banned* here — it explodes under the scaled scene hierarchy, `character-pipeline.md` §Step 4).

### Effort + risk of the three paths (Q5)
| Path | What it means | Blast radius | Verdict |
|---|---|---|---|
| **(a) Rigify for NEW clips only, on the current rig** | Incoherent as stated — Rigify makes a *new* rig. To use Rigify controls and land on the `mixamorig` DEF skeleton you must **retarget every authored clip** (add-on, likely paid) OR re-skin the mesh to a Rigify skeleton. Adds a permanent bridge step per clip. | Medium-High (per-clip retarget + fidelity risk + new dependency) | **Not worth it** |
| **(b) Full re-rig to Rigify** | Replace the skeleton; re-export/retarget all clips; re-wire Animator; re-seat all props; re-run acceptance gate. | **Very High** (full re-adoption) | **Reject** |
| **(c) Stay Mixamo + repair/re-source bad clips** | Keep the Generic `mixamorig` rig untouched. Fix `pickaxe_mine` by re-sourcing (existing follow-up); if that fails, **import the clip onto the `mixamorig` skeleton in Blender, slerp-resample the corrupted bone curves, re-export Without-Skin FBX**. New bespoke verbs, if ever needed, authored the same way. | **Low** (clip layer only; zero rig/Animator/seat churn) | **Recommend** |
- **Precedent that (c) is already how this team works:** the sneak-gait jerk was a mid-cycle Mixamo clip keyframe discontinuity fixed by **`SneakGaitCurveFix.cs` slerp-resampling the corrupted bone-curve run** in-repo (`procedural-animation-verbs.md`). A contorted `pickaxe_mine` is the *same class of defect* — a bad curve on a good skeleton — repairable in Blender on the `mixamorig` skeleton with **no rig swap** and full `bpy`/headless scriptability. (**STRONG** — repo precedent.)

### Licensing / cost (Q6)
- **Rigify:** bundled with Blender, **GPL, $0**, enable in Preferences (**STRONG**).
- **Mixamo:** free with a free Adobe account; **manual web step, no API/MCP**, fixed library you cannot edit (`character-pipeline.md` §Step 3; project reality).
- **Cost is not a differentiator — both are free.** The differentiators are *capability fit* and *blast radius*, both of which favor staying on Mixamo.

## Application to Far Horizon

**Recommendation (one page):**

1. **Keep the Mixamo Generic 41-bone `mixamorig` rig as the castaway's skeleton. Do NOT adopt Rigify as the rig.** Rigify's game export is a documented pain (deform-chain split, manual bake-down), and switching would break the entire Generic wiring: clip binds, Animator states, `HeldAxeRig`/`CastawayArmPose` bone-name lookups, and every per-identity dialed seat/euler — a full character re-adoption for zero gameplay gain.

2. **The current pain is a bad CLIP, not a bad RIG.** Rigify produces no clips, so it cannot fix `pickaxe_mine`. Fix it in the clip layer: **(i)** re-source a cleaner Mixamo mine/swing clip (the cheapest option, already a follow-up); **(ii)** if re-sourcing fails, repair the contorted clip's bad bone curves on the `mixamorig` skeleton in Blender via headless `bpy` (the `SneakGaitCurveFix` pattern, moved to Blender), re-export **Without-Skin**, bind by transform path — no rig/Animator/seat changes. This route is in-house, free, and `bpy`-scriptable via the established Blender pipeline.

3. **Hold Rigify as a conditional future tool, not the default.** The *only* scenario where Rigify (or Auto-Rig Pro) earns its keep is if the Sponsor brings in a **human animator to hand-author a large bespoke clip library** — Rigify's IK/FK controls genuinely speed *interactive* posing. Even then: author on the control rig, **bake down onto a `mixamorig`-named deform skeleton** so the Generic Animator/seat wiring survives, and budget the retarget-add-on cost. For a solo-sponsor, kid-friendly, small-team project whose clips come from mocap + `bpy` curve-repair (not a pro animator hand-keying), that scenario is not on the horizon.

**Asset-routing impact:** **No routing change.** The **Characters** route stays *Hyper3D Rodin → Mixamo → Unity (Generic)*; the **Action-verb animation** route stays the two idioms in `procedural-animation-verbs.md`. Rigify was evaluated and **declined** as a rig source.

**Optional clarifying footnote (not a routing change) — offered for Priya/Sponsor to accept or drop.** To stop a future session mistaking Rigify for the answer, append one line to `asset-routing.md`'s **Characters** row (or its notes):

> *Clip authoring/repair is done on the Mixamo `mixamorig` deform skeleton in Blender (`bpy`), NOT via a rig swap. Rigify was evaluated 2026-07-21 and declined: it produces no clips, exports poorly to game engines (deform-chain split; manual bake-down), and re-rigging would break the Generic clip-bind + Animator + held-prop seat wiring. See `team/erik-consult/rigify-vs-mixamo-research.md`.*

*(Per the committed-artifact citation rule: if any spec cites this note as LOCKED authority, this file must be committed to `main` before the citing artifact merges.)*
