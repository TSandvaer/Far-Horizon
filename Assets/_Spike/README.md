# Hyper3D Castaway — in-engine viability spike (ticket 86ca8r72j)

**THROWAWAY R&D spike.** Judges whether the Hyper3D-generated + Mixamo-rigged castaway animates
cleanly and reads on-style in OUR Unity/URP build, before deciding whether to swap the live chibi
(`CastawayCharacter`). Everything here lives under `Assets/_Spike/` and is deleted when the verdict
is recorded — it does NOT touch `Boot.unity`, the live `CastawayCharacter`, or any production asset.

## Verdict

**VIABLE.** The asset imports clean, animates clean, and reads squarely on-style (chunky toy-cartoon
castaway — sandy hair, big dark eyes, yellow tee, brown shorts, barefoot) in a real shipped URP build.
Proven on BOTH rig modes:

- **Humanoid** (the ticket's prescribed pipeline: Idle.fbx → CreateFromThisModel avatar; Walking.fbx →
  CopyFromOther → Idle's avatar) — Idle + Walk both deform cleanly. See `captures/02`,`03`.
- **Generic** (each FBX creates an avatar from its own identical `mixamorig` skeleton; clips bind by
  transform path, no retarget — the same choice `CharacterAssetGen` makes for the live chibi) — Idle +
  Walk both deform cleanly. See `captures/04`,`05`.

Bind pose (animator OFF) is the import sanity baseline — a clean chibi (`captures/01`).

## Concrete findings

- **Scale:** Idle.fbx imports at ~2.18u intrinsic; height-normalized to ~1u via `globalScale` (the
  ~1u-calibrated scene). No giant/tiny issue after normalize.
- **Material:** a flat de-lit URP/Lit material from `texture_diffuse` (smoothness ~0, no metallic)
  reads toon-ish/on-style — the baked albedo already carries the toon shading. Normal map bound at low
  strength.
- **Clip names:** BOTH Mixamo FBX export their single take as `mixamo.com` (NOT "Idle"/"Walk") — an
  "Idle"/"Walk" token match loops ZERO clips (the T-pose-mid-walk class). The generator matches the
  `mixamo.com` take and renames to `SpikeIdle`/`SpikeWalk`. (Idle len 8.33s, Walk len 1.03s.)
- **Rig:** Mixamo Standard skeleton (`mixamorig:*`, 46 bones, rootBone `mixamorig:Hips`), 0 null bones,
  46 bindposes; the SMR node carries `localScale(100,100,100)` (cm→m FBX compensation) — relevant to
  any code that reconstructs world bounds from `BakeMesh` (see the capture-tool note below).
- **Import warnings:** none blocking; clean Humanoid avatar (`valid=True human=True`) and clean Generic
  avatar both produced.

## CAPTURE-TOOL BUG caught mid-spike (diagnose-via-trace, NOT an asset defect)

The first captures rendered a giant amber **cone/tent smear**, which looked like a catastrophic skin
mangle. Diagnose-via-trace (`Hyper3DSpikeSceneDiag` + `Hyper3DSpikePoseDiag`) proved the skeleton and
mesh were FINE (Head/Hips/Feet posed correctly; `smr.bounds` size 0.52×1.00×0.46). The cone was MY
capture component's bug: it computed bounds via `BakeMesh(useScale:true)` then applied
`TRS(pos, rot, Vector3.one)` — which DROPPED the SMR node's 100× scale, yielding a near-zero bound, so
the framing put the camera ~1u from a mesh it thought was 0.005u → **camera buried inside the mesh**;
the "cone" was the camera clipping through the interior. Fix: read `smr.bounds` (already correct world
AABB). After the fix, every capture (both rigs, Idle + Walk + bind) is a clean chibi. **Lesson: a
verify-capture that frames from reconstructed `BakeMesh` bounds must account for the renderer node's
lossy scale — `smr.bounds` is the safer source for a windowed/rendered build** (sibling of the
unity-conventions.md stale-SMR-bounds trap, which is about NEVER-rendered headless reads).

## Reproduce

```
UNITY="/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
# Humanoid (ticket-prescribed):
"$UNITY" -batchmode -quit -nographics -projectPath . -executeMethod FarHorizon.Spike.EditorTools.Hyper3DSpikeGen.BuildSpike -logFile ci-out/spike/logs/gen.log
# (or Generic):  ...Hyper3DSpikeGen.BuildSpikeGeneric
"$UNITY" -batchmode -quit -nographics -projectPath . -executeMethod FarHorizon.EditorTools.FarHorizonBuilder.BuildWindows -logFile ci-out/spike/logs/build.log
Build/Windows/FarHorizon.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -spikeCapture -captureDir ci-out/spike/caps
#   -spikeRest  → captures the raw bind pose (animator OFF) for the import A/B
```

The generator registers the spike scene as the ONLY build scene; `BuildWindows` ships it. **Restore the
production build scene before any real build:** `git checkout -- ProjectSettings/EditorBuildSettings.asset`
(or re-run `BootstrapProject.Run`). The spike build dirties URP/Graphics/ProjectSettings too — revert
those (`git checkout -- Assets/Settings Assets/UniversalRenderPipelineGlobalSettings.asset ProjectSettings`).

## Files

- `Editor/Hyper3DSpikeGen.cs` — import (Humanoid/Generic) + de-lit material + Idle↔Walk controller + scene + build registration.
- `Scripts/Hyper3DSpikeCapture.cs` — windowed shipped-exe capture (Idle/Walk/`-spikeRest` bind).
- `Editor/Hyper3DSpike*Diag.cs` — diagnose-via-trace probes (clip names / scene bounds / pose+skeleton). Throwaway.
- `Hyper3DCastaway/` — copied source FBX + textures + generated material/controller.
- `captures/` — the judgment frames (shipped-exe, 1280×720, neutral backdrop, post-enabled URP).
