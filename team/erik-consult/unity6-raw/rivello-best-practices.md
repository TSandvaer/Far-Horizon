# Rivello — Unity Best Practices (raw extract)

**Source key:** `rivello-best-practices`
**Type:** web
**Primary URL:** https://samuel-asher-rivello.medium.com/best-practices-3-unity-8abcce214ddc
**Author:** Samuel Asher Rivello (Rivello Multimedia Consulting; "25+ years game dev"). MIT-licensed companion repo.
**Fetched:** 2026-06-16. WebFetch on the landing page + the resolvable linked articles + the GitHub template README.
**Focus requested:** project architecture, folder structure, naming conventions, design patterns, code best practices.

> **Honesty caveat (read first).** This source is a multi-part Medium *series* that is largely **high-level / promotional**, pointing readers to (a) the author's GitHub template repo and (b) a paid Udemy course for the concrete rules. The landing "Best Practices — 3" article and the GitHub README carry the actionable content; the deeper parts (MVC, Testing, CI/CD) read as course-marketing overviews with little concrete technical detail. **None of this material is Unity-6-specific** — it is engine-version-agnostic project-hygiene advice. Where the requested detail (exact folder tree, full naming rules) lives only in the repo's files or the paid course, that is flagged as a gap rather than invented. No values are fabricated.

---

## Topic: Project folder structure

**Finding (from "Coding Standards & Project Structure", Best Practices — 3).** Recommended top-level `Assets/` layout:
- `ThirdParty` — external libraries / imported asset-store packages
- `Art` — sprites, images, animations, models
- `Documentation` — readme files, links, diagrams
- `Prefabs`, `Scenes`, `Resources` — Unity-specific component folders
- `Scripts` — divided into **Editor**, **Runtime**, and **Test** subfolders

From the GitHub template README, the concrete on-disk layout the author actually ships:
- `Unity/` — the folder you open in the Editor (project root one level above `Assets/`)
- `Unity/Assets/Scenes/` — playable scenes ("open and press Play")
- `Unity/Assets/Documentation/ReadMe.asset` — in-editor setup guidance asset
- `Unity/Assets/Scripts/Runtime/RMC/Templates/` — coding-standard example scripts (note the vendor/namespace folder `RMC` between `Runtime` and the feature folders)
- `Unity/ProjectSettings/ProjectVersion.txt` and `Unity/Packages/manifest.json` — config files

**Why it matters.** A consistent template streamlines onboarding, reduces "where does this go?" churn, keeps editor-only code out of runtime builds (Editor folder), and isolates tests.

**How it applies to Far Horizon (Unity 6 / URP desktop).** Far Horizon already follows the Editor/Runtime/Test split via asmdefs (`FarHorizon.Runtime` / `FarHorizon.Editor` / `FarHorizon.EditTests` / `FarHorizon.PlayTests`). The `Art` / `Prefabs` / `Scenes` / `Resources` / `ThirdParty` / `Documentation` top-level split is a clean, low-cost convention to confirm we mirror. The `RMC`-style vendor folder under `Scripts/Runtime/` maps to our `FarHorizon` namespace folder.

