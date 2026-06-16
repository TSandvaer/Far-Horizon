# Unity manual (PDF) — deep-read extract

> **SOURCE key:** `unity-manual-pdf`
> **Title (actual):** *Unity Game Development Essentials* — Will Goldstone, **Packt Publishing**.
> **The "loc" filename ("Unity3D Manual.pdf") is a misnomer** — this is NOT the official Unity Manual. It is a Packt beginner tutorial book.
> **Published:** First published **October 2009** (copyright page, p.[ii]). ISBN 978-1-847198-18-1, Production Reference 1250909.
> **Target Unity version:** **Unity 2.5 era** (preface, p.3: "With 2009 seeing the release of Unity version 2.5, and its first steps onto PC format"). The book predates Unity 3 (2010) entirely.
> **Retrieved:** Full PDF, 5.2 MB, ~290 pages. Read in batches: preface + Ch1 (3D/Unity concepts), Ch3 (scripting), Ch10 (build/quality/input), plus the complete table of contents (Ch2 environments/terrain, Ch4 interactions, Ch5 prefabs/HUD, Ch6 instantiation/rigidbodies, Ch7 particles, Ch8 menus, Ch9 finishing touches, Ch11 testing).

> ## ⚠️ AGE & RELEVANCE WARNING (read first)
>
> This source is **~16 years old (2009) and targets Unity 2.5**. For a **Unity 6 / 6.4 (6000.4.x)** project it is **largely obsolete on specifics** and should be treated as a *conceptual history* source only. Far Horizon is on Unity 6 + URP; almost every workflow/API detail below has a modern replacement. **What endures** is a thin layer of genuinely timeless game-3D fundamentals (coordinate spaces, the GameObject/Component model, the Update/FixedUpdate/Time.deltaTime contract, prefabs, colliders, power-of-2 textures). **What is dead** (do NOT apply to Far Horizon):
> - **UnityScript / "Unity's JavaScript" and Boo** — the book's entire code is in UnityScript. **UnityScript was deprecated and removed (~Unity 2017.2); Boo removed even earlier.** Far Horizon is **C#** only. Translate every `var x : float`, `function Update()`, `@script RequireComponent`, `static var` example to C# mentally; the *concepts* (Update timing, GetComponent, RequireComponent) survive, the *syntax* does not.
> - **Unitron / UniSciTE script editors** — gone; modern Unity uses VS / VS Code / Rider.
> - **Web Player / `.unity3d` / `.unityweb` plugin builds, Web Player Streamed** — Unity Web Player is dead (browsers dropped NPAPI ~2015; Unity removed it). Successor was WebGL; **Far Horizon is Windows-desktop only, no web target anyway.**
> - **OS X Dashboard Widget, PowerPC / Universal Binary** builds — all dead platforms.
> - **Legacy particle system** (`Particle Emitter` + `Particle Animator` + `Particle Renderer` components, Ch7) — replaced by the **Shuriken** particle system (Unity 3.5) and now the **VFX Graph** (HDRP/URP, GPU). Do not use the component triad described.
> - **Legacy Animation component** (the `Animation`/`Animations[]` array shown in the Inspector screenshot, Ch1) — superseded by **Mecanim / the Animator + Animation Controller** (Unity 4). Far Horizon's castaway uses Mixamo→Humanoid (Mecanim), per `.claude/docs/character-pipeline.md`.
> - **`OnGUI()` / `GUILayout` / `GUI` class / GUI Texture / GUI Text** immediate-mode UI (Ch5 HUD, Ch8 menus) — legacy IMGUI, kept only for editor tooling. Runtime UI is **uGUI (UnityEngine.UI Canvas)** or **UI Toolkit**. Do not build runtime HUD/menus with `OnGUI`.
> - **`Indie vs Pro` licensing, "Powered by Unity" splash, Pro-only dynamic shadows** — licensing model is completely different now; dynamic shadows are not Pro-gated.
> - **Quality Settings as fixed presets "Fastest…Fantastic"** — the *names* and exact layout differ in Unity 6; the *axes* (pixel light count, shadow distance/resolution/cascades, anisotropic, AA, soft vegetation, VSync) still exist conceptually but live under URP Asset / Quality settings now.
>
> **Bottom line for Erik's consult:** mine this for *vocabulary and first-principles framing* a non-programmer could use; cite nothing here as a current Unity-6 API or workflow without re-verifying against Unity 6 docs.

