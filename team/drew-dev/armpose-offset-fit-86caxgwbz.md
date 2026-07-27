# CastawayArmPose offset fit, per live clip — measurement (86caxgwbz)

**Measure-only. No pose-chain behavior changed.** Ticket: 86caxgwbz ("CastawayArmPose applies idle-carry
arm eulers unconditionally on every clip frame — per-clip-pose-range trap"). Source of the flag: Drew's
#337 salvage report OOS item 4.

**Verdict up front.**

- **(a) The always-on arm eulers are FINE across the entire shipped clip set.** The hypothesis as the ticket
  framed it is **REFUTED**. Worst case over all 22 live clips × both arms: **19.9° of hand arc / 0.246
  shoulder-widths of hand displacement**, versus **9.8° / 0.175 SW at the idle carry the dial was tuned on** —
  a ≤2.1× spread, and the displacement points **AWAY from the torso in 44 of 44 clip/arm rows**. Nothing is
  distorted; nothing self-intersects.
- **(b) The measurement DID surface a live instance of the same trap family in the same component — the
  RUN-LOWER, not the carry eulers.** `runLowerEuler = (-10, 12, -42)` is gated on `CastawayCharacter.IsRunning`,
  which is **velocity-derived and completely independent of which Animator state is playing**. It therefore
  applies its **full ~47° arc / up to 0.896 SW hand displacement to any overlay clip reachable at run speed** —
  including the five per-class attack swings, which fire with **no locomotion gate**. Scoped fix proposal +
  follow-up ticket draft in §5.

---

## 1. Method

Instrument: `Assets/Scripts/Editor/ArmPoseOffsetFitDiag.cs` (read-only headless Editor diag, written for this
ticket; measurement approach copied from `AttackClipPoseDiag` on the unmerged `drew/86cav8xg9-pickaxe-clip`
branch — that branch was NOT cherry-picked). Parked on branch `drew/86caxgwbz-armpose-instrument` (pushed, no
PR — a non-main branch push triggers no CI); it belongs in the fix PR as the reuse-first instrument.

```
Unity.exe -batchmode -quit -nographics -projectPath . \
  -executeMethod FarHorizon.EditorTools.ArmPoseOffsetFitDiag.Run
```

- Poses each clip via `AnimationClip.SampleAnimation` on the **live v4 rig**
  (`Assets/Art/Character/Castaway/v4/castaway_v4_rigged.fbx`, `UseCastawayV4=True`), 25 samples per clip.
  **Not** an Animator tick — headless `Time.deltaTime ≈ 0` means the Animator never advances
  (`procedural-animation-verbs.md` / the walk-float saga).
- At each sample it reads the hand positions, then applies **exactly** what `CastawayArmPose.LateUpdate`
  applies — `bone.localRotation = bone.localRotation * offsetQ * runLowerQ` — and re-reads them.
- Offsets are the **shipped** ones (`MovementCameraScene.AddArmPose`, v4 branch):
  `rightArmEuler = (-5, -22, 0)`, `leftArmEuler = (-5, 22, 0)`, `runLowerEuler = (-10, 12, -42)`.
- Torso frame is derived from **geometry** (up = hips→head, right = L→R shoulder), never from assumed bone
  axes. All lengths normalised by shoulder width ⇒ scale-immune.
- Clip set = all 22 clips the shipped `CastawayAnimator.controller` can play (locomotion, both jumps, chop,
  5 per-class attacks, both crouch lanes + the smoothed crouch-walk `.anim`, getting-up, picking-up, stunned,
  5 hit-reacts).

**Self-check that validates the model:** min elbow interior angle is byte-identical base→posed on every clip
(`149→149`, `71→71`, `44→44`, …). The offset rigidly rotates the whole arm sub-chain about the shoulder, so it
**cannot** hyper-extend or fold a joint. Only shoulder-relative *orientation* changes.

## 2. The mechanism (why per-clip range matters at all)

With `bone.localRotation = R_clip` and offset `Q` right-multiplied, the hand's position **relative to the
shoulder** goes from `R_clip·u` to `R_clip·Q·u`, where `u` is the hand's position expressed in the upper-arm
bone's own frame — i.e. the arm's **internal fold** (elbow flexion + forearm twist) that the clip wrote.

Therefore:

- **Displacement MAGNITUDE** = `|u − Q·u|` and **shoulder ARC** = `angle(u, Q·u)` depend **only on the internal
  fold**, not on where the clip points the arm. A near-straight arm whose axis lies near `Q`'s axis barely
  moves; a folded arm swings.
- **DIRECTION** — and hence self-intersection risk — depends on the **full** pose, so torso/head clearance has
  to be measured separately. It is not derivable from the magnitude.
- **The arc is bounded above by `|Q|` itself.** `Euler(-5,-22,0)` is ≈22.6°; measured arcs run 9.3°→19.9°, i.e.
  **41%→88% of that ceiling realised**. Idle sits at the low end (elbow ≈149°, near-straight); the attack /
  hit-react clips sit at the high end (elbow 44°–86°).

**The reusable rule:** *a fixed additive arm offset's blast radius is capped by its own rotation magnitude;
the per-clip variation is the fraction of that cap the clip's elbow fold realises (here 41%→88%). A small dial
is safe on any clip by construction. A LARGE dial is the one that needs per-clip gating — and a dial gated on
something other than animation state (velocity, held item) is the one that reaches clips it was never dialed
against.*

## 3. Pass 1 — always-on offsets (`runWeight = 0`), all 22 clips

`arc` = max hand swing about the shoulder; `d` = max hand displacement (shoulder-widths); `torso` / `head` =
MIN clearance over the clip, base → posed.

| clip | R arc | R d | R torso | R head | L arc | L d | L torso |
|---|---|---|---|---|---|---|---|
| **idle(breathing)** *(dial reference)* | **9.8°** | **0.175** | 0.702→0.863 | 1.383→1.431 | **9.9°** | **0.176** | 0.698→0.859 |
| walk | 10.3° | 0.191 | 0.805→0.929 | 1.505→1.529 | 13.7° | 0.233 | 0.762→0.869 |
| run | 18.7° | 0.240 | 0.677→0.854 | 1.063→1.095 | 19.6° | 0.238 | 0.646→0.825 |
| jump_idle | 16.3° | 0.234 | 0.698→0.794 | 1.157→1.139 | 17.2° | 0.232 | 0.693→0.783 |
| jump_running | 18.2° | 0.240 | 0.689→0.893 | 0.974→1.048 | 19.3° | 0.239 | 0.776→0.814 |
| melee(chop) | 18.4° | 0.237 | 0.772→0.896 | 1.088→1.037 | 12.1° | 0.202 | 0.876→0.978 |
| atk_axe | 18.5° | 0.234 | 0.813→0.929 | 1.067→1.039 | 12.1° | 0.203 | 0.833→0.938 |
| atk_pickaxe | 19.9° | 0.234 | 0.570→0.737 | 0.790→0.815 | 17.2° | 0.246 | 0.502→0.654 |
| atk_dagger | 19.8° | 0.225 | 0.736→0.841 | 0.864→0.908 | 19.6° | 0.229 | 0.763→0.875 |
| atk_spear | 19.2° | 0.230 | 0.701→0.859 | 1.000→1.048 | 18.6° | 0.229 | 0.449→0.570 |
| atk_sword | 17.6° | 0.237 | 0.654→0.824 | 1.040→1.045 | 16.4° | 0.237 | 0.798→0.978 |
| crouch_idle | 12.2° | 0.222 | 1.159→1.279 | 1.480→1.506 | 16.5° | 0.246 | 0.856→1.013 |
| crouch_walk | 11.4° | 0.203 | 1.022→1.150 | 1.488→1.517 | **9.3°** | **0.171** | 1.424→1.445 |
| getting_up | 16.4° | 0.239 | 1.025→1.177 | 1.258→1.310 | 16.1° | 0.231 | 0.954→1.123 |
| picking_up | 18.8° | 0.233 | 0.722→0.853 | 1.124→1.104 | 17.1° | 0.234 | 0.738→0.850 |
| stunned | 18.8° | 0.239 | 0.611→0.747 | 0.742→0.810 | 19.8° | 0.238 | 0.750→0.856 |
| hit_body | 18.8° | 0.225 | 0.567→0.722 | 0.729→0.824 | 19.7° | 0.233 | 0.719→0.888 |
| hit_head | 19.2° | 0.236 | 0.579→0.709 | 0.696→0.773 | 19.6° | 0.236 | 0.768→0.916 |
| hit_bigstomach | 19.5° | 0.240 | 0.575→0.678 | 0.583→0.679 | 19.8° | 0.232 | 0.523→0.695 |
| hit_stomach | 19.3° | 0.212 | 0.536→0.694 | 0.707→0.796 | 19.7° | 0.227 | 0.511→0.720 |
| hit_rib | 18.6° | 0.225 | 0.644→0.785 | 0.729→0.832 | **19.9°** | 0.209 | 0.342→0.531 |
| crouch_walk_smoothed | 11.4° | 0.203 | 1.022→1.150 | 1.488→1.517 | 9.3° | 0.171 | 1.424→1.445 |

**Aggregates.**

- Arc span across all 44 clip/arm rows: **9.3° → 19.9°** (idle 9.8/9.9). Max ratio to idle: **×2.02** (right,
  `atk_pickaxe`) / **×2.01** (left, `hit_rib`).
- Displacement span: **0.171 → 0.246 SW** (idle 0.175/0.176). Max ratio to idle: **×1.40**.
- **Torso clearance increases in 44 / 44 rows.** The offset is a spread; it pushes both hands *out of* the body
  on every single clip. The tightest posed clearance anywhere is 0.531 SW (`hit_rib` left, up from 0.342).
- **Head clearance** (right hand) decreases on only 4 of 22 rows, all marginally and all still ≥1.037 SW:
  `jump_idle` 1.157→1.139, `melee` 1.088→1.037, `atk_axe` 1.067→1.039, `picking_up` 1.124→1.104. Nothing
  approaches the head.
- `crouch_walk_smoothed` is numerically identical to `crouch_walk` — the `SneakGaitCurveFix` resample touched
  toe curves only, so it is a clean control row.

**Per-clip verdict: FITS on all 22.** No clip is distorted by the always-on eulers. Two reasons, both measured:
the dial is small (≈22.6° ceiling), and its direction is outward on every pose in the shipped set.

## 4. Pass 2 — right arm with the run-lower at full weight (`runWeight = 1`)

| clip | R arc | R d | peak decomposition (out / fwd / up) | R torso | R head |
|---|---|---|---|---|---|
| idle(breathing) | 45.8° | 0.803 | +0.382 / +0.661 / +0.254 | 0.702→1.156 | 1.383→1.467 |
| walk | 45.9° | 0.859 | +0.352 / +0.670 / +0.410 | 0.805→1.233 | 1.505→1.562 |
| **run** *(dial reference)* | 47.3° | 0.691 | +0.450 / +0.524 / +0.009 | 0.677→0.988 | 1.063→1.172 |
| jump_idle | 46.9° | 0.872 | +0.559 / +0.509 / +0.410 | 0.698→0.833 | 1.157→1.121 |
| jump_running | 47.2° | 0.822 | +0.112 / +0.372 / +0.688 | 0.689→0.912 | 0.974→1.161 |
| melee(chop) | 47.6° | 0.826 | +0.235 / +0.717 / **−0.316** | 0.772→1.085 | 1.088→1.090 |
| atk_axe | 47.6° | 0.817 | +0.252 / +0.712 / **−0.288** | 0.813→1.070 | 1.067→1.072 |
| **atk_pickaxe** | 47.6° | **0.896** | +0.775 / +0.426 / −0.265 | **0.570→0.524** | 0.790→0.971 |
| atk_dagger | 47.6° | 0.810 | **−0.344** / +0.674 / +0.266 | 0.736→0.895 | 0.864→0.949 |
| atk_spear | 47.5° | 0.805 | +0.218 / +0.422 / **−0.648** | 0.701→0.940 | 1.000→1.092 |
| atk_sword | 47.3° | 0.798 | +0.278 / +0.738 / −0.158 | 0.654→1.033 | 1.040→1.211 |
| crouch_idle | 46.2° | 0.826 | +0.432 / +0.697 / −0.135 | 1.159→1.273 | 1.480→1.653 |
| crouch_walk | 46.0° | 0.815 | −0.271 / +0.695 / +0.324 | 1.022→1.056 | 1.488→1.313 |
| getting_up | 47.4° | 0.819 | +0.342 / +0.687 / +0.315 | 1.025→1.061 | 1.258→1.356 |
| picking_up | 47.3° | 0.817 | +0.604 / +0.420 / +0.335 | 0.722→0.940 | 1.124→1.186 |
| stunned | 47.3° | 0.850 | −0.057 / +0.829 / −0.173 | 0.611→0.861 | 0.742→1.056 |
| hit_body | 47.3° | 0.576 | +0.530 / +0.174 / −0.134 | 0.567→0.803 | 0.729→1.023 |
| hit_head | 47.4° | 0.764 | +0.622 / +0.441 / −0.086 | 0.579→0.781 | 0.696→0.999 |
| hit_bigstomach | 47.4° | 0.690 | +0.570 / +0.355 / −0.128 | 0.575→0.795 | 0.583→0.894 |
| hit_stomach | 47.4° | 0.528 | +0.482 / +0.128 / −0.202 | 0.536→0.793 | 0.707→1.002 |
| hit_rib | 47.3° | 0.587 | +0.503 / +0.109 / −0.250 | 0.644→0.803 | 0.729→1.023 |
| crouch_walk_smoothed | 46.0° | 0.815 | −0.271 / +0.695 / +0.324 | 1.022→1.056 | 1.488→1.313 |

**Aggregates.**

- Arc span **45.8° → 47.6°** — essentially **flat across every clip**. The composite `Euler(-5,-22,0) *
  Euler(-10,12,-42)` is ≈47°, and the measured arc realises ~97–100% of it on every clip: the hand vector is
  near-perpendicular to the composite axis in every pose. So the run-lower's *magnitude* is uniformly maximal;
  it is **not** a "some clips more than others" problem.
- Displacement span **0.528 → 0.896 SW** — **3.0× to 5.1× the always-on offsets' worst case.**
- Torso clearance still improves in **21 / 22** rows. The single decrease is **`atk_pickaxe` 0.570 → 0.524 SW**
  — the tightest posed hand-to-torso clearance measured anywhere in this study.
- Head clearance decreases on 3 rows (`jump_idle` 1.157→1.121, `crouch_walk` + its smoothed twin
  1.488→1.313); all three are states where the run weight is 0 in practice, so they are not live cases.
- The **direction** flips per clip in a way the run clip alone could never have exposed: `atk_spear` drives the
  hand **0.648 SW DOWN** at the thrust peak, `melee`/`atk_axe` **0.29–0.32 SW DOWN** at the strike,
  `atk_dagger` **0.344 SW toward the midline**, `atk_pickaxe` **0.775 SW outward**. Each of those is a
  materially different strike silhouette from the one the clip author (and the Sponsor's swing soaks) approved.

## 5. Reachability, and the scoped fix proposal

`IsRunning` is set in `CastawayCharacter.LateUpdate:1539` purely from planar agent speed
(`walking && planarSpeed >= runSpeedThreshold * RunEngageFraction`). It knows nothing about the Animator.
`CastawayArmPose.LateUpdate` then applies `runLowerQ` unconditionally. So the run-lower reaches:

| overlay state | reachable while `IsRunning`? | evidence |
|---|---|---|
| **5 per-class attack swings + chop** | **YES — no locomotion gate anywhere on the path** | `ChopTree.BeginChopSwing` / `ChopTree.Chop` → `TriggerChop`; `MineBoulder.cs:517/535` + `MineOre.cs:545/566` → `TriggerMine`; `MeleeAttack.PerformAttack:221` → `TriggerAttack`. None checks `IsRunning`/`IsWalking`. |
| jump_running | YES | `TryJump` — "the agent keeps driving XZ; this only adds the vertical arc" (`CastawayCharacter.cs` doc comment). Benign: measured clearances all improve, and the run-lower was dialed on the run pose this clip shares. |
| hit-reacts / stunned / getting-up / picking-up | **latent only** | `procedural-animation-verbs.md`: the params exist + are controller-test-covered, but "the actual TRIGGERING from gameplay/damage systems is not yet wired". |
| crouch lanes | no | crouch speed is below the run engage threshold. |

Also note `runLowerBlendRate = 8` ⇒ ~0.4 s ease-out. Even a player who releases sprint the instant they click
carries a decaying run-lower over the opening of a 2.0–5.2 s swing clip; a player who keeps holding sprint
carries the full 47° for the whole swing.

**Not measured (bounded silence):** how *often* a player actually swings at run speed, and whether the
resulting silhouette reads as wrong to the eye. This study measures magnitude and clearance only. A Sponsor
soak (sprint into a tree/boulder, click, judge the swing) is the read-gate for the "does it look wrong"
question — the geometry says it is a large unbudgeted change to an authored strike, not that it is ugly.

### Follow-up ticket draft — `fix(anim): gate CastawayArmPose run-lower on locomotion STATE, not velocity`

- **Size** S. **Lane** Unity-build. **Priority** low-med (latent; no Sponsor defect report attributed).
- **Why:** measured above — the run-lower applies a ~47° arc / up to 0.896 SW hand displacement to every attack
  swing fired while sprinting, and drops `atk_pickaxe`'s hand-to-torso clearance to 0.524 SW (the tightest in
  the shipped set). Dialed against the run clip; reaches five clips it was never dialed against.
- **Preferred shape (cheapest, matches the existing idiom):** keep the velocity-derived `IsRunning` as the
  *target*, but **force the run-weight target to 0 while a non-locomotion overlay state is active** — i.e. drive
  `target` from `Animator.GetCurrentAnimatorStateInfo(0)` being in the `Locomotion`/`Idle`/`Jump*` set rather
  than from `IsRunning` alone. The existing `runLowerBlendRate = 8` ease then hands the arm back to the clip
  over ~0.4 s at swing start and restores it on return to locomotion. **No new Animator clip / state / layer /
  AvatarMask** (`procedural-animation-verbs.md` non-negotiable).
- **Rejected alternatives:** per-clip weight curves (needs a new authored channel per clip — heavier than the
  defect); zeroing the whole `CastawayArmPose` during overlays (would also drop the always-on carry spread,
  which §3 proves is *correct* on every clip and is load-bearing for `HeldAxeRig` seating).
- **Regression guard:** `RunWeight` must still reach ≈1 during a plain sprint (the 86caa83wn "axe into the
  head" fix must not regress), and must rest at 0 at walk/idle. `arc` on the five attack clips must return to
  the §3 Pass-1 band (≤19.9°) while an attack state is active.
- **Instrument:** reuse `ArmPoseOffsetFitDiag` (branch `drew/86caxgwbz-armpose-instrument`) — re-run it after
  the fix and diff Pass 2 against Pass 1.
- **OOS:** the always-on carry eulers (§3 proves they fit — do not touch); any `HeldAxeRig` re-architecture;
  the per-class weapon seat dials.

## 6. Doc-worthy findings

1. **The fixed-offset blast radius rule** (§2) — arc is capped by `|Q|`; per-clip variation is the fraction of
   that cap the elbow fold realises. Belongs in `procedural-animation-verbs.md` as the sizing heuristic for any
   new additive arm offset: *dials under ~25° are clip-safe by construction; dials over ~40° need a state gate.*
2. **A LateUpdate offset gated on a GAMEPLAY signal (velocity, held item) rather than on ANIMATION STATE is the
   actual trap shape**, not "the dial was tuned at idle". The always-on eulers survive precisely *because* they
   are unconditional and small; the conditional one is what leaks into clips it never met. Same family as the
   `run-lower engagement is state-gated, not always-on` caveat already in `procedural-animation-verbs.md` — that
   entry documents the debug-tool side of the gate; this is the *runtime* side.
3. **Elbow-angle invariance is a free correctness self-check** for any right-multiplied bone offset — if it
   ever changes, the offset is being applied to the wrong bone or composed in the wrong order.
