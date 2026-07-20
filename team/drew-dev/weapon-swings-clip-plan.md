# Weapon-Swings Clip Plan + Sponsor Mixamo Checklist — `86caffwv5` (PHASE 1)

**Author:** Drew (Dev2). **Status:** PHASE 1 — doc-only prep (clip plan + Sponsor Mixamo checklist).
No Unity build, no runtime code this phase. **Base:** `origin/main` @ `af6f93c` (castaway v4 is the
live hero, PR #317 merged — the impl gate is CLEARED).

> **File-location note:** the dispatch brief named `team/drew/…`; the established repo workspace
> folder is `team/drew-dev/` (matches the persona bio + the `team/uma-ux/` naming precedent), so the
> deliverable lands here. No `team/drew/` folder exists on `main`.

**Diagnose-before-hypothesize applied.** The ticket body says *"Humanoid Animator."* I verified against
the repo instead of assuming — **it is GENERIC, not Humanoid** (see §1). The plan is built on the
verified rig, not the ticket's framing.

---

## 0. TL;DR for the Sponsor

You source **5 NEW Mixamo attack clips** (axe, pickaxe, knife/dagger, spear, sword-slash). Per your
decisions on the ticket (comment `90150243232739` + the 2026-07-20 popup) — per-class distinctiveness
over sourcing economy; Uma's "axe+pickaxe share one clip" economy is rejected. Download each new clip
**Without Skin, FBX-for-Unity, 30 FPS, Keyframe Reduction None** and drop the 5 FBXs into
`Assets/Art/Character/Castaway/` with the filenames in §3. Then Phase 2 (a separate dispatch) wires them.

**Sword decision (RESOLVED 2026-07-20, Sponsor verbatim):** *"sword should have a real slash (sideways
swing) it should also have heavy attack (swing from above)."* → The sword's **LIGHT (left-click) attack
is a NEW slash clip** (included in the 5). The existing overhead clip (`Melee_Attack.fbx` / CastawayMelee)
is **RESERVED for the future sword HEAVY attack** — that mechanic (a second input + its own damage/timing)
is **OUT OF SCOPE for `86caffwv5`**; a separate follow-up ticket owns it. Phase 2 must NOT repurpose the
overhead clip elsewhere. See §5.

---

## 1. LIVE v4 rig facts — VERIFIED from `origin/main` @ `af6f93c`

### 1.1 Avatar type — **GENERIC** (ticket's "Humanoid" is wrong)

