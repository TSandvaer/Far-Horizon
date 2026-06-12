# Testing Bar — Far Horizon (Unity translation)

**Sponsor directive (2026-05-02, carried from RandomGame):** "I want you to use a lot of time testing, I don't want to debug and return findings all the time."

Translation: by the time anything reaches the Sponsor for sign-off, it must already have been hammered thoroughly. Sponsor's role is **acceptance**, not bug-finding. This document is binding on every role; Tess enforces it. Engine surfaces translated to Unity 2026-06-12.

A ticket is "complete" only when ALL of:

1. **Paired tests.** Every behavior change ships EditMode and/or PlayMode tests in the same PR (`FarHorizon.EditTests` / `FarHorizon.PlayTests`). Bug fixes pin the regression first. Edge probes (negative-path + boundary) are part of this, not optional extras.
2. **Green checks.** Full EditMode + PlayMode suites green — verified from the `-testResults` XML's `<test-run result="Passed">` line, not exit codes. CI (from U4 onward) chains BootstrapProject.Run → tests → FarHorizonBuilder.BuildWindows.
3. **Shipped-build verification** (successor to RandomGame's HTML5 gate). Editor evidence is never sufficient — the editor-vs-runtime divergence class (Awake-no-serialize, shader stripping, NavMesh-not-shipped; see `.claude/docs/unity-conventions.md`) is proven by spike incidents. Anything UX/visually-visible needs evidence captured from the BUILT exe (windowed launch, in-game capture, HUD build-stamp visible) attached to the PR/ticket.
4. **Self-Test Report.** UX-visible PRs carry an author-posted Self-Test Report comment (what was run, on which build stamp, what was observed — concrete values only, never invented) before Tess reviews.
5. **Tess sign-off.** QA review verdict (APPROVE / APPROVE_WITH_NITS / REQUEST_CHANGES) as a PR comment. Tess-authored PRs get a Drew/Devon peer reviewer instead (Tess can't self-QA).
6. **Sponsor soak** only where the gate is subjective feel or first-of-class visuals — right-size the ask; always include the exe path + the expected HUD build stamp.
