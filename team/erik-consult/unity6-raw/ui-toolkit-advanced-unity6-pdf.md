# UI Toolkit for advanced Unity developers (Unity 6 edition) — actionable extract

**Source:** Unity Technologies e-book, "UI Toolkit for advanced Unity developers (Unity 6 edition)", © 2025. Local file `C:/Temp/UI_Toolkit_for_advanced_Unity_developers_Unity_6_2025.pdf`, 147 internal pages (157 PDF pages). Main author Wilmer Lin; samples *UI Toolkit Sample – Dragon Crashers* and *QuizU*.
**Coverage:** Pages 1–100 and 119–147 read in full. Pages 101–118 (mid-Localization: Localization API, String/Asset Tables, Smart Strings, asset localization) skimmed via TOC only — deliberately deprioritized for a single-language desktop game; see Gaps.
**Relevance to Far Horizon:** Unity 6 / URP desktop game. UI Toolkit is the modern, recommended runtime UI system. All findings below apply to a desktop URP target unless flagged. Anything version-specific or in-preview is marked.

---

## 0. Foundations & install (pp. 9–16, 26–29)

- **Topic:** UI Toolkit is built into Unity 6 core.
  **Finding:** No separate package install for Unity 6+. The namespace/module is `UIElements` (UI Toolkit, UI Builder, and features all included). Starting a new project from any template is sufficient.
  **Why it matters:** Zero setup friction; no package-manager step for the team's bootstrap.
  **Applies:** Far Horizon on `6000.4.10f1` already has it available. (p. 9, 11)

- **Topic:** Three pillars / web analogy.
  **Finding:** UXML = structure/layout (≈ HTML/XML), USS = appearance/style (≈ CSS), C# = interaction logic (≈ JS). A UI is a *Visual Tree* of *visual elements* (the base class of every control; the "GameObject equivalent" of UI Toolkit). UXML files store hierarchy + inline styling; USS files store reusable rule-based styling.
  **Why it matters:** Enforces separation of concerns — designers/artists work UXML+USS, programmers work C#, in parallel without breaking each other.
  **Applies:** Maps to the team's role split (Uma UX on layout/style; Devon/Drew on logic). (pp. 12–14, 30–31)

- **Topic:** UI Toolkit vs legacy uGUI / IMGUI.
  **Finding:** UI Toolkit benefits: faster iteration (global style management, live authoring), rendering performance (Render Hints + dynamic atlases), better collaboration (logic/structure/style split), reusability across Editor + runtime. The previous e-book covered uGUI + UIElements (Unity 2021 LTS); this one is Unity-6-specific.
  **Why it matters:** Justifies choosing UI Toolkit over uGUI for new work.
  **Applies:** Default runtime-UI choice for HUD/menus. (p. 12)

- **Topic:** Runtime setup — UIDocument + PanelSettings.
  **Finding:** To render UI in Game view, a GameObject needs a **UIDocument** component holding a **Panel Settings** asset + a **Source Asset** (the UXML Visual Tree). UI Toolkit elements do NOT appear in the Scene view — only Game view / UI Builder preview. Add via `Add Component` or right-click Hierarchy → `UI Toolkit > UI Document` (auto-assigns default Panel Settings). **Sort Order** field controls draw order between documents sharing the same Panel Settings.
  **Why it matters:** You can have **multiple Panel Settings** assets — e.g. a dedicated one for HUD/minimap separate from menus, each with its own scaling.
  **Applies:** Give Far Horizon's HUD and menus separate Panel Settings if they need different scale modes. Create via `Assets > Create > UI Toolkit > Panel Settings Asset`. (p. 29)

- **Topic:** UI Builder workflow basics.
  **Finding:** UI Builder (`Window > UI Toolkit > UI Builder`) is the WYSIWYG editor: StyleSheets pane, Hierarchy, Library (containers/controls/templates), Viewport (gizmo editing), Code Previews (live UXML+USS), Inspector. **Save from the Viewport `File > Save`** (saves all open UXML+USS). The game can run in the Editor while you edit UI live (asterisk `*` in the Canvas header = unsaved). Clicking the icon in a Code Preview opens that file in your IDE.
  **Why it matters:** Live-edit-while-playing is a real iteration speed win — supports the project's Sponsor-interactive tuning ethos.
  **Applies:** Uma/Drew can tune UI while a build/play session runs. (pp. 24–25, 28)

