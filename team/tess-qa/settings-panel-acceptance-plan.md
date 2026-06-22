# Settings Panel — Acceptance-Test Plan

**Ticket:** `86caa4bqp` — feat(ui): in-game tweakable settings panel (live-adjust gameplay params during soak)
**Author (impl):** Devon · **Reviewer (peer):** Drew · **QA:** Tess
**Status:** TEST-DESIGN-AHEAD — authored in PARALLEL with Devon's impl; this is the verification checklist for when his PR lands. NO code yet.
**Sources:** ticket ACs (ClickUp 86caa4bqp) · `team/uma-ux/ui-toolkit-panels-ux-spec.md` §3 + §5 + §6 · `team/TESTING_BAR.md` · `.claude/docs/unity6-mastery.md` §9.

This is a **soak-tuning instrument** (give-the-Sponsor-the-knob), not a shipping options menu. The headline AC is the **extensible registry** (AC2): a future setting must register + render with no panel-code rebuild. Most checks below trace back to that.

---

## 0. PR-intake gates (verify BEFORE walking ACs — hard bounces)

- [ ] **Self-Test Report comment present** (UX-visible PR → mandatory; TESTING_BAR §4). Missing = REQUEST_CHANGES, no further review.
- [ ] **Shipped-build capture** attached/quoted: panel OPENS + a tweak takes effect LIVE, captured from the BUILT exe (not editor). `capture_gate.sh` PASS line + the `BUILD <tag> | <UTC> | <sha>` HUD stamp quoted (TESTING_BAR §3 / shipped-build capture gate). Editor evidence is necessary, never sufficient.
- [ ] **Regression-guard line** in the Done/PR body naming the AC6 test by method name.
- [ ] **Cross-lane integration check** in the Self-Test Report — explicitly: the `Esc` toggle does NOT clash with WASD / Shift(run) / Ctrl(crouch) / Tab(inventory), AND `Input.*` polling (camera zoom/scroll, belt 1–5) is GATED while the panel is open (`_settingsOpen` flag — Uma spec §4.1 input-bleed note). This is the silent-killer surface: a panel that opens but lets keystrokes bleed through to the world is the "AC green ≠ behaves correctly" gap.
- [ ] PR diff does NOT touch `team/DECISIONS.md` (non-Priya PR → bounce if it does).

---

## 1. AC1 — runtime panel + sensible toggle key

- [ ] Panel toggles on **`Esc`** (Uma §3.1; free per input-map audit). Press opens, press closes.
- [ ] Toggle key does **NOT** collide with WASD / Shift / Ctrl / Tab. Verify each in the running build, not by reading the binding table.
- [ ] Built with **UI Toolkit** (`UIDocument` + UXML/USS), NOT IMGUI/uGUI for the new panel (unity6-mastery §9; Uma §1 `SettingsDocument`).
- [ ] Toggle implemented via `style.display` flip (`DisplayStyle.None`↔`.Flex`), NOT `opacity = 0` (unity6-mastery §9 show/hide cost; Uma §1). Verify in source — `opacity:0`-for-hiding is a NIT-or-worse depending on whether it leaves the panel paying render cost.
- [ ] Styling reads on-tone (carved-wood/warm, sub-1.0 channels) — spot-check against Uma §2 `:root` token block; no pure-white labels, no `#FFFFFF`/`#FF0000`/neon. This is a soak-judge item, not a hard gate, but flag drift toward generic-overlay grey.

## 2. AC2 — extensible registry (THE HEADLINE)

- [ ] **A new setting registers + renders WITHOUT panel-code changes.** Verification: read the registration site — adding a setting must be "pick an archetype + label + bind to a live param", a few lines, NOT a UI rebuild (ticket AC2; Uma §3.2 "Registering a new setting = pick an archetype class + give it a label. No new USS"). If adding a setting requires editing the panel's UXML layout or a switch-statement in panel code, that FAILS AC2 — the whole point is later tickets (`86caa4bya` inventory/belt) slot in cheaply.
- [ ] Each setting is a **named, typed entry** (float slider / int stepper / min-max range) bound to a LIVE gameplay param.
- [ ] **Changing a value updates the game IMMEDIATELY** (no restart) — verify in the running build by dragging a wired slider and watching the world respond same-frame-ish.
- [ ] Binding uses `[CreateProperty]` on the SO property + `[SerializeField, DontCreateProperty]` on the backing field (Uma §3.3; both required or it silently falls back to reflection — check this in source; the silent-reflection-fallback is a non-obvious correctness trap).
- [ ] `TwoWay` binding (settings input), NOT `ToTarget` read-only (unity6-mastery §9 binding table; Uma §3.3).
- [ ] `element.dataSource = settingsSO` assigned in C# at Start (UXML `data-source` left UNRESOLVED) — keeps the SO swappable/testable for the AC6 test (Uma §3.3).

## 3. AC3 — wire the available settings; greyed extension hooks for the rest

- [ ] **Wired + live** (features EXIST now): zoom range (OrbitCamera), mouse view-angle range (OrbitCamera pitch clamp), walk speed (WASD `86ca9yq2x`, merged). Each: drag in the panel → the corresponding live system changes in the running build.
- [ ] **Extension hooks only** (features NOT built / not-yet-merged): run speed, jump height, tool-use speed. These must be **present-but-greyed** (`.setting-row--disabled` → `--ink-dim` text + a `(soon)` tag, Uma §3.2), NOT faked params. Verify: the row renders, is visibly disabled, and is NOT bound to a real param that doesn't exist. Faking a param that does nothing = REQUEST_CHANGES (ticket AC3 "don't fake params that don't exist yet").
- [ ] If run-on-Shift (`86ca9yq34`) has merged by PR-time and the run-speed param exists, confirm whether Devon wired it live or left it greyed — either is acceptable per AC3 wording ("wire when it merges / if the param exists"); just confirm it matches reality, not a stub.

