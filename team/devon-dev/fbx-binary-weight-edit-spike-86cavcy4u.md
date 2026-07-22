# Spike: raw-FBX binary weight-edit feasibility (Option C) — 86cavcy4u

**Question (from the ticket):** Can the right thumb's mis-bound verts be re-assigned index-chain →
thumb-chain (mirroring the left) by a targeted binary/raw edit of the v7700 FBX — preserving
everything else — such that Unity imports it cleanly with the Generic 41-bone avatar and all clips
retarget unchanged, WITHOUT ever re-exporting the node hierarchy (the PR #330 helicopter kill)?

**Verdict: INFEASIBLE via the hypothesized clean mechanism.** The `io_scene_fbx.parse_fbx →
io_scene_fbx.encode_bin.write` re-serialize path — the exact mechanism the ticket proposed — produces
a file that **Unity's Autodesk FBX SDK importer rejects outright**, at BOTH v7700 and v7400. The only
theoretically-surviving Option-C variant is a bespoke surgical in-place byte-patcher, which is
high-effort custom tooling with SDK-acceptance risk and, even if it worked, yields only an approximate
mirror. Not the cheap win Option C was hoped to be. Details below.

All work done on COPIES in `scratchpad/` + a throwaway `Assets/_fbxspike/` (deleted). Zero shipped-asset
changes. Source FBX: `Assets/Art/Character/Castaway/v4/castaway_v4_rigged.fbx` (sha1 of copy
`bfb6d41a9db8747bd720295a20e93d90a47a06ba`, 296064 bytes, v7700).

---

## 1. Ground truth — the right hand's SKIN is structurally asymmetric (cluster membership), not just mis-weighted

RAW-parse (`tools/debug/fbx_skin_cluster_dump.py`, no importer) of the shipped v4 rigged FBX. The tool
enumerates skin **Cluster** deformers, so it measures the SKIN structure — it can prove a cluster is
ABSENT, but it says nothing about the bone hierarchy (a zero-weight bone has no cluster yet still exists):

- 1 Skin deformer, **32 Cluster** deformers (41-bone rig; 9 bones drive no verts).
- RIGHT thumb chain: `righthandthumb1` (18 verts) + `righthandthumb2` (1 vert) — **NO `righthandthumb3`
  CLUSTER.** The `righthandthumb3` BONE itself EXISTS on both sides and carries ZERO skin influence on the
  right — per 86cau4za2's QA-verified correction (comment 90150243345817: "the skeleton was never missing
  anything"). So the gap is a missing skin cluster/deformer, NOT a missing bone.
- LEFT thumb chain: `lefthandthumb1` (26) + `lefthandthumb2` (6) + `lefthandthumb3` (3) — HAS a thumb3 cluster.
- RIGHT index chain: index1 **56** / index2 18 / index3 10 — vs LEFT index 14 / 8 / 6. Right `index1` alone
  carries **+42** verts vs the left (56 − 14) — the mis-bound thumb geometry.
- Overlap: all **18** right-thumb verts are ALSO owned by the right index clusters; **42** verts are
  right-index-only (the mis-bound thumb geometry — the ticket's "48" in file/control-point space).

**Consequence for any edit:** "mirror the left" cannot be a weight-VALUE overwrite. The mis-bound verts
must MOVE from the index clusters (currently listing them) INTO the thumb clusters (currently NOT listing
most of them) → cluster vertex-MEMBERSHIP changes → **array LENGTHS change**. A pure length-preserving
in-place value patch is structurally incapable of this fix. (And there is no `righthandthumb3` CLUSTER to
mirror into — the bone is present, so a full left-mirror would have to CREATE a thumb3 cluster + its
bone connection, not merely move verts; short of that, only an approximate thumb1/thumb2 distribution.)

## 2. The re-serialize mechanism preserves the rig at the PARSE level — but that is not enough

`scratchpad/roundtrip.py`: parse the FBX with `parse_fbx`, faithfully convert the parsed tree into an
`encode_bin` tree (type-exact per-property mapping), re-serialize with `encode_bin.write`. It never
touches a Blender scene/armature, so it structurally cannot rebake rest orientations (the PR #330 cause).

Validated against `tools/debug/fbx_rest_convention_diff.py`:

| File | version | rest-diff vs orig | notes |
|---|---|---|---|
| identity round-trip (no edit) | 7700 | **0 of 42 changed** | GlobalSettings identical, 42=42 models |
| membership-move edit (22 verts index→thumb1) | 7700 | **0 of 42 changed** | index1 56→38, index2 18→15, index3 10→9 |
| identity round-trip forced v7400 | 7400 | **0 of 42 changed** | rest preserved even at 7400 |

So at the `parse_fbx` layer the mechanism looks perfect: bones untouched, canary version controllable,
length-CHANGING array edits round-trip. **But `parse_fbx`/`encode_bin` are one-way Blender tools with no
supported round-trip, and the re-encoded file is materially different (296064 → 285500 bytes, ~3.5%
smaller — different array compression + offset layout).**

## 3. The kill — Unity's FBX SDK importer REJECTS the encode_bin output

Headless Unity import smoke (throwaway `Assets/_fbxspike/`, `-executeMethod FbxSpikeImport.Run`,
Unity 6000.4.11f1). Control = a pristine COPY of the shipped FBX:

| File | version | Unity import result |
|---|---|---|
| `v4_orig.fbx` (pristine copy, control) | 7700 | **OK** — SMR=1, bones=32, verts=3840 |
| `v4_roundtrip.fbx` (identity round-trip) | 7700 | **"File is corrupted"** — SMR=0, no mesh |
| `v4_edit.fbx` (membership-move edit) | 7700 | **"File is corrupted"** — SMR=0, no mesh |
| `v4_rt7400.fbx` (identity, forced v7400) | 7400 | **"Couldn't read file / None of the registered readers can process the file"** — SMR=0 |

**The identity round-trip (ZERO edits) fails identically to the edited file** → the corruption is caused
by the `encode_bin` RE-SERIALIZATION itself, not by any weight edit. And it is **not a converter bug**:
the top-level node structure is byte-faithful (both files: identical 11-node tree `FBXHeaderExtension,
FileId, CreationTime, Creator, GlobalSettings, Documents, References, Definitions, Objects, Connections,
Takes`). The rejection is a fundamental incompatibility — Blender's `encode_bin` output for a
parse-then-re-encode is re-readable by the lenient `parse_fbx` but fails the Autodesk SDK's stricter
validation. (Blender's normal EXPORT path uses the same `encode_bin` and IS Unity-valid — so the problem
is specifically the parse→re-encode direction, not `encode_bin` per se.)

Forcing v7400 does not rescue it (still rejected), and v7400 would trip the `RiggedCastawayFbx_
IsGenuineMixamoExport_NotBlenderRoundTrip_86cau4za2` canary anyway.

## 4. The only surviving Option-C variant, and why it is not worth it

A **true surgical in-place binary patcher** — start from the SDK-valid original bytes, change only the
specific Cluster `Indexes`/`Weights` arrays, recompute the `EndOffset` chain up to root + rewrite the
footer — is the one path that keeps the byte structure the SDK accepts. But:

1. The fix is inherently **length-changing** (verts move between clusters), so it is NOT the trivial
   value-overwrite case; it needs the full offset-chain rewrite = a bespoke targeted FBX binary writer.
2. Several `Weights` arrays are **zlib-compressed** (encoding=1 in the raw scan), so even value-only
   edits would force recompression → different lengths → the same offset-rewrite machinery.
3. There is **no `righthandthumb3` CLUSTER** on the right (the thumb3 BONE exists on both sides with zero
   skin influence — 86cau4za2 comment 90150243345817), so a full left-mirror would have to CREATE a thumb3
   cluster + its bone connection, not merely re-assign existing verts → only an APPROXIMATE thumb1/thumb2
   mirror is cheaply possible.
4. It must STILL clear the Sponsor-eye soak that defeated 9 prior rounds (metrics proved insufficient).

Net effort/risk of the surgical patcher **meets or exceeds Option B (a targeted Mixamo re-rig)** — without
B's advantages (a clean, SDK-valid, correctly-normalized skin produced by the tool that's designed to do
exactly this). It is not a shortcut.

## 5. Recommendation

- **Kill the re-serialize path** (`parse_fbx → encode_bin`) — empirically dead for a Unity-importable file.
- With Option A (Blender source edit / no re-rig — PR #330) and now Option C (binary weight edit) both
  empirically CLOSED for a Mixamo-Generic v7700 character, the realistic resume routes are:
  - **Option B — targeted Mixamo re-rig**, then re-apply the Sponsor-accepted left-hand dial on top as a
    post-step (the left dial is a baked seed VALUE, portable across a re-rig).
  - **Ship-as-is** (the Sponsor's current, deferred choice): the accepted "block-with-a-thumb" right hand.
- The deferred defect has **no cheap surgical binary fix.** Evidence-based input for the resume decision.

## Artifacts / reproduce

- Durable instrument (committed): `tools/debug/fbx_skin_cluster_dump.py` (+ registry row) — the raw
  skin-cluster/weight dumper; sibling of `fbx_rest_convention_diff.py`.
- Spike scratch (not committed): `scratchpad/roundtrip.py` (parse→patch→encode_bin round-trip harness,
  `--edit`/`--ver=` flags), `scratchpad/probe_skin.py`, `scratchpad/struct_probe.py`,
  `Assets/_fbxspike/Editor/FbxSpikeImport.cs` (throwaway Unity import smoke; deleted after use).
- Gates the produced files pass at the parse layer: `fbx_rest_convention_diff.py` 0/42, v7700 canary
  green. The gate they FAIL is the Unity FBX SDK import — which is the decisive one.
