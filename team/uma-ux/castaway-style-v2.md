# Castaway Style v2 — Cartoonish Stylization Direction Brief

**Ticket:** 86ca8ca1m · **Owner:** Uma (direction) → Devon (implementation) · **Reviewer:** Tess · **Visual gate:** Sponsor soak · **Status:** DIRECTION — docs only, no mesh/code here.

**Sources of truth (look before building):**
- **Style pole:** [`inspiration/2026-06-12_21h00_32.png`](../../inspiration/2026-06-12_21h00_32.png) — chunky cartoon castaway. STYLE ONLY.
- **Identity pole (LOCKED):** RandomGame `_castaway_judge/castaway_v4_south_2x.png` + `_castaway_v4_east_2x.png` (read-only archive at `c:/Trunk/PRIVATE/RandomGame`). This is *who* the castaway is.
- Companion: [`style-guide-v2.md`](style-guide-v2.md) §2 (the character section this brief expands into Devon-ready detail).

---

## 0. The tonal anchor (read this first)

**He's a little wooden-toy adventurer — young, hopeful, and barely shipwrecked, carved chunkier.** The stylization makes him *cuter and more toy-like*, never older or tougher. The single most important sentence in this brief: **we are stealing the reference's BODY, not its SOUL.** The reference is a grizzled, bearded, shirtless survivor — that man is explicitly NOT our castaway (Sponsor-clicked, 86ca8ca1m). What we take from him is purely geometric: the big head, the big eyes, the sausage limbs, the mitten hands, the blocky feet, the flat smooth-shaded material read.

If a stylization beat makes him read older, harder, leaner, or more rugged, it is wrong even if it's faithfully chunky — cut it. The gate the Sponsor will soak against is *"cartoonish like the reference, still MY castaway"* — both halves must land. Bright open face + sandy/ginger hair + warm khaki shirt + bare feet = he stays the hopeful boy who washed ashore. Big head + big eyes + chunky limbs = he becomes a toy. Those are additive, not in tension.

**The trap to avoid:** chunky proportions on an adult-faced, bearded, shirtless body is the reference — and the reference is the rejected identity. Chunky proportions on a smooth-faced, shirted, bright-eyed young castaway is the target. The proportions are identical; the identity payload is everything.

---

## 1. What we KEEP from the identity pole (non-negotiable)

From the `_castaway_judge` v4 sheets — these are the locked identity and they carry through the stylization unchanged in *meaning* (the shapes get chunkier, the identity does not):

- **Young, smooth face** — no beard, no stubble, no age/brow lines. The reference's beard is OUT.
- **Bright, open, friendly expression** — hopeful, a little eager. NOT the reference's heavy-browed scowl.
- **Warm sandy / copper-ginger hair** — the U2-6 hair value carries.
- **Clothed:** warm khaki/sand shirt with rolled sleeves, leather sash/strap + satchel accent, muted teal-blue rolled trousers. The reference is shirtless — we are NOT. (Clothing slots come straight from the U2-6 recolor; §3.)
- **Bare feet** — this ONE thing the reference and our castaway share, and it fits the shipwrecked read. Keep it; just make the feet chunky/blocky (§2).
- **Axe-on-back silhouette** — carries from the identity sheets (prop is a separate ticket; do not re-author it here).

---

## 2. Target proportions (Devon-implementable ratios)

The reference reads ~3 heads tall with a dominant head, sausage limbs, mitten hands, blocky feet. Transfer the *ratios*, not the body.

| Measure | Target ratio | Implementation note |
|---|---|---|
| **Head : total height** | **~1 : 3** (≈3 heads tall) | The single biggest readability lever — **oversize the head first.** Land **3.0 heads** as the baseline; the Sponsor soak can push toward **2.5** ("cuter") if asked. Do NOT go below 2.5 (drifts into bobblehead) or above 3.3 (loses the toy read). Scale the head up about the neck pivot so the rig's head bone stays centred. |
| **Hands** | **oversized blocky mittens, ~1.3–1.5×** the realistic hand relative to the new (chunkier) arm | Simplify to a **mitten/club** — minimal or no separated fingers (a thumb-suggestion is fine). Reads cleaner at orbit distance AND deforms cleaner on the existing hand bones than splayed fingers. |
| **Feet** | **chunky blocky bare feet — wide and short**, no thin ankle | Wide stable base. Keep barefoot (identity + castaway read). Short toe-suggestion at most; no individuated toes. |
| **Limbs (arms + legs)** | **chunky cylindrical, slightly tapered, no thin joints** | Sausage-chunky, not anatomical. Even taper; soft elbow/knee. Chunky limbs survive rig deformation better than thin ones (less pinch at the joint). |
| **Torso** | **compact, slightly stout, SHORTER** than realistic | The big head dominates; the torso is short and a little barrel-stout. Keep the shirt + sash silhouette legible on it. |
| **Neck** | **short / nearly absent** | Big head sits close on the shoulders — part of the toy read. |

