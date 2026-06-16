# Raw extract — Purdue "Introduction to Unity" (CS 490/590 VR course notes)

- **SOURCE key:** purdue-intro-unity-pdf
- **Kind:** webpdf
- **Location:** https://www.cs.purdue.edu/cgvlab/courses/490590VR/notes/Introduction%20to%20Unity.pdf
- **Fetched:** yes (WebFetch returned only embedded hyperlinks because the Google-Slides export uses compressed content streams; the binary was saved locally and read page-by-page via the Read tool, 37 pages total, which gave full slide text).
- **Document nature:** A ~37-slide Google-Slides lecture deck for an academic VR course (Purdue CS 490/590VR). It is an **introductory, fundamentals-level** deck — substantive but basic. It targets **Unity 2019.1.11f1** (stated explicitly on the Installation slide), so all version-specific specifics are 2019-era, NOT Unity 6. The enduring conceptual material (component-entity model, MonoBehaviour lifecycle, Transform/Vector3/Quaternion math, colliders/triggers, raycasting, GetComponent) carries forward to Unity 6 unchanged. The deck has a heavy VR/Oculus slant at the end (OVRCameraRig, OVRInput) which is OUT OF SCOPE for Far Horizon (desktop, not VR) — flagged below as not-applicable.

> **Honest scope caveat for Far Horizon:** This is intro-101 material. It contains ZERO Unity-6-specific content (no URP, no Render Graph, no UI Toolkit, no Addressables, no new Input System, no DOTS/ECS, no Burst/Jobs, no GC/performance, no testing framework). It is useful only as a foundations refresher and to confirm enduring-basics naming. Everything version-specific below is Unity-2019-era and should be cross-checked against current Unity 6 docs before acting. Where a 2019 detail is known to have changed by Unity 6, I note it.

---

## Topic area: What Unity is (slides 2–3)

- **Finding:** Unity is a cross-platform game-development system consisting of a game engine + an IDE; used for games and AR/VR apps across many platforms. Example shipped titles cited: Beat Saber, SUPERHOT VR, Pokémon GO.
- **Why it matters:** Frames Unity as engine + editor; baseline orientation only.
- **Far Horizon application:** Confirms the engine choice category; no actionable specifics. (Marketing-level slide.)
- **Citation:** slide 2 "Unity"; slide 3 example titles.

## Topic area: Installation & version (slide 3)

- **Finding:** Course pins **Unity 2019.1.11f1**, downloaded as "Personal Edition" from https://unity3d.com/get-unity, with platform "Add Modules" (Android, iOS, tvOS, Linux, Mac, UWP, Vuforia AR, WebGL, Windows IL2CPP build supports selectable at install).
- **Why it matters:** Establishes the deck's era. Modules-at-install is still the Unity Hub model in Unity 6, but the version (2019.1.11f1) is far behind Far Horizon's **6000.4.10f1**.
- **Far Horizon application:** Confirms the install-modules workflow concept; Far Horizon needs Windows Build Support (already on `6000.4.10f1`). **VERSION-SPECIFIC / OUTDATED:** 2019.1.11f1 is irrelevant to the project.
- **Citation:** slide 3 "Installation".

## Topic area: Documentation entry points (slide 4)