**Citation.** Best Practices — 3, "Coding Standards & Project Structure"; GitHub README (https://github.com/SamuelAsherRivello/unity-project-template).
**Version note:** version-agnostic; no Unity-6 specifics.

---

## Topic: Naming conventions & coding standards

**Finding.** The GitHub README enumerates the *scope* of the standards but the concrete rules live in the repo's `Templates/` example files and the author's site (`SamuelAsherRivello.com/best-practices`), not in the prose. Documented scope headings:
- "Naming Conventions"
- "File Naming and Organization"
- "Formatting and Indentation"
- "Comments and Documentation"
- "Classes, Functions and Interfaces"
- "Testing"

Stated benefits of adopting consistent standards: code integration, team-member integration, maintenance, uniform problem-solving, reduced communication overhead, performance optimization, cost reduction.

**Why it matters.** A team-consistent, readable standard reduces review friction and merge conflicts.

**How it applies.** Far Horizon is an orchestrator + named-agent team where multiple personas write code in parallel worktrees — a single documented C# standard reduces cross-persona divergence (cf. the project's parallel-agent shared-vocabulary discipline). The concrete rules are NOT in the article; if we want Rivello's exact conventions we'd need to read the `Templates/` example scripts in the repo directly.

**Citation.** Best Practices — 3, "Coding Standards & Project Structure"; GitHub README scope list.
**GAP:** the exact naming rules (PascalCase/camelCase/_prefix/const casing/namespace rules/member ordering) are not in any fetched page — they exist only in the repo's example files or the paid course. **Not fabricated here.**

---

## Topic: Design principles & patterns

**Finding (from "Design Principles and Patterns", Best Practices — 3).**
- Apply **DRY** (Don't Repeat Yourself) and **KISS** (Keep It Simple, Stupid).
- Organize project structure with consistent coding practices.
- Use established **design patterns** for common problems; build frameworks/architectures to manage larger complexity.
- **Divide games into manageable scenes.**
- **Use prefabs** to simplify development and maintenance.
- The series promotes **MVC architecture** (Best Practices — 4) for separation of concerns "crucial for larger game projects" — but that article is a promo for a Udemy course and gives no concrete Model/View/Controller mapping, dependency direction, or code patterns.

**Why it matters.** Pattern discipline keeps a growing survival-game codebase navigable.

**How it applies.** Scene-splitting and prefab-first authoring align with Far Horizon's `Boot.unity` + regenerated-scene workflow. MVC is offered but unsubstantiated in the free material — treat as a pointer, not a recipe.

**Citation.** Best Practices — 3, "Design Principles and Patterns"; Best Practices — 4 (MVC, promotional only).
**GAP:** MVC implementation detail not disclosed in free content (paid course).

---

## Topic: ScriptableObjects & packages

**Finding (from "Scriptable Objects and Packages", Best Practices — 3).**
- **ScriptableObjects** are recommended as an alternative to MonoBehaviour for **storing data**.
- Consider incorporating from project start: an **async/await** library, **localization**, Unity's **Input System**, and **UI Toolkit**.

**Why it matters.** SO-based data avoids scene-coupling and enables designer-tunable config; adopting Input System / UI Toolkit early avoids costly migration later.

**How it applies.** SOs are a natural fit for Far Horizon's survival-tuning data (needs decay rates, craft recipes, item defs). Input System is already implied by the spike. UI Toolkit is the modern HUD route for the `BUILD <tag> | <UTC> | <sha>` stamp + survival HUD.

**Citation.** Best Practices — 3, "Scriptable Objects and Packages".

---

## Topic: Performance & efficient settings

**Finding (from "Efficient Settings and Structures", Best Practices — 3).**
- Use **IL2CPP** to improve runtime performance across platforms.
- Enable **"Enter Play Mode Options"** to reduce iteration/compile time (requires code written to not depend on domain/scene reload — i.e., reset statics manually).
- Implement the **Addressables** system (replaces older Asset Bundles) for asset management, delivery, and memory optimization.

**Why it matters.** IL2CPP is the standard for shipped desktop builds; Enter Play Mode Options is the single biggest editor-iteration speedup; Addressables is the modern memory/streaming story.

**How it applies (Unity 6 / URP desktop).** For `Build/Windows/FarHorizon.exe`, IL2CPP scripting backend is the expected ship config. Enter Play Mode Options speeds the soak/iterate loop but requires static-reset discipline — a known trap. Addressables is appropriate as the world/asset count grows (the "big round island" with dense jungle) for memory control.

**Citation.** Best Practices — 3, "Efficient Settings and Structures".
**Version note:** IL2CPP / Enter Play Mode Options / Addressables all exist in Unity 6 unchanged in concept; advice is version-agnostic.

---

## Topic: Version control & Unity version selection

**Finding (from "Version Control and Unity Versions", Best Practices — 3).**
- Use a VCS (Git / GitHub) for backups, sharing, history.
- Work with the latest **LTS** version for stability; avoid unstable/preview versions in production.

**Why it matters.** LTS = stability + longer patch support.

**How it applies.** Far Horizon is pinned to `6000.4.10f1` (Unity 6). Note: confirm whether `6000.4.x` is the LTS track Far Horizon intends to ride — the advice favors LTS for a shipping product.

**Citation.** Best Practices — 3, "Version Control and Unity Versions".

---

## Topic: Automated workflows / build & test automation

**Finding (from "Automated Workflows", Best Practices — 3).**
- Automate **builds triggered on code commit**.
- Include **automated unit tests** for reliability.
- Make **any scene loadable directly from the editor**.
- Implement **automatic project builds**.

From the GitHub template: ships **Assembly Definitions**, **Unity Test Framework**, and **Code Coverage** packaged in.

**Why it matters.** Commit-triggered builds + tests catch regressions before they reach the player; loadable-any-scene removes the "play from scene 0" friction.

**How it applies.** Far Horizon already has headless entry points (`BootstrapProject.Run`, `FarHorizonBuilder.BuildWindows`, `-runTests`) and a shipped-build capture gate — this advice validates that direction. Assembly Definitions + Test Framework + Code Coverage are all already in play. The "any scene loadable" tip is a low-cost editor-QoL win.

**Citation.** Best Practices — 3, "Automated Workflows"; GitHub README package table.

---

## Topic: Testing (Best Practices — 5)

**Finding.** The dedicated testing article is an overview only. Concrete points it makes:
- Test types: **Unit Testing** ("backbone … individual components"), **Integration Testing** ("different parts work together"), **Automated Testing**.
- Best practices listed: **Regular Testing** (throughout the cycle), **Test-Driven Development**, balancing **Automated vs. Manual** testing.
- Advanced tools mentioned: **Mocking and Stubbing**, **Performance Testing**, **Continuous Integration**.

**Why it matters.** Reinforces TDD + CI as the testing posture.

**How it applies.** Matches Far Horizon's paired EditMode/PlayMode + green-checks + shipped-build-verification + Tess sign-off bar.

**Citation.** Best Practices — 5 (https://samuel-asher-rivello.medium.com/unity-testing-for-unity-elevating-your-game-development-skills-eb76fc0bbea3).
**GAP:** no concrete EditMode-vs-PlayMode, NUnit attributes, test asmdef, AAA pattern, naming, Test Runner, or coverage detail. **Not fabricated.**

---

## Topic: CI/CD (Best Practices — 6, Buildalon)

**Finding.** Overview/promotional. States CI/CD benefits (higher code quality, faster deployment, reduced production time, reduced cost/tech-debt) and Buildalon features (cross-platform build automation incl. Windows/macOS; GitHub version-control integration; automated cross-platform testing).

**Why it matters.** Confirms commit→build→test→artifact pipeline as the target.

**How it applies.** Far Horizon's CI must upload artifacts before cleanup (`Build/`, `Captures/`, `*.log`, `test-results*.xml` are gitignored). The article does not provide GitHub Actions YAML, headless commands, caching, or artifact-handling specifics.

**Citation.** Best Practices — 6 (https://samuel-asher-rivello.medium.com/best-practices-6-ci-cd-with-unity-buildalon-be3286f05274).
**GAP:** no concrete pipeline steps / workflow examples / caching strategy. **Not fabricated.**

---

## Topic: Build/target configuration (from the template)

**Finding (GitHub README).** The shipped template's configured defaults:
- **Target platform:** "Standalone MAC/PC"
- **Render pipeline:** **Universal Render Pipeline (URP)**
- **Game View aspect:** "10x16" (portrait — note: this is a mobile-ish portrait default, NOT a fit for a desktop landscape game)
- **Packages included:** Cinemachine, Physics (2D/3D), ProBuilder, Post-Processing, URP, TextMesh Pro, Unit Testing Framework, Code Coverage, Assembly Definitions.
- **License:** MIT.

**Why it matters.** A pre-wired URP + Cinemachine + post-processing + ProBuilder + TMP stack is close to a low-poly desktop game's needs out of the box.

**How it applies (Far Horizon).** URP + Post-Processing aligns with the "Zone D" look (bloom/grading/fog/gradient skybox). **Cinemachine** is directly relevant to the mouse-orbit + zoom camera. **ProBuilder** is useful for blockout/whitebox of the island. **TMP** for HUD. **Override the 10x16 portrait aspect → landscape** for a desktop title.

**Citation.** GitHub README, build/configuration + package sections.

---

## Series links (for follow-up; resolved where possible)

- Best Practices — 1 (Project Structure): `https://link.medium.com/7Sy6FyjBe8` — **redirector; true destination not retrieved** (do not extrapolate the slug).
- Best Practices — 2 (C# Coding Standards): `https://link.medium.com/29obXJWBe8` — **redirector; true destination not retrieved.**
- Best Practices — 4 (MVC): https://samuel-asher-rivello.medium.com/unleashing-the-power-of-mvc-architecture-in-unity-a-journey-of-structured-game-development-492ef9c53817 (promotional)
- Best Practices — 5 (Testing): https://samuel-asher-rivello.medium.com/unity-testing-for-unity-elevating-your-game-development-skills-eb76fc0bbea3
- Best Practices — 6 (CI/CD Buildalon): https://samuel-asher-rivello.medium.com/best-practices-6-ci-cd-with-unity-buildalon-be3286f05274
- Best Practices — 7 (Asset Store Packages): https://samuel-asher-rivello.medium.com/best-practices-7-best-unity-asset-store-packages-77d7b48f46a7
- Game Architectures Part 2: https://sam-16930.medium.com/unity-game-architectures-part-2-672958fcb33a
- Game Architectures Part 3: https://sam-16930.medium.com/unity-game-architectures-part-3-d7c97b8ed2b
- GitHub template repo: https://github.com/SamuelAsherRivello/unity-project-template

---

## Overall assessment

Useful as a **project-hygiene checklist** (folder split, Editor/Runtime/Test, DRY/KISS, ScriptableObjects-for-data, IL2CPP, Enter Play Mode Options, Addressables, LTS, commit-triggered builds, scene-splitting, prefab-first). It is **not** a deep technical reference and contains **zero Unity-6-specific** material. The most concrete artifact is the **GitHub template repo**; the deepest specifics (exact naming rules, MVC implementation, test/CI mechanics) sit behind the repo's example files or a paid course and were honestly NOT extractable from the free pages.