**Silhouette gate (Tess QA pin):** at orbit-camera distance the castaway must read as *toy-chunky* from outline alone — big round head, no spindly geometry anywhere. If any limb or the neck reads thin/realistic, the proportion edit is incomplete.

---

## 3. Face treatment (the "expressive" read — identity stays young/hopeful)

The reference's defining feature is its eyes; that's exactly what we want — but our expression, not its.

- **Eyes: big, dark, rounded — ADOPT the size & placement.** Large dark almond/oval eyes set fairly low and wide on the face, taking a confident share of the face area (the reference reads ~1.5–2× a realistic eye). **This is the most identity-changing single move on the face** — big eyes = young + expressive + toy-like, which is squarely on our identity.
- **Expression: bright, open, friendly — KEEP ours, REJECT the reference's.** No heavy brow, no scowl, no squint. The eyes are wide and clear; a soft upward suggestion at the mouth. The reference's eyes are big but *hard*; ours are big and *warm.* Same geometry, opposite mood.
- **No beard, no stubble, no age lines.** Smooth-shaded young skin. This is the line between "cartoon castaway" and "the rejected rugged adult" — it is load-bearing.
- **Simplified features:** small simple nose, simple mouth, flat smooth-shaded skin with soft AO under the chin/brow. Warm tan skin from U2-6 carries unchanged.
- **Hair:** the copper-ginger sweep from U2-6/the v4 sheets; chunk it up to match the new head scale (a few bold faceted hair masses, not fine strands) — but keep the colour and the boyish forward sweep.

**Eye-implementation note for Devon:** the current Quaternius rig carries eyes as a material slot (`Eyes` — one of the 6, see §4), not necessarily as separable geometry. Making the eyes read *big* may require either (a) enlarging/repositioning the eye geometry/UV island on the head mesh, or (b) if eyes are texture-on-face, enlarging them in the eye material's albedo. Either is fine — the gate is the *read* (big dark warm eyes), not the technique. Flag which route you take in your Self-Test Report so Tess pins the right QA criterion.

---

## 4. Palette / material carry from U2-6 (guard-constrained, NOT removed)