- **Finding:** Two canonical docs roots presented as "your best friends": Unity User Manual (`https://docs.unity3d.com/Manual/index.html`) and Scripting API / ScriptReference (`http://docs.unity3d.com/ScriptReference/index.html`). Plus Unity Official Scripting video playlists (Beginner + Intermediate) as a C# intro.
- **Why it matters:** These two roots are still the correct, version-aware doc entry points in Unity 6 (the URL auto-redirects to the installed version's docs).
- **Far Horizon application:** Manual + ScriptReference remain the authoritative lookups for the team; pair any ScriptReference URL with the project's Unity-6 version selector. **ENDURING.**
- **Citation:** slide 4 "Documentation".

---

## Topic area: Core project structure — Project / Scenes / Packages (slide 5)

- **Finding:**
  - **Project** = contains all elements that make up the game: models, assets, scripts, scenes, etc.
  - **Scene** = a collection of game objects that constitute the world the player sees at any time.
  - **Package** = an aggregation of game objects + associated metadata.
- **Why it matters:** Defines the top-level containers; enduring vocabulary.
- **Far Horizon application:** Matches the project's scene-centric layout (`Boot.unity`); "Package" here is the loose lecture sense, not today's UPM package-manager sense (which is the precise Unity-6 meaning).
- **Citation:** slide 5 "Unity Basic Concepts".

## Topic area: Prefabs (slide 6)

- **Finding:** A prefab is a template for grouping assets under a single header; used to create multiple instances of a common object (e.g., street lights, trees); **prefabs can be instantiated during runtime**.
- **Why it matters:** Prefab = reuse + runtime instantiation, the core authoring unit for repeated content.
- **Far Horizon application:** Directly relevant to a low-poly survival world with many repeated props (trees in the "dense jungle" island, rocks, campfire, axe). Author repeated world objects as prefabs; instantiate procedurally. **ENDURING** — though Unity 6's nested prefabs + prefab variants (post-2018.3) extend this well beyond what the slide describes.
- **Citation:** slide 6 "Unity Basic Concepts (continued)".

## Topic area: Editor IDE layout (slide 7)

- **Finding:** Four labeled regions: **Object hierarchy** (Hierarchy window), **Project assets** (Project window), **Scene View**, **GameObject Inspector**. Inspector shows Transform, Mesh Renderer, materials, lighting/lightmap settings, Box Collider, etc. Window title shows the active scene + project + platform (e.g. `<DX11>`/`Android`).
- **Why it matters:** Standard editor anatomy; unchanged conceptually in Unity 6.
- **Far Horizon application:** Baseline editor literacy; the labeled Inspector fields (Mesh Renderer → Cast/Receive Shadows, Lightmap Static) are still where shadow/lightmap toggles live. **ENDURING.**
- **Citation:** slide 7 "Overview of the Unity IDE".

## Topic area: Editor (Scene-view) camera controls (slide 9)

- **Finding:**
  - Alt + Left Click & drag → rotate (orbit) camera.
  - Alt + Right Click & drag (or scroll wheel) → zoom in/out.
  - Alt + Middle Click & drag → pan (move camera up/down/left/right).
  - **Flythrough mode:** hold Right Mouse Button → FPS-style WASD movement, Q/E for up/down.
  - Doc: `http://docs.unity3d.com/Manual/SceneViewNavigation.html`.
- **Why it matters:** Editor navigation muscle memory; unchanged in Unity 6.
- **Far Horizon application:** Useful for any persona inspecting `Boot.unity` in-editor; note Far Horizon's GAME camera is PoE-style orbit + zoom (a runtime system), distinct from these editor controls. **ENDURING.**
- **Citation:** slide 9 "Editor Camera Controls".

## Topic area: Creating primitive geometry via the editor (slide 10)

- **Finding:** `GameObject → 3D Object →` menu offers: Cube, Sphere, Capsule, Cylinder, Plane, Quad, plus Terrain, Tree, Wind Zone, 3D Text, Ragdoll. Also `GameObject → Create Empty` (Ctrl+Shift+N) and `Create Empty Child` (Alt+Shift+N).
- **Why it matters:** Primitive creation menu; the primitives still exist in Unity 6 (menu reorganized slightly).
- **Far Horizon application:** Greybox/blockout primitives for prototyping the survival loop before final low-poly art lands; `Create Empty` for organizational/root transforms. **ENDURING** (menu still present; minor path drift).
- **Citation:** slide 10 "Creating Geometry via the Unity Editor".

## Topic area: Scene (gameplay) camera setup (slides 11–12)

- **Finding:**
  - Scene/game camera ≠ editor camera (explicitly warned).
  - Scenes ship a default **"Main Camera"** GameObject; it carries the **`MainCamera` tag**, "useful for accessing the camera from your scripts."
  - **Camera Preview** box shows what the camera sees = what you'll see on Play.
  - Position the camera by (a) editing Transform values in Inspector, or (b) moving the editor camera then `GameObject → Align With View` (**Ctrl+Shift+F**) with the camera selected. (Related: `Align View to Selected`, `Move To View` Ctrl+Alt+F.)
- **Why it matters:** Camera placement workflow + the `MainCamera` tag → `Camera.main` access pattern. Enduring.
- **Far Horizon application:** "Align With View" is a fast way to frame the gameplay cam during blockout; the `MainCamera` tag underpins `Camera.main`. **Gotcha (enduring + still true in Unity 6):** `Camera.main` does a tag search and historically was non-cached/slow — cache the camera reference in a field rather than calling `Camera.main` every frame in the orbit-camera controller. **ENDURING** (workflow); the perf note is enduring best-practice the slide does NOT mention.
- **Citation:** slides 11–12 "Setting Up The Scene Camera".

---

## Topic area: Importing external 3D objects (slide 13)

- **Finding:** Export from Maya/Blender/3ds Max as **`.OBJ` or `.FBX`** (save anywhere) then `Assets → Import New Asset…`; OR drop the file directly into the `Assets` folder and right-click → **Refresh** to register it; then drag from the Assets/Project window into the scene hierarchy. "Unity will take care of everything for you." Shows `.blend`, `.fbx`, `.obj`, `.mtl` files in the import dialog.
- **Why it matters:** The model-import pipeline; FBX is still the recommended interchange in Unity 6.
- **Far Horizon application:** Confirms the Blender→Unity route the project already uses (Blender MCP authoring per `character-pipeline.md` / `unity-conventions.md`). FBX export from Blender + drop-into-Assets is the carry-forward workflow. **ENDURING.** (The deck does NOT cover FBX import settings — scale factor, normals/tangents import, Humanoid rig mapping, read/write — which are the actual gotchas captured in the project's own `unity-conventions.md`.)
- **Citation:** slide 13 "Import External Objects".

## Topic area: Importing & using textures (slides 21–22)

- **Finding:** Same flow as importing a model but select `.PNG`/`.JPG`/etc. (or drop into Assets). To apply: select object → expand shader props in Inspector → drag the texture onto the **Albedo** slot of the Standard shader. Standard shader Main Maps shown: Albedo, Metallic + Smoothness slider, Normal Map, Height Map, Occlusion, Detail Mask, Emission, Tiling/Offset.
- **Why it matters:** Texture import + Albedo assignment basics.
- **Far Horizon application:** Low-poly smooth-shaded look uses flat/gradient colors more than detailed albedo textures; still, Tiling/Offset + Albedo assignment basics apply. **VERSION NOTE:** the deck uses the **built-in Standard shader**; Far Horizon is **URP**, where the equivalent is **Lit / Simple Lit** (and Shader Graph), with a different Inspector layout. Treat the Standard-shader specifics as built-in-pipeline-only, NOT URP. **PARTIALLY OUTDATED for this project.**
- **Citation:** slides 21–22 "Importing Textures" / "Using Textures".

## Topic area: Assets — what counts as an asset (slide 19)

- **Finding:** An asset = any resource used as part of an object's component. Listed: Scenes, Prefabs, Scripts, Textures, Animations, Models, Particles, Sprites, etc. Project window shows a `Packages` section (Analytics Library, In App Purchasing, Package Manager UI, TextMesh Pro, Unity Collaborate, Unity Timeline, Unity UI).
- **Why it matters:** Defines the asset taxonomy + shows the Package Manager existed (2019-era package set).
- **Far Horizon application:** Standard taxonomy; the `Packages` list is dated (Unity Collaborate is deprecated; the project uses URP/UI packages not shown here). **ENDURING** concept, **OUTDATED** package list.
- **Citation:** slide 19 "Assets".

---

## Topic area: The GameObject / component model (slides 14–16)

- **Finding:**
  - GameObjects are all the "things" in a scene: light sources, audio sources, cameras, gameplay logic, UI, etc.
  - **"Everything is a GameObject."** A GameObject **does nothing on its own**.
  - Every GameObject **always has a Transform component** (position/rotation/scale).
  - You **add Components** to give a GameObject behavior. Built-in components named: **Mesh Filter, Mesh Renderer, Rigidbody, Colliders, VideoPlayer**. Custom behavior = your own scripts that **inherit from `MonoBehaviour`**.
- **Why it matters:** This is THE foundational composition model of Unity, fully enduring through Unity 6.
- **Far Horizon application:** Reinforces composition-over-inheritance: build the castaway/survival systems as small components on GameObjects (mover, orbit-camera, inventory-need, chop-interaction) rather than monolithic god-objects. Transform is guaranteed; everything else is opt-in. **ENDURING — core.**
- **Citation:** slides 14 "Game Objects", 15 "Everything is a GameObject", 22(Components slide) "Components".

## Topic area: Scene graph / transform hierarchy (slide 17)

- **Finding:** A scene graph is nodes in a tree; **in Unity every node has exactly one parent and may have many children**; **operations applied to a parent apply to all child nodes** (e.g., transform a parent → children move with it).
- **Why it matters:** Parent-transform inheritance is the basis of rigging, grouping, and local-vs-world space.
- **Far Horizon application:** Parent props under organizational empties; the held axe parents under the chibi's hand bone so it inherits hand motion (the project's "axe-in-hand" work). Moving a parent island/root moves all its children. **ENDURING — core.**
- **Citation:** slide 17 "Scene graph".

## Topic area: Public fields ↔ Inspector serialization (slide 18, "Scripts")

- **Finding:** Components your scripts define inherit `MonoBehaviour`; **public variables show up in the Inspector**; a variable that is a Component type can also be assigned via the Inspector. **Critical gotcha shown:** an Inspector-set value **OVERWRITES the value in code** (slide annotates `public float rotationRate = 5.0f;` being overwritten by the Inspector's `15`). i.e., the serialized scene/prefab value wins over the C# initializer.
- **Why it matters:** A frequent source of "why isn't my default applying?" confusion — the serialized Inspector value, not the field initializer, is what runs.
- **Far Horizon application:** When a persona changes a default in code but the built scene still shows the old number, suspect the serialized Inspector override — this is a genuine editor-vs-runtime serialization trap (the class the project already tracks in `unity-conventions.md`). **ENDURING — important gotcha.** (Unity 6 adds `[SerializeField] private` for serializing non-public fields and `[field: SerializeField]` for auto-props — not in the slide.)
- **Citation:** slide 18 "Scripts".

## Topic area: Adding components / scripts to GameObjects (slide 18, "Adding Components")

- **Finding:** Attach a script either by drag-drop onto the GameObject in the Inspector, OR `Add Component → Scripts → <YourScriptName>`. Default new-script skeleton shows `void Start()` ("// Use this for initialization") and `void Update()` ("// Update is called once per frame"). Note: a script whose filename ≠ class name shows **"Missing"** in the Inspector (visible in the slide's screenshot).
- **Why it matters:** Script attachment workflow + the filename-must-match-class-name rule.
- **Far Horizon application:** **Gotcha (enduring + still true Unity 6):** the C# file name MUST match the `MonoBehaviour` class name or the component won't attach / shows "Missing". Watch for this in headless/bootstrap-generated scripts. **ENDURING.**
- **Citation:** slide 18 "Adding Components to Game Objects".

---

## Topic area: Scripting language + script skeleton (slides 23, 18)

- **Finding:** Scripting in Unity is **C#**. Scripts are components associated with a GameObject. Canonical skeleton:
  ```csharp
  using UnityEngine;          // basic Unity-Engine objects
  using System.Collections;   // basic structures (ArrayList, HashTable, ...)
  public class MyGameObject : MonoBehaviour {
      void Start()  { /* initializations (like a constructor in Java) */ }
      void Update() { /* code repeated every update cycle */ }
  }
  ```
- **Why it matters:** Defines the minimum component shape; `Start` = init, `Update` = per-frame.
- **Far Horizon application:** Baseline pattern for every gameplay script. **ENDURING.**
- **Citation:** slide 23 "Scripting in Unity"; slide 18 skeleton.

## Topic area: MonoBehaviour fundamental class & lifecycle/methods (slide 24)

- **Finding:** Creating a script makes a class extending **MonoBehaviour**, which contains functions/events available to scripts on GameObjects. Enumerated:
  - Lifecycle: **Awake, Start, Update, FixedUpdate** (LateUpdate referenced elsewhere in the deck's link set).
  - Collision events: **OnCollisionEnter, OnCollisionStay, OnCollisionExit**.
  - Messaging/lookup: **GetComponent, SendMessage, BroadcastMessage**.
  - Lifecycle ops: **Destroy, Instantiate**.
  - Full list: `https://docs.unity3d.com/ScriptReference/MonoBehaviour.html`.
- **Why it matters:** The execution-order primitives. Enduring through Unity 6.
- **Far Horizon application:**
  - `Awake` (before Start) for self-wiring / `GetComponent` caching; `Start` for cross-object wiring; `Update` for per-frame input/movement; **`FixedUpdate` for physics** (Rigidbody forces) since it runs on the fixed physics timestep. Use `Instantiate` for spawning prefabs (trees, props), `Destroy` for despawn.
  - **Best-practice note (enduring, not in the slide's depth):** prefer cached `GetComponent` in `Awake`/`Start` over per-frame calls; prefer direct refs over `SendMessage`/`BroadcastMessage` (reflection-based, slow). **ENDURING — core.**
- **Citation:** slide 24 "Fundamental Classes: MonoBehavior".

## Topic area: GameObject class — find/tag APIs (slide 25)

- **Finding:** GameObject is the generic base type for everything placeable in the hierarchy. GameObjects have a **name** and a **tag**. Lookup APIs: `GameObject.Find("Main Camera")`, `GameObject.FindWithTag("Player")`, `GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy")`.
- **Why it matters:** Object discovery by name/tag.
- **Far Horizon application:** **Gotcha / best-practice (enduring):** `GameObject.Find` (string-name search) and tag searches are **slow and fragile** (rename breaks them; runs a scene scan). Prefer serialized Inspector references or a registry/service-locator over `Find` in hot paths; reserve `FindWithTag` for one-time `Start` wiring. The slide presents them uncritically — Far Horizon should treat them as last-resort. **ENDURING concept; performance caveat is the team's to enforce.**
- **Citation:** slide 25 "Fundamental Classes: GameObject".

## Topic area: Transform class (slide 26)

- **Finding:** Every GameObject has a **Transform** storing position/rotation/scale. Query via `transform.position` and `transform.eulerAngles` (rotation as Euler).
- **Why it matters:** Transform is the universal spatial handle.
- **Far Horizon application:** Movement/placement reads & writes `transform.position`; the orbit camera and click-to-move mover both manipulate transforms. **ENDURING.** (Note: `eulerAngles` exposes rotation as degrees but is internally a Quaternion — see next.)
- **Citation:** slide 26 "Fundamental Classes: Transform".

## Topic area: Vector3 (slide 27)

- **Finding:** Vector3 = struct for 3D vectors/points, used everywhere to pass positions & directions; has common vector-op functions. Common methods: **Cross, Dot, Normalize, Lerp, Reflect, Distance**. Related math classes: Quaternion, Matrix4x4.
- **Why it matters:** The fundamental 3D math type.
- **Far Horizon application:** `Vector3.Distance` for interaction range (is the castaway close enough to chop?); `Vector3.Lerp`/`MoveTowards` for smooth motion; `Dot` for facing checks. **ENDURING.**
- **Citation:** slide 27 "Vector3".

## Topic area: Quaternion (slide 28)

- **Finding:** Quaternions represent rotations internally in Unity. Advantages over Euler: avoid gimbal lock, interpolate easily. Have x,y,z,w; **non-commutative**; "you likely will never need to modify the components individually." Use the helpers instead: **Quaternion.LookRotation, Quaternion.Angle, Quaternion.Euler, Quaternion.Slerp, Quaternion.FromToRotation, Quaternion.identity**.
- **Why it matters:** Correct rotation handling without hand-editing x/y/z/w.
- **Far Horizon application:** `Quaternion.LookRotation` to face the castaway toward the click-to-move target; `Quaternion.Slerp` for smooth turn; `Quaternion.identity` as the default spawn rotation for instantiated props. **Gotcha (enduring):** never lerp Euler angles across the 360/0 wrap — use Slerp on Quaternions. **ENDURING.**
- **Citation:** slide 28 "Quaternion".

## Topic area: Matrix4x4 (slide 29)

- **Finding:** 4×4 transformation matrix; does translation/rotation/scale/shear/perspective via homogeneous transforms. **Column-major**: in `mat[a, b]`, `a` = row index, `b` = column index. Used by Transform, Camera, Material, and GL functions. Common: determinant, inverse, transpose, LookAt, Ortho, Perspective, Rotate, Scale, Translate, **TRS**.
- **Why it matters:** Low-level transform math; rarely hand-used in gameplay code.
- **Far Horizon application:** Mostly not needed for gameplay; `Matrix4x4.TRS` shows up in GPU-instancing / `Graphics.DrawMeshInstanced` if the project ever batch-draws many low-poly props (relevant to a "dense jungle" of trees). **ENDURING but advanced/rare.**
- **Citation:** slide 29 "Matrix4x4".

## Topic area: Accessing components — GetComponent (slide 30)

- **Finding:** To modify component values at runtime, use **`GetComponent<T>()`**. Example: `Rigidbody rb = GetComponent<Rigidbody>(); rb.mass = 10f;` Unity defines a class type per component.
- **Why it matters:** The canonical way to reach a sibling component.
- **Far Horizon application:** Cache in `Awake`/`Start`: `_rb = GetComponent<Rigidbody>();` then reuse. **ENDURING.**
- **Citation:** slide 30 "Accessing Components".

## Topic area: Accessing members of other scripts (slide 31)

- **Finding:** Cross-object access pattern: `player.GetComponent<PlayerController>().DecreaseHealth();` — get the other GameObject (e.g. via `GameObject.Find("Player")` or a serialized ref), then `GetComponent<TheirScript>()` and call its public method/field.
- **Why it matters:** Inter-script communication pattern.
- **Far Horizon application:** The chop interaction (axe script → tree's health/choppable script) and the survival need (interaction → need-meter script) use exactly this `GetComponent<OtherScript>().Method()` pattern. **Best practice (enduring):** prefer serialized references / events over repeated `Find`+`GetComponent` in hot paths. **ENDURING.**
- **Citation:** slide 31 "Accessing Members of Other Scripts".

---

## Topic area: Colliders & Triggers (slide 32)

- **Finding:** Events come from the user (input), at intervals (`Update()`), or from the game itself. **Colliders** = physical objects that should not overlap; **triggers** = invisible barriers that send a signal when crossed. Event functions:
  - Colliders: `OnCollisionEnter()`, `OnCollisionStay()`, `OnCollisionExit()`.
  - Triggers: `OnTriggerEnter()`, `OnTriggerStay()`, `OnTriggerExit()`.
- **Why it matters:** Distinguishes solid collision from overlap-detection; the two callback families behave differently (collision passes a `Collision`, trigger passes a `Collider`).
- **Far Horizon application:** Triggers for pickup/interaction zones (walk into a choppable-tree radius → enable "chop" prompt); colliders for solid world geometry the castaway can't walk through. **Gotcha (enduring, not in slide):** trigger/collision callbacks only fire if at least one of the pair has a Rigidbody and the colliders are configured correctly (Is Trigger checkbox). **ENDURING.**
- **Citation:** slide 32 "Colliders and Triggers".

## Topic area: Worked example — rotate script (slides 18, 33)

- **Finding:** Frame-rate-independent rotation:
  ```csharp
  public float rotationRate = 5.0f;       // degrees/sec
  void Update() {
      Vector3 axis = new Vector3(0, 1, 0);              // or Vector3.up
      float amountToRotate = rotationRate * Time.deltaTime;
      this.transform.Rotate(axis, amountToRotate);
  }
  ```
- **Why it matters:** Demonstrates the **`* Time.deltaTime`** idiom — the single most important per-frame-motion best practice (decouples motion from frame rate).
- **Far Horizon application:** EVERY per-frame movement/rotation in the project (orbit camera, character turn, any spin) MUST multiply by `Time.deltaTime` in `Update` (or use `Time.fixedDeltaTime` in `FixedUpdate`). **ENDURING — core best practice.**
- **Citation:** slide 18 "Scripts" (RotateY), slide 33 "Example: Rotate script" (RotateYFinal).

## Topic area: Raycasting (slide 34)

- **Finding:** Full signature shown:
  ```csharp
  public static bool Raycast(
      Vector3 origin, Vector3 direction,
      out RaycastHit hitInfo, float maxDistance,
      int layerMask, QueryTriggerInteraction queryTriggerInteraction);
  ```
  Casts a ray from `origin` along `direction` of length `maxDistance` against all colliders in the scene; optional **LayerMask** filters which colliders can be hit; results in `out RaycastHit hitInfo`. Doc: `Physics.Raycast`.
- **Why it matters:** Raycasting is the backbone of click-to-pick and ground-detection.
- **Far Horizon application:** **HIGH RELEVANCE** — the PoE-style **click-to-move** converts the mouse position to a ray (`Camera.ScreenPointToRay`) and `Physics.Raycast` against the ground layer to get the world target point; the returned `RaycastHit.point` is the move destination. Use a **LayerMask** to hit only the ground (not props/UI). Also useful for "what am I clicking to chop?" object picking. **ENDURING — core for this project's locked click-to-move feel.**
- **Citation:** slide 34 "Raycasting".

---

## Topic area: Lighting (slide 23, "Lighting")

- **Finding:** Lighting via the **Light component**; types: **Directional, Point, Spot, Area (baked only)**. `GameObject → Light →` menu (Directional/Point/Spot/Area + Reflection Probe + Light Probe Group). Inspector shows: Type, Color, Mode, Intensity, Indirect Multiplier, Shadow Type (e.g. Soft Shadows), Realtime-shadow Strength/Resolution/Bias/Normal Bias/Near Plane, Cookie, Render Mode, Culling Mask. Docs: Lighting.html, GlobalIllumination.html.
- **Why it matters:** The four light types + Area=baked-only are enduring facts. Directional = the sun.
- **Far Horizon application:** The "warm/lush gradient lighting" Zone-D look centers on a **Directional Light** (sun) + ambient/GI; the slide confirms Area lights are bake-only (not for the dynamic outdoor sun). **VERSION NOTE:** the deck's lighting/GI is **built-in pipeline (Enlighten-era)**; Far Horizon is **URP**, where lighting settings, shadow cascades, and the Lighting window differ, and where the "Zone-D" bloom/grading/fog come from **URP Volume + post-processing**, NOT the built-in stack the slide implies. Treat light-TYPE facts as enduring; treat GI/shadow specifics as built-in-only. **PARTIALLY OUTDATED for this URP project.**
- **Citation:** slide 23 "Lighting".

## Topic area: Shading & Materials (slide 20)

- **Finding:** Unity provides built-in shaders incl. the **Unity Standard shader**; you can write your own. **Shaders are written in Cg/HLSL and wrapped in ShaderLab.** Docs: ShadersOverview.html, shader-StandardShader.html, Materials.html.
- **Why it matters:** Material/shader basics + the ShaderLab+HLSL authoring model.
- **Far Horizon application:** **MAJOR VERSION CAVEAT:** the **Standard shader is built-in-pipeline only and does NOT work in URP** (it renders magenta). Far Horizon (URP) uses **URP Lit / Simple Lit / Unlit** shaders and **Shader Graph** for custom looks (the gradient/toon low-poly smooth-shaded direction). The "Cg/HLSL wrapped in ShaderLab" statement is still true for hand-written shaders, but modern URP authoring leans on **Shader Graph** (node-based), which the 2019 deck predates/omits. **OUTDATED for this project's pipeline** — use as a "what a material/shader is" primer only, not for actual shader choices.
- **Citation:** slide 20 "Shading and Materials".

---

## Topic area: VR / Oculus (slides 35–37) — OUT OF SCOPE for Far Horizon

- **Finding:**
  - **OVRCameraRig** — component controlling stereo rendering + head tracking; maintains three child "anchor" transforms (left eye, right eye, virtual center eye); main interface between Unity and the HMD cameras; attached to a prefab for easy VR. Public member "Updated Anchors" filters tracking poses. GameObject structure includes **TrackingSpace** (reference frame for tracking; `OVRPlayerController` changes its position/rotation to follow head-pose yaw).
  - **OVRInput** — Oculus Touch controller mapping: `Axis2D.PrimaryThumbstick`, `Button.PrimaryThumbstick` (stick press), `Button.Start`, `Button.One`, `Button.Two`, `Axis1D.PrimaryIndexTrigger`, `Axis1D.PrimaryHandTrigger`, etc.
- **Why it matters:** This is the course's VR focus.
- **Far Horizon application:** **NOT APPLICABLE.** Far Horizon is a **desktop (Windows) mouse/keyboard** game — no VR, no HMD, no Oculus. These slides are explicitly out of scope; ignore OVRCameraRig/OVRInput entirely. The deck's later third is VR-specific and yields nothing for this project. **OUT OF SCOPE.**
- **Citation:** slides 35 "Oculus Utilities for Unity", 36 "OVRInput".

---

## Coverage gaps (what this source does NOT contain — relevant to Far Horizon)

This intro deck contains **none** of the following Unity-6-era topics the brief asked for; they must be sourced elsewhere:
- Unity 6 new features/APIs (Render Graph, GPU Resident Drawer, GPU Occlusion Culling, Awaitables, etc.).
- **URP** specifics (Renderer Features, Volume/post-processing for the Zone-D look, URP Lit vs built-in Standard, shadow cascades, Forward+).
- **Shader Graph** (node-based authoring — the actual route for the low-poly smooth-shaded/gradient look).
- The **new Input System** (the deck shows no input handling at all — not even legacy `Input.GetAxis`; the relevant slides are VR/OVRInput only).
- **UI Toolkit** (and even legacy uGUI is only mentioned as a package, never taught).
- **Addressables** / asset management at scale.
- **Performance / CPU-GPU-memory / GC** (no profiler, no GC.Alloc avoidance, no Burst/Jobs/DOTS, no draw-call batching/SRP Batcher/GPU instancing details).
- **Testing** (no EditMode/PlayMode, no NUnit, no Test Runner) — directly relevant to Far Horizon's paired-test bar but absent here.
- **Build/deployment** specifics (no Build Settings/Player Settings/scenes-in-build/IL2CPP detail beyond the install-module checkbox).
- **Animation** (Animator/Mecanim/Humanoid rig/Animation Clips) — only a "Ragdoll" menu item and "Animations" asset-type bullet appear; the Mixamo→Humanoid rig pipeline (Far Horizon's character route) is uncovered.
- **Audio** (only "Audio sources" listed as a GameObject type and an `Audio` menu entry; no AudioSource/AudioListener/mixer detail).
- **ScriptableObjects** (the data-asset pattern) — absent.
