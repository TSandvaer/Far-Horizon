# About / Credits Surface — UX & Visual Direction

**Ticket:** `86cay4k73` (feat(ui): in-game credits/about surface) · **Owner (impl):** Devon · **Reviewer:** Drew · **Direction:** Uma
**Status:** DIRECTION — docs only. No implementation here.
**Verified against:** `origin/main` @ `c8ce948`. Every path, line number and quoted string below was read in this worktree; none is relayed. *(Re-verified at `c8ce948` during round 2 — the `SettingsPanel.uxml` / `.uss` / `.cs`, `SettingsCategory.cs`, `BootHud.cs`, `BootstrapProject.cs` and `Castaway_Attribution.txt` refs cited below all still resolve, with no drift from the original `3992e96` pin.)*

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
- The **verbatim** text of every shipped attribution file (§4.3) — the literal discharge of *"retain this attribution."*
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
| **G2** | For each path, bundled text == on-disk text, byte-for-byte. | The EDIT case — an attribution file changed without regenerating. |
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
| `about-view__verbatim` | `--ink-dim`, 11px, `white-space: normal`, full width | Monospace-ish is *wrong* here — it would read as a console dump. Same face, smaller and dimmer. |
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
- `.claude/docs/art-direction.md` + `inspiration/2026-06-12_21h16_13.png`, `21h13_31.png` (looked at) ·
  `.claude/docs/game-juice.md` §0 amplitude, §1.1 easing · `.claude/docs/unity6-mastery.md` §9 UI Toolkit +
  §Critical Don'ts · `.claude/docs/vision-far-horizon-game-concept.md` · `.claude/docs/unity-conventions.md`
  §editor-vs-runtime.
