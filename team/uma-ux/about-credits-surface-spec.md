# About / Credits Surface — UX & Visual Direction

**Ticket:** `86cay4k73` (feat(ui): in-game credits/about surface) · **Owner (impl):** Devon · **Reviewer:** Drew · **Direction:** Uma
**Status:** DIRECTION — docs only. No implementation here.
**Verified against:** `origin/main` @ `c8ce948`. Every path, line number and quoted string below was read in this worktree; none is relayed. *(Re-verified at `c8ce948` during round 2 — the `SettingsPanel.uxml` / `.uss` / `.cs`, `SettingsCategory.cs`, `BootHud.cs`, `BootstrapProject.cs` and `Castaway_Attribution.txt` refs cited below all still resolve, with no drift from the original `3992e96` pin.)*

> ### ⚠ REVISION 2 — 2026-07-31, re-verified at `origin/main` @ `8ad6e24`
>
> §§0–8 landed in **PR #368** and stand. **Revision 2 adds §§9–14** and closes four gaps that
> R1 left open — one of which is a **defect that would have shipped an AC4 violation**:
>
> | # | Gap | Closed in |
> |---|---|---|
> | 1 | R1 says render *"the verbatim text of every shipped attribution file."* At today's file that is **all 146 lines** — including `Viktor.G` and `"Mini Chibi Kid"`, the two names **AC4 explicitly bars**. R1 therefore violates AC4 by construction. | **§9** — marker-delimited extraction + the exact retained text, quoted. |
> | 2 | R1 specifies **no dismissal** — the word `Esc` does not appear in it, and it never says what view the drawer shows when reopened. | **§10** |
> | 3 | R1 does not state the Danish-keyboard constraint on its entry point, nor the footer-swap wiring trap. | **§10** |
> | 4 | R1 has no developer-verifiable AC list and no crisp OOS list. | **§13**, **§14** |
>
> **Drift check at `8ad6e24`:** `git diff --stat c8ce948..8ad6e24` over `SettingsPanel.cs` / `.uss` /
> `.uxml`, `Palette.uss` and `Castaway_Attribution.txt` is **empty** — every R1 line-ref still resolves
> (spot-checked `:409`, `:444`, `:579`, `:619`, `:637`, `:645`, `:671`). The §1 debt scan re-run at
> `8ad6e24` still returns exactly one attribution file, zero `.ttf`/`.otf`, zero `.wav`/`.ogg`/`.mp3`,
> and a `Packages/manifest.json` with no non-`com.unity.*` dependency.

---

## 0. Tonal anchor (read this first)

**This is the maker's mark on the underside of a hand-made thing — not a movie's end-roll.**

