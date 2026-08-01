# First/Third-Person Continuous Zoom — Engine-Capability Research

## Question
Ticket `86cahnmb4` (design GRILLED + LOCKED with Sponsor 2026-07-02, dispatch-ready): scroll-zoom
should carry continuously past today's closest 3rd-person orbit distance into a 1st-person view
(no mode key), hiding the castaway body / showing the held item, switching to FPS mouselook, with
verb actions unchanged. This note answers the **engine-capability** question the orchestrator asked
me to grade — what breaks in the rig, the camera, and the verification harness, and how expensive
this is — not the design (already locked) and not an implementation plan.

⚠ **Source-tree caveat.** I have no worktree; I read the orchestrator's tree at
`c:/Trunk/PRIVATE/Far-Horizon`, which sits on `orch/coordination` and lags `main`. Everywhere below
I cite a file+line, I opened that exact file in this tree this session — that is "measured on this
tree," not necessarily "measured on `main` HEAD." One concrete instance of the lag: `away-queue.md`
(read this session) cites `WeaponFindVerifyCapture.cs:217/240` as a real, existing file from a QA
finding tied to PR #351 — but that file does **not exist** in the tree I can read. Per the brief's
own instruction, I am treating that as a **staleness signal** (the file exists on a branch/PR my
tree hasn't merged), not as evidence the component doesn't exist. Anywhere I say "N gates do X," N
is a lower bound from a keyword grep over the tree I can read, not an exhaustive audit of `main`.

## Bottom line
Continuous zoom-through into 1st person is **technically achievable** in Unity 6/URP on this rig —
nothing about the Generic-rig / additive-arm-pose architecture forbids it — but it is **not cheap**:
it is a genuine architecture change across three systems (camera, movement-input model, and
character self-visibility) that none of them currently have any hooks for, and it invalidates or
needs re-deriving a real (bounded, not exhaustively counted) slice of the shipped-build capture-gate
suite that hard-codes a gameplay-back 3rd-person framing as ground truth. The single biggest and
most concrete risk is that this ticket and the **already-open** `86caz5pk9` (a capture-framing
defect gate whose own AC1 defines "the player's reachable range" as `OrbitCamera.minDistance..
maxDistance`) are about to redefine the same quantity in opposite directions if sequenced carelessly.

## Evidence

### 1. What breaks in the character rig at close range

- **The additive-arm-pose chain itself does not break — it is camera-agnostic by construction.**
  `CastawayArmPose` (`Assets/Scripts/Runtime/CastawayArmPose.cs:52`, `[DefaultExecutionOrder(50)]`)
  and `HeldAxeRig` (`Assets/Scripts/Runtime/HeldAxeRig.cs:67-68`, `[DefaultExecutionOrder(100)]`)
  read Animator clip pose + gameplay state (run flag, held-item id) and never read the camera at
  all — verified by grep across both files for `Camera`/`OrbitCamera`/`fieldOfView` (no matches).
  **Strong (measured on this project, this session).** So there is nothing in the chain's *mechanism*
  for a first-person camera to break; it will keep producing whatever pose it always produces
  regardless of where the camera sits. This is a genuinely different conclusion from "the chain
  needs a first-person mode" — it doesn't need one.
- **But camera distance changes what defects the chain's OUTPUT exposes — cosmetic, not
  architectural, and there is direct project precedent for exactly this failure shape.** Ticket
  `86caz5pk9` (read this session) quotes Drew's independent played-verification: a held sword "did
  NOT draw his eye at default overhead framing — only reading clearly once he LOWERED the camera."
  **Strong (quoted verbatim from the ticket body I fetched this session).** The held-weapon seat
  dials in `procedural-animation-verbs.md` ("Held-weapon seat dials are per weapon CLASS…") were
  tuned and judged at the existing 3rd-person orbit distance (`distance=14`, per `OrbitCamera.cs:35`
  default). At 1st-person range (well under 1 world-unit from the hand) the same seat offset that
  reads as "close enough" at 14u away will show any residual rotation/translation error far more
  visibly — the `86cay4282` findings in `procedural-animation-verbs.md` (measured clip-hand-line
  wander 21° mean / 36.6° max through a swing) are exactly the kind of error that is invisible at
  orbit range and would not be invisible at 1P range. **Verdict: cosmetic re-dial risk, not a chain
  redesign** — same idiom, likely different (probably smaller) offset constants needed for 1P.
- **Body-hide (AC2) has no existing hook — new work, not an extension.** Grep of
  `CastawayCharacter.cs` for `SetActive(false)` / `renderer.enabled` / `shadowCastingMode` found
  zero matches. **Strong (measured, zero hits).** No visibility-toggle of any kind exists on the
  castaway body today. Implementing AC2 is new plumbing, not a flag flip.
- **The shadow question has a standard, already-present-in-this-codebase answer, but it is an
  explicit choice nobody has made yet.** `Renderer.shadowCastingMode` is already a live, understood
  API surface in this project (13+ call sites across `LowPolyZoneGen.cs`, `MovementCameraScene.cs`,
  `WorldBootstrap.cs`, `LogPile.cs`/`OrePile.cs`/`StonePile.cs` — all using `.Off`/`.On`, verified by
  grep this session). **Strong for "the API is in active use here."** None of those call sites use
  `ShadowCastingMode.ShadowsOnly` — the value that would let the body keep casting a ground shadow
  while its mesh is hidden from the camera (the standard FPS "invisible player, visible shadow"
  trick). That `ShadowsOnly` exists as a `UnityEngine.Rendering.ShadowCastingMode` enum member is
  from my general Unity API knowledge, **not** re-verified against a Unity 6000.4.11f1 manual page
  opened this session — **grade this piece Moderate, not Strong**, and flag it as a cheap thing to
  confirm before relying on it. Either way: the ticket text ("castaway mesh HIDDEN") doesn't say
  whether the shadow should persist, and neither answer is free — full hide (`renderer.enabled =
  false`) is the cheaper default and matches most survival-FPS games' first-person model; keeping
  the shadow is a small extra step (`ShadowsOnly`) that needs its own visual check in this project's
  gamma-locked, bloom/grading pipeline before trusting it looks right (`unity6-mastery.md` §3's
  gamma-is-a-deliberate-look-lock note is exactly the kind of thing that can make a normally-simple
  shadow-only render read wrong here).
- **Near-plane clipping through the head/torso is a real, currently-unaddressed gap — closer to
  architectural than cosmetic, because there is no existing "eye position" concept at all.** Grep
  across `Assets/Scripts` for `EyePosition` / `EyeSocket` / `CameraSocket` / `HeadSocket` /
  `FirstPerson` found **zero matches** — no prior first-person camera work exists anywhere in this
  codebase. **Strong (measured, zero hits).** Grep for `nearClipPlane` across all of `Assets` found
  it set only on **isolated verify-capture rigs** (e.g. `WeaponSetVerifyCapture.cs:90`, `= 0.01f`,
  a hero-shot camera that is not the gameplay camera) — never on the live gameplay camera built by
  `MovementCameraScene.BuildOrbitCamera` (`Assets/Scripts/Editor/MovementCameraScene.cs:4082-4096`,
  which sets `fieldOfView = 45f` but no near-clip). **Strong (measured, this session).** I did not
  open the live `Camera` component's serialized Inspector value to read its actual current near-clip
  — I can't state that number without fabricating it, so: it needs measuring before implementation,
  not assumed. The architectural point stands regardless of the exact number: the Mixamo Generic
  head is solid geometry with no eye-socket bone or marker, so "the camera position for 1P" is a new
  quantity this project has never had to define (an offset from `mixamorig:Head`, chosen by hand,
  verified against the near-clip so the transition band never shows interior mesh faces). This is
  new authoring + tuning work, not a parameter flip.

### 2. What breaks in the camera itself

- **`OrbitCamera.minDistance = 6f` today** (`OrbitCamera.cs:49`), and scroll-zoom clamps distance
  to `[minDistance, maxDistance]` every frame (`OrbitCamera.cs:217-221`). **Strong (measured).**
- **The locked design answers the mode-vs-shrink question directly, and the code shape agrees with
  it.** The ticket's design is explicit: "ONE continuous dial, NO mode key" — i.e., NOT a distinct
  mode switched by a keypress. But "no mode key" is a UX/input statement, not an implementation
  statement — internally, the code almost certainly still needs a **distinct render/behaviour state**
  for 1P, because AC2 (hide body) and AC3 (FPS mouselook replaces orbit) are both **discontinuous**
  behaviours gated on a distance threshold, not continuous functions of distance the way pitch/yaw/
  zoom are today. Concretely: `OrbitCamera.LateUpdate` currently drives yaw/pitch from RMB-drag +
  computes `transform.SetPositionAndRotation` from an **external** orbit around `_followPos`
  (`OrbitCamera.cs:266-270`); 1st-person mouselook needs the camera **at** a point rigidly attached
  to the head bone with rotation driven by raw mouse deltas, not an orbit around a followed point.
  These are two different position/rotation update models, not two ends of one lerp. **This is my
  inference from reading the class, not a claim the ticket or any doc states outright — grade this
  Moderate-inference**, but it follows directly from code already read: `Apply()` computes
  `desiredPos = _followPos − rot * Vector3.forward * distance` (`OrbitCamera.cs:268`), which is
  degenerate/meaningless once distance would need to go **negative** (camera in front of the head)
  or the "look-at point" itself needs to become the camera's own position (1P has no look-at target
  distinct from the camera). **Verdict: distinct mode internally, continuous dial externally** — the
  "one dial, no mode key" UX is satisfied by driving the *transition* off the same zoom scalar, but
  the position/rotation math itself is not a single continuous formula across the whole range; it is
  two formulas cross-faded (or hard-switched at a threshold) by that scalar.