---

## Topic: 3D coordinate fundamentals (ENDURING)

- **Cartesian X/Y/Z, written (X, Y, Z):** Z is depth, X horizontal, Y vertical. Any comma-separated triple in Unity is X,Y,Z order. (Ch1 "Coordinates", p.9.)
  - *Why it matters:* baseline literacy for reading Transform/position/rotation values.
  - *Far Horizon application:* still exactly true in Unity 6; relevant to the world-placement / nudge-tool work (radial island heightmap, axe-in-hand transform tuning).
- **Local (object) space vs World space:** every 3D world has an origin (0,0,0); world positions are relative to world-zero. **Local/Object space** gives each object its own zero point (its center, from which its axis handles emerge). **Parent-child relationships** make a child's positions relative to the parent — the parent's position becomes the child's new zero. (Ch1 "Local space versus World space", p.10.)
  - *Why it matters:* parenting + local-space math is foundational and unchanged in Unity 6.
  - *Far Horizon application:* directly relevant to attaching the axe to the chibi's hand bone (the hand is the parent; the axe's local transform is what you tune). The book's own `transform.TransformDirection(...)` example (Ch3) is the local→world conversion in action.
- **Vectors:** lines with direction + length, described in Cartesian coords; usable to compute distances, relative angles, and directions between objects. (Ch1 "Vectors", p.10.)
  - *Why it matters:* `Vector3` is still the core math type; concept is timeless.
- **Cameras:** the viewport (pyramid-shaped frustum / field of vision = FOV). Can be placed anywhere, animated, or attached to objects. Multiple cameras per scene are supported; cameras can be assigned to render objects on particular **layers** for optimization, and effects (lighting, motion blur, lens flare) are applied at the camera. (Ch1 "Cameras", p.10–11.)
  - *Why it matters:* multi-camera + layer-based render culling is still a real Unity-6 optimization technique.
  - *Far Horizon application:* mouse-orbit camera + zoom (Sponsor-locked feel); camera FOV/clip-plane and fog tuning is part of the "Zone D" look.

## Topic: Meshes, polygons, polygon count (ENDURING with caveats)

- **Polygons → triangles → edges → vertices:** Unity converts all imported polygons to **polygon triangles** (faces); three connected edges meet at points/vertices. Many linked polygons form a **mesh**. Mesh vertex data is reused for collisions (Mesh Colliders) and can encode navigation data. (Ch1 "Polygons, edges, vertices, and meshes", p.11.)
- **Polygon count is a performance lever:** higher poly count = more render work. The book notes the historical trend toward higher detail (Quake 1996 → Gears of War 2006). (Ch1, p.11.)
  - *Why it matters:* tris/verts budget is still THE core mesh-perf metric in Unity 6.
  - *Far Horizon application:* directly supports the **low-poly smooth-shaded** art direction — low triangle counts are a feature, not just an optimization. Note `.claude/docs/unity-conventions.md` covers low-poly mesh/normals patterns; this source only states the principle, not the Unity-6 specifics.

## Topic: Materials, textures, shaders (ENDURING principle; shader specifics dated)

- **Material = visual appearance; Texture = image(s) inside a material; Shader = the rendering-style script.** A material picks a shader from a built-in library; you can write your own or use community ones. (Ch1 "Materials, textures, and shaders", p.12.)
- **Textures should be square and power-of-2** (128, 256, 512, 1024…) so the engine can tile/compress them efficiently; larger textures cost more — use the smallest power-of-2 that holds quality. (Ch1, p.12.)
  - *Why it matters:* **power-of-2 + square textures still benefit GPU mip-mapping/compression in Unity 6** — this rule endures.
  - *⚠️ Dated:* the "select a shader from a large built-in library" framing predates URP/Shader Graph. Far Horizon uses **URP Shader Graph** (per `in-house-asset-routes-over-paid-tools` memory). The built-in legacy shaders the book implies are not the URP path.