- **Topic:** Canvas background + Match Game View.
  **Finding:** In UI Builder, set Canvas background to a **Color**, an **Image** (replicate mockup/reference art), or **Camera** (live gameplay behind the UI). Select the loaded UI Document in Hierarchy + check **Match Game View** to size the Viewport to the project Reference Resolution (visualization only — doesn't modify the UI files). You can also preview different themes from a dropdown.
  **Why it matters:** Judge UI styling against the actual game backdrop or a reference mockup before building.
  **Applies:** Set the Camera background to preview HUD over the actual island scene. (pp. 25–26)

---

## 1. Graphic & font asset preparation (pp. 15–24, 60–69)

- **Topic:** PPU (Pixels Per Unit) governs sprite UI size.
  **Finding:** Most UI graphics render in screen space, not world scale. PPU controls a sprite's UI size: a sprite meant for 128px-per-grid-unit resolution → set PPU to 128. Mesh Type **Tight** (default) hugs opaque pixels to reduce overdraw; tune it in the Sprite Editor under Outline.
  **Why it matters:** Wrong PPU = wrong on-screen size; loose mesh = wasted overdraw.
  **Applies:** Any icon/sprite art for HUD. (p. 16)

- **Topic:** Render Texture asset for in-UI 3D/effects (and UI-on-3D).
  **Finding:** Render textures (`Assets > Create > Rendering`) capture a camera view per frame and can be displayed inside UI Toolkit elements (mini-maps, character-preview, particle effects over buttons). The reverse — UI rendered onto a 3D model surface — is done by assigning a Render Texture to the Panel Settings + Camera, then applying it to a material. **Render textures are expensive**; profile and use sparingly. Full-screen UI without other gameplay is usually fine.
  **Why it matters:** Enables a live character/inventory preview or world minimap rendered into the HUD.
  **Applies:** Possible route for a Far Horizon minimap or item preview — but cost-gate it. (p. 17)

- **Topic:** Texture packing — Sprite Atlas vs Dynamic Atlas.
  **Finding:** Two atlasing systems. **Sprite Atlas** = Unity's editor-time atlasing tool (auto-packs assets in a folder; supports normal/mask maps, platform variants, an API). Commonly used to pack assets in the Editor, **not at runtime**. **Dynamic atlas** = UI Toolkit's automatic runtime+editor pre-pass for UI graphics NOT packed by Sprite Atlas; criteria (min/max texture size, filters) set in **Panel Settings**; preview in the Texture Atlas Viewer in the UI Toolkit Debugger. Dynamic atlas works at runtime — good for dynamically-generated UI (e.g. inventory).
  **Why it matters:** Atlasing reduces draw calls / batch breaks (see §8). Choose static atlas for fixed content, dynamic for runtime-generated.
  **Applies:** Far Horizon inventory/crafting UI → dynamic atlas; fixed HUD icons → Sprite Atlas. (pp. 21–22, 136–137)

- **Topic:** Asset-prep good practices.
  **Finding:** Author mockups at the **highest target resolution** (e.g. 4K) from the start; never upscale rasters after creation (causes blur). Use **Presets** to save importer settings and reapply per asset type; use **AssetPostProcessor** API for automated/mass import-setting changes. 2D PSD Importer allows multi-layer PSDs to import as sliced sprites and auto-refresh on save (fast placeholder iteration); deselect **Use as Rig** for UI assets (2D-skeletal-only, irrelevant to UI). For UI assets the 2D Enhancers package offers an AI upscale in the Sprite Editor.
  **Why it matters:** Avoids re-do work and keeps the import pipeline reproducible.
  **Applies:** Far Horizon's Blender/MCP asset route can pair with Presets + AssetPostProcessor for consistent UI-sprite import. (pp. 18–20, 22–23)

- **Topic:** Fonts — Font vs FontAsset; SDF; TextCore.
  **Finding:** UI Toolkit text uses **TextCore** (based on TextMesh Pro). TTF/OTF are auto-converted to **FontAssets** in the background; FontAsset is the **recommended** format (fine-tune kerning/baseline, atlas population options, fallbacks — important for stylized game fonts). Generate via `Create > Text Core > Font Asset` (SDF preferred — crisp when scaled/magnified). **SDF font rendering** generates assets that stay crisp when transformed/magnified.
  **Why it matters:** SDF fonts scale cleanly across desktop resolutions; FontAsset gives full control + fallbacks.
  **Applies:** Far Horizon's UI font should be an SDF FontAsset. (pp. 60–62)

- **Topic:** Font atlas resolution & padding (build-size lever).
  **Finding:** Padding between glyphs in the Font Texture must leave room for the SDF gradient; larger padding = smoother rendering + thick outlines. **Aim for padding ≈ 1:10 ratio with sampling point size.** ASCII-only fonts: 512×512 atlas with padding 5 suffices; large char sets need bigger or multiple atlases (drives build size up). Atlas population modes: **Static**, **Dynamic**, **Dynamic OS** (uses the OS's built-in font — saves memory; great for Global Fallbacks / emojis on platforms with system fonts).
  **Why it matters:** Atlas size directly impacts build size and memory; Latin-only desktop game can stay small.
  **Applies:** Far Horizon (English desktop) → static or dynamic ASCII atlas, modest resolution. (pp. 62, 67–68)

- **Topic:** Rich text, gradients, sprite/emoji-in-text, Text Style Sheets.
  **Finding:** Enable **Rich Text** in the element's Extra Settings to make tags (`<b>`, `<color>`, `<rotate>`, `<gradient>`, `<sprite>`, `<style>`) take effect. Gradients: create `Create > Text Core > Gradient Color` asset under `Resources/`, reference its folder in a Text Settings asset, then `<gradient="name">…</gradient>`. Sprites/emoji in text: make a Sprite Asset (`Create > Text Core > Sprite Asset`) under `Resources/`, link it in Text Settings, then `<sprite index=0>` or `<sprite name="x">`. **Text Style Sheets** (`Create > Text Core > Text Stylesheet`) bundle opening/closing rich-text tags under one `<style=name>` tag — one place to update, fewer error-prone manual tags; ideal for text-heavy apps.
  **Why it matters:** Stylized in-line text (damage numbers, dialogue, tinted keywords) without separate textures.
  **Applies:** Useful for Far Horizon survival/dialogue text if any; Text Style Sheets keep formatting DRY. (pp. 63–69)

---

## 2. Layout — Flexbox / Yoga (pp. 29–43)

- **Topic:** Layout engine = Yoga (Flexbox subset).
  **Finding:** UI Toolkit positions elements with **Yoga**, an HTML/CSS layout engine implementing a subset of Flexbox. Responsive by construction: nest parent/child boxes, children respond to parent container changes. Standard Flexbox/Yoga knowledge transfers directly.
  **Why it matters:** Web-CSS layout mental model applies; resolution-adaptive layouts come free.
  **Applies:** Desktop windowed/resolution changes handled by Flexbox without per-resolution hand-tuning. (pp. 29–30)

- **Topic:** Relative vs Absolute positioning.
  **Finding:** **Relative** (default): children follow parent's Flexbox rules (Direction Row default = left-to-right), resize/move dynamically with parent and own size rules. **Absolute**: anchors to parent container (like uGUI Canvas), ignores parent flex (Grow/Shrink/Margins) but still respects its own size rules; uses Left/Top/Right/Bottom as anchors (e.g. Right=0, Bottom=0 pins to bottom-right). Use Relative for permanent/grouped/multi-element UI; Absolute for pop-ups, decorative overlays, or elements following an in-game position (e.g. a health bar above a character).
  **Why it matters:** Absolute is how you do world-anchored floating UI (damage popups, name tags).
  **Applies:** Far Horizon floating labels/health over the castaway or resources → Absolute. (pp. 32–33)

- **Topic:** Size + Flex (Basis/Grow/Shrink) resolution.
  **Finding:** **Unity 6 default Grow = 1** for new visual elements → they take all available container space unless given fixed Width/Height. Width/Height + Min/Max Width/Height (px, %, auto, initial) bound expansion/contraction. **Basis** = default size before Grow/Shrink. Grow 1 = fill available space (0.5 = half); Grow 0 = stay at Basis; Shrink 1 = shrink to fit; Shrink 0 = overflow. **Fixed-px elements ignore Basis/Grow/Shrink.** Resolution order: compute Width/Height → check spare/overflow in parent → distribute spare to Grow elements → reduce Shrink elements on overflow → apply Min/Basis constraints → final size.
  **Why it matters:** The Unity-6 Grow=1 default is a behavior trap — empty containers expand unexpectedly.
  **Applies:** Set explicit Grow/size on Far Horizon containers to avoid surprise full-bleed expansion. (pp. 33–35)

- **Topic:** Direction, Wrap, Align, Justify.
  **Finding:** **Direction** (Column/Column-reverse/Row/Row-reverse) = main-axis order; hierarchy order = element order. **Wrap** (No-wrap/Wrap/Wrap-reverse) controls overflow to next row/column. **Align Items** (start/center/end/stretch/auto) = cross-axis alignment; Stretch respects Min/Max; **prefer explicit options over Auto** (Auto only for special cases). **Justify Content** (start/center/end/space-between/space-around) = main-axis distribution. **Align Self** lets a child override the container's align.
  **Why it matters:** Standard Flexbox alignment vocabulary — fast, predictable layouts.
  **Applies:** HUD bars, button rows, inventory grids. (pp. 36–38)

- **Topic:** Box model (Margin/Border/Padding/Content).
  **Finding:** Unity uses a CSS-box-model variant: Content Space (inner) → Padding (inside Border) → Border (colored/rounded; thickness expands **inward**) → Margin (outside Border). For Absolute-positioned elements, Margin has no effect — use Position settings instead.
  **Why it matters:** Same spacing model as web; border-thickness-grows-inward is a subtle difference from naive expectation.
  **Applies:** Spacing/rounded-corner panels in the HUD. (pp. 37–38)

- **Topic:** Measuring units + Scale Mode (Panel Settings).
  **Finding:** Four unit kinds: **Auto** (engine computes), **Percentage** (% of parent — best for multi-resolution scalability), **Pixels** (fixed; keep small elements readable), **Initial** (reset to Unity default). Panel Settings **Scale Mode**: **Constant Pixel Size** (fixed px, optional Scale Factor), **Constant Physical Size** (same physical size across DPIs via Reference DPI), **Scale With Screen Size** (resize by resolution; Screen Match Mode = width/height/blend; Reference Resolution = base size; Match value = width vs height vs mix).
  **Why it matters:** **Scale With Screen Size + Reference Resolution** is the standard way to make a desktop UI adapt across monitor resolutions/aspect ratios.
  **Applies:** Set Far Horizon Panel Settings to Scale With Screen Size with a chosen Reference Resolution (e.g. 1920×1080) + Match for the dominant axis. (pp. 39–40)

- **Topic:** Overridden properties + UXML templates.
  **Finding:** Modified (inline) properties show **bold + a white line** in the Inspector ("inline styling"); leave unmodified values at default for easy management; reset via the ⋮ menu (Unset / Unset all). **UXML-as-templates** = prefab-like reusable UXML: right-click a UXML → Create Template → drag into any Hierarchy or instantiate from code; appears in Library (Project tab). Useful for repeated items (inventory slot, list row).
  **Why it matters:** Templates are the reuse primitive for repeated UI (inventory cells, list rows).
  **Applies:** Far Horizon inventory/crafting slots → one UXML template instantiated N times. (pp. 41–42)

---

## 3. Styling — USS (pp. 42–56)

- **Topic:** USS selectors + specificity.
  **Finding:** Create USS via `Assets > Create > UI Toolkit > StyleSheet`. Without a USS, all edits embed as **inline styles** in the UXML (not reusable). Selector types (least→most specific): **C# Type** (`Button` — no prefix), **Style class** (`.smallFont` — dot prefix, added to element's Class List), **Name/ID** (`#title` — hash prefix; **note: names need NOT be unique** in UI Toolkit, unlike HTML IDs, because templates reuse names). Combinators: **Direct child** (`#title > Label`), **Child at any depth** (`.parent .child`), **Pseudo-class** (`:hover`, `:focus`, `:active`, `:inactive`, `:checked`, `:disabled` — colon prefix; modify existing selectors). **Specificity order: Inline > ID (#) > Class (.) > Type.** Tie-break = later-in-USS wins.
  **Why it matters:** Hundreds of buttons get one selector instead of per-element edits; pseudo-classes give free hover/focus feedback.
  **Applies:** All Far Horizon UI styling should live in USS selectors, not inline. (pp. 43–49)

- **Topic:** Performance caveat on broad selectors.
  **Finding:** **Avoid overly broad selectors** — especially those ending in `*` or targeting generic Unity classes like `.unity-button`. Deep child selectors can slow performance if they evaluate a large portion of the visual tree.
  **Why it matters:** Selector breadth has a runtime style-resolution cost (see §8 update mechanisms).
  **Applies:** Keep Far Horizon selectors specific/shallow. (p. 46)

- **Topic:** Extract inline → selector workflow.
  **Finding:** "Add Style Class to List" types a class name (`.` prefix) and converts ALL inline styles to a selector; or per-property ⋮ → "Extract Inlined Style to Selector / Add Class". When editing a selector, select the **Style Class in the StyleSheet pane**, NOT the element in Hierarchy (else you edit the element's inline style). Double-click a Style Class in the Inspector to deselect the element and select the selector.
  **Why it matters:** The "am I editing the selector or the element?" trap is a common source of styling confusion.
  **Applies:** Team styling discipline. (pp. 43–48)

- **Topic:** USS variables.
  **Finding:** USS variables (e.g. `--margins-size`) centralize repeated values; update once → all consumers update. **Unity 6.1** adds UI-Builder editing of variables. Types: float, color, string, asset reference (e.g. background image), dimensions (px/deg/%), enums. **Variables are selector-scoped** — usable across selectors but defined per selector; selectors can apply to many elements.
  **Why it matters:** Design-token style theming (one color/spacing source of truth).
  **Applies:** Far Horizon could define a palette/spacing token set as USS variables for the "Zone D" look. **Version-flag:** UI-Builder variable editing is 6.1+. (p. 50)

- **Topic:** USS transition animations.
  **Finding:** **Transition Animations** property (Inspector) interpolates between styles on state change (e.g. `.green-button` → `.green-button:hover`). Config: **Property** (default `all`, or specific e.g. Color/Transform), **Duration** (s or ms; must be > 0 to be visible), **Easing** (full easing-function set: Sine/Quad/Cubic/Quart/Quint/Expo/Circ/Back/Elastic/Bounce in In/Out/InOut), **Delay**, **Add Transition** (chain multiple, overlapping). Transition events: `TransitionRunEvent`, `TransitionStartEvent`, `TransitionEndEvent`, `TransitionCancelEvent` (for sequencing/looping). Pseudo-class state changes auto-trigger defined transitions — **no extra code** for hover/active/focus animations.
  **Why it matters:** Polished menu/button motion with zero scripting.
  **Applies:** Far Horizon menu/button hover polish for the warm "alive" feel. (pp. 51–53)

- **Topic:** Swapping styles in code + overriding Unity default selectors.
  **Finding:** Change styling at runtime via `visualElement.RemoveFromClassList("common")` / `AddToClassList("legendary")` (UI Element APIs). Trigger `:active`/`:inactive` pseudo-classes via the element's enabled state. Complex built-in controls (Tab view etc.) have system-predefined child styles; **override Unity defaults by double-clicking the in-use `.unity-…` selector to copy it into your USS**.
  **Why it matters:** Data-driven style swaps (item rarity, state) and customizing built-in controls.
  **Applies:** Item-rarity coloring, stateful HUD elements. (pp. 53–54)

- **Topic:** Themes (TSS — Theme Style Sheets).
  **Finding:** `Create > UI Toolkit > TSS theme file`. TSS files behave like USS but support **theme inheritance**: a new TSS inherits from another (e.g. Unity Default Runtime Theme) + adds theme-specific USS. Missing selectors fall back to the inherited theme — so you can make a theme that only changes fonts, leaving colors/padding from the base. Use cases: light/dark mode, per-character UI, seasonal/event themes. At runtime, reference the theme in **Panel Settings > Theme Style Sheet**.
  **Why it matters:** Clean mechanism for variant looks without duplicating the whole stylesheet.
  **Applies:** Far Horizon could ship a base theme + optional event/biome variants later. (pp. 54–56)

---

## 4. Naming conventions — BEM (pp. 57–59)

- **Topic:** BEM (Block-Element-Modifier) recommended for visual-element + USS names.
  **Finding:** Names are string identifiers queried in code (`root.Query<Button>("foo").First()`), so standardize them. Recommended: **BEM** — `block-name__element-name--modifier-name` (e.g. `navbar-menu__shop-button--small`). Double-underscore `__` joins block↔element, double-dash `--` adds modifier; parts use Latin/digits/dashes (kebab-case). Examples: `menu__button-home`, `navbar-menu__shop-button--large`.
  **Why it matters:** Self-descriptive names → fewer errors, clearer hierarchy as the project grows; favored over brevity.
  **Applies:** Adopt BEM for all Far Horizon UXML names + USS classes. (pp. 57–58)

- **Topic:** Naming guidance.
  **Finding:** Keep names short+clear; emphasize role/relationship (`inventory__slot--equipped` not `inventory__button--equipped`); **avoid presentational names that may change** (use `button--quit` not `button--red` — semantic over presentational); omit Type names (Button/Label) unless they add clarity; extend conventions to art assets (sprites/textures); prefix classes when reusing across projects to avoid clashes; use `AddToClassList()` in the constructor to apply USS classes at instantiation.
  **Why it matters:** Semantic naming survives restyling; consistency between code and assets.
  **Applies:** Team naming standard for UI + UI art. (p. 59)

---

## 5. Data binding (runtime) — Unity 6 NEW (pp. 70–95)

- **Topic:** Runtime data binding system (Unity 6 introduces it).
  **Finding:** **Unity 6 introduces a runtime data binding system** linking a visual-element property directly to a data source — changes propagate automatically, no manual sync/observer boilerplate. Follows **MVVM** (View ← data binding → ViewModel → Model). Core concepts: **Data source** (object holding data — any C# object: ScriptableObject, MonoBehaviour, struct, custom class), **Data source path** (the property/field to connect to), **Binding mode** (one-way/two-way). Create a `DataBinding` instance to bind. The demo uses ScriptableObjects as data sources (serialize in Inspector).
  **Why it matters:** Big reduction in UI-sync boilerplate; the "right" Unity-6 way to wire HUD to game state.
  **Applies:** Far Horizon HUD (health/hunger/inventory counts) → bind directly to game-state objects. (pp. 70–75)

- **Topic:** `CreateProperty` / `DontCreateProperty` + property bags.
  **Finding:** Binding uses **property bags** (Unity Properties module). By default Unity generates property bags via **reflection on first access** → small runtime overhead. Mark bindable members `[CreateProperty]` to generate binding code at **compile time** (no reflection, faster). Pattern: backing field `[SerializeField, DontCreateProperty] int m_Value;` + `[CreateProperty] public int Value { get => m_Value; set => m_Value = value; }`. `DontCreateProperty` excludes a serialized field from binding.
  **Why it matters:** `[CreateProperty]` is the perf-correct way to expose bindable data — avoid reflection cost.
  **Applies:** Mark Far Horizon data-source properties `[CreateProperty]`. (pp. 73, 76, 141)

- **Topic:** Setting up bindings — UI Builder, UXML, C#.
  **Finding:** **UI Builder:** select element → Inspector ⋮ → **Add Binding** → set Data Source (object/type), Data Source Path, Binding Mode. **UXML:** generates a `<Bindings><ui:DataBinding property="text" data-source-path="Health"/></Bindings>` block; hand-writing in UXML gives precise control, faster bulk edits, and cleaner version-control diffs. **C#:** `label.SetBinding("text", new DataBinding { dataSource = playerData, dataSourcePath = new PropertyPath(nameof(PlayerDataSO.Health)), bindingMode = BindingMode.ToTarget });`. Use `PropertyPath(nameof(...))` for refactor-safe paths.
  **Why it matters:** Three entry points; UXML for static/default config, C# for dynamic runtime sources.
  **Applies:** Far Horizon HUD bindings — UXML for layout-fixed, C# for runtime-assigned sources. (pp. 74–75, 82)

- **Topic:** Binding modes.
  **Finding:** **TwoWay** (default) — both directions; use for interactive inputs (sliders, text fields). **ToTarget** — source→UI only; read-only display. **ToSource** — UI→source only; input where you don't show current value initially. **ToTargetOnce** — source→UI once, no further tracking.
  **Why it matters:** Pick the cheapest mode that's correct (read-only HUD = ToTarget).
  **Applies:** Far Horizon HUD readouts = ToTarget; settings sliders = TwoWay. (pp. 77–78)

- **Topic:** Data-source inheritance.
  **Finding:** Child visual elements **automatically inherit the parent's data source** unless explicitly assigned a new one. In UI Builder a child's Data Source field is pre-filled with the parent's but can be overridden. Same in C#: set `root.dataSource`; children use it; `child.dataSource = other;` overrides for that subtree.
  **Why it matters:** Set the data source once on a container; all children bind by path only. Swapping the container's source re-targets the whole subtree.
  **Applies:** Set a HUD root's data source to the player state; child bars bind by path. Swapping data source = swap whole panel's data (e.g. character select). (pp. 76–77, 81)

- **Topic:** Conflict avoidance — UXML vs C# sources.
  **Finding:** If UXML hard-codes `data-source="PlayerDataSO.asset"`, the binding is **fixed and cannot change at runtime**. To allow runtime source changes, leave `data-source` empty or use `data-source-type`. Rule: UI Builder/UXML bindings for static/default configs; C# bindings for dynamic/runtime source changes.
  **Why it matters:** A hard-coded UXML source silently blocks runtime data swapping — a real footgun.
  **Applies:** Far Horizon character/inventory panels that re-point at runtime → don't hard-code the source in UXML. (pp. 75–76, 85)

- **Topic:** Unresolved (hybrid) bindings.
  **Finding:** Set a **Data Source Type** + path in UI Builder but leave the actual data source **unresolved** (hollow icon). At runtime resolve with one line: `myElement.dataSource = myNewDataSource;`. Resolves all placeholder bindings at once — eliminates repetitive `SetBinding` calls while keeping UXML flexible. Dragon Crashers uses this: next/last buttons set the current character as data source with no UXML change.
  **Why it matters:** Best of both — designer sets paths visually, code supplies the source once at runtime.
  **Applies:** Far Horizon list/detail panels (character/item) → unresolved bindings + single runtime assignment. (p. 84)

- **Topic:** Type converters (Unity 6).
  **Finding:** **Type converters** transform raw data into UI-friendly formats (radians→degrees, float health→Color, etc.). Two kinds: **Global converters** (`ConverterGroups.RegisterConverterGroup` — apply to any binding needing the conversion), **Per-binding converters** (granular, set in Edit Binding > Advanced > Local converters). Example: a `ConverterGroup("HealthColor")` with `AddConverter((ref float pct) => new StyleColor(...))` lerping green→yellow→orange→red, registered via `ConverterGroups.RegisterConverterGroup(...)`. Register in both Editor (`[InitializeOnLoadMethod]`) and runtime (`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`) using `#if UNITY_EDITOR`/`#else`.
  **Why it matters:** Bind a raw float to `style.backgroundColor` and let a converter map it to a gradient — no manual transform logic in the view.
  **Applies:** Far Horizon health/hunger bar color-by-value via a converter group. (pp. 86–90)

- **Topic:** Type-converter best practices.
  **Finding:** Keep converter delegates lightweight (frequent calls); keep them simple/focused; handle basic conversions in the data source itself (e.g. pre-format a percentage as a SO property) and reserve DataConverters for UI-binding-specific conversions.
  **Why it matters:** Converters run often; heavy logic = perf hit.
  **Applies:** Far Horizon converters stay trivial; pre-compute in the data SO where possible. (p. 90)

- **Topic:** ListView data binding (Unity 6).
  **Finding:** With Unity 6, a **ListView can bind directly to a data source** — no custom populate/refresh code. Setup: (1) data source = a SO holding a `List<…>`; (2) make a **UXML item template** (VisualTreeAsset) for one row, with **unresolved** bindings (Data Source Type + path, no source); (3) add a `ListView` to the main UXML, assign the template as **Item Template**. Complete at runtime: `m_ListView.dataSource = m_TeamData;` then `m_ListView.SetBinding("itemsSource", new DataBinding { dataSourcePath = new PropertyPath("Players") });`. ListView auto-populates rows and tracks add/remove/reorder — no per-row wiring.
  **Why it matters:** Inventory/leaderboard/quest-log lists become near-zero-boilerplate and auto-update.
  **Applies:** Far Horizon inventory list, crafting list, any scrollable collection. (pp. 91–93)

- **Topic:** Optimizing data binding.
  **Finding:** Default binding system **updates every frame** — fine for small UI, a bottleneck at scale. Levers: minimize unnecessary bindings/redundant updates; watch **boxing** for value types (int/float/struct) since `dataSource` is `object`; consolidate bindings that track rarely-changing data; precompute/cache heavy values; only bind elements that need frequent updates (assign others directly or on events). **Update triggers:** *Every frame* (constant, e.g. health bar), *On change detection* (when data changes or every frame if detection impossible — stats/inventory), *When marked dirty* via `MarkDirty` (infrequent — settings menus). **Change-tracking interfaces:** `IDataSourceViewHashProvider` (version-hash equality; update only when source meaningfully changes — static/semi-static data), `INotifyBindablePropertyChanged` (property-level change → refresh only affected bindings). Combine `[GeneratePropertyBag]` (whole type, compile-time bag) + `[CreateProperty]` for best perf.
  **Why it matters:** Per-frame binding can dominate CPU on a busy HUD; change-tracking interfaces cut needless refreshes.
  **Applies:** Far Horizon HUD: per-frame for health; change-detection/mark-dirty for inventory/settings. Implement `INotifyBindablePropertyChanged` + `IDataSourceViewHashProvider` on data sources that change rarely. (pp. 94–97, 141–142)

---

## 6. Custom controls — Unity 6 NEW attributes (pp. 120–129)

- **Topic:** `UxmlElement` / `UxmlAttribute` (replaces UxmlTraits/UxmlFactory).
  **Finding:** **Unity 6 simplifies custom controls** with `[UxmlElement]` + `[UxmlAttribute]` attributes — directly expose custom controls + properties to UXML and UI Builder, far less boilerplate than the old `UxmlTraits` + `UxmlFactory` classes. Create a custom control: define a `public partial class` inheriting `VisualElement` (or a subclass like `Button`), add `[UxmlElement]`. It then appears in the UI Builder Library under **Custom Controls (C#)**, draggable into the Hierarchy.
  **Why it matters:** This is the modern, low-friction way to build reusable game controls (health bars, rating stars, tab views).
  **Applies:** Far Horizon custom HUD widgets (e.g. a survival-stat bar) → `[UxmlElement] public partial class`. (pp. 120–123)

- **Topic:** Lifecycle — no Awake/OnEnable.
  **Finding:** Visual elements are NOT GameObjects → **no Awake/OnEnable/OnDisable/OnDestroy**. Initialize in the **constructor**. To delay init until added to the UI, register `AttachToPanelEvent`; detect removal via `DetachFromPanelEvent`.
  **Why it matters:** Common mistake is reaching for MonoBehaviour lifecycle on a visual element.
  **Applies:** Far Horizon custom-control init goes in ctor + AttachToPanelEvent. (p. 122)

- **Topic:** `UxmlAttribute` exposes Inspector fields.
  **Finding:** `[UxmlAttribute]` on a property makes it editable in the UI Builder Inspector (no code edits needed to retune). Optional `name:` arg renames the displayed attribute (`[UxmlAttribute(name:"my-text")]` → "My Text"). Decorator attributes work like MonoBehaviour fields: **TextArea, Tooltip, Range, Header, Min, Multiline, Space, Delayed** (e.g. `Range` adds a slider).
  **Why it matters:** Designers tune custom-control parameters in the Inspector without touching C#.
  **Applies:** Expose max-value/colors/labels on Far Horizon custom controls via UxmlAttribute. (pp. 122–123)

- **Topic:** Worked example — SlideToggle.
  **Finding:** A custom slide toggle inherits `BaseField<bool>` (pick the most suitable base class). In ctor: `AddToClassList(...)`, query the input via `this.Q<…>(className: BaseField<bool>.inputUssClassName)`, build a knob child, set element names. Event handling via `RegisterCallback<ClickEvent>` and `RegisterCallback<KeyDownEvent>` (toggle on Enter/Return/Space; guard `panel?.contextType == ContextType.Player` to ignore Editor). Use `SetValueWithoutNotify(bool)` to update visual state WITHOUT firing a ChangeEvent (called internally on value change → no infinite update loop). Consume the control from a MonoBehaviour: `root.Q<SlideToggle>("master-audio-toggle")`, then `RegisterValueChangedCallback`.
  **Why it matters:** Canonical pattern for a stateful, keyboard+mouse-accessible custom control; `SetValueWithoutNotify` is the key anti-loop idiom.
  **Applies:** Far Horizon settings toggles (mute, fps counter, gameplay options). (pp. 124–128)

- **Topic:** Custom-control ideas + USS transitions.
  **Finding:** Good custom-control candidates: health/mana/power bars (expose max/current/status-colors via UxmlAttributes for gradients), rating stars (segmented progress bar; child elements toggle filled/unfilled; expose `int` max), tab view (row of tab buttons + content area; dynamic add/remove). Most can trigger USS transitions for visual flair.
  **Why it matters:** Catalog of reusable widgets that map onto common game UI.
  **Applies:** Far Horizon survival bars, level/skill stars, tabbed inventory/crafting. (p. 129)

---

## 7. Localization (pp. 96–120, partial)

- **Topic:** Localization package + UI Toolkit integration (Unity 6).
  **Finding:** Unity 6 integrates the **Localization** package (install from Package Manager; eval'd version 1.5.3, Oct 2024) with UI Toolkit. Key class **Locale** (a language+region). Features: **String Localization** (`LocalizedString`, auto-update on Locale switch; **Smart Strings** for placeholders/plurals), **Asset localization** (swap textures/assets per Locale), **Data Binding integration** (link UI to String/Asset Tables — Locale/state changes auto-update), **String/Asset Table management** (key-value pairs), **Locale Switching** (real-time, no restart). Setup: install → `Project Settings > Localization` (create Settings asset) → create Locales (Locale Generator) → create String/Asset Tables → add key-value entries (`Window > Asset Management > Localization Tables`) → build UXML with localizable elements. Use FlexBox auto-sizing to absorb text-length differences across languages.
  **Why it matters:** If Far Horizon ever localizes, this is the integrated path; FlexBox already handles variable text length.
  **Applies:** **Low priority for a single-language English desktop game** — note but don't invest now. The "use FlexBox auto-sizing for variable text length" tip is good UI hygiene regardless. (pp. 96–100, 119)
  **Note:** Detailed Localization API, CSV/Google-Sheets sync, Smart Strings placeholders, and asset-table specifics (pp. 101–118) were NOT deep-read — see Gaps.

---

## 8. Optimizing performance (pp. 130–148) — HIGH VALUE for desktop

- **Topic:** Four update mechanisms + their cost.
  **Finding:** Visual tree update mechanisms (CPU unless noted): **Style resolution** (apply USS selectors/styles; triggered by class/style/color changes; **large/deep hierarchies make this expensive** — minimize frequent changes). **Layout recalculation** (size/position fit; triggered by size/position/alignment changes; **use transforms instead of altering positions** for animation). **Vertex buffer updates** (geometry e.g. rounded corners/borders; **resource-intensive — avoid frequent geometry changes**). **Rendering state changes** (textures/blending/masking that disrupt batching; **excessive state changes raise CPU — batch + limit unique textures/masks**).
  **Why it matters:** Tells you which UI changes are cheap vs expensive — the basis for all perf decisions.
  **Applies:** Far Horizon: animate transforms not layout; keep hierarchies shallow; limit per-frame restyling. (p. 131)

- **Topic:** Batching + Vertex Budget.
  **Finding:** UI Toolkit batches visual elements sharing the **same GPU state** (shaders, textures, mesh data) into one draw call (like GameObject draw-call batching). A different texture/state between elements **breaks the batch** = small inefficiency. Vertex buffers store geometry; a Panel pre-allocates **one** vertex buffer; exceeding capacity creates more buffers → fragments batching → more draw calls. Tune **Vertex Budget** in Panel Settings (`Buffer Management > Vertex Budget`; default 0 = auto). For complex UI, manually raising it (e.g. to 20,000) can fit the UI in **one draw call** — diagnose with the **Frame Debugger**. Beware over-allocating memory; use Frame Debugger + Profiler to find the balance.
  **Why it matters:** A single Panel-Settings number can collapse many draw calls into one for a busy HUD.
  **Applies:** If Far Horizon HUD shows multiple draw calls in Frame Debugger, raise Vertex Budget. (pp. 132–134)

- **Topic:** Uber shader + 8-texture limit.
  **Finding:** UI Toolkit uses a single versatile **"uber shader"** with **dynamic branching** (one shader path chosen at runtime — fewer shader switches, slight GPU branching cost). It supports up to **8 textures per batch** → elements with up-to-8 different textures render in ONE draw call. **Exceeding 8 textures forces the batch to split** (many draw calls). Mitigate by consolidating textures into atlases to stay ≤8 per batch.
  **Why it matters:** The 8-texture batch ceiling is the concrete number governing UI draw-call counts.
  **Applies:** Keep Far Horizon HUD per-batch texture count ≤8; atlas icons. (pp. 134–136)

- **Topic:** Dynamic texture atlases (runtime).
  **Finding:** Dynamic atlas merges multiple images into one texture (fewer state changes); configure in Panel Settings; visualize in **Dynamic Atlas Viewer** (UI Toolkit Debugger). Heavy add/remove churn can **fragment** the atlas — `ResetDynamicAtlas` API restores it. Use 2D Sprite Atlas (static/predefined content) and dynamic atlas (runtime-driven content) **side by side**.
  **Why it matters:** Runtime-generated UI (inventory) stays batched via dynamic atlas; reset if it fragments.
  **Applies:** Far Horizon runtime inventory/crafting icons → dynamic atlas; reset on heavy churn. (pp. 136–137)

- **Topic:** Masking (stencil cost).
  **Finding:** Masking uses the **stencil buffer** (GPU state) → can break batches. Nested masked elements add stencil-tracking cost per depth. Two types: **Rectangular masks** = shader-based, **preserve batching, no stencil, nest without depth limit**. **Rounded-corners/complex masks** = stencil-based, **may break batches**, **max 7 nested levels**. Optimize: prefer rectangular masks; minimize nesting depth (flat hierarchy); one mask over a parent vs many over children; if multiple layers unavoidable, apply the **Mask Container** usage hint sparingly.
  **Why it matters:** Rounded-corner clipping is more expensive than rectangular — design choice with a real cost.
  **Applies:** Far Horizon scroll/clip regions → rectangular masks where possible. (p. 138)

- **Topic:** Animations & transitions — transform over layout.
  **Finding:** Changing layout props (`width`/`height`/`top`/`left`) triggers expensive **layout recalculations**. Prefer **transform-based** animation (`translate`/`scale`/`rotate`) — processed on the **GPU**, no layout recalc → smoother. Enable usage hints: **`DynamicTransform`** (GPU-handled position/transform updates, bypasses vertex recalc), **`GroupTransform`** (one transform on a parent, GPU propagates to all children — big win for many animated children). Avoid **class switching** for style changes in large hierarchies during animation (triggers extensive style recalc); update inline properties directly instead. Verify with Frame Debugger.
  **Why it matters:** The single most impactful animation rule — animate transforms, not layout; set the right usage hint.
  **Applies:** Far Horizon UI motion (panels sliding, pulsing bars) → translate/scale + DynamicTransform/GroupTransform. (pp. 139–140)

- **Topic:** Showing/hiding elements — cost table.
  **Finding:** Methods + trade-offs (Bindings/Layout update, Render/Style/Change cost, Styles/Meshes memory):
  - **`opacity: 0`** — still updates bindings+layout, High render, Full meshes memory. (transition-friendly, no savings)
  - **Off-screen** — bindings+layout update, Medium render, Full meshes.
  - **`visible = false`** (Visibility: Hidden) — bindings+layout update, Medium render, **Stencil** meshes memory; prevents rendering but stays in layout.
  - **`style.display = DisplayStyle.None`** — bindings update, **no layout, no render, no style eval**, Full meshes; **most efficient for hiding**, but re-show costs a layout recalc.
  - **Hierarchy removal (`RemoveFromHierarchy`)** — **no bindings, no layout, no render**, **no meshes memory**; highest re-add spike (full rebuild).
  **Rule:** choose by toggle frequency. Frequently toggled → display:none. Rarely shown (dialogs/settings) → remove from hierarchy.
  **Why it matters:** `opacity:0` is the worst hide for perf (everything still runs); `display:none` or hierarchy removal are the cheap ones.
  **Applies:** Far Horizon: HUD toggles → display:none; modal dialogs/settings → RemoveFromHierarchy. (p. 143)

- **Topic:** Overdraw.
  **Finding:** UI Toolkit renders with transparency → overlapping elements cause overdraw (each pixel processed multiple times); the uber shader adds per-layer complexity. Mitigate: use `style.display = DisplayStyle.None` (not `opacity = 0`) to hide; don't stack — remove/hide fully obscured elements; use ListView **virtualization** for scrollable content (renders only visible rows); set `style.overflow = Overflow.Hidden` to clip outside-bounds rendering.
  **Why it matters:** Stacked transparent layers silently tank fill-rate.
  **Applies:** Far Horizon full-screen panels over the world → hide the world UI underneath; virtualize long lists. (pp. 143–144)

- **Topic:** Memory management.
  **Finding:** USS/UXML reference fonts/textures/assets directly; loading the file pulls ALL referenced assets into memory (consumed even when unused). Strategies: **Asset Bundles / Addressables** to load only per-scene UI; **unload when unused** (`RemoveFromHierarchy` + `Addressables.Release` / `AssetBundle.Unload(true)`); **selective loading** — split large UXML/USS into smaller modular templates (VisualTreeAssets) loaded on demand.
  **Why it matters:** Big monolithic UXML/USS = large always-resident memory footprint.
  **Applies:** Far Horizon — modularize UI docs; consider Addressables if UI asset memory grows. (pp. 144–145)

- **Topic:** Profiling tools.
  **Finding:** **Unity Profiler**, **UI Toolkit Debugger**, **Frame Debugger** for draw calls/batches/layout/style/vertex costs. **`SetPanelChangeReceiver`** (Panel Settings) lets you log every UI change + its source (Editor/dev builds only) via `IDebugPanelChangeReceiver.OnVisualElementChange(VisualElement, VersionChangeType)` — isolates what's changing and causing slowdowns.
  **Why it matters:** Concrete instrumentation to find the actual UI hot spot — aligns with the project's "build an instrument" ethos.
  **Applies:** Far Horizon UI perf diagnosis → Frame Debugger first; SetPanelChangeReceiver to trace mystery updates. (pp. 145–146)

- **Topic:** Unity 6 UI Toolkit perf enhancements (out-of-the-box).
  **Finding:** Unity 6 brings: **simplified event dispatching** (~2× faster), **mesh generation enhancements** (jobified classic-element geometry, native vector API, **parallelized text generation**), **Custom Geometry API** (new public API to generate custom geometry at full perf), **deep-hierarchy layout** (improved layout-computation caching → smoother deep hierarchies), **optimized TreeView for large datasets** (new high-perf Entities backend).
  **Why it matters:** Several wins are free on Unity 6 — text gen, event dispatch, deep layouts all faster than prior versions.
  **Applies:** Far Horizon benefits automatically; TreeView is viable for large data on Unity 6. (p. 146)

---

## Cross-cutting takeaways for Far Horizon (Unity 6 / URP desktop)

1. **Panel Settings = Scale With Screen Size** + Reference Resolution + Match (dominant axis) for resolution-adaptive desktop UI. Consider separate Panel Settings for HUD vs menus. (pp. 39–40, 29)
2. **All styling in USS selectors**, BEM names, keep selectors specific/shallow; use USS variables as design tokens; USS transitions for free hover/active polish. (pp. 43–59)
3. **Runtime data binding (Unity 6)** with `[CreateProperty]` data sources; ToTarget for read-only HUD, TwoWay for inputs; unresolved bindings + one runtime `dataSource =` for swappable panels; ListView binds directly to lists. (pp. 70–95)
4. **Custom controls via `[UxmlElement]`/`[UxmlAttribute]`** (init in ctor, no MonoBehaviour lifecycle; `SetValueWithoutNotify` to avoid loops) for survival bars/toggles/tabs. (pp. 120–129)
5. **Perf rules:** animate transforms not layout (+ DynamicTransform/GroupTransform hints); hide with `display:none` or RemoveFromHierarchy (never `opacity:0`); ≤8 textures/batch via atlases; raise Vertex Budget to collapse draw calls; rectangular over rounded masks; virtualize lists; profile with Frame Debugger + SetPanelChangeReceiver. (pp. 130–146)
6. **Localization is integrated but low priority** for an English desktop game; FlexBox auto-sizing already handles variable text length. (pp. 96–120)