Far Horizon's whole material read is "someone built this by hand out of wood." The UI is already carved from that
same wood ([`gameplay-ui-direction.md`](gameplay-ui-direction.md) §0: *"warm cream ink on soft dark-walnut plates…
as if carved from the same wood as the axe haft"*). The board images say the same thing at world scale — I looked at
`inspiration/2026-06-12_21h16_13.png` and `21h13_31.png` before writing this: faceted saturated hills, soft daylight,
a lot of quiet air, nothing shouting.

A credits surface is one of the very few places the game speaks *about itself*. Two ways to get it wrong:

- **Too corporate** — a scrolling wall of names with a fade-out. That is a different game's voice. It reads as
  compliance paperwork bolted onto the painting.
- **Too decorative** — a parallax horizon, a slow pan, a musical sting. Sponsor's own tone rule kills this:
  `game-juice.md` §0, *"if the effect reads louder than that sentence, it's miscalibrated."* A credits panel that
  performs is a credits panel that got above its station.

**The target read:** the player pulls open the same small wooden drawer he tunes his needs in, and finds a short,
warm, honest note on the back of it. It takes ten seconds to read. It says thank you, it names who helped, and it
tells you which build you're standing in. Then you close it and get back to the island.

**The gate:** every beat below must serve that read. If a beat makes this surface longer, louder, or more like a
product page, cut it — even if it's tidy.

---

## 1. What attribution is actually owed (established, not assumed)

I ran each check below in this worktree. **Result: the debt is small, and it is entirely the castaway pipeline.**

| Check I ran | Result |
|---|---|
| `git ls-files \| grep -Ei "attribution\|licen\|credit\|third.?party\|NOTICE"` | **Exactly one file:** `Assets/Art/Character/Castaway/Castaway_Attribution.txt` (+ its `.meta`). Nothing else in the tree. |
| `Assets/Art/Character/Castaway/Castaway_Attribution.txt:24-25` | verbatim: *"Retain this attribution in any distribution of the game (an in-game / about-screen credits entry covers it)."* |
| `cat Packages/manifest.json` | **Every dependency is `com.unity.*`** — engine/URP/test-framework/modules only. No third-party package. |
| `git ls-files \| grep -Ei "\.(ttf\|otf)$"` | **empty** — no third-party font ships. |
| `git ls-files \| grep -Ei "\.(wav\|ogg\|mp3)$"` | **empty** — no third-party audio ships. |
| `git log -3 -- …/Castaway_Attribution.txt` | `03bca30` *"refresh Castaway_Attribution.txt to the live v4 hero (86cay4hyz) (#356)"* — **the correction has LANDED.** The Sequencing clause in the ticket resolves to its good branch: the panel inherits corrected content. |

### 1.1 The floor — what MUST appear

Read from `Castaway_Attribution.txt:27-41`:

1. **Mixamo (Adobe)** — the animation clips **and** the auto-rigger skeleton + skin weights. Load-bearing detail:
   this applies to **every** hero version *including the live v4* (`:30-31` — v4's mesh is in-house but its rig is a
   fresh Mixamo Standard auto-rig, `:126-130`). It is not a legacy credit; it covers what the player is watching
   move right now.
2. **Hyper3D Rodin (Creator-tier web export)** — the v1 / v2 / v3 hero meshes + their diffuse/normal maps. Not the
   live hero, but the file is explicit at `:32-37` that the credit stays because all three remain in the repo as the
   rollback chain, and it deliberately declines to assert whether an unselected FBX lands in a given built player.
   **Credit it.**
3. **The verbatim retained text itself.** The instruction is *"Retain this attribution"* — the strongest reading of
   "retain" is that the text survives distribution verbatim, not merely in paraphrase. §4.3 below carries it.

### 1.2 What must NOT appear

- **No CC-BY entry of any kind.** `Castaway_Attribution.txt:43-48` states plainly that no castaway asset is
  CC-licensed and that no `*_License_CC-Attribution.txt` ships; my `git ls-files` scan above independently confirms
  no such file exists anywhere. Specifically **no Viktor.G ("One-handed stylized axe")** and **no joaobaltieri
  ("Mini Chibi Kid")** — both assets are gone (`86cabh907` / PR #100; `86cay47zh` / PR #352).
- **No credit for the v4 hero mesh or its palette texture.** In-house, Blender, this project (`:38-41`, `:118-125`).
- **No credit for openai-image.** The A-pose concept images were an *input* to the Rodin generation; they are not a
  shipped asset. Naming them widens the list past the debt, which AC4 calls its own defect.

### 1.3 The honest caveat — say this out loud, do not paper over it

`Castaway_Attribution.txt:49-52` carries its own **OPEN QUESTION**, and I am repeating it rather than resolving it,
because I have no source that would let me resolve it:

> *"the exact licence / terms-of-use text behind the retain instruction above is not recorded anywhere in this repo.
> Treat the retain instruction as the operative rule, and re-read the Hyper3D Rodin (Creator tier) and Adobe Mixamo
> terms before any public distribution."*

**So the precise claim this spec supports is:** the surface below discharges the attribution obligation *as recorded
in the repo*. It does **not** establish that the recorded instruction is the complete obligation — nobody has read
the upstream Rodin / Mixamo terms and written the result down. **`Hypothesis (unverified):`** free-tier Mixamo and
Creator-tier Rodin may carry conditions beyond attribution (e.g. on redistributing source assets vs. shipping them
inside a game). I have not checked and am not asserting it either way. → Sponsor-input item S1 (§8).

---

## 2. Placement — the call, and why

**Recommendation: a dedicated ABOUT VIEW inside the existing F1 player Settings drawer, reached from a text button
in that drawer's footer. No new keybind, no new panel, no `.uxml` change.**

The ticket's AC1 already locks the container (F1 drawer, no new keybind) and leaves the shape to me. Here is the
shape and the reasoning, plus the reachability check I actually ran rather than assumed.

### 2.1 Reachability is verified, not assumed

The brief asks whether this fights a WASD + mouse-orbit scheme. It does not, and here is the ground truth:
`OrbitCamera.cs:192-196` + `:226-234` — the cursor is **free and visible at all times except while the RIGHT mouse
button is held** for orbit, and `UiInputGate.CaptureWorldInput` forces `rmbHeld` false the frame a panel opens
(`:226`), so the cursor releases to free+visible as the drawer appears. The comment at `:194-195` states the intent
verbatim: *"we restore the free, visible cursor so the Sponsor can click menus / inventory / belt."*

A click target inside F1 is therefore consistent with how the whole drawer already works (sliders, steppers, the
reset button, the dev corner-picker are all mouse-driven). **No detour, no mode change, no new key.**

### 2.2 Why a footer button + view-swap, and not a collapsible row

The ticket's own default offers *"a tab or a collapsible section at the bottom."* I am choosing neither. Five reasons,
in priority order:

1. **Discoverability is the whole AC.** AC1's destination is *reachable*. The footer (`.settings-panel__footer`) sits
   **outside** the ScrollView — it is on screen the instant F1 opens, always, at any scroll position. A collapsed
   section at the bottom of the row list is below the fold today with 8 player rows and will be further below it the
   moment a ninth is registered. A legal surface must not depend on the player scrolling to the end of a settings
   list to find it.
2. **It keeps "credits is not a setting" true visually, not just structurally.** AC1's second constraint bars
   smuggling credits through `SettingsCatalog` / `SettingsCategory.PlayerIds`. A collapsible that lives *inside* the
   settings ScrollView, styled like a row, re-asserts "credits is a row" to the eye even though the code says
   otherwise. A separate view says the opposite, which is the truth.
3. **Overflow degrades cleanly (see §6).** In a one-ScrollView design, expanding credits changes the settings view's
   scroll extent — the player can end up scrolled into the middle of prose with his need-toggles somewhere above.
   Two views, two independent scroll contexts: the credits block can grow to any length and the settings layout is
   untouched. Forever.
4. **It fixes the "Reset to defaults" mismatch for free.** A destructive-looking button parked next to static legal
   text is a small wrongness that would nag every time. In the view-swap, the footer shows `← Back` *instead of*
   `Reset to defaults` while About is open (§5.3) — the footer always offers exactly the actions that make sense for
   what is on the plate.
5. **There is an exact in-repo precedent for the scoping.** `SettingsPanel.BuildCornerPicker(devContainer)`
   (`SettingsPanel.cs:645-655`) builds a chip into **one** drawer's chrome only. `BuildAbout(playerContainer)` is
   the mirror image — F3 never sees it, and the shared `SettingsPanel.uxml` shell is **not touched at all**. That
   shell is cloned by both drawers (`:579-583`); anything added there leaks into the dev console. Building in C#
   scoped to the player container is both the established pattern (every row is C#-built, `BuildRows` `:671-691`)
   and the only leak-free one.

### 2.3 What I am *not* recommending, and why

- **A tab strip in the header.** Would either leak into F3 via the shared shell or need conditional building anyway
  — same cost as the footer route with more chrome, and it re-frames a quiet drawer as a tabbed dialog.
- **A main-menu / title-screen About.** No main menu exists (the game boots straight into `Boot.unity`); building
  one is a Sponsor-scale design decision and is explicitly OOS on the ticket.
- **Its own keybind.** F1/F3/F9/F10 is a Sponsor-facing map (`SettingsPanel.cs:134-155`); AC1 bars adding to it, and
  I agree — a legal surface does not deserve a top-level key that a real feature will want later.

---

## 3. The copy — my words, verbatim, ready to paste

The ticket lists Uma as *"consulted only if she wants her own words on the copy."* I do. Priya's default (`<source>
— <what it covers>`, no prose) is factually right and tonally *absent* — it reads as a manifest. One warm sentence
costs nothing and makes this the game's voice instead of a compliance artifact.

### 3.1 Intro — two lines, above the list

```
Some of what you're standing in was made by other hands.
These are them, with thanks.
```

**Why these words.** *"what you're standing in"* is the island, at human scale — the game's own north-star framing,
not a company's. *"other hands"* carries the whole hand-made material read in two words and is literally true (a rig,
a skeleton, three meshes). *"with thanks"* reframes a licence condition as gratitude — legally identical, tonally
the entire difference. No product name, no version, no marketing adjective, nothing that ages.

**What I deliberately did NOT write:** anything claiming a team size or composition. There is no Sponsor-approved
public roster and inventing one is not my call (→ S2, §8).

### 3.2 The entries — name + one caption line each

Exactly two today. **Name in cream, caption dim beneath it** (not an em-dash on one line — see §5.2 for why).

```
Mixamo  (Adobe)
The castaway's skeleton, and every animation he plays.

Hyper3D Rodin
Earlier versions of the castaway, still carried in this build.
```

Both captions check out against the source: Mixamo covers rig + clips **including on the live v4**
(`Castaway_Attribution.txt:28-31`, `:126-133`); Rodin covers v1/v2/v3 meshes + maps which are not live but remain in
the tree (`:32-37`). *"still carried in this build"* is the honest phrasing of the rollback chain — plainer than
"fallbacks," and it does not leak dev vocabulary at the player.

**⚠ The displayed label is NOT the guard's match key.** `Mixamo  (Adobe)` does **not** occur in the attribution
text — the file says *"Mixamo animation clips (Adobe, free account)"* (`:28`). So each entry carries **two distinct
strings**, and §4.3's **G3 asserts on the token, never on the displayed label**:

| Entry | `Source` (displayed to the player) | `MatchToken` (what G3 asserts) |
|---|---|---|
| 1 | `Mixamo  (Adobe)` | `Mixamo` |
| 2 | `Hyper3D Rodin` | `Hyper3D Rodin` |

Verified against `origin/main` @ `c8ce948`: `Mixamo` and `Hyper3D Rodin` are both present in
`Castaway_Attribution.txt`; `Mixamo  (Adobe)` and `Mixamo (Adobe)` are **both absent**. Conflating the two fields
is what makes G3 go red on day one — see §4.3.

### 3.3 Build stamp — the last line

```
BUILD <tag> | <UTC> | <sha>
```

Rendered dim, smallest text, bottom of the view, above nothing. **This is functional, not decoration:** it is the
first thing anyone needs when reporting a problem, and it is what the word "About" means. It also reuses a value
that already ships — `BootHud.cs:26` builds `"BUILD " + BuildInfo.Stamp` from `Resources/BuildStamp.txt`, written
editor-time by `BootstrapProject.WriteBuildStamp` (`BootstrapProject.cs:269-277`). **Read the same
`BuildInfo.Stamp` field; do not re-derive or re-format it** — the placeholder line above is the shape only, the
live value is whatever that field returns.

### 3.4 Ordering rule

AC4's tunable default is alphabetical. **I am changing it to: grouped by role, alphabetical within a group.**
Groups appear only when non-empty, in this fixed order: **Character · World · Sound · Engine & tools.** Today only
*Character* exists and holds both entries — so with two entries the visible difference is one small dim group label,
and the rule is what matters: it is what makes a list of fifteen scannable later, and it means a new sound credit
lands in an obvious place instead of alphabetically between two character credits.

**Hard constraint on whatever ordering ships: it must be deterministic.** The manifest in §4 is a committed,
diffed artifact; a non-deterministic sort makes it churn on every regeneration and destroys the guard's signal.

---

## 4. What must be in it vs. what may be — and the anti-drift shape

### 4.1 MUST (the floor)

- The two entries in §3.2, with their captions.
- ~~The **verbatim** text of every shipped attribution file (§4.3) — the literal discharge of *"retain this attribution."*~~
  **⚠ SUPERSEDED BY §9.** *Every shipped attribution file* resolves today to all 146 lines of
  `Castaway_Attribution.txt`, which contains `Viktor.G` and `"Mini Chibi Kid"` at `:46-48` — the two
  names **AC4 bars from the panel**. The floor is now: **the marker-delimited attribution block**
  defined and quoted verbatim in §9.
- The build stamp (§3.3).

### 4.2 MAY — my calls

| Candidate | Call | Reason |
|---|---|---|
| Build stamp | **YES** | §3.3 — functional, and it is what "About" means. |
| Game title line | **NO** | The drawer header already reads `About` when the view is open (§5.1). A title inside a titled panel is redundancy, and the intro line is the better opener. |
| Unity / URP engine acknowledgement | **NOT NOW** — but build for it | Explicitly OOS on the ticket. The `Engine & tools` group in §3.4 exists precisely so this drops in later with zero restructuring. See §8 / S3. |
| Team credits (names, roles) | **NO** | Sponsor decision about public naming, not mine. → S2. |
| Tooling acknowledgements (Blender, MCP, AI tooling) | **NO** | Not owed, and naming production tooling is a brand call. → S2. |
| Version history / changelog | **NO** | The build stamp is the version surface. A changelog is a different product. |
| Logos | **NO** | Every logo is a raster asset with its own usage rules, and it would be the only non-carved-wood pixel in the UI family. Text names only. |

### 4.3 The anti-drift shape (AC2) — UX-binding properties, then a recommended route

**Two properties are UX-binding.** Devon owns the route (`AC2: "Route is yours"`); these two are not negotiable
because a violation is visible to the player as a wrong or missing credit:

- **P1 — the legally-load-bearing text is GENERATED, never typed.** The verbatim block is a deterministic
  editor-time copy of the attribution files. A human never retypes it. This is AC2's own constraint and the reason
  it exists (a hand-copy *is* the drift).
- **P2 — the warm caption layer is AUTHORED, and paired to the generated set by a guard.** The captions in §3.2
  cannot be derived from prose, so they are hand-written — and therefore every caption must be pinned to a real
  generated source **via its `MatchToken`, not via its displayed label** (see the row shape below), and every
  generated source must have a caption. Neither may exist alone.

**Recommended route (non-binding on Devon):**

- **Generated artifact** — `Assets/Resources/AttributionBundle.txt`, written by an editor pass, carrying for each
  attribution file: its repo path, then its text verbatim, between plain `=== FILE <path>` / `=== END` markers.
  **⚠ AMENDED BY §9:** "its text verbatim" means **the marker-delimited block**, not the whole file.
  Plain text, not JSON: the diff stays human-readable (a reviewer can *see* what changed), and the prose needs no
  escaping. **Precedent for exactly this generated-editor-time / loaded-at-runtime-from-Resources pattern:**
  `Resources/BuildStamp.txt` (`BootstrapProject.cs:74`, `:269-277`), read at runtime by `BootHud`.
- **Caption table** — a pure-C# static table (no `UnityEngine` types), so the guard runs in EditMode with no scene
  and no play mode. **Precedent:** `SettingsCategory.cs:20-24` states this discipline explicitly — *"Pure C# (no
  UnityEngine) so the categorization guard (AC4) is fully EditMode-testable with no scene."* Same shape here.

  **Row shape — UX-binding, because G3's correctness depends on it.** Four fields, and `Source` and `MatchToken`
  **must be separate**:

  | Field | Role | Example |
  |---|---|---|
  | `Group` | §3.4 grouping (`Character` / `World` / `Sound` / `Engine & tools`) | `Character` |
  | `Source` | **Displayed** to the player — §3.2's name line. Free-form; may add parentheticals, spacing, punctuation. | `Mixamo  (Adobe)` |
  | `MatchToken` | **Asserted by G3** as a substring of the bundled text. Never rendered. | `Mixamo` |
  | `Caption` | §3.2's warm one-liner beneath the name. | *"The castaway's skeleton, and every animation he plays."* |

  **Why they cannot be one field:** the displayed label is a *design* string and must stay free to read well
  (`Mixamo  (Adobe)`, double-space and parenthetical per §5.2); the match token is an *evidentiary* string and must
  stay exactly as the vendor is named in the attribution file (`Mixamo`). Fusing them forces one of the two to be
  wrong — either the guard fails on a correct credit, or the copy is bent to satisfy a substring test. **The panel
  renders `Source` + `Caption`; the guard reads `MatchToken` + the bundle. They never swap.**

  **`MatchToken` constraint (keeps G3 from being trivially satisfiable):** it must be the vendor / source proper
  name **as written in the attribution file**, `Trim()`-non-empty and **≥ 4 characters**. A short or generic token
  (`"a"`, `"the"`, `"free"`) would match any prose and silently disarm the over-crediting catch that is G3's whole
  purpose. Assert the constraint in the guard itself (**G3b**) so a future entry cannot weaken G3 by construction.
- **File-scan net for the guard** — match, under `Assets/`, any `.txt` whose filename matches
  `(?i)(attribution|licen[cs]e|notice|third[-_]?party)`. That is deliberately wider than today's single
  `*_Attribution.txt`, because it also catches the retired `*_License_CC-Attribution.txt` convention named at
  `Castaway_Attribution.txt:44-45` — so a future asset arriving with *either* naming convention trips the guard.

**The four assertions (all EditMode, no scene, no build, no play mode):**

| # | Assertion | Catches |
|---|---|---|
| **G1** | The set of file paths in the bundle == the set found by the scan. | **The ADD case** (AC2's named priority), plus remove and rename. |
| **G2** | For each path, bundled text == on-disk text, byte-for-byte. **⚠ AMENDED BY §9.2 — compare against the on-disk BETWEEN-MARKERS text, not the whole file.** | The EDIT case — an attribution file changed without regenerating. |
| **G3** | Every entry's **`MatchToken`** (never its displayed `Source`) occurs as a substring in at least one bundled text. | **Over-crediting (AC4).** A resurrected "Viktor.G" or "joaobaltieri" entry matches nothing and goes RED. |
| **G3b** | Every `MatchToken` is `Trim()`-non-empty and **≥ 4 chars**. | A degenerate token (`"a"`, `"the"`) that would match any prose and silently disarm G3. |
| **G4** | Every bundled file has ≥1 entry pointing at it. | **Under-crediting at the readable layer** — a new sourced asset ships its attribution file, and the panel silently says nothing about it. |

G1+G2 are file compares; G3+G3b+G4 are set/string operations. None needs Unity. G3 is the assertion I care most
about beyond the ticket's letter — it is the only one that makes the *human-readable* layer factually accountable to
the shipped files, and it is precisely the error class `86cay47zh` just spent a whole ticket cleaning up.

> **⚠ G3 asserts the token, NOT the displayed label — this is the difference between the guard working and the
> guard failing on day one.** Verified at `origin/main` @ `c8ce948`: with today's two entries, asserting the
> *displayed* label would go **RED immediately** — `Mixamo  (Adobe)` is absent from
> `Castaway_Attribution.txt` (and so is the single-spaced `Mixamo (Adobe)`); the file says *"Mixamo animation clips
> (Adobe, free account)"* (`:28`). Asserting `MatchToken` passes: `Mixamo` and `Hyper3D Rodin` are both present.
> **The `Source`/`MatchToken` split in the row shape above is therefore load-bearing, not a nicety** — and G3 must
> be written against `MatchToken` in its very first implementation, because a guard that is red on arrival gets
> quarantined instead of trusted.

---

## 5. Layout & visual spec

**No new palette, no new font, no new panel chrome** (AC1). Every colour below is an existing `Palette.uss` token,
all already sub-1.0 (verified: `Palette.uss:15-31`).

### 5.1 Structure

Inside `.settings-panel` of the **player** drawer only, as a sibling of the rows ScrollView, between header and
footer:

```
settings-panel (player drawer, unchanged plate)
├── settings-panel__header      ← title label flips "Settings" ⇄ "About"
├── settings-panel__rows        ← existing ScrollView   (display:Flex when About closed)
├── about-view                  ← NEW ScrollView        (display:None  when About closed)
│     ├── about-view__intro         two cream lines (§3.1)
│     ├── about-group               dim group label ("Character") — one per non-empty group
│     │    └── about-entry          × N
│     │          ├── about-entry__source    cream, bold
│     │          └── about-entry__covers    dim caption
│     ├── about-view__expander      "Full attribution text" toggle
│     ├── about-view__verbatim      generated block (display:None until expanded)
│     └── about-view__stamp         dim build stamp
└── settings-panel__footer      ← "Reset to defaults" ⇄ "← Back"; "About" button
```

**Header title flip.** `SetupDrawerCommon` already sets a per-drawer title (`SettingsPanel.cs:637-639`) and registers
it for text-scaling. Flipping `.text` to `About` on view-open and back to `Settings` on close is one line and is the
beat that sells "same drawer, turned over" rather than "a new window appeared."

### 5.2 Type & tokens

| Element | Token / size | Notes |
|---|---|---|
| `about-view__intro` | `--ink-cream`, 13px, `white-space: normal` | Two lines, ~10px bottom margin, then a 1px `--panel-edge` @ α0.4 hairline (the exact row-separator idiom, `SettingsPanel.uss:100-101`). |
| `about-group` | `--ink-dim`, 11px, bold, letter-spaced | One dim label per non-empty group. Deliberately quiet — it organises, it does not announce. |
| `about-entry__source` | `--ink-cream`, 14px, bold | Same weight/size as `.setting-row__label` (`:104-111`) so the About view reads at the same rhythm as the settings rows. |
| `about-entry__covers` | `--ink-dim`, 12px, `white-space: normal` | The caption. Wraps; never truncates. |
| `about-view__expander` | `--ink-dim` → `--ink-cream` on hover, 12px, borderless | **Reuses the `.settings-reset` idiom verbatim** (`:82-90`) — the drawer's established "quiet text action." |
| `about-view__verbatim` | `--ink-dim`, 11px, ~~`white-space: normal`~~, full width | Monospace-ish is *wrong* here — it would read as a console dump. Same face, smaller and dimmer. **⚠ `white-space: normal` SUPERSEDED BY §11.2** — `normal` REFLOWS the source's line breaks, and "retain this attribution" is poorly served by silently re-flowing the retained text. §11.2 gives the requirement + two routes. |
| `about-view__stamp` | `--ink-dim`, 11px | Bottom of the view. |

**Entry as two lines, not `name — caption` on one.** The panel is 470px wide (`:24`) and captions run 45–60
characters; an em-dash single line wraps mid-caption and the wrap looks like an accident. Two lines is a deliberate
rhythm, it scans faster, and it grows without ever changing shape.

**Text scaling — easy to miss, please don't.** Every text element must be registered through
`SettingsPanel.RegisterText(el, basePx)` (`:444-449`), the same call every row label, readout and badge uses.
Unregistered text is the *only* text in the panel that would ignore the `UI text scale` dial — an obvious
inconsistency the moment anyone moves that slider. No fixed-px value columns here, so `RegisterScaledWidth`
(`:455-460`) is not needed.

> **Note on visibility, so nobody reaches for a seam that isn't there:** `RegisterText` is **`private`**
> (`SettingsPanel.cs:444`). This instruction works because the About view is built **inside `SettingsPanel`** — the
> same way every other row is. It is **not** a public API. If the build ever moves to a separate class, the call
> must go with it or the registration has to be handed in explicitly; **do not make it public just to reach it
> from outside**, and do not read this line as licence to do so.

### 5.3 The footer

- **About closed:** `Reset to defaults` (left, unchanged) · `About` (right).
- **About open:** `← Back` (left, in place of Reset) · nothing right.

Reset must not be reachable while About is open — a destructive action next to static legal text is a small,
permanent wrongness.

> **⚠ "in place of" is a VISIBILITY swap, not a re-text — see §10.4.** `settings-reset` is queried **by name**
> (`SettingsPanel.cs:632`) and bound to `Registry.ResetAll()` (`:636`) inside `SetupDrawerCommon` (`:617`);
> the code-shell fallback builds the same-named button at `:1321`. Re-texting or re-binding that button to
> serve as `← Back` silently breaks reset-to-defaults (AC10). §10.4 specifies the three-button visibility
> contract that avoids it.

**USS:** give the About button the `.settings-reset` rule by widening that selector to
`.settings-reset, .settings-about { … }` (and the same for its `:hover`). One-word diff to an existing rule, zero
duplicated declarations — the "reuse an archetype class, no new CSS" discipline from
[`gameplay-ui-direction.md`](gameplay-ui-direction.md) §9.

### 5.4 Motion

**A 90ms opacity cross-fade on the appearing view. Nothing translates, nothing scales, no sound.**

The drawer did not open — its contents changed. Re-playing the 120ms slide-up (`SettingsPanel.uss:32-35`) would
say "new window," which is the wrong sentence. The plate stays exactly where it is; only what is written on it
changes. That is the card-turning-over read.

Use the existing idiom, don't invent one: `display = Flex`, then set opacity next layout via
`panel.schedule.Execute(...)` — the exact pattern `OpenDrawer` already uses (`SettingsPanel.cs:409-426`). Hide with
`display: None`, never `opacity: 0` (`unity6-mastery.md` §Critical Don'ts). Easing per `game-juice.md` §1.1 —
ease-out, never linear.

**Explicitly ruled out:** any scroll animation, any auto-scroll, any sting, any parallax, any horizon imagery. Each
would be a beat that serves itself instead of the anchor. Cut on sight.

---

## 6. Scroll & overflow — how this degrades as the list grows

Four mechanisms, in the order they take effect:

1. **The verbatim block is collapsed by default** and contributes **zero height** until expanded (`display: None`,
   not `opacity`/`height`). This is the single most important growth property: the longest content in the surface
   is opt-in, so ten attribution files do not make the readable layer longer at all.
2. **The captions layer is short by construction** — one name + one line each, ~44px per entry, matching the
   settings-row rhythm (`.setting-row { min-height: 44px }`, `:98`). Panel is `max-height: 70%` (`:25`); at 1080p
   that is ~750px, so intro + ~10 entries + stamp fit with no scroll at all. The list would have to roughly
   quintuple before the *readable* layer scrolls.
3. **When it does scroll: its own ScrollView, vertical only.** `mode = Vertical` +
   `horizontalScrollerVisibility = Hidden`, exactly as `SetupDrawerCommon` sets for both existing drawers
   (`SettingsPanel.cs:619-623`). Independent of the settings view's scroll position — that isolation is reason 3 in
   §2.2 and it is what stops growth ever disturbing the settings layout.
4. **Nothing truncates, ever.** `white-space: normal` on the intro, captions and verbatim block — a long line wraps
   to a second line rather than ellipsing. This is the panel's standing rule (`.setting-row__label:110` +
   `.setting-row { flex-wrap: wrap }`, `:97`, both added by the `86cabeqj9` soak NITs) and the reason there is
   **never a horizontal scrollbar** in this panel family. A truncated credit is a wrong credit.

**Growth stress the design already answers:** ten sources → readable layer still fits one screen (mechanism 2);
fifty → readable layer scrolls in its own view, settings untouched (3); a 2,000-word licence text arrives → it lands
in the collapsed verbatim block and costs nothing until someone deliberately opens it (1).

---

## 7. What this spec does NOT cover

Implementation; any `SettingsCatalog` / `SettingsCategory` change (barred by AC1 and correctly so); the shipped-build
capture harness itself (AC3 — Devon's, following `SettingsVerifyCapture.cs`); editing `Castaway_Attribution.txt`
(landed already, `86cay4hyz` / #356); a main menu; a third-party **package** licence audit (OOS — see S3); audio (no
cue, no bus, no dB target — this surface is silent by design, §5.4).

---

## 8. Sponsor-input items

- **S1 — the upstream terms have never been read.** `Castaway_Attribution.txt:49-52` says so itself, and I could not
  resolve it from any repo source. This surface discharges the *recorded* retain instruction; it does not establish
  that the recorded instruction is the whole obligation. **Recommend: read the Hyper3D Rodin (Creator tier) + Adobe
  Mixamo free-account terms once, write the result into the attribution file, before any public build ships.**
  `Hypothesis (unverified):` those terms may carry conditions beyond attribution — I have not checked.
- **S2 — team credits and tooling acknowledgements: in or out?** Both are *choices*, not obligations, and both are
  public-naming decisions. My default is OUT (§4.2), and the design accommodates either without restructuring.
- **S3 — should the panel also carry engine / package licences?** Every dependency is `com.unity.*` (verified
  §1), so there is no third-party package debt — but Unity's own distribution terms are a separate question this
  ticket puts OOS. The `Engine & tools` group (§3.4) exists so a "Made with Unity" line drops in with no
  restructuring if the Sponsor wants it. **Flagged for a follow-up ticket per the ticket's own instruction; not
  widened here.**
- **S4 — copy sign-off.** §3.1's two lines are the game speaking in its own voice for the first time. Worth ten
  seconds of Sponsor eyes on the words, not just on the capture frame.

**Decision draft:** *In-game attribution ships as an ABOUT VIEW inside the F1 player Settings drawer (footer
`About` button → view-swap), not as a settings row, a tab, or a new panel. The legally-load-bearing attribution text
is machine-generated from the shipped attribution files; the human-readable caption layer is authored and pinned to
that generated set by an EditMode guard that fails on add, edit, over-credit and under-credit — pinned via a
**match token held separately from the displayed label**, so the player-facing copy stays free to read well while
the guard still asserts against the vendor name exactly as the attribution file writes it. Ticket `86cay4k73`;
spec `team/uma-ux/about-credits-surface-spec.md`.*

---

# REVISION 2 — §§9–14

*Everything below was added 2026-07-31 and verified at `origin/main` @ `8ad6e24`.*

---

## 9. The EXACT text that must appear — and why "the whole file" is a defect

R1's floor (§4.1) said *"the verbatim text of every shipped attribution file."* **That is wrong, and it is wrong
in a way that ships an AC4 violation.** This section replaces it with a delimited block, quotes that block
verbatim, and pins it with a checksum so QA can diff rather than judge.

### 9.1 The defect, stated concretely

`Assets/Art/Character/Castaway/Castaway_Attribution.txt` is **146 lines** (`wc -l`, at `8ad6e24`). It is an
*engineering* document, not a player-facing one. Rendering it whole puts all of the following in front of a
player, one click deep:

| Source lines | What a player would read | Why it must not ship |
|---|---|---|
| **`:46-48`** | *"…the Sketchfab axe **(Viktor.G)** and the **"Mini Chibi Kid"** base…"* | **Decisive.** AC4's constraint is *"no Viktor.G … and no joaobaltieri."* Both assets are deleted. Rendering the whole file **resurrects both retired credits in the credits surface** — the exact mirror-image error `86cay47zh` spent a ticket retiring. **This alone disqualifies the whole-file route.** |
| `:4-6`, `:14-20` | `STATUS 2026-07-30`, `CharacterAssetGen.FbxPath (:228-231)`, `UseCastawayV4Default = true (:217)`, `activation ticket 86catvb6u` | Source paths, code line numbers, internal toggle constants and ticket IDs. Dev-voice at the player. |
| `:49-52` | *"the exact licence / terms-of-use text behind the retain instruction above is **not recorded anywhere in this repo**"* | An internal to-do. Publishing *"we have not read our licences"* inside the legal surface is actively harmful — it is the one place that sentence must not be. |
| `:54-58` | *"The Mixamo Humanoid muscle-space retarget **EXPLODED** the skinned mesh into a cone at runtime"* | An engineering post-mortem. |
| `:137-140` | *"**DO NOT** re-export this rigged FBX through Blender … whole-skeleton "helicopter" … **Known accepted defect, Sponsor-deferred**: the RIGHT hand's thumb weights sit on the index chain"* | A shipped, player-visible admission of an unfixed rig defect. |
| `:141-146` | *"**OPEN QUESTION (unverified)**: v4 has NO committed provenance README"* | Internal. |

**On feel, not just correctness:** §0's anchor is *"the maker's mark on the underside of a hand-made thing."*
A 146-line dump of status blocks, toggle constants and deferred-defect notes is not a maker's mark — it is the
workshop's job-tracking whiteboard photographed by accident. R1 got the mechanics right (generated, guarded,
collapsed) and the **content boundary** wrong.

### 9.2 The fix — marker-delimited extraction (NOT a line range)

A line range (`22-41`) would be drift by construction — the exact failure AC2 exists to stop. **Delimit in the
source file instead**, so the boundary moves with the text:

**Two literal marker lines are added to `Castaway_Attribution.txt`**, at column 0, each alone on its line:

```
--- BEGIN ATTRIBUTION (player-visible) ---
--- END ATTRIBUTION (player-visible) ---
```

- **`BEGIN` goes immediately before** the line `ATTRIBUTION — WHAT THIS BUILD OWES CREDIT FOR` (line 22 today).
- **`END` goes immediately after** the line `      was generated by a third-party service nor sourced from an asset library.` (line 41 today).
- **Nothing else in the file is touched** — no wording changes, no reordering, no deletions.

**The generator** copies the lines **strictly between** the markers, byte-for-byte: no trimming, no reflow, no
re-indenting, no marker lines. **The guard suite changes as follows:**

| # | Assertion | Status |
|---|---|---|
| **G0** *(new)* | Every file caught by §4.3's filename scan contains **exactly one** `BEGIN` marker and **exactly one** `END` marker, `BEGIN` before `END`, with **≥1 non-blank line** between them. | **New.** A new attribution file that arrives without markers goes **RED** — the ADD case at the right granularity. Also catches a marker deleted by a later edit. |
| **G1** | Bundle path-set == scan path-set. | Unchanged (§4.3). |
| **G2** | Bundled text == the on-disk **between-markers** text, byte-for-byte. | **Amended** — was "on-disk text". |
| **G3 / G3b / G4** | `MatchToken` present / ≥4 chars / every file has ≥1 entry. | Unchanged (§4.3). |
| **G5** *(new)* | No bundled text contains `Viktor.G`, `joaobaltieri`, `Mini Chibi Kid`, or `CC-Attribution`. | **New.** A *belt-and-braces* assert of AC4's named bar, at the bundle layer. If a future marker move re-admits `:43-48`, this goes RED instead of shipping. Cheap, and it is the assertion that would have caught this defect. |

### 9.3 The exact text — verbatim, as it must render today

This is the **expected generator output at `8ad6e24`**, reproduced byte-for-byte (LF endings; the file has no
CRLF — verified with `cat -A`). It is the **day-one expected value for review and QA**, *not* a second source of
truth: the file stays authoritative, and G2 is what keeps them equal.

```text
ATTRIBUTION — WHAT THIS BUILD OWES CREDIT FOR

  Retain this attribution in any distribution of the game (an in-game / about-screen credits
  entry covers it).

  Third-party, in use regardless of which hero version is live:
    - Mixamo animation clips (Adobe, free account) — every clip FBX in this folder, played on
      the selected hero mesh by CastawayAnimator.controller.
    - The Mixamo auto-rigger skeleton + skin weights (Adobe, free account) — EVERY hero
      version, v4 included, is rigged by Mixamo (mixamorig:* Standard skeleton).
  Third-party, committed here — credit retained regardless of which version is selected:
    - v1 / v2 / v3 hero meshes + their diffuse/normal maps — GENERATED 3D content
      (Hyper3D Rodin, Creator-tier web export), from openai-image A-pose concept references.
      None of these is the live hero mesh today, but all three stay in the repo as the rollback
      chain, so the credit stays. Whether an UNSELECTED FBX is included in a given built player is
      a Unity build-inclusion question this file deliberately does not assert either way.
  IN-HOUSE, no third-party credit owed:
    - the v4 hero MESH (hand-modeled in Blender in this project, driven through the Blender MCP
      tooling) and its palette texture — ticket 86catpwc4, look-dev approved 2026-07-18. Neither
      was generated by a third-party service nor sourced from an asset library.
```

**Checksum (so review is a diff, not a judgement):**

```
$ sed -n '22,41p' Assets/Art/Character/Castaway/Castaway_Attribution.txt | sha256sum
5b178b654c23acafde48f5b8d94b75f7430ad13faa50fa42006bd38443d375a8
```

I ran that command in this worktree at `8ad6e24`. If the generated block's `sha256` differs and
`Castaway_Attribution.txt` has not changed, **the extraction is wrong** — do not "fix" it by editing the
expected value here.

### 9.4 Where the boundary is, and the one wart I am NOT hiding

**Included** (`:22-41`): the heading, the retain instruction, what is owed (Mixamo rig + clips; Rodin v1/v2/v3
meshes), and what is **not** owed (the in-house v4 mesh + palette). The last part stays in deliberately — drop it
and a reader reasonably concludes the live hero is Rodin's too.

**Excluded:** `:1-21` (status / version-selection internals), `:43-48` (the no-CC paragraph — **it carries the two
barred names**), `:49-52` (the OPEN QUESTION), `:54-146` (rig notes, per-version file inventories, the deferred
defect).

**The wart, named rather than quietly accepted:** the included block still says `CastawayAnimator.controller`
(`:29`), `ticket 86catpwc4` (`:40`), and *"a Unity build-inclusion question this file deliberately does not assert
either way"* (`:37`). That is dev-voice leaking into a player surface. **I am not hand-fixing it here** — editing
the file's wording is `86cay4hyz`'s job and this ticket's OOS, and doing it inside the panel would create the
second source of truth AC2 exists to prevent. §12's framing line is what makes it read as *the primary document,
unedited* rather than as sloppiness. → **follow-up S6 (§14.3).**

### 9.5 This widens the ticket's OOS by two lines — flagging, not deciding

The ticket's OOS says *"Editing `Castaway_Attribution.txt`'s content — that is `86cay4hyz`."* §9.2 adds **two
marker lines** to that file.

**My reading:** this is not the edit the OOS bars. `86cay4hyz` was about *correcting stale claims*; it landed
(`03bca30`, PR #356). Adding two delimiters changes **no assertion, no wording, and no fact** — a `git diff` is
exactly `+2` lines. And there is no lower-cost alternative: heading-matching is a silent-breakage heuristic, and a
line range is the drift AC2 forbids.

**But it is still a diff to a file this ticket puts OOS, so it is flagged, not assumed** → **S5 (§14.3)**.
If Priya or the Sponsor prefers, the markers can land as a separate 2-line ticket ahead of implementation; this
spec is unchanged either way.

---

## 10. Entry point, keys, and dismissal

R1 fixed the *entry* (§2) but never specified **exit**. The word `Esc` does not appear in §§0–8, and R1 never
says which view the drawer shows when it is reopened. Both are specified here.

### 10.1 Entry point — F1, and it is already layout-agnostic

| Fact | Ground truth (read at `8ad6e24`) |
|---|---|
| The player drawer opens on **F1** | `Assets/Scripts/Editor/MovementCameraScene.cs:4906` — `panel.toggleKey = KeyCode.F1;` (F3 dev console at `:4907`). |
| The C# default is deliberately **not** F1 | `SettingsPanel.cs:144` — `public KeyCode toggleKey = KeyCode.None;`, so the scene-presence guard is non-tautological. |
| F1 is polled directly | `SettingsPanel.cs:330` — `if (Input.GetKeyDown(toggleKey)) SetPlayerOpen(!IsPlayerOpen);` |
| **F-keys are certified Danish-safe in-code** | `SettingsPanel.cs:138`, verbatim: *"Layout-agnostic + Danish-safe (an F-key) + verified non-clashing with WASD/Shift/Space/Tab/F7-F10 (F2 UNBOUND) ([[sponsor-danish-keyboard-layout]])"*. |

**The About view adds no key to reach it** (AC1). Entry is a mouse click on the footer `About` button, and the
cursor is already free + visible whenever a panel is open (`OrbitCamera.cs:192-196`, `:226-234`; R1 §2.1).

**Layout-agnostic constraint — binding on every key this surface ever uses.** The Sponsor uses a **Danish**
keyboard. Only keys whose physical position and `KeyCode` are layout-invariant may be used: **F1–F12, Esc, Tab,
Space, Enter, Backspace, the arrow keys, PageUp/PageDown, Home/End, Insert/Delete, Shift/Ctrl/Alt**.
**Never** any punctuation or symbol key — `/`, `\`, `;`, `'`, `[`, `]`, `-`, `=`, `` ` ``, `,`, `.` all sit
elsewhere on a Danish layout (and several require AltGr), so a US-layout binding is simply unreachable.
Precedent in this same file: `PageUp`/`PageDown` were chosen for the nudge (`SettingsPanel.cs:340-341`) because
they are *"Danish-keyboard-safe + NOT a locomotion key (WASD/arrows/Shift/Space)"* (`:335`).

### 10.2 Dismissal — two routes ship, both layout-agnostic

**Route A — the `← Back` button (primary).** Footer-left, replacing `Reset to defaults` while About is open
(R1 §5.3, wiring contract in §10.4). Returns to the Settings view; the drawer **stays open**; the header title
flips `About` → `Settings`. Mouse-only, and that is fine — it mirrors how the whole drawer is already operated.

**Route B — F1 (closes the whole drawer).** Already live: `SettingsPanel.cs:330` toggles `SetPlayerOpen`. No code
is needed to make F1 close the drawer while About is showing — but **one thing IS needed**, and it is the AC below.

> ### 10.3 ⚠ THE RESET-ON-CLOSE REQUIREMENT (the gap R1 left)
>
> **When the player drawer closes, the About view must reset to closed, so the drawer always REOPENS on the
> Settings view — never on About.**
>
> **Why this is not optional.** F1 is the *settings* key. A player who taps F1 to nudge a decay slider and lands
> on a page of licence text has been handed the wrong drawer, and it will happen every time until he finds the
> Back button. Worse, it is *sticky*: the wrong state persists across every subsequent open for the rest of the
> session. On feel, §0's read is *"you close it and get back to the island"* — a surface that greets you again
> uninvited is the opposite of a maker's mark on the underside.
>
> **The hook already exists.** `OpenDrawer`'s `if (!open)` block (`SettingsPanel.cs:391-406`) is exactly where
> per-drawer state is torn down on close — it already clears `_playerFocusedFields` + `_playerPointerOver`
> (`:393-394`) and the nudge selection `_active` (`:402-403`). The About-view flag is the **fourth member of that
> same family**, and the merged code comment at `:395-396` names the class outright: *"the third shared
> single-state of the FIX1 focus / FIX4 pointer class."* Reset it there, scoped to the player drawer
> (`isDev == false`), so closing F3 can never disturb F1's view state.
>
> **Verifiable:** open F1 → About → F1 (close) → F1 (open) → the **Settings rows** are showing and the header
> reads `Settings`. This is **AC-D3** in §13.

### 10.4 The footer-swap wiring contract (three buttons, visibility-toggled)

`settings-reset` is queried **by name** (`SettingsPanel.cs:632`) and bound to `Registry.ResetAll()` (`:636`) in
`SetupDrawerCommon` (`:617`); the code-shell fallback creates the same-named button at `:1321`. **Re-texting that
button to `← Back`, or rebinding its `clicked`, silently breaks reset-to-defaults (AC10) and its tests.**

The contract:

| Button | `name` | Lives | About CLOSED | About OPEN |
|---|---|---|---|---|
| Reset to defaults | `settings-reset` *(existing — do not rename, re-text, or re-bind)* | footer-left | `display: Flex` | `display: None` |
| `← Back` | `settings-about-back` *(new)* | footer-left, sibling of reset | `display: None` | `display: Flex` |
| `About` | `settings-about` *(new)* | footer-right | `display: Flex` | `display: None` |

Both new buttons take the existing `.settings-reset` USS rule by widening the selector (R1 §5.3):
`.settings-reset, .settings-about, .settings-about-back { … }` and the same for `:hover`
(`SettingsPanel.uss:82` / `:90`). **No new USS declarations.** All three register through `RegisterText(el, 13f)`
so they honour the `UI text scale` dial (R1 §5.2's note, and its warning that `RegisterText` is `private` and
must not be made public).

**Scope:** built into the **player** container only, mirroring `BuildCornerPicker(devContainer)`
(`SettingsPanel.cs:645`) — the dev console (F3) must never grow an About button. The shared `SettingsPanel.uxml`
is **not** edited (it is cloned by both drawers, `CloneShell` `:579`).

### 10.5 `Esc` — RECOMMENDED, but a Sponsor call. Do not implement it unasked.

`Esc` is the project's established close/cancel key: `BuildMenuUI.cs:66` and `CraftingMenuUI.cs:62` both declare
`closeKey = KeyCode.Escape`, and `CampfirePlacement.cs:83`, `CraftingTablePlacement.cs:91`, `ForgePlacement.cs:74`
all use it to cancel. **But `SettingsPanel` has no `Escape` handling at all** — I grepped the whole runtime; the
only `KeyCode.Escape` occurrences are those five files.

So making `Esc` back out of About introduces an **asymmetry**: `Esc` would dismiss the About view but still not
close the Settings drawer itself. That is a keymap / information-architecture decision on a Sponsor-facing key
map, and the dispatch is explicit that menu IA is flagged, not decided. → **S7 (§14.3).** My recommendation is in
that entry; **ship Routes A + B only** unless the Sponsor says otherwise. Neither is blocked by the flag.

> **Doc defect found while verifying this — corrected in the same PR.**
> [`gameplay-ui-direction.md`](gameplay-ui-direction.md) §2.1 and its §8 input-map table state that **`Esc`**
> opens the settings panel. **It does not, and never did in the shipped build** — `MovementCameraScene.cs:4906`
> assigns `F1`, and the panel was later SPLIT into F1-player / F3-dev by `86cah8ukr`. That table is the first
> place anyone checks for a free key, so a stale row there is a live trap. Corrected in place using that file's
> own `⚠ CORRECTED` idiom.

---

## 11. Scroll & overflow — the two cases R1's §6 does not cover

R1 §6 handles list *growth* well (collapsed verbatim block, short captions, own ScrollView, never truncate).
Two behaviours remain unspecified.

### 11.1 Expanding the verbatim block must not move what the player is reading

The `Full attribution text` expander sits near the bottom of the About view. Expanding it inserts a tall block
**above the stamp and below the expander**, which in a naive implementation yanks the scroll position.

- **On expand:** the expander row itself stays put on screen. Do not auto-scroll to the top; do not auto-scroll
  to the bottom of the newly-revealed text. If the toolkit needs a nudge, scroll **the expander** into view
  (`ScrollView.ScrollTo(expander)`) — never the block's end.
- **No animated scroll, no height animation.** R1 §5.4's rule holds: `display: None` ⇄ `Flex`, not an animated
  height. Motion here would be a beat serving itself.
- **On collapse:** if the scroll offset now exceeds the shrunken content, clamp to the new maximum rather than
  snapping to zero. The player should be roughly where he was.

### 11.2 The retained text keeps the source's line breaks

R1 §5.2 specified `white-space: normal` for `about-view__verbatim`. **That reflows the retained text into one
justified paragraph blob** — the indented `- Mixamo animation clips…` structure collapses, and the surface that
exists to *retain* an attribution silently re-typesets it.

**Requirement (binding):** the block preserves the source's own line breaks, and a source line too long for the
470px panel (`SettingsPanel.uss:24`) **wraps** — it must never produce a horizontal scrollbar (the panel family's
standing rule, `.setting-row { flex-wrap: wrap }` `:97` + `.setting-row__label { white-space: normal }` `:110`).

**Two routes; Devon's call:**

1. `white-space: pre-wrap` on `about-view__verbatim` — honours `\n`, wraps long lines. **Verify it in a BUILT
   player before relying on it:** the only `white-space` value used anywhere in this project today is `normal`
   (`SettingsPanel.uss:110` — the sole occurrence in `Assets/`), so `pre-wrap` is **unproven in this Unity
   version here**. I am not asserting it works.
2. **One `Label` per source line**, each `white-space: normal`, stacked in a column container. Verbose but
   works on any USS version and cannot regress.

Route 2 is the safe default if route 1 does not verify in the shipped-build capture.

---

## 12. Copy — the framing line above the retained text

The retained block is dev-voice (§9.4). Dropped in bare under a `Full attribution text` toggle it reads as a
leak. **One line above it turns the same text into an asset** — it announces the block as the primary document,
which is exactly what the retain instruction is honouring:

```
Kept exactly as written in the project's own files.
```

`--ink-dim`, 11px, sits between the expander and the block, visible only when expanded. Registered via
`RegisterText(el, 11f)`.

**Why these words.** *"Kept"* is the retain instruction in the player's language. *"exactly as written"* pre-empts
the reaction the wart in §9.4 would otherwise cause — the reader now understands the plain phrasing is
faithfulness, not carelessness. *"the project's own files"* is honest and ages with no maintenance. Nine words,
no product name, no date, no legal register.

**Expander label:** `Full attribution text` (R1 §5.1) — keep it. It is accurate and unglamorous, which is right.

---

## 13. Developer-verifiable acceptance criteria

Every row is a check a developer runs and reads a pass/fail from — no judgement calls. **AC-D*n*** numbering is
this spec's own; it sits **under** the ticket's AC1–AC4, which remain authoritative.

| # | Criterion | How it is verified |
|---|---|---|
| **AC-D1** | The generated bundle's block for `Castaway_Attribution.txt` equals `sed -n '22,41p' <file>` byte-for-byte at `8ad6e24`, `sha256 = 5b178b65…d375a8` (§9.3). | EditMode (G2) + a one-line `sha256sum` in the Self-Test Report. |
| **AC-D2** | The rendered surface contains **none** of `Viktor.G`, `joaobaltieri`, `Mini Chibi Kid`, `CC-Attribution`; and **none** of `CharacterAssetGen`, `UseCastawayV4Default`, `helicopter`, `OPEN QUESTION`, `STATUS 2026-`. | EditMode G5 over the bundle + a substring assert over the composed view text. |
| **AC-D3** | **Reset-on-close.** F1 → `About` → F1 → F1 shows the **Settings** rows, header `Settings`. | PlayMode: `SetPlayerOpen(true)`, open About, `SetPlayerOpen(false)`, `SetPlayerOpen(true)`, assert the About container is `DisplayStyle.None` and the title `Label.text == "Settings"`. |
| **AC-D4** | `← Back` returns to Settings with the drawer still open (`IsPlayerOpen == true`). | PlayMode, real `ClickEvent` / `Button.clicked` — not a direct field poke. |
| **AC-D5** | Reset-to-defaults still works after an About open/close cycle, and `settings-reset` is never re-texted or re-bound. | The existing AC10 reset test, re-run after an About round-trip; plus assert `Q<Button>("settings-reset").text == "Reset to defaults"`. |
| **AC-D6** | No `About` control exists in the **F3 dev** drawer. | EditMode/PlayMode: `devContainer.Q<Button>("settings-about") == null`. |
| **AC-D7** | `SettingsPanel.uxml` is unmodified by this PR. | `git diff --stat origin/main -- Assets/UI/SettingsPanel.uxml` is empty. |
| **AC-D8** | No new USS custom property, colour literal, or font is introduced; every colour resolves to a `Palette.uss` token. | Review + `grep -E "#[0-9A-Fa-f]{6}|rgba?\(" ` over the PR's USS diff returns only the widened `.settings-reset` selector. |
| **AC-D9** | **No horizontal scrollbar** in the About view at 1920×1080 or 1280×720, verbatim block expanded. | Shipped-build capture frames at both resolutions. |
| **AC-D10** | Expanding the verbatim block does not scroll-jump (§11.1). | PlayMode: record `scrollOffset`, expand, assert the expander is still within the viewport rect. |
| **AC-D11** | The retained block's line breaks match the source's (§11.2). | Assert the rendered text's newline count equals the source block's. At `8ad6e24` the block is **20 lines** (`sed -n '22,41p' … \| wc -l` = 20) → **19** `\n` separators. Assert against the source, not against the literal 19, so the check survives an edit. |
| **AC-D12** | **A `credits_*` capture frame from the BUILT exe** shows the About view populated, driven the `SettingsVerifyCapture` way (programmatic open + real `ChangeEvent`s), wired into the CI capture gate. | Ticket AC3. An editor `RenderTexture` shot does **not** satisfy it — `unity-conventions.md:197` (⚠ **the ticket cites `:182`; that ref has DRIFTED** — see §13.2). The About view is a **UI-Toolkit overlay**, so per the boundary sentence at `unity-conventions.md:9` the capture **MUST stay WINDOWED** (`-screen-fullscreen 0`, no `-batchmode`): overlays composite to the swapchain and never into a camera's `RenderTexture`. |
| **AC-D13** | **G0 goes RED on a marker-less file.** Add a throwaway `Assets/Art/_guardprobe_Attribution.txt` with no markers → the EditMode guard fails; delete it → green. | Demonstrate the RED in the Self-Test Report (`team/TESTING_BAR.md`; PR #383 is tightening this rule — *a gate is not a gate until demonstrated RED*). |
| **AC-D14** | **G1 goes RED on the ADD case** (ticket AC2's named priority): a new marker-carrying attribution file with no bundle entry fails. | Same probe-then-delete demonstration. |
| **AC-D15** | Every text element in the About view scales with the `UI text scale` dial. | PlayMode: change the scale, assert each About `Label`'s `fontSize` changed (R1 §5.2). |

### 13.1 Predict-Before-Soak

Not soak-gated (ticket AC3). If the Sponsor is shown the capture frame anyway, the falsifiable prediction is:
**he will accept placement and copy without a dial round, and any comment will be about the retained block's
dev-voice wording (§9.4's wart) rather than about the panel.** If instead he objects to *placement* — that the
About button belongs somewhere other than the F1 footer — then §2.2's five-reason argument is wrong and §14.3's
S8 is the escalation, not a tweak.

### 13.2 ⚠ Two `unity-conventions.md` line refs in the TICKET have drifted — use these

The ticket body was authored against `origin/main` @ `fee2604`; that doc has grown since. **Re-derived by
direct read at `8ad6e24`:**

| Cited in the ticket | Actually at `8ad6e24` | The rule |
|---|---|---|
| `unity-conventions.md:182` | **`:197`** | *"Judgment-grade prop/visual captures must come from the SHIPPED exe … never from an editor RenderTexture — an editor capture can show a false NEGATIVE as easily as a false positive."* (`:182` today is an unrelated bullet about recoloring a toon atlas by UV cells.) |
| `unity-conventions.md:185` | **`:200`** | *"Component-in-source-but-not-serialized-into-scene is a named failure class …"* (U7 / PR #6 `CaptureGate`) — the rule behind the ticket's AC3 scene-presence assert. |

Also worth reading before wiring the capture, and **not cited by the ticket at all**:
`unity-conventions.md:9` — the launch-mode **boundary sentence** (*"cite it verbatim, do not paraphrase it"*).
It is what makes AC-D12's WINDOWED requirement non-negotiable for this surface.

---

## 14. Out of scope, and the Sponsor flags

### 14.1 Out of scope — this SPEC (it is direction, not code)

Implementation of any kind; UXML/USS/C# authoring; the capture harness; the EditMode guard's code; scene wiring.

### 14.2 Out of scope — the TICKET (do not widen; several are already filed or flagged)

1. **Editing `Castaway_Attribution.txt`'s wording / facts** — `86cay4hyz`, landed (`03bca30`, PR #356).
   *(The two marker lines in §9.2 are the one deliberate exception; flagged as S5.)*
2. **A main menu / title screen.** None exists; the game boots straight into `Boot.unity`.
3. **A new keybind to reach About** (ticket AC1). Entry is a footer click.
4. **Any `SettingsCatalog` / `SettingsCategory` change** (ticket AC1) — credits is not a setting, and
   `SettingsCategory.PlayerIds` is asserted row-for-row by the existing categorization test.
5. **A third-party PACKAGE licence audit.** Re-verified at `8ad6e24`: every `Packages/manifest.json` dependency
   is `com.unity.*`, so there is **no third-party package debt**. Unity's own distribution terms are a separate
   question → S3 (R1 §8).
6. **Retiring the `.txt` file** in favour of the panel. It stays — it is what the guard reads.
7. **`team/DECISIONS.md`** — append-only, Priya-only. A `Decision draft:` line goes in the PR body instead.
8. **Adding `Esc` handling to `SettingsPanel`** — S7 below; not implemented unless the Sponsor asks.
9. **Team credits / tooling acknowledgements / logos / changelog** — R1 §4.2; S2.
10. **Audio.** This surface is silent by design (R1 §5.4): no cue, no bus, no dB target, no sting. Deliberate —
    a legal surface that announces itself is a beat serving itself.

### 14.3 Sponsor-input items added by Revision 2

*(S1–S4 are in R1 §8 and still open.)*

- **S5 — the two marker lines widen the ticket's stated OOS by `+2` lines in `Castaway_Attribution.txt`.**
  §9.5 gives my reading (not the edit the OOS bars: no assertion, wording or fact changes) and the alternative
  (land the markers as a separate 2-line ticket first). **Recommend: allow it inside this ticket** — a separate
  ticket for a two-line delimiter costs a full build slot. Needs a Priya/Sponsor nod either way.
- **S6 — dev-voice inside the retained block.** The included `:22-41` still says
  `CastawayAnimator.controller`, `ticket 86catpwc4`, and *"a Unity build-inclusion question this file
  deliberately does not assert either way."* §12's framing line mitigates it; a genuine fix is a wording pass on
  the source file, which is `86cay4hyz`-class work. **Recommend: a small follow-up ticket** — *"re-voice
  `Castaway_Attribution.txt:22-41` for a player audience without changing any claim."* Not blocking.
- **S7 — should `Esc` do anything here?** Three options, mine first:
  **(a) Nothing — ship Back + F1 only** *(recommended: no keymap change, no asymmetry, zero risk)*;
  **(b) `Esc` = Back from About only** (matches `BuildMenuUI`/`CraftingMenuUI`, but `Esc` then dismisses a view
  and not the drawer that contains it);
  **(c) `Esc` = close the whole player drawer** (most conventional, but it is a real keymap addition to a
  Sponsor-facing map and can collide with a future pause menu). **Menu IA is a Sponsor surface — flagged, not
  decided.**
- **S8 — placement sanity, one look.** Not a dial session (ticket AC3 is explicit this is not soak-gated) — just
  the `credits_*` capture frame in front of the Sponsor once, to confirm the About button reads right in the F1
  footer. §13.1 is the prediction it grades against.

**Decision draft (R2):** *The in-game attribution renders a MARKER-DELIMITED block of
`Castaway_Attribution.txt` — not the whole file. Rendering the whole file would put `Viktor.G` and
`"Mini Chibi Kid"` in the credits surface, which ticket `86cay4k73` AC4 explicitly bars, alongside internal
ticket IDs, source paths and a deferred-defect note. The source file gains a two-line
`--- BEGIN/END ATTRIBUTION (player-visible) ---` marker pair; the generator extracts strictly between them; new
guards G0 (markers present) and G5 (no barred name in the bundle) enforce it. The About view is dismissed by a
`← Back` button and by F1, and the player drawer MUST reopen on the Settings view — never on About.
`Esc` is deliberately unhandled pending a Sponsor IA call. Ticket `86cay4k73`; spec
`team/uma-ux/about-credits-surface-spec.md` §§9–14.*

---

## Cross-references

- Ticket **`86cay4k73`** (this) · **`86cay47zh`** + PR #352 (the CC-BY retirement that routed this out) ·
  **`86cay4hyz`** + PR #356 (the attribution-text correction — **landed**, `03bca30`) · **`86cabh907`** + PR #100
  (the axe + its CC-BY licence file deleted).
- `Assets/Art/Character/Castaway/Castaway_Attribution.txt` — the single source of the debt (`:24-25` retain
  instruction, `:27-41` what is owed, `:43-48` no-CC, `:49-52` the open question).
- `Assets/Scripts/Runtime/Settings/SettingsPanel.cs` — `:134-155` key map, `:409-426` the open/fade idiom to reuse,
  `:444-449` `RegisterText`, `:579-583` the shared shell clone (why not to touch the UXML), `:619-623` ScrollView
  setup, `:637-639` the per-drawer title, `:645-655` `BuildCornerPicker` (the per-drawer-scoped precedent),
  `:671-691` C#-built rows.
- `Assets/Scripts/Runtime/Settings/SettingsCategory.cs` — `:20-24` the pure-C#-for-EditMode-testability discipline
  the caption table should mirror; `:29-39` the `PlayerIds` allowlist AC1 bars touching.
- `Assets/UI/Palette.uss:15-31` — every token used here. `Assets/UI/SettingsPanel.uss` — `:24-35` panel + open
  transition, `:82-90` the `.settings-reset` idiom to widen, `:97-111` wrap/no-truncate rules, `:100-101` the
  hairline.
- `Assets/Scripts/Runtime/BootHud.cs:26` + `Assets/Scripts/Editor/BootstrapProject.cs:74`, `:269-277` — the build
  stamp value and the generated-into-`Resources` precedent for §4.3.
- `Assets/Scripts/Runtime/SettingsVerifyCapture.cs:8-52` — the shipped-build capture pattern AC3 extends (and the
  PR #83 false-green it exists to prevent).
- [`gameplay-ui-direction.md`](gameplay-ui-direction.md) — §0 tonal anchor (carved-from-the-same-wood), §1 the
  palette this inherits, §9 the "reuse an archetype class, no new CSS" discipline.
- `.claude/docs/art-direction.md` + `inspiration/2026-06-12_21h16_13.png`, `21h13_31.png` (looked at again for
  R2 — faceted saturated hills, soft daylight, a lot of quiet air, nothing shouting) ·
  `.claude/docs/game-juice.md` §0 amplitude, §1.1 easing · `.claude/docs/unity6-mastery.md` §9 UI Toolkit +
  §Critical Don'ts · `.claude/docs/vision-far-horizon-game-concept.md` · `.claude/docs/unity-conventions.md`
  §editor-vs-runtime.

### Added by Revision 2

- `Assets/Art/Character/Castaway/Castaway_Attribution.txt` — **`:22-41`** the marker-delimited block (§9.3,
  `sha256 5b178b65…d375a8`); **`:46-48`** the `Viktor.G` / `"Mini Chibi Kid"` names that make the whole-file
  route an AC4 violation; `:4-6`, `:14-20`, `:49-52`, `:54-58`, `:137-146` the excluded internals.
- `Assets/Scripts/Editor/MovementCameraScene.cs:4906-4907` — `toggleKey = KeyCode.F1` / `devToggleKey =
  KeyCode.F3`, the ONLY place the shipped keys are assigned.
- `Assets/Scripts/Runtime/Settings/SettingsPanel.cs` — `:138` the in-code "Danish-safe (an F-key)" certification;
  `:144` `toggleKey = KeyCode.None` default; `:330` the F1 poll; `:335` + `:340-341` the PageUp/PageDown
  Danish-safe precedent; `:391-406` the close-teardown block AC-D3 hooks (`:393-394`, `:395-396`, `:402-403`);
  `:617` `SetupDrawerCommon`; `:632` + `:636` the `settings-reset` query + `ResetAll()` bind that §10.4 protects;
  `:1321` the code-shell fallback button.
- `Assets/UI/SettingsPanel.uss:24` (470px width), `:82` + `:90` (`.settings-reset` + `:hover`), `:97` + `:110`
  (the wrap / never-h-scroll rules), `:38` / `:73` (header / footer). `Assets/UI/Palette.uss:16-31` — every token
  re-verified sub-1.0 (max channel 234).
- `Assets/Scripts/Runtime/BuildMenuUI.cs:66`, `CraftingMenuUI.cs:62`, `CampfirePlacement.cs:83`,
  `CraftingTablePlacement.cs:91`, `ForgePlacement.cs:74` — every `KeyCode.Escape` in the runtime (§10.5). None is
  in `SettingsPanel`.
- `.claude/docs/unity-conventions.md:9` (launch-mode boundary sentence — WINDOWED for a UI-Toolkit overlay),
  **`:197`** (no editor `RenderTexture` for judgement captures), **`:200`** (component-in-source-not-in-scene).
  ⚠ The ticket cites `:182` / `:185` for the latter two — **drifted; see §13.2.**
- [`gameplay-ui-direction.md`](gameplay-ui-direction.md) §2.1 + §8 — **corrected in this PR**: they claimed `Esc`
  opens the settings panel; the shipped key is `F1` (§10.5).