## Topic: Physics — Rigidbody & colliders (ENDURING concepts)

- **Physics engine:** the book's Unity used **Nvidia PhysX**. (Ch1 "Rigid Body physics", p.12.) — *Still PhysX-family in Unity 6 (now PhysX 4.x / optional alternatives), so directionally accurate.*
- **Not everything is physics-driven:** physics is expensive and often unnecessary (e.g. a driving game: cars have Rigidbodies, the track/trees don't). Give a **Rigidbody** only to objects that should be under physics control. (Ch1, p.13.)
  - *Why it matters:* still the right mental model — Rigidbody only where needed.
- **Rigidbody properties:** Mass, Gravity, Velocity, Friction. (Ch1, p.13.) — *Still the core Rigidbody concepts.*
- **Colliders = invisible net around an object** that reports collisions. Choice of collider shape is a perf trade-off: **primitive colliders (sphere/capsule/box) are cheap; Mesh Colliders are accurate but "expensive."** Example: bowling — sphere collider for the ball, capsule for the pins (cheap) vs mesh collider (accurate, costly). (Ch1 "Collision detection", p.12–13.)
  - *Why it matters:* **primitive-vs-mesh-collider cost trade-off is identical in Unity 6** and is a real perf decision.
  - *Far Horizon application:* prefer primitive colliders for the survival-loop props (axe, campfire, trees) over mesh colliders unless precision is required.
- **Physic Material** (e.g. a "Bouncy" preset on a collider) controls how a Rigidbody reacts to surfaces (bounciness/friction). (Ch1 "The Unity way", p.14.) — *Still exists (now "Physics Material"); concept endures.*
- **Ray casting:** an invisible (non-rendered) vector line between two points used to detect intersection with a collider; returns hit point, distance (ray length), etc. Can be cast forward from a character with a set length or until it hits an object. Used to **pre-empt collisions** and avoid the "frame miss" (fast objects tunneling through thin colliders between frames). (Ch4 intro + "Ray casting", p.101–102, 99–100.)
  - *Why it matters:* `Physics.Raycast` is still core (interaction, click-to-move ground picking, line-of-sight). The "frame miss" / tunneling problem is still real and still solved by raycasting / continuous collision detection.
  - *Far Horizon application:* PoE-style **click-to-move** typically raycasts from the camera through the cursor to the ground — this concept is directly relevant (though the modern API is C# `Physics.Raycast(ray, out hit)`).

## Topic: The Unity object model — GameObject / Component / Transform (ENDURING, central)

- **GameObject (GO) + Component composition model:** break a game into manageable GameObjects; build behavior by **attaching Components**. Components expose **variables** (settings) you tune. This composition approach scales from trivial to complex. (Ch1 "Essential Unity concepts" + "Components", p.14–15.)
  - *Why it matters:* **this is still the absolute core of Unity in 2026** — GameObject + Component is unchanged (and is the conceptual ancestor of DOTS/ECS, though the book predates ECS).
- **Every GameObject has at least a Transform component** = position, rotation, scale (each X,Y,Z; scale is "dimensional"). The Transform is addressable in scripts to set position/rotation/scale. (Ch1 "Game Objects", p.15.) — *Unchanged in Unity 6.*
- **"The Unity way" worked example (bouncing ball):** create sphere (gets a mesh + **Renderer** component to be visible) → add **Rigidbody** (mass/gravity/forces) → set a **Bouncy Physic Material** on the collider → it bounces. Demonstrates the additive-component philosophy. (Ch1, p.14.)
  - *Far Horizon application:* the additive-component build pattern is exactly how you'd assemble the castaway, the axe, the campfire — concept fully current.

## Topic: Project structure & asset/scene concepts (ENDURING)

- **Assets** = all input files (images, 3D models, sounds, scripts). Everything lives under a child folder literally named **`Assets`**. Scripts are themselves treated as assets. (Ch1 "Assets", p.15.)
  - *Why it matters:* **`Assets/` folder is still the project root for content in Unity 6.** Far Horizon's CLAUDE.md notes empty dirs carry `.meta` files to survive commits — that `.meta` discipline is the modern continuation of this asset model.
- **Scenes** = individual levels / areas / menus. Splitting a game across scenes distributes load times and lets you test parts independently. (Ch1 "Scenes", p.15.) — *Still true; Far Horizon uses `Boot.unity`.*
- **Prefabs** = reusable templates: a complex GameObject (with components + current config) stored as an asset, then "spawned/cloned" at runtime; each instance is individually modifiable. (Ch1 "Prefabs", p.16.)
  - *Why it matters:* **Prefabs are still fundamental in Unity 6** (with nested prefabs + prefab variants added later — the book predates those). The "template you instantiate many of" framing is exactly right.
  - *Far Horizon application:* trees, props, scattered survival items → prefabs; the book's Ch5 (battery prefab) and Ch6 (coconut prefab) workflows are conceptually how you'd author Far Horizon props, minus the dead UnityScript/legacy-particle specifics.

## Topic: The Editor interface & scene-navigation hotkeys (MOSTLY ENDURING)

- **Five core windows:** Scene (1, where you build, perspective+orthographic), Hierarchy (2, GameObjects in current scene), Inspector (3, context-sensitive properties of selection), Game (4, play-mode preview), Project (5, asset library). (Ch1 "The interface", p.17.)
  - *Why it matters:* this five-panel layout is **still the default Unity 6 editor layout** (with additions). Genuinely enduring.
- **Transform-tool hotkeys: Q (Hand/pan), W (Translate/move), E (Rotate), R (Scale).** Alt+Hand orbits; Ctrl(PC)/Cmd(Mac)+Hand zooms; Shift speeds both. **F = focus Scene view on the selected object.** (Ch1 "The Scene window and Hierarchy", p.18.)
  - *Why it matters:* **Q/W/E/R and F are unchanged in Unity 6** (Unity later added T for Rect and Y for Transform). These hotkeys are worth knowing for any hands-on scene work / nudge-tool iteration.
- **Inspector:** context-sensitive; shows all components of a selection; each component has a checkbox to temporarily disable it; the top checkbox disables the whole GameObject; the gear/Cog menu → **Reset** reverts component values. (Ch1 "The Inspector", p.18–19.)
  - *Why it matters:* all of this is unchanged in Unity 6. The per-component enable checkbox and Reset are still there.

## Topic: Scripting fundamentals (CONCEPTS ENDURE; ALL SYNTAX IS DEAD UnityScript)

> ⚠️ Every code sample in the book is **UnityScript ("Unity's JavaScript")** — removed from Unity ~2017. Far Horizon is **C#**. The concepts below survive; rewrite syntax to C#.

- **Scripts become Components when attached to a GameObject;** Unity provides a built-in `Behavior`/MonoBehaviour-style base ("a set of scripting instructions you call upon"). A script can reference other objects by name or tag, not just the one it's attached to. (Ch3 "Scripting basics", p.81.)
  - *C# equivalent:* class derives from `MonoBehaviour`; the "by name or tag" idea = `GameObject.Find` / `FindWithTag` (modern guidance prefers serialized references, but the concept stands).
- **Variables, data types:** common types named — `string`, `int`, `float`, `boolean` (bool), `Vector3` (a set of XYZ). Explicitly typing a variable is more efficient than letting the engine infer. (Ch3 "Variables"/"Variable data types", p.82.) — *All these types exist in C#; explicit typing is mandatory in C# anyway.*
- **Public vs private member variables → Inspector exposure:** variables declared outside a function (public member variables) **automatically appear as editable fields in the Inspector**; `private` hides them. Inspector-edited values **override** the script's default at runtime (don't rewrite the script); Cog → Reset restores script defaults. (Ch3 "Public versus private", p.83.)
  - *Why it matters:* **THIS IS STILL TRUE AND CENTRAL in Unity 6** — `public` fields (or `[SerializeField] private`) show in the Inspector; Inspector values override code defaults. This is the basis of the Sponsor-tunable "nudge/slider" instruments the project favors (`sponsor-prefers-direct-tweak-tools-for-fiddly-placement` memory).
  - *Far Horizon application:* expose axe-position / world-gen params as serialized fields so the Sponsor dials them in the Inspector and you bake the values — the exact pattern this section describes.
- **Functions / event functions:**
  - **`Update()`** is called **once per rendered frame** → use for per-frame logic & input polling. (Ch3 "Update()", p.84.)
  - **`FixedUpdate()`** is called on a **fixed timestep, in sync with the physics engine** → use it for Rigidbody/physics/gravity logic, because `Update()` frequency varies with frame rate. (Ch3, p.85 + Ch3 FPSWalker deconstruction, p.92.)
  - **`OnMouseDown()`** fires when the mouse clicks the object (or a GUI element). (Ch3, p.85.)
  - *Why it matters:* **The Update vs FixedUpdate distinction is one of the most enduring and most-violated Unity rules** — still 100% correct in Unity 6: physics in FixedUpdate, input/visual in Update.
- **`Time.deltaTime`:** multiplying a value inside Update/FixedUpdate by `Time.deltaTime` converts a per-frame effect into a **per-second** effect (frame-rate independence). Example: `moveDirection.y -= gravity * Time.deltaTime;` and `controller.Move(moveDirection * Time.deltaTime)` → "meters per second, not meters per frame." (Ch3, p.94–95.)
  - *Why it matters:* **`Time.deltaTime` for frame-rate-independent movement is still essential and correct in Unity 6.** (Modern nuance: use `Time.fixedDeltaTime` semantics inside FixedUpdate; the book's point stands.)
- **Operators & control flow (language-agnostic, carry to C#):** `=` assigns vs `==` "comparative equals" compares; `&&` = AND, `||` = OR; `++`/`--` increment/decrement; `+=`/`-=`/`*=` compound assignment; `//` comments; if / else if / else. (Ch3 "If else statements"/"Multiple conditions", p.86–87.) — *Identical in C#.*
- **`GetComponent(...)`:** fetch a component attached to the same object, e.g. `GetComponent(CharacterController)` to drive a `CharacterController.Move(...)`. (Ch3 "Moving the character", p.95.)
  - *Why it matters:* `GetComponent<T>()` is still the primary way to reach sibling components in Unity 6 (the generic `<T>` form replaced the typeof-arg form shown). Modern guidance: cache the result (don't call every frame).
- **`Input.GetAxis("Horizontal"/"Vertical")`** returns a smoothed −1..1 value (A/D or ←/→ = horizontal; W/S or ↑/↓ = vertical), idling back toward 0; **`Input.GetButton("Jump")`** (Space by default). The **Input Manager** (Edit ▸ Project Settings ▸ Input) defines named axes/buttons with Name/Positive/Alt buttons, Gravity, Dead, Sensitivity, Snap, Invert, Type, Axis, Joy Num. (Ch3 p.93 + Ch10 "Player Input settings", p.273–274.)
  - *⚠️ Dated:* this is the **legacy Input Manager** (`UnityEngine.Input`). Unity 6 strongly favors the **new Input System package** (action maps, devices). The book's named-axis concept survives in legacy mode but is not the recommended path for new projects.
- **`transform.position` / dot syntax:** address a component hierarchically, e.g. `transform.position.y = 50;`. `transform.TransformDirection(v)` converts local XYZ to world XYZ. (Ch3 "Dot syntax" p.88 + p.94.) — *Concept & `TransformDirection` unchanged in C#.*
- **`static` globals & cross-script access:** `static var speed` makes a value accessible as `ScriptName.speed = 15;` from any script (script name = class name in UnityScript). (Ch3 "Using static to define globals", p.88.)
  - *⚠️ Caution:* technically still possible in C# (`static` fields), but **global mutable static state is an anti-pattern** now — modern Unity prefers serialized references, events, or ScriptableObjects. Treat this section as "what NOT to lean on."
- **`@script RequireComponent(CharacterController)`** auto-adds a required component to the host GameObject. (Ch3 "@Script commands", p.97.)
  - *C# equivalent:* the **`[RequireComponent(typeof(CharacterController))]` attribute** — alive and recommended in Unity 6. (The `@script` syntax is dead; the feature is not.)
- **CharacterController & CollisionFlags:** a `CharacterController` collider exposes `CollisionFlags` (None/Sides/Above/Below); `grounded = (flags & CollisionFlags.CollidedBelow) != 0;` is a bitmask ground check after `controller.Move(...)`. (Ch3 "Checking grounded", p.96.)
  - *Why it matters:* `CharacterController` + `CollisionFlags` bitmask grounding still works identically in Unity 6 — a valid (non-Rigidbody) movement approach. Relevant if Far Horizon's castaway uses CharacterController-based click-to-move rather than NavMesh/Rigidbody.

## Topic: Terrain & environment authoring (CONCEPTS ENDURE; tool UI dated)

- **Terrain editor** (Ch2) covers: import/export **heightmaps**, set heightmap resolution, **lightmap** creation, **mass-place trees**, flatten heightmap, and a toolset — Raise/Paint/Smooth Height, Paint Texture, Place Trees, Paint Details, Terrain Settings. (TOC Ch2, p.26–35.)
  - *Why it matters:* Unity 6 **still has a Terrain system with heightmaps, tree/detail painting, and texture splatting** — the *concepts* (heightmap-driven terrain, painted trees/details) endure, though the UI and the underlying tooling have been heavily revised.
  - *Far Horizon application:* the **big round procedural island** (radial heightmap, water on all sides, dense jungle — `world-is-big-round-island` memory) is conceptually a heightmap + tree/detail-paint problem. *But note:* the book teaches manual painting; Far Horizon's direction is **procedural generation + URP Shader Graph** (in-house asset route), so use this only as conceptual grounding, not workflow.
- **External modellers & model import** (Ch2 "Take Me Home! Introducing models", p.57–61): import a model package, common model import settings, set up an imported model. The book name-checks **Blender** as a free modeller that works well with Unity. (Preface p.6, Ch2.)
  - *Far Horizon application:* aligns with the project's **Blender + Blender MCP** asset-creation route (CLAUDE.md). The "common settings for models" import-tuning concept is still relevant (FBX import settings), though the Unity-6 importer is far richer (and `.claude/docs/unity-conventions.md`/`character-pipeline.md` hold the actual gotchas).

## Topic: Instantiation & Rigidbodies in gameplay (CONCEPTS ENDURE)

- **Instantiation = spawning/cloning prefabs at runtime** (Ch6): pass in an object, give it a position + rotation, name instances, assign velocity, then **manage/remove** spawned instances to avoid unbounded growth ("instantiate restriction and object tidying"). (TOC Ch6, p.151–184.)
  - *Why it matters:* `Instantiate(prefab, position, rotation)` is still the runtime-spawn API in Unity 6. The "tidy up / restrict spawns" discipline anticipates **object pooling** (the modern best practice the book doesn't name).
  - *Far Horizon application:* spawning gatherable resources / particles / projectiles → instantiate; for anything spawned frequently, prefer pooling over the book's create-then-Destroy pattern.

## Topic: Build, deployment, quality & input settings (HEAVILY DATED — read as history)

- **Build Settings:** "Scenes to build" list — **scene order matters; index 0 loads first** (the first/menu scene must be at position 0); drag to reorder. (Ch10 "Build Settings", p.258 + p.267.)
  - *Why it matters:* **the Scenes-in-Build list + index-0-loads-first rule is unchanged in Unity 6.** The first scene in the list is the entry point.
  - *Far Horizon application:* `Boot.unity` must be the first scene in the build list. (Far Horizon's headless builder targets `Build/Windows/FarHorizon.exe` per CLAUDE.md.)
- **Standalone Windows build:** produces a folder containing a **`.exe` + accompanying assets/data folder**. Standalone = best performance (local files, no browser overhead). (Ch10 "OS X/Windows Standalone", p.262 + "Building standalone", p.266–267.)
  - *Why it matters:* Windows standalone is still `.exe` + `*_Data` folder — directionally correct and exactly Far Horizon's target.
- **Platform detection:** `Application.platform == RuntimePlatform.WindowsWebPlayer / OSXWebPlayer …` to branch behavior per build target (e.g. hide a Quit button on web because `Application.Quit()` is a no-op in-browser). (Ch10 "Quit button platform automation", p.263–265.)
  - *⚠️ Dated:* the **`RuntimePlatform.*WebPlayer` enums are gone** (Web Player removed). `Application.platform` + `RuntimePlatform` still exist with current values (e.g. `WindowsPlayer`, `WindowsEditor`); `Application.Quit()` still works on desktop. Use `#if UNITY_STANDALONE_WIN` / platform defines for compile-time branching today.
- **Texture compression & "Strip Debug Symbols":** "Compress Textures" (on by default) compresses based on per-asset import settings; "Strip Debug Symbols" removes `Debug.Log` etc. from release builds. (Ch10 "Texture compression and debug stripping", p.266.)
  - *Why it matters:* texture compression (per-platform, set in importer) and code/debug stripping are **still real Unity-6 build optimizations** (now "Managed Stripping Level" / IL2CPP); the *concept* endures, the *toggle names* differ.
- **Quality Settings** (Ch10, p.270–273): presets Fastest…Fantastic. Per-setting meaning (these axes endure even though presets/location changed):
  - **Pixel Light Count** — number of per-pixel lights (vs cheaper vertex lights); pixel lights look better, cost more. *(URP now handles this via "Additional Lights" per-pixel/per-vertex + light limits.)*
  - **Shadows / Shadow Resolution / Shadow Cascades / Shadow Distance** — dynamic-shadow quality + the **Cascaded Shadow Maps** technique (more shadow detail near the camera) + a distance beyond which shadows aren't rendered (a perf LOD lever). *⚠️ Book says shadows are Unity-Pro-only — NO LONGER TRUE; standard in Unity 6/URP.* Shadow distance/cascades still exist in URP Asset.
  - **Blend Weights / "bones"** — number of bone weights blended per vertex for skinned/rigged characters (Unity recommended 2 bones as a perf/quality trade-off). *(Still a skinned-mesh quality setting.)*
  - **Texture Quality** — global texture-size/compression scaler.
  - **Anisotropic Textures** — anisotropic filtering improves textures at grazing angles (hills), costs perf; can also be per-texture in Import Settings. *(Still exists.)*
  - **Anti Aliasing** — softens edges (e.g. 2x MSAA), costs perf. *(URP: MSAA on the URP Asset / post-process AA like FXAA/SMAA.)*
  - **Soft Vegetation** — alpha-blending for terrain vegetation/trees (better transparent edges). *(Concept persists in terrain rendering.)*
  - **Sync To VBL (VSync)** — sync to monitor refresh; prevents tearing, generally lowers perf. *(Still "VSync Count" in Unity 6.)*
  - *Far Horizon application:* the **"Zone D" look** (bloom/grading/fog/gradient skybox) is achieved via URP Volume post-processing, NOT these legacy Quality presets — but the *axes* (shadow distance, AA, pixel lights, anisotropic) are real Unity-6 perf/quality knobs worth tuning for a desktop low-poly game.
- **Resolution Dialog / Display Resolution Dialog / Default Is Full Screen** (Ch10, p.266, 272): a startup dialog letting players pick resolution + Graphics Quality + remap Input. Can be disabled (Edit ▸ Project Settings ▸ Player) and forced via holding Alt at launch for test builds.
  - *⚠️ Dated:* the **standalone "Resolution Dialog" was removed (~Unity 2019.1)**; resolution is now handled in-game via `Screen.SetResolution` / settings menus. Do not expect it in a Unity-6 build.
- **Player Settings** (Edit ▸ Project Settings ▸ Player): Company Name, Product Name, default screen width/height, full-screen, icon/banner, "Run In Background", etc. (Ch10, p.260.)
  - *Why it matters:* **Player Settings still exists in Unity 6** with these fields (and many more: scripting backend IL2CPP, API level, etc.). Company/Product name + default resolution are still set here.

## Topic: Particles, HUD, menus, finishing-touches (LEGACY — replaced wholesale)

- **Particle systems (Ch7):** built from **Particle Emitter + Particle Animator + Particle Renderer** components; used to build fire + smoke for a campfire. (TOC + Ch7 p.185–205.)
  - *⚠️ FULLY DEAD:* this is the **legacy particle system**. Unity 6 uses **Shuriken** (one `ParticleSystem` component with modules) or **VFX Graph** (GPU, URP). A Far Horizon campfire = Shuriken or VFX Graph, not these three components. *Only the high-level idea (separate systems for flame vs smoke, image-per-particle for realism) carries over.*
- **HUD & GUI (Ch5):** **GUI Texture** objects + scripting; battery-collection HUD; **GUI Text** hints + fonts. (TOC Ch5 p.127–150.)
  - *⚠️ DEAD:* GUI Texture / GUI Text components were removed (~Unity 5.x/2018). Runtime HUD → **uGUI Canvas** (Image/Text/TextMeshPro) or **UI Toolkit**.
- **Menus (Ch8):** "Approach 1" GUI Texture buttons; "Approach 2" **`OnGUI()` + `GUILayout`/`GUI` immediate-mode** with flexible positioning + GUI skins. (TOC Ch8 p.207–231; code samples in Ch10 use `GUILayout.Button`.)
  - *⚠️ DEAD for runtime:* `OnGUI`/IMGUI is editor-tooling only now. Build the Far Horizon main menu with **uGUI** or **UI Toolkit**.
- **Finishing touches (Ch9):** volcano particle effect; **Trail Renderer** on thrown coconuts; **Camera Clip Planes + fog** as a perf tweak; ambient lighting; text animation via **Lerp** (linear interpolation); scene fade-in. (TOC Ch9 p.233–256.)
  - *Endures:* **Trail Renderer** still exists; **camera far-clip-plane + fog** is still a legit perf/atmosphere technique (and directly relevant to the "Zone D" fog look); **Lerp** for smooth interpolation is timeless (`Mathf.Lerp`/`Vector3.Lerp`). Ambient lighting concept endures (now via Environment Lighting / URP).
  - *Far Horizon application:* far-clip-plane + fog is a real lever for the "world feels BIG and ENDLESS" north-star (fog hides the draw-distance edge while culling far geometry).

## Topic: Testing & further study (advice, age-neutral)

- The book's closing advice (Ch11, p.275–277): share builds to get **unbiased external feedback** (catches bugs + unintuitive design the author is blind to); and the (tongue-in-cheek) "study next: Scripting, Scripting, Scripting" — there is no substitute for learning the engine's classes/commands even though Unity is visual-first.
  - *Why it matters:* the "external playtest to catch what the author can't see" principle is timeless and aligns with Far Horizon's **shipped-build capture gate + Tess sign-off** discipline (editor-vs-runtime divergence is a proven failure class per CLAUDE.md).

---

## Honest assessment of source quality for this consult

- **Genre:** entry-level, hands-on **tutorial book for non-programmers** (Packt, 2009), not a reference manual. Tone is gentle and conceptual; depth is shallow by design.
- **Strength:** clear first-principles framing of timeless 3D-game concepts (coordinate spaces, GameObject/Component, Update/FixedUpdate/deltaTime, colliders, prefabs, power-of-2 textures, scene-order, build basics). Good for shared vocabulary across a non-programmer-inclusive team.
- **Weakness for a Unity-6 project:** ~80% of the *specifics* (language, particle system, GUI, animation, input, build targets, licensing, quality-preset layout) are obsolete. **Zero coverage** of anything Unity-6-specific: URP, Shader Graph, Mecanim/Animator, new Input System, Addressables, UI Toolkit, ECS/DOTS, Burst/Jobs, async/Awaitable, render graph, GC/memory profiling. It is not a source for Unity-6 features.
- **Net:** use for *conceptual scaffolding and history*; pair every enduring concept with a current Unity-6 doc before acting. Do not cite any API/workflow detail here as current.