## 4. AC4 — range settings clamp the live system (both ends)

- [ ] Zoom-range and view-angle-range rows expose **BOTH** min AND max (archetype B, two thumbs, two readouts — Uma §3.2).
- [ ] The live system is **clamped to both ends**: set zoom min/max, then try to zoom past each bound in the running build — orbit camera must refuse to exceed. Same for pitch min/max.
- [ ] Clamp-hit feedback: thumb flashes `--accent-deny` (~150ms `.setting-row__thumb--clamp`) when min==max or a hard limit is hit (Uma §3.2). NIT-class if absent, but note it.
- [ ] **Edge probe:** set min == max (degenerate range) and min > max (inverted) — system must not break (no NaN, no flipped clamp, no exception). Negative-path coverage per TESTING_BAR §1.

## 5. AC5 — persistence (session + ideally across runs)

- [ ] Tweaked values persist for the session.
- [ ] **Across relaunch** (PlayerPrefs / settings asset, ideal per AC5): dial a value → quit the exe → relaunch → value survives. This is the soak workflow's whole point (Sponsor tweaks survive a relaunch, then reports dialed values to bake as defaults). Verify in the BUILT exe, not editor PlayerPrefs.
- [ ] Persistence is **single-authority**: the setter applies the live effect AND writes PlayerPrefs in ONE call — NOT split between the TwoWay-drive and a separate save path (Uma §3.3 "do NOT split TwoWay-drive and PlayerPrefs-save"). A split is the classic "saved value disagrees with applied value" bug class — check in source.
- [ ] Persistence is SILENT (no "saved" toast — Uma §3.3). Spurious toast = NIT.
- [ ] `reset to defaults` is nice-to-have (AC5) — if present, verify it restores defaults AND re-applies them live; if absent, NOT a bounce.

## 6. AC6 — regression guard (EditMode/PlayMode test)

- [ ] **A registered setting binds + drives its param:** test sets the SO property → asserts the bound live param changed (ticket AC6). This is the success-test.
- [ ] **A range setting clamps the system:** test drives a range past its bound → asserts the system value is clamped to the bound (ticket AC6 / AC4 pairing).
- [ ] **Registry-extensibility coverage (the headline AC deserves a test):** a test that REGISTERS a fresh setting against the registry and asserts it renders/binds — proves AC2 mechanically, not just by code-reading. If Devon omits this, REQUEST it: the extensible-registry is the headline and "renders without panel-code changes" is exactly the kind of claim that silently rots (cf. the dual-spawn `pickup_count > 0` false-green class).
- [ ] **Test isolates the SO** (assigns a test SO via `dataSource`, per Uma §3.3) — the AC6 test should NOT require the full panel UXML to be present; binding is testable in EditMode against the SO + a headless element.
- [ ] Suite is genuinely green — verified from `-testResults` XML `<test-run result="Passed">`, total > 0 (TESTING_BAR §2; empty run = failure).
- [ ] **N≥8 discipline** does NOT apply here (no sampling/determinism claim) — but if Devon claims "live update is instant/deterministic", require the evidence; don't accept N=1.

## 7. Shipped-build capture check (UX-visible → mandatory)

- [ ] Pull the SHA-pinned artifact for the PR (or build locally), run `.github/workflows/scripts/capture_gate.sh Build/Windows/FarHorizon.exe`, confirm `frame_check.py` PASS (no black/empty/uniform/magenta frames).
- [ ] **Panel reads correctly in the BUILT exe:** open the panel in the running exe and confirm rows render with the carved-wood styling, readouts are legible, sliders are draggable. The editor-vs-runtime divergence class (USS not shipped, font-asset stripped, UIDocument sort-order covering the build-stamp) is exactly what this gate exists to catch (TESTING_BAR §3; Uma §7 "scrim dims the WORLD, not the HUD chrome — build-stamp must stay readable").
- [ ] **Build-stamp NOT covered** by the settings scrim (Uma §7) — the soak loop breaks if the stamp is occluded. Verify the stamp is visible with the panel open.
- [ ] Capture shows a tweak TAKING EFFECT live (open panel → drag → world responds), per the ticket success-test.

## 8. Verdict routing

- **APPROVE** when: all 6 ACs green, registry-extensibility proven (code-read + AC6 test), shipped-build capture confirms panel + live tweak, Self-Test Report + cross-lane integration check present, persistence single-authority.
- **APPROVE_WITH_NITS** for: missing clamp-flash feedback, spurious save-toast, styling drift that's still on-tone-enough — file a mechanical NITs follow-up.
- **REQUEST_CHANGES** for: missing Self-Test Report; faked extension-hook params; registry NOT extensible (adding a setting needs a panel rebuild); AC6 test absent or not green; persistence split between drive + save; `Esc` clashes with a movement key or input bleeds through an open panel; DECISIONS.md touched.

---

**Note:** when the PR lands, do the branch checkout in the Tess worktree, run the full EditMode+PlayMode suite, then the shipped-build capture pass before walking §1–§7. Cross-lane integration (input-bleed gating + key-clash) is the highest-value silent-killer surface here — a panel that opens but doesn't gate world input passes every isolated AC and still ruins the soak.