The U2-6 recolor (commit `3bebc5b`, PR #10, ticket `86ca8bdhb`) is the colour identity and it **carries verbatim** — the stylization changes SHAPE, not COLOUR. Source: `Assets/Scripts/Runtime/CastawayCharacter.cs` (6-part recolor + leather slot).

- **The 6-part recolor enumerates ALL materials** — Shirt / Skin / Pants / Eyes / Hair / (Socks→repurposed) + the NEW leather slot (belt/strap/satchel). **Hard trap (`unity-conventions.md` §FBX/rigs): recolor must enumerate ALL materials — a 4-slot assumption silently erased the face before (iter7→iter8).** If the proportion edit re-imports or re-bakes the mesh, re-verify all slots map; do NOT regress to fewer.
- **Verified U2-6 anchor:** `Castaway_Shirt _BaseColor` float32 == `0.72, 0.60, 0.42` (warm khaki/sand). That value is the identity benchmark — keep it (or a Sponsor-tuned warm-bright sibling), never let it drift dark.
- **LUMA GUARD STAYS — this is guard-constrained tuning, not guard removal.** The EditMode shirt-luminance identity guard (**>0.6**) in `Assets/Tests/EditMode/CastawayCharacterTests.cs` fails CI on any dark/grizzled drift. **It must stay GREEN through this work** (explicit 86ca8ca1m AC: "identity guards updated, not discarded; luminance guard carries"). Any recolor you touch during the mesh edit keeps shirt-luma >0.6. Do NOT relax or remove the guard to make a darker stylization pass — if a stylization beat wants a darker shirt, that beat is wrong (it's drifting toward the grizzled reject).
- **Flat smooth-shaded materials** (no realistic skin shading); **sub-1.0 on every channel** (HDR/sRGB-clamp discipline carries from style-guide-v2 §6 + Zone-D). Saturated/warm is fine; blown-out is not.
- **Proportion-guard (optional, encouraged — 86ca8ca1m AC "add proportion assertions if feasible"):** consider an EditMode assert on the head:total-height bounds ratio (≈2.5–3.3) so a future rig edit that flattens the head fails CI the same way the luma guard catches a dark shirt. Pin it loose (a range, not a point) so Sponsor soak-tuning of "cuter" (toward 2.5) doesn't red the build.

---

## 5. Model-source recommendation

**RECOMMENDED: Blender-MCP proportion-edit of the EXISTING rigged Quaternius mesh (first choice).**

Per `unity-conventions.md` §Asset creation, Blender MCP is the first-choice creation/edit route, and for the character specifically: **edit the existing rigged mesh (scale-up head about the neck pivot, chunk-up hands/feet/limbs, enlarge eyes) rather than sourcing a new base.** Rationale:

1. **Preserves the NavMesh/Animator rig → Idle/Walk survive** — the hard 86ca8ca1m AC. Editing vertex positions on the existing skinned mesh keeps the existing bones/weights/avatar, so the Animated-Men Idle/Walk clips keep driving it. A fresh base means a fresh `avatarSetup` T-pose round (`unity-conventions.md` §FBX/rigs — the avatar-from-T-pose trap) and re-rigging risk.
2. **Keeps the U2-6 material slots intact** — the 6-part recolor + leather slot stay mapped (re-verify per §4); a new mesh re-opens the recolor-all-materials trap from scratch.
3. **Cheaper + lower-risk than new-mesh**, and the chunky target is a *deformation* of the current proportions (bigger head, fatter limbs) — exactly what vertex-edit-on-rig is good at.

**Fallback (only if the rig can't take it cleanly):** new stylized CC0 base (re-rig + re-avatar + re-recolor all 6 slots + re-normalize height). Use ONLY if scaling the head/limbs on the existing mesh tears weights past acceptable deformation at the joints. Devon owns this call — but start with the edit-in-place route.

**Hard trap-flags for Devon (from `unity-conventions.md`), in order:**
1. **Normalize intrinsic import height to ~1u after the mesh edit** — scaling the head/limbs changes bounds; re-measure and re-normalize `globalScale` or camera/shadow/size calibration breaks (Animated-Men intrinsic is ~4.96u — §FBX/rigs).
2. **Recolor enumerates all 6 materials** (re-verify post-edit; §4) — the 4-slot assumption erased the face before.
3. **Idle/Walk must survive** — `HumanArmature|` clip-name prefix means use `.Contains` matching + the `looped < expected` guard; verify NO legs-up / T-pose-mid-walk regression on the chunked rig. This is THE binding rig check.
4. **Editor-time serialized + shipped-build capture** — build the edited mesh into the scene/prefab via the executeMethod path; Awake-built procedural hierarchies ship MANGLED (the iter6 "legs-up" class). Final evidence comes from the SHIPPED exe via `serve_soak.sh`, never an editor RenderTexture (editor capture can show a false negative — hero-axe PR #21 precedent).
5. **Blob shadow re-fit** — the wider chunky stance needs the blob/contact shadow re-sized to the new footprint (explicit 86ca8ca1m AC).
6. **Luma guard stays green** (§4) — non-negotiable identity gate.

---

## 6. Acceptance read (what "done right" looks like — for Tess + the Sponsor soak)

- **Proportions:** ~3 heads tall, big head, big dark warm eyes, mitten hands, blocky bare feet, sausage limbs, short stout torso — reads toy-chunky in outline at orbit distance (§2 silhouette gate).
- **Identity:** unmistakably the YOUNG, HOPEFUL castaway — smooth bright face, no beard, sandy-ginger hair, warm khaki shirt + leather sash, teal trousers. No grizzled/rugged drift. Luma guard green.
- **Rig:** Idle/Walk animate clean (no legs-up class); blob shadow fits the new stance.
- **Evidence:** SHIPPED-build capture (serve_soak, stamp == HEAD), not editor-only.
- **Sponsor soak (the binding feel-gate):** *"cartoonish like the reference, still my castaway."* Both halves.

---

## Cross-references

- Ticket **86ca8ca1m** (this brief) · **86ca8bdhb** / commit `3bebc5b` (U2-6 recolor + luma guard — the colour identity this preserves).
- [`inspiration/2026-06-12_21h00_32.png`](../../inspiration/2026-06-12_21h00_32.png) — style pole (STYLE ONLY).
- RandomGame `_castaway_judge/castaway_v4_*` — identity pole (LOCKED young/hopeful, read-only).
- [`team/uma-ux/style-guide-v2.md`](style-guide-v2.md) §2 — the character section this brief expands; §6 HUD/HDR-clamp palette discipline.
- [`.claude/docs/unity-conventions.md`](../../.claude/docs/unity-conventions.md) — §Asset creation (Blender-MCP route); §FBX/rigs (avatar T-pose, `HumanArmature|` clip prefix, height-normalize, recolor-all-materials); §Editor-vs-runtime (Awake-no-serialize / legs-up class, editor-RenderTexture false-negative); §Low-poly mesh patterns (smooth-shaded ~60° smoothing).
- `Assets/Scripts/Runtime/CastawayCharacter.cs` (6-part recolor) · `Assets/Tests/EditMode/CastawayCharacterTests.cs` (shirt-luma >0.6 guard) — Devon's edit + guard targets.