- **Collision behaviour also needs a decision the ticket doesn't make.** `OrbitCamera`'s terrain
  occlusion pull-in (`ResolveCameraCollision`, `OrbitCamera.cs:285-315`) exists to keep the 3rd-person
  camera from clipping through hills/rocks between it and the player. In 1P the camera **is** the
  player's eye — that collision logic (raycast from follow-point toward the desired camera pos, pull
  in on hit) becomes meaningless once "desired camera pos" collapses onto the head. Not a defect,
  just an unaddressed seam: the collision system needs to know it's inert inside the 1P band, or it
  will do something undefined (e.g. treat the character's own head/body colliders as terrain-mask
  hits and pull the "camera" somewhere strange). **Moderate (inference from the code's own logic,
  not tested)** — flag it as an integration risk to verify early, not a blocker.
- **Movement input is camera-relative today, and 1P's design explicitly requires a different model
  — this is the largest camera-adjacent architecture change, and it lives outside `OrbitCamera.cs`
  entirely.** `WasdMovement.cs` is documented and coded as strictly camera-relative: "W moves the
  character in the direction the orbit camera FACES… rotated into the camera's planar basis each
  frame" (`WasdMovement.cs:10-13`, `CameraRelativeDirection`, `WasdMovement.cs:512`). **Strong
  (measured, this project's own doc comments + code).** AC3 of the ticket requires the opposite
  causality in 1P: "mouse steers the character's facing; W walks the way you look" — i.e., in 1P the
  **character facing drives movement direction**, not "movement direction is read relative to a
  camera that orbits independently of the character." Today the character's body yaw is set from
  *travel* direction (comment at `WasdMovement.cs:21`: "yaw the model toward travel"); FPS mouselook
  needs the body yaw set from *look* direction instead, with travel then computed off that facing.
  This is an inversion of who-drives-whom, not a camera-only change — `WasdMovement` and whatever
  currently yaws `CastawayCharacter` toward travel both need a second code path. **This is the
  finding most likely to be under-scoped if this ticket is read as "a camera ticket only"** — the
  ticket's own Meta line calls it a camera ticket, and AC3 is scoped as camera work, but the
  implementation reaches into `WasdMovement.cs` and character-facing logic to satisfy it.
- **The existing UI-input-gate pattern is reusable, not a new problem.** `OrbitCamera` already
  gates orbiting/zoom against `UiInputGate.CaptureWorldInput`/`PointerOverConsole`
  (`OrbitCamera.cs:190-216`) so panels don't fight camera input. 1P mouselook will need the same
  gating (don't steer facing while a menu is open) — this is a **pattern to copy**, not new research;
  noting only because the ticket's Meta line calls out "respect the existing input-gate conventions"
  and the pattern is confirmed to already exist and be reusable.

### 3. What breaks in the verification harness

**This is the part the brief flagged as most likely under-estimated, and the evidence bears that
out.** A keyword grep (`SetPitch|SetDistance|\.pitch\b|OrbitCamera|fieldOfView|defaultPitch`) across
every `*VerifyCapture*.cs` file under `Assets/Scripts/Runtime` matched **24 of the ~40 `VerifyCapture`
files present in this tree** (`Assets/Scripts/Runtime/*VerifyCapture*.cs`, file count from a
`files_with_matches` grep this session). **This is a lower bound, stated as one deliberately**: the
grep pattern would miss a gate that frames its subject via, e.g., a raw `cam.transform.position =`
without touching the words "distance"/"pitch"/"fieldOfView"/"OrbitCamera", and I did not open every
one of the ~40 files line-by-line — only the 24 keyword hits, plus targeted reads of a handful.
Within those 24, three distinct risk shapes, each confirmed by reading the actual matching lines:

**Group A — hard-codes the current gameplay pitch/distance/FOV as its evidence baseline (6 files,
each confirmed by reading the exact line, all `Strong — measured this session`):**
- `IslandVerifyCapture.cs:261,332-333,497` — `orbit.SetPitch(55f); orbit.SetDistance(14f);`
  literally, with an inline comment "the default gameplay pitch"/"the default gameplay distance."
- `PocIslandVerifyCapture.cs:77-78,404` — `SetPitch(14f)/SetDistance(18f)` and a second near-horizon
  variant `SetPitch(16f)/SetDistance(18f)`.
- `SeaVerifyCapture.cs:89-90,282-283,352-353` — `orbit.SetPitch(pitch); orbit.SetDistance(dist);`
  with a comment "default gameplay dist=14."
- `WorldLookVerifyCapture.cs:32-34,84-85,105-106` — `public float defaultPitch = 55f;  // the locked
  gameplay default`, plus a `horizonPitch = 10f` variant.
- `SkyVerifyCapture.cs:164-172` — the most explicit instance: `const float gameplayPitch = 8f; …
  gameplayDist = 14f; … gameplayFov = 45f;` with a comment naming these as reproducing
  `MovementCameraScene`'s live bake by hand.
- `SettingsVerifyCapture.cs:46,276-282` — the **most directly implicated gate for this ticket**: its
  own doc comment says it proves "zoom-range MIN/MAX clamping `OrbitCamera.minDistance/maxDistance`,"
  and the code I read exercises the **max** end (`Mathf.Min(zoom.UpperLimit, 18f)` → assert
  `orbit.maxDistance` clamped). I did not find a symmetric min-end assertion in the section I read —
  **I cannot confirm from what I read whether this gate also asserts on `minDistance` as a hard
  floor; flag as unverified, not "confirmed absent."** Either way, this gate's entire premise is
  "the settings-driven zoom range clamps hard at OrbitCamera's min/max" — exactly the invariant this
  ticket's design changes (min becomes a transition threshold, not a hard floor).

**Group B — captures explicitly "from the REAL OrbitCamera (gameplay framing)," i.e. assume
whatever the live orbit camera is doing IS the gameplay-representative view (their own doc comments
say this, quoted verbatim, `Strong — measured`), without pinning a specific pitch/distance:**
`WasdVerifyCapture.cs`, `RunVerifyCapture.cs`, `SnakeVerifyCapture.cs`, `SneakVerifyCapture.cs`,
`LocomotionHitReactVerifyCapture.cs`, `WalkGroundingVerifyCapture.cs` (6 files). These don't hard-code
a number, so they won't silently go stale the way Group A can — but their entire evidentiary claim
("this is what the player sees") stops being unambiguous once "the player" can be in two structurally
different camera states. A gate in this group run while the camera happens to be zoomed into 1P
would capture a 1P frame and call it "the gameplay framing" with no signal that it isn't the 3P view
the gate was written to prove.

**Group C — isolated "hero shot" gates using the shared `VerifyCaptureFraming.ComputeFrame` helper
(`Assets/Scripts/Runtime/VerifyCaptureFraming.cs:59-95`) with their own fixed FOV constant, fully
decoupled from `OrbitCamera.minDistance/maxDistance/pitch` (confirmed by reading `VerifyCaptureFraming
.cs` — it takes bounds/viewDir/FOV as explicit parameters and never reads `OrbitCamera` at all,
`Strong`):** `AxeVerifyCapture.cs` (FOV 40f), `CastawayVerifyCapture.cs` (FOV 40f),
`WeaponSetVerifyCapture.cs` (FOV 38f), `HandsVerifyCapture.cs` (FOV 35f, configurable),
`RimVerifyCapture.cs` (FOV 40f), `RockVerifyCapture.cs` (FOV 40f), `FlatShadingVerifyCapture.cs`
(FOV 40f), `WaterAcquisitionVerifyCapture.cs` (FOV 40f), `LootPromptVerifyCapture.cs` (FOV 40f),
`FreshwaterPondVerifyCapture.cs` (mixed fixed FOVs + an `OverheadFov` constant) — 10 files. These are
**not threatened by the OrbitCamera min/max change** (they don't read those fields), but per
`unity-conventions.md`'s own framing ("an isolated verify capture is a smoke-test that the asset
EXISTS, NOT proof of how it READS in play"), none of them can stand in as 1P evidence either — a new
1P-specific capture gate (which AC7 of the ticket explicitly requires: "1P frame with held item
visible") is new gate authoring, not a re-point of an existing one.

**One file didn't fit either bucket cleanly:** `BoulderVerifyCapture.cs:238` sets
`cam.fieldOfView = main.fieldOfView` — it **copies** whatever the live main camera's FOV is rather
than hard-coding one, so it inherits the live gameplay FOV by reference; noted for completeness, not
counted as broken by either group.

**The concrete, already-open cross-cutting risk.** `86caz5pk9` (read this session, "to do") reports
that a capture component frames its subject at 5.5 world-units — **below** the current
`OrbitCamera.minDistance = 6` — and calls that "a view the player cannot reach." Its own AC1 defines
the fix as: the framing distance "must be inside the players reachable range (`OrbitCamera
minDistance..maxDistance`)." **If `86cahnmb4` ships first, "the player's reachable range" grows to
include everything from `maxDistance` down through the old `minDistance` and on into the 1P band** —
so `86caz5pk9`'s own AC1 definition of "reachable" goes stale the moment this ticket ships, and a fix
landed against today's `[minDistance..maxDistance]` window could need re-deriving again. **This is
not a hypothetical extrapolation — both tickets are open on the same board right now, both touch the
same `OrbitCamera.minDistance` field, and their sequencing has not been decided.** Recommend
Priya/orchestrator explicitly sequence or flag this dependency rather than letting both land
independently.

### 4. Is this cheap or expensive, and what's the smallest thing that would tell us

**Not cheap, on architecture-touch-count alone.** This ticket, read at engine-capability depth,
touches: `OrbitCamera.cs` (a second position/rotation model, not a parameter), `WasdMovement.cs` +
character-facing logic (movement causality inversion), a new body-visibility + shadow mechanism on
`CastawayCharacter`, a new eye-position/near-clip authoring pass with no prior art in this codebase,
and a bounded-but-real slice of the capture-gate suite (at minimum the 6 Group-A files, likely all
24 grep-matched files needing at least a "does this still mean what it says" read, plus new 1P-gate
authoring for AC7). None of these are individually hard engineering — Unity 6/URP has no capability
gap here (no missing rendering feature, no IL2CPP/build-surface concern, no asset-pipeline change) —
but the count of touched systems is what makes this Size-M-or-bigger, not any single piece of it.

**The smallest thing that would produce a real answer, not a demo:** a single throwaway PlayMode/
editor spike that (a) attaches the live gameplay camera rigidly to the `mixamorig:Head` bone at a
hand-picked offset with the body renderer disabled, and (b) reads back whether the near-clip clips
visible interior geometry and whether the held-item seat (today's `HeldAxeRig` output, unmodified)
looks acceptable from that distance. That single spike answers the two most expensive unknowns
cheaply: whether the "new eye-position + near-clip" work is a five-minute constant or a real tuning
pass, and whether the held-item seat needs re-dialing for 1P range or survives unchanged (per the
`86caz5pk9`-cited precedent that camera distance changes what reads). It does **not** need to solve
the FPS-mouselook movement inversion or touch any capture gate — those are separable follow-on
questions once the "does the rig even look right up close" unknown is resolved. This is a
half-day-or-less spike, not a full implementation slice, and it directly informs the M-vs-L sizing
call without guessing.

## Application to Far Horizon

- **Rendering pipeline / Unity 6 capability:** no gap. Nothing about Forward+ vs Forward, GPU
  Resident Drawer, or the low-poly smooth-shaded Zone-D look is implicated by this ticket — it's a
  camera-and-input feature, not a rendering-pipeline one. Do not scope any shader/pipeline work here.
- **Character pipeline:** the Generic-rig / additive-`CastawayArmPose`→`HeldAxeRig` idiom
  (`character-pipeline.md`, `procedural-animation-verbs.md`) is compatible as-is — no rig change, no
  re-import, no Mixamo re-rig. The cost lands in *authoring a new camera offset* (an eye position)
  and *possibly re-dialing existing seat constants* for close range, not in the pipeline itself.
- **Shipped-build capture gate discipline (CLAUDE.md hard rule):** this ticket is a direct stress
  test of that discipline. AC7 already requires 3P / transition-midpoint / 1P shipped-build captures
  with the held item visible — correct per the project's own gate philosophy. The finding above (§3)
  is the concrete argument for treating the existing capture-gate suite as **in-scope maintenance
  cost**, not a byproduct: at minimum the 6 Group-A gates need their hard-coded gameplay-pitch/
  distance constants re-examined against whatever `OrbitCamera`'s post-implementation semantics are,
  and `86caz5pk9`'s "reachable range" definition needs to be resolved against this ticket's landing,
  not independently.
- **In-house-tooling posture / cost:** no licensing or asset-pipeline-route implication — this is
  pure runtime/camera engineering, doesn't touch procedural mesh gen, Blender, or Hyper3D/Mixamo.
- **What I could not verify:** the live gameplay camera's actual current near-clip-plane value (not
  set anywhere in code I could find — likely the Camera component's serialized Inspector default,
  unread this session); whether `ShadowCastingMode.ShadowsOnly` is the exact correct Unity 6 API name
  (Moderate confidence, not re-checked against the manual this session); whether
  `SettingsVerifyCapture.cs` asserts on `minDistance` anywhere beyond the `maxDistance` clamp I read;
  and the true count of framing-assumption gates beyond my 24-file keyword-grep lower bound (some of
  the ~40 total `*VerifyCapture*` files were not opened at all). All flagged inline above at the
  point they're used, not just here.