| Fact | Value | Source |
|---|---|---|
| v4 hero rig | `Assets/Art/Character/Castaway/v4/castaway_v4_rigged.fbx` | live hero (PR #317) |
| v4 `animationType` | **`2` = Generic** (0=None,1=Legacy,2=Generic,3=Humanoid) | `…/v4/castaway_v4_rigged.fbx.meta:101` |
| v4 `importAnimation` | `0` (rig FBX ships mesh+skeleton only, no clip) | `…/v4/castaway_v4_rigged.fbx.meta:82` |
| v4 mesh `globalScale` | `0.49832228` (Blender-export scale compensation) | `…/v4/castaway_v4_rigged.fbx.meta:36` |
| Existing chop clip | `Assets/Art/Character/Castaway/Melee_Attack.fbx` | ticket body + repo |
| Its `animationType` | **`2` = Generic** | `Melee_Attack.fbx.meta:130` |
| Its clip name / take | `CastawayMelee` / `mixamo.com`, frames `0–68`, `loop:0`, `loopTime:0` | `Melee_Attack.fbx.meta:34–45` |

**Why this matters:** Generic rigs retarget by **transform-path binding** (bone-name matching), NOT by
Humanoid muscle-space. A clip plays on v4 iff its skeleton bone paths match v4's. Do NOT set any new
clip to Humanoid — that would fight the Generic pipeline (and Humanoid on this scaled hierarchy is the
known "explode-to-a-cone" trap, `character-pipeline.md` §4).

### 1.2 Bone naming / root — standard Mixamo, 41 bones

Extracted from the raw v4 FBX (`grep -aoE "mixamorig:[A-Za-z0-9]+" … | sort -u`): **41 bones**, root
`mixamorig:Hips`, standard Mixamo names (`Spine/Spine1/Spine2`, `Neck/Head`, `Left|Right`
`Shoulder→Arm→ForeArm→Hand`, `UpLeg→Leg→Foot→ToeBase→Toe`). Hands carry **thumb + index chains only**
(`…Thumb1-4`, `…Index1-4`) — the mitten-hand v4 model; no middle/ring/pinky bones.

**Consequence for clip sourcing:** any Mixamo animation downloaded Without-Skin ships a `mixamorig:*`
skeleton with these same standard names → it binds onto v4 by transform path, exactly like the existing
`Melee_Attack.fbx` does today. Mixamo's Standard-Skeleton names are consistent regardless of which
character was uploaded, so the new clips will retarget the same way.

### 1.3 How the existing chop clip is imported + retargeted onto v4 TODAY

Chain (verified end to end):

1. **Import** — `Melee_Attack.fbx` imports as Generic (`animationType:2`) with `avatarSetup:1`
   (create-avatar-from-this-model). Its single take is renamed to **`CastawayMelee`** at import by
   `Assets/Scripts/Editor/CharacterAssetGen.cs` (`RenameNonLooping`, `MeleeClip` const line 238;
   rename call line 1071).
2. **Controller state** — `Assets/Art/Character/Castaway/CastawayAnimator.controller` has an **`Attack`
   state** (line 118) whose motion is guid `88296f2328c44695ba918efe10d2cc3e` = `Melee_Attack.fbx`
   (matches `Melee_Attack.fbx.meta:2`). Its `m_SpeedParameter` = **`ChopSpeed`** (line 135).
3. **Transition** — reached by **`AnyState → Attack`** on the **`Chop`** trigger; the state one-shots
   and returns to `Locomotion`/`Idle` (the base-layer OVERLAY-state idiom, `procedural-animation-verbs.md`).
4. **Retarget** — the `Attack` state plays the Generic `CastawayMelee` clip on the live castaway
   Animator (whose avatar comes from the v4 Generic rig). Binding is by transform path — **works on v4
   because bone names match** (§1.2). Proven: chop already plays correctly on v4 in the shipped build.
5. **Trigger source** — `Assets/Scripts/Runtime/CastawayCharacter.cs` `TriggerChop()` (line 480) pushes
   `ChopSpeed` then fires the `Chop` trigger (`ChopParam="Chop"` line 235, `ChopSpeedParam="ChopSpeed"`
   line 240, `MeleeClipName="CastawayMelee"` line 257). `MeleeClipLength` (line 500) reads the live
   clip length so hold-chop cadence ties to the real authored length.
6. **Held weapon follows automatically** — `HeldAxeRig` (execution order 100) seats the held prop on
   the **hand bone** AFTER `CastawayArmPose` (order 50) poses the arm. So any swing clip that moves the
   arm carries the held weapon with it — no per-clip held-weapon work needed.
   → **The deferred v4 right-thumb skin-weight defect (`86cau4za2`) does NOT gate this ticket:** the
   swing reads through arm/hand-bone motion + the hand-seated weapon, not thumb articulation.

**Controller is code-generated**, not hand-authored: `CharacterAssetGen.cs` builds states/params via
`AddState`/`AddParameter`/`AnyState→state` (params at lines 1217-1237; `Attack`/Chop/HitRegion there).
So Phase-2's new states are authored by extending `CharacterAssetGen.cs`, regenerating, and **committing
the regenerated `.controller` + the new clip `.fbx`+`.meta`** (the build ships the committed snapshot —
`[[unity-procedural-committed-assets-go-stale]]`).

---

## 2. The 5 proposed Mixamo clips (per-class distinctiveness)

Motion signatures are Uma's (`combat-cluster-design-brief.md` §1.1). All 5 are NEW downloads; the sword's
LIGHT attack is a new slash (the existing overhead is reserved for the future sword HEAVY attack — §5).
The data layer already names the swing per class via `WeaponDef.AnimationId` —
verified in `Assets/Scripts/Runtime/Combat/WeaponCatalog.cs`: **5 opaque ids** `axe_chop`,
`pickaxe_mine`, `dagger_stab`, `sword_slash`, `spear_thrust` (shared across wood/stone/iron tiers — the
material tier does NOT change the motion, only weight/sound flavor).

> **Mixamo clip names below are SEARCH TERMS + unverified candidate suggestions** — the Sponsor confirms
> the exact clip in Mixamo's live library. I do not assert any exact library clip name as fact.

| Class | `AnimationId` | New clip? | Motion signature (Uma §1.1) | Mixamo search terms | Candidate clips (verify in library) | `swingImpactDelaySeconds` start |
|---|---|---|---|---|---|---|
| **Axe** | `axe_chop` | **NEW** | Heavy vertical/diagonal overhead chop, whole-body commit, long follow-through | `axe`, `chop`, `great sword slash`, `melee attack downward` | a two-handed/diagonal power chop **distinct from the reserved overhead** | **~0.40s** |
| **Pickaxe** | `pickaxe_mine` | **NEW** | Overhead pick strike, pointed downward drive (a mining swing, not a slash) | `mining`, `pickaxe`, `axe`, `swing` | a downward pick/mining strike | **~0.40s** |
| **Knife / Dagger** | `dagger_stab` | **NEW** | Short forward stab/jab, elbow-driven, minimal body rotation, fastest cadence | `stab`, `knife attack`, `dagger`, `jab` | a quick forward knife stab | **~0.18s** |
| **Spear** | `spear_thrust` | **NEW** | Forward linear lunge, both hands behind the point, longest reach; impact at full extension | `spear`, `thrust`, `stab`, `lunge` | a two-handed spear thrust | **~0.34s** |
| **Sword (light)** | `sword_slash` | **NEW** | Wide diagonal/horizontal **slash** (sideways swing), hips leading, arcing follow-through | `sword slash`, `sword and shield slash`, `stable sword outward slash` | a horizontal/diagonal one-handed sword slash | **~0.28s** |
| _Sword (heavy) — OOS_ | _(future)_ | reserve existing `CastawayMelee` | Overhead swing-from-above (the existing `Melee_Attack.fbx`) — **future sword HEAVY attack, separate ticket; do NOT wire or repurpose here** | (existing, in-repo) | — | — |

**Selection criteria to hand the Sponsor per clip** (Uma §1.1, distilled):
1. Reads as **Generic** in Unity (do NOT check Humanoid) — bone-path binding onto v4.
2. **One-shot, non-looping**, clear start-and-return (wires like `Attack`/`Jump`).
3. **One unambiguous impact frame** (so `swingImpactDelaySeconds` tunes to it) — reject mushy/multi-hit peaks.
4. **Matches the class motion signature** (overhead / pick / jab / thrust) — a slash on the spear breaks the reach read.
5. **Reads at orbit-cam distance** — big legible arm motion; subtle wrist-only clips vanish.
6. **In place** — the swing should not translate the character (the NavMeshAgent owns XZ). Prefer a
   standing attack; if the chosen clip steps forward and Mixamo offers an **"In Place"** checkbox, CHECK it.

---

## 3. SPONSOR DOWNLOAD CHECKLIST (executable with zero repo/Unity context)

**Is a character upload needed?** *Not strictly* — the existing chop clip proves a Mixamo clip binds
onto our castaway by bone-name alone (any Mixamo character's Without-Skin export carries the standard
`mixamorig:*` skeleton). **But best fidelity** (feet/hands land where they should, less sliding) comes
from applying the animation to the SAME castaway already in your Mixamo library (the character you
rigged v4 from). So:

**Step A — pick the character in Mixamo (mixamo.com, your Adobe login):**
- **PREFERRED:** open **My Assets** and select the castaway character you already uploaded/rigged (the
  one used for the v4/v3 hero). Apply animations to it.
- **FALLBACK (only if that character is gone from your library):** click **Upload Character** and
  upload `Assets/Art/Character/Castaway/v4/castaway_v4_rigged.fbx` from the repo; run the Auto-Rigger
  (markers on chin / both wrists / both elbows / both knees / groin; Use Symmetry ON; Skeleton LOD =
  Standard). Then apply animations to it.

**Step B — for EACH of the 5 clips (axe, pickaxe, knife/dagger, spear, sword-slash):**
1. Search Mixamo using the §2 search terms; pick a clip matching that class's motion signature +
   the 6 selection criteria (§2). Preview it on the castaway — watch the shoulders/elbows for pinch.
2. Click **Download** with these settings (identical to how the existing clips were sourced):
   - Format: **FBX for Unity (.fbx)**
   - Skin: **Without Skin** ← critical (skeleton + animation only; binds by path under Generic)
   - Frames per Second: **30**
   - Keyframe Reduction: **none**
   - **In Place: checked** if the clip offers it and has any forward step (keeps the swing stationary)
3. Save the downloaded file to the repo folder **`Assets/Art/Character/Castaway/`** with this **exact
   target filename**:

| Class | Save as (exact) | Intended clip name (Phase 2 renames the take to this) |
|---|---|---|
| Axe | `Attack_Axe.fbx` | `CastawayAxeSwing` |
| Pickaxe | `Attack_Pickaxe.fbx` | `CastawayPickaxeSwing` |
| Knife / Dagger | `Attack_Dagger.fbx` | `CastawayDaggerStab` |
| Spear | `Attack_Spear.fbx` | `CastawaySpearThrust` |
| Sword (light slash) | `Attack_Sword.fbx` | `CastawaySwordSlash` |

**Sanity check per file:** a Without-Skin FBX is small (**hundreds of KB**, no mesh). If it's multi-MB
you accidentally downloaded With-Skin — re-download Without Skin.

**Sword HEAVY attack:** no download this ticket — the existing `Melee_Attack.fbx` (overhead) is reserved
for it and stays in-repo untouched; the heavy-attack mechanic is a separate follow-up ticket (§5).

**Handoff:** drop the 5 FBXs in the folder and tell the orchestrator "swing clips are in." Phase 2
(a separate dispatch) imports + wires them. Do NOT open Unity or edit anything — Phase 2 owns the import
settings + controller wiring.

---

## 4. Phase-2 wiring plan (for the next dispatch — NOT this phase)

### 4.1 Animator: one attack per class via an int-selector (mirror HitRegion)

The controller already has the perfect precedent: **hit-react uses one `Hit` trigger + a `HitRegion`
int** to pick among 5 clips via `AnyState→state` transitions gated on `Hit` **AND** `HitRegion Equals N`
(controller lines 338/341 for HitToBody; params generated in `CharacterAssetGen.cs` lines 1234-1235).

**Plan:** add a **`WeaponClass` int** param + one attack state per class, all reached by
`AnyState→AttackX` on the **existing `Chop` trigger AND `WeaponClass Equals N`**. Each one-shots and
returns to `Locomotion`/`Idle` (the `Attack`/`Jump` overlay idiom). All keep `m_SpeedParameter=ChopSpeed`
(tool-use-speed scaling stays free).

| `WeaponClass` | Clip | State | `AnimationId` |
|---|---|---|---|
| `0` axe | `CastawayAxeSwing` (`Attack_Axe.fbx`) | `AttackAxe` (new) | `axe_chop` |
| `1` pickaxe | `CastawayPickaxeSwing` (`Attack_Pickaxe.fbx`) | `AttackPickaxe` (new) | `pickaxe_mine` |
| `2` dagger | `CastawayDaggerStab` (`Attack_Dagger.fbx`) | `AttackDagger` (new) | `dagger_stab` |
| `3` spear | `CastawaySpearThrust` (`Attack_Spear.fbx`) | `AttackSpear` (new) | `spear_thrust` |
| `4` sword (light) | `CastawaySwordSlash` (`Attack_Sword.fbx`) | `AttackSword` (new) | `sword_slash` |

**Reserved (do NOT wire this ticket):** the existing `Attack` state + `CastawayMelee` (`Melee_Attack.fbx`)
overhead is KEPT in the controller but left unwired from the new selector — it is the future **sword
HEAVY attack** clip (a separate follow-up ticket owns the second-input mechanic + damage/timing). Phase 2
must not delete, remap, or repurpose it. (Tree-chop moves onto `AttackAxe`; the old `Attack` state no
longer drives any current light-attack trigger — it simply waits for the heavy-attack ticket.)

Authored by extending `CharacterAssetGen.cs` (`AddState`+`AddParameter`+transitions), regenerate,
**commit** the `.controller` + the 5 new `.fbx`+`.meta`. Import each new FBX as **Generic** (mirror
`Melee_Attack.fbx.meta` — `animationType:2`, non-looping, root XZ locked). Add a `WeaponClassParam`
mirror const to both `CastawayCharacter.cs` and `CharacterAssetGen.cs` (the ControllerParamNamesMatch
contract).

### 4.2 Left-click routing (already exists — extend the placeholder)

`Assets/Scripts/Runtime/Combat/MeleeAttack.cs` (Combat POC #224) already does the input work:
- `Update()` reads `Input.GetMouseButtonDown(0)` OR the injectable `RequestAttackClick()` latch.
- Guards via the pure `ShouldAttackOnClick(weaponSelected, targetInReach, uiPanelOpen, pointerOverUI,
  rmbHeld)` — one click = one strike, UI-click + camera-drag (RMB) guarded (`UiInputGate.CaptureWorldInput`).
- Resolves `SelectedWeapon` (the belt `WeaponDef`) → has `weapon.AnimationId`.
- Cooldown = `baseAttackCooldown / attackSpeed` (single-flight throttle).
- **Currently PLACEHOLDER** (line 178): `character.TriggerChop()` for ALL weapons.

**Phase-2 change (minimal):** replace the placeholder with a per-class trigger. Add
`CastawayCharacter.TriggerAttack(int weaponClass, float speed)` that sets `WeaponClass` + `ChopSpeed`
then fires the `Chop` trigger (the existing `TriggerChop` becomes `TriggerAttack(0, chopSpeed)` — axe —
so tree-chop keeps playing the axe swing). Map `weapon.AnimationId` → `WeaponClass` in `MeleeAttack`
(`axe_chop`→0 … `sword_slash`→4). **No new input path, no parallel attack fork** — the guard truth-table
+ single-flight are untouched.

**Cross-verb unification:** `axe_chop` also drives tree-chop (`ChopTree`→`TriggerChop`) and
`pickaxe_mine` drives the mine verb (`MineBoulder`/`MineOre`). The `AnimationId`→`WeaponClass` map is the
single swing-selection seam for combat AND resource verbs — one place, no duplication.

### 4.3 Per-clip retarget verification (Phase 2, before wiring each)

For each new FBX: confirm `animationType:2` (Generic) in the `.meta`; confirm the clip's skeleton root is
`mixamorig:Hips` and bone names are standard (`grep -aoE "mixamorig:[A-Za-z0-9]+"`); play the state on
the live castaway and confirm the arm swings + the held weapon follows (HeldAxeRig seats it). If a clip
imports with a wrong avatar or slides the feet, re-check Without-Skin + In-Place at download.

### 4.4 Test plan

- **EditMode (deterministic, the real gate):**
  - Extend `ChopAnimatorControllerTests.cs` / add `AttackSwingControllerTests.cs`: assert the controller
    has params `WeaponClass`(int) + `Chop`(trigger); an `AnyState→AttackX` transition per class gated on
    `Chop` + `WeaponClass Equals N`; each attack state's clip name matches the intended clip; each is
    non-looping and returns to `Locomotion`/`Idle`.
  - `ControllerParamNamesMatch`-style: `CastawayCharacter.WeaponClassParam` == `CharacterAssetGen`'s.
  - Extend `WeaponSetTests.cs`: assert every `WeaponDef.AnimationId` maps to a defined `WeaponClass`
    (no orphan id) — a guard against a new weapon with an unmapped swing.
  - `MeleeAttack` mapping test: `PerformAttack` on an axe def sets `WeaponClass=0`; spear sets `3`; etc.
    (via a fake CastawayCharacter capturing the last-set class) — proves per-weapon routing without the
    Animator ticking headlessly.
  - `CommittedAssetDriftGuardTests.cs`: the regenerated controller matches the committed one (no drift).
- **PlayMode (advisory — CI PlayMode is unreliable for interaction, `[[advisory-playmode-job-unreliable-soak-is-interaction-gate]]`):**
  extend `CombatPlayModeTests.cs` — `RequestAttackClick()` with each weapon selected fires exactly one
  swing (`SwingsFired` +1), single-flight blocks a second click within cooldown. Assert via the
  input-independent latch (Animator doesn't advance headlessly — deltaTime≈0).
- **Shipped-build capture (the visual gate):** a `-verifySwings`-class capture that plays each state on
  the built exe and captures a frame mid-swing (editor-vs-runtime divergence is a proven failure class).
- **Soak (the real interaction + feel gate):** the Sponsor left-clicks each equipped weapon and judges
  per-class distinctiveness + impact timing.

### 4.5 Predict-Before-Soak drafts (per weapon — graded at the soak)

- **Axe:** "Left-click with the axe equipped plays a heavy overhead chop once and returns to idle;
  impact lands ~0.40s in at the bottom of the arc; a second click before recovery is ignored."
- **Pickaxe:** "Left-click with the pickaxe plays a downward pick strike once and returns to idle;
  impact ~0.40s at the low point; reads as a mining swing, not a slash; single-flight holds."
- **Knife/Dagger:** "Left-click with the dagger plays a quick forward stab once and returns to idle;
  impact early ~0.18s; noticeably the fastest cadence in the set; single-flight holds."
- **Spear:** "Left-click with the spear plays a two-handed forward thrust once and returns to idle;
  impact at full extension ~0.34s; longest visible reach; single-flight holds."
- **Sword (light slash):** "Left-click with the sword plays a sideways/diagonal slash once and returns
  to idle; impact ~0.28s mid-sweep; reads as a wide horizontal cut (not an overhead); single-flight holds."

---

## 5. Sword resolution — RESOLVED 2026-07-20 (Sponsor popup)

The earlier tension (existing clip is an OVERHEAD, but the sword's id `sword_slash` + Uma §1.1 want a
slash) is settled. **Sponsor verbatim:** *"sword should have a real slash (sideways swing) it should also
have heavy attack (swing from above)."*

**Applied:**
- **Sword LIGHT attack (left-click) = a NEW slash clip** (`Attack_Sword.fbx` → `CastawaySwordSlash`,
  `sword_slash`). Included in the 5 downloads (§2/§3) and wired as state `AttackSword` (§4.1). This is
  what `86caffwv5` delivers for the sword.
- **Sword HEAVY attack (swing-from-above) = the existing overhead** (`Melee_Attack.fbx` / `CastawayMelee`),
  **RESERVED** — kept in-repo + in the controller, unwired. **OUT OF SCOPE for `86caffwv5`:** the heavy
  attack is a distinct mechanic (a second input + its own damage/timing) that needs its own ticket.
  Phase 2 must not repurpose the overhead clip for anything else.

**Follow-up ticket to file (Priya):** *"Sword HEAVY attack — second-input overhead swing"*: wire the
reserved `CastawayMelee` overhead to a heavy-attack input, with its own damage/impact-timing/cooldown;
depends on `86caffwv5` (the sword light-slash + attack-state scaffolding) landing first.

**Decision draft (for Priya's weekly DECISIONS batch):** "Weapon swing clips (`86caffwv5`): sword gets a
NEW light slash clip (sideways swing); the existing `CastawayMelee` overhead is reserved for a future
sword HEAVY attack (swing-from-above), which is a separate follow-up ticket (OOS here). Total = 5 new
Mixamo clips (axe, pickaxe, dagger, spear, sword-slash). Sponsor-decided 2026-07-20."

---

## 6. Cross-lane integration notes (Phase-2 will preserve)

- **`[combat-trace]`/Mob.pos contract:** untouched — this is a player-swing seam, not a mob change.
- **Player iframes / Damage formula:** untouched — swings route through the existing
  `MeleeAttack.PerformAttack` → `Health.ApplyDamage` seam; no constant changes.
- **Chop/mine verbs:** preserved — `axe_chop` still drives tree-chop, `pickaxe_mine` still drives mine;
  `TriggerChop` becomes `TriggerAttack(axe)` (same behavior for the tree case).
- **Single-flight + input guards:** the `ShouldAttackOnClick` truth-table + cooldown are reused as-is —
  no new input fork (`[[active-input-not-proximity-auto-for-actions]]`).
- **Committed-asset drift:** regenerate + COMMIT the controller and clip metas
  (`[[unity-procedural-committed-assets-go-stale]]`) — the build ships the committed snapshot.

## References
- Ticket `86caffwv5` body + comments (`90150243232739` per-class-clips decision; 2026-07-20 popup:
  sword light-slash + reserved-overhead heavy — §5).
- `team/uma-ux/combat-cluster-design-brief.md` §1 (motion signatures, impact timing).
- `team/uma-ux/weapon-tool-style-spec.md` §Correction-2026-07-19 (3-tier + per-class swing clips).
- `.claude/docs/procedural-animation-verbs.md` (overlay-state idiom; chop→Mixamo-clip ruling).
- `.claude/docs/character-pipeline.md` §3-4 (Mixamo Without-Skin, Generic-not-Humanoid, retarget).
- Code: `CastawayCharacter.cs`, `Combat/MeleeAttack.cs`, `Combat/WeaponCatalog.cs`, `Combat/WeaponDef.cs`,
  `Editor/CharacterAssetGen.cs`, `CastawayAnimator.controller`; metas cited inline in §1.
</content>
</invoke>
