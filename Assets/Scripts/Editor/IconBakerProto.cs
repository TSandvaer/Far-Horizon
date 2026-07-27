using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// ICONBAKER PROTOTYPE (ticket 86camyvwn — Sponsor route decision 2026-07-27: "render the ACTUAL props to
    /// slot sprites"). Frames a real in-world prop with its own camera + light rig and bakes it to a slot-sized
    /// sprite PNG via an OFFSCREEN RenderTexture readback. iron_ore + iron_ingot are the first two subjects.
    ///
    /// THIS IS A PROTOTYPE. It produces a CANDIDATE CONTACT SHEET for the Sponsor to judge (framing / lighting /
    /// background variants). It deliberately does NOT wire anything into <see cref="FarHorizon.ItemCatalog"/> or
    /// <see cref="FarHorizon.InventoryUI"/> — productionization follows the Sponsor's pick.
    ///
    /// ── Capture path (WHY RT-readback, not a windowed capture) ────────────────────────────────────────────
    /// unity-conventions.md §Headless/CLI rituals: the historical "-batchmode produces no frames" rule has two
    /// separable causes — (a) `-nographics` forces a Null device, and (b) backbuffer `ScreenCapture` has no
    /// swapchain under batchmode. Rendering a camera into an offscreen RenderTexture and reading the pixels back
    /// works under `-batchmode` WITHOUT `-nographics` (proven editor-side by the #248 spike and player-side by
    /// #250). So this tool runs:
    ///
    ///   Unity.exe -batchmode -quit -projectPath &lt;p&gt; \
    ///     -executeMethod FarHorizon.EditorTools.IconBakerProto.BakeCandidates \
    ///     -iconOutDir &lt;abs-or-rel dir&gt; -logFile &lt;log&gt;
    ///
    /// NO `-nographics` (would give device=Null → black frames), NO windowed capture (the windowed lane is
    /// runner-1-pinned and contended by CI — [[single-unity-build-slot-serializes-orchestration]]).
    ///
    /// ── Known-caution ledger (read before trusting a judgement off these PNGs) ────────────────────────────
    /// • unity-conventions.md §Editor-vs-runtime: "editor `Camera.Render()` in batchmode mis-renders
    ///   multi-submesh URP materials" (hero-axe PR #21). This tool prefers URP's
    ///   <c>RenderPipeline.SubmitRenderRequest</c> (the full-pipeline path) and LOGS which path it used, so a
    ///   silent downgrade to `cam.Render()` is visible. Every subject here is single-submesh per renderer, so the
    ///   #21 class does not apply — but material-FIDELITY judgement still belongs on a shipped-exe re-bake, and
    ///   this sheet is for SHAPE / LIGHTING / BACKGROUND judgement.
    /// • unity-conventions.md §Editor-vs-runtime "zoom-to-fit is a false-green" (PR #39) is about GAMEPLAY
    ///   VISIBILITY gates — a subject-fit close-up cannot prove the player can see a thing at real scale. An
    ///   inventory ICON is the opposite case: fit-to-slot IS the requirement, so auto-fit framing is correct
    ///   here and is NOT the #39 anti-pattern.
    /// • Post-processing is OFF on the icon camera. Bloom/tonemap would wash a 52px read AND destroy the alpha
    ///   cutout the transparent-background variant depends on.
    ///
    /// ── Global-state discipline ───────────────────────────────────────────────────────────────────────────
    /// The bake runs in a FRESH EMPTY SCENE (`EditorSceneManager.NewScene`) that is NEVER saved, and touches
    /// only that scene's ambient/fog `RenderSettings`. It deliberately does NOT touch
    /// <c>RenderSettings.skybox</c> — that is the exact vector of the PR #231 `GradientSky.mat` `_HorizonColor`
    /// 0.8→0.42 corruption (an EditMode run mutating a LIVE singleton asset through global engine state, then a
    /// same-session regen committing the polluted value). Prior ambient/fog values are snapshotted + restored
    /// anyway.
    /// </summary>
    public static class IconBakerProto
    {
        // ── Palette cites ───────────────────────────────────────────────────────────────────────────────────
        // The two ore-node tints, verbatim from MovementCameraScene.cs:2876-2877 (they are `private static
        // readonly` there, so a prototype in a sibling file cannot reference them). PRODUCTIONIZATION NOTE:
        // these belong hoisted to a shared palette type so the icon and the world prop provably share ONE
        // constant instead of two copies that can drift.
        private static readonly Color OreRockGrey = new Color(0.50f, 0.48f, 0.45f); // warm stone grey
        private static readonly Color OreVeinRust = new Color(0.44f, 0.25f, 0.18f); // rusty iron-ore red-brown

        // Iron ingot tint — the cool steel-grey end of the shipped weapon palette (blender-asset-pipeline.md §2
        // "MetalGrey #7F8C8D" / the iron blue-grey tile). Deliberately COOL + desaturated so the ingot reads as
        // a DIFFERENT MATERIAL from the warm rusty ore — the whole point of ticket 86camyvwn.
        private static readonly Color IronIngotGrey = new Color(0.50f, 0.55f, 0.575f);

        // Background chip colours taken from the REAL Pack slot, not invented:
        //   Assets/UI/InventoryPalette.uss:14  --slot-empty: rgba(58, 48, 42, 0.92)  → #3A302A
        //   Assets/UI/InventoryPalette.uss:13  --panel-edge: rgb(90, 70, 50)         → #5A4632
        private static readonly Color BgSlotWell = new Color(58f / 255f, 48f / 255f, 42f / 255f, 1f);
        private static readonly Color BgWarmChip = new Color(90f / 255f, 70f / 255f, 50f / 255f, 1f);

        private const int IconSize = 64;   // the Pack slot well is 64x64 (InventoryPanel.uss:77-78)
        private const int RefSize = 128;   // the 128 reference bake (see the report note on the 52px inner area)

        // ── Variant matrix ──────────────────────────────────────────────────────────────────────────────────

        private static readonly string[] Subjects =
        {
            "iron_ore_pile",       // the ACTUAL looted pickup prop, as shipped (grey rock material)
            "iron_ore_pile_rust",  // same shipped mesh, paired with the shipped ore-VEIN rust material
            "iron_ore_veined",     // the ACTUAL in-world ore NODE recipe (grey rock + 3 rust veins)
            "iron_ingot_proto",    // PROTOTYPE stand-in — no ingot mesh exists in the project
        };

        private enum LightRig { KeyRim, Flat }

        private struct Variant
        {
            public string Id;       // filename token + sheet label
            public float Yaw;       // degrees, 0 = straight-on side profile
            public float Pitch;     // degrees, downward tilt
            public LightRig Rig;
            public Color Bg;        // alpha 0 = transparent cutout
            public string Note;
        }

        private static readonly Variant[] Variants =
        {
            new Variant { Id = "A_hero34_keyrim_bgNone", Yaw = 32f, Pitch = 24f, Rig = LightRig.KeyRim,
                          Bg = new Color(0f, 0f, 0f, 0f),
                          Note = "3/4 hero angle, key+rim light, TRANSPARENT background" },
            new Variant { Id = "B_hero34_keyrim_bgWell", Yaw = 32f, Pitch = 24f, Rig = LightRig.KeyRim,
                          Bg = BgSlotWell,
                          Note = "3/4 hero angle, key+rim light, dark slot-well chip #3A302A" },
            new Variant { Id = "C_side_flat_bgNone",     Yaw = 0f,  Pitch = 6f,  Rig = LightRig.Flat,
                          Bg = new Color(0f, 0f, 0f, 0f),
                          Note = "SIDE PROFILE (silhouette check), flat fill light, TRANSPARENT" },
            new Variant { Id = "D_hero34_keyrim_bgWarm", Yaw = 32f, Pitch = 24f, Rig = LightRig.KeyRim,
                          Bg = BgWarmChip,
                          Note = "3/4 hero angle, key+rim light, warm palette-tinted chip #5A4632" },
        };

        // ── Entry point ─────────────────────────────────────────────────────────────────────────────────────

        public static void BakeCandidates()
        {
            int exit = 1;
            var manifest = new StringBuilder();
            try
            {
                string outDir = ResolveOutDir();
                Directory.CreateDirectory(outDir);
                Debug.Log("[IconBaker] start outDir=" + outDir +
                          " device=" + SystemInfo.graphicsDeviceType +
                          " batchmode=" + Application.isBatchMode +
                          " pipeline=" + (GraphicsSettings.currentRenderPipeline != null
                                          ? GraphicsSettings.currentRenderPipeline.name : "NULL(built-in)"));

                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                {
                    Debug.LogError("[IconBaker] FAIL — graphics device is NULL. The bake would write black " +
                                   "frames. Relaunch WITHOUT -nographics (see the reviewer-ritual trap in " +
                                   "unity-conventions.md §Headless/CLI rituals).");
                    EditorApplication.Exit(1);
                    return;
                }

                // Fresh empty scene — never saved. Keeps the bake out of Boot.unity entirely.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                AmbientMode prevAmbientMode = RenderSettings.ambientMode;
                Color prevAmbient = RenderSettings.ambientLight;
                bool prevFog = RenderSettings.fog;

                int baked = 0;
                try
                {
                    RenderSettings.fog = false; // an icon must never be fog-washed

                    manifest.AppendLine("# IconBaker prototype manifest — ticket 86camyvwn");
                    manifest.AppendLine("# subject|variant|size|file|verts|tris|renderPath|device");

                    foreach (string subjectId in Subjects)
                    {
                        foreach (var v in Variants)
                        {
                            baked += BakeOne(subjectId, v, outDir, manifest);
                        }
                    }

                    File.WriteAllText(Path.Combine(outDir, "manifest.txt"), manifest.ToString());
                }
                finally
                {
                    RenderSettings.ambientMode = prevAmbientMode;
                    RenderSettings.ambientLight = prevAmbient;
                    RenderSettings.fog = prevFog;
                }

                int expected = Subjects.Length * Variants.Length * 2;
                Debug.Log("[IconBaker] complete baked=" + baked + " expected=" + expected + " dir=" + outDir);
                exit = baked == expected ? 0 : 1;
                if (exit != 0)
                    Debug.LogError("[IconBaker] FAIL — baked " + baked + " of " + expected + " expected PNGs.");
            }
            catch (Exception e)
            {
                Debug.LogError("[IconBaker] EXCEPTION " + e);
                exit = 1;
            }

            EditorApplication.Exit(exit);
        }

        // Bake one (subject, variant) pair at both sizes. Returns the number of PNGs written.
        private static int BakeOne(string subjectId, Variant v, string outDir, StringBuilder manifest)
        {
            GameObject subject = null, camGo = null, keyGo = null, rimGo = null, fillGo = null;
            var temps = new List<UnityEngine.Object>();
            int written = 0;
            try
            {
                subject = BuildSubject(subjectId, temps, out int verts, out int tris);

                // ── Lighting rig ────────────────────────────────────────────────────────────────────────────
                // EXPOSURE NOTE (round-1 self-review, 2026-07-27): the first pass ran the key at 1.30 with
                // ambient 0.30 and the mid-grey ore (_Tint 0.50) came back reading near-WHITE — a "quartz
                // shard", not iron ore. albedo here is vertexColour × _Tint, and the OrePile lump mesh carries
                // NO colour stream (so IN.color = white), which makes the prop fully exposed to the key. Key
                // tamed to 1.00 and a low FILL added opposite the key so no facet drops to near-black either
                // (the round-1 ingot had an almost-black −X face). Judged by eye on the contact sheet, not by
                // a luma metric — a metric is green on nonsense (lowpoly-quality.md §0).
                RenderSettings.ambientMode = AmbientMode.Flat;
                if (v.Rig == LightRig.KeyRim)
                {
                    RenderSettings.ambientLight = new Color(0.34f, 0.34f, 0.36f);
                    keyGo = MakeDirLight("IconKey", new Vector3(38f, v.Yaw - 45f, 0f),
                                         new Color(1.00f, 0.96f, 0.90f), 1.00f);
                    rimGo = MakeDirLight("IconRim", new Vector3(8f, v.Yaw + 155f, 0f),
                                         new Color(0.74f, 0.82f, 0.96f), 0.55f);
                    fillGo = MakeDirLight("IconFill", new Vector3(16f, v.Yaw + 62f, 0f),
                                          new Color(0.90f, 0.92f, 1.00f), 0.35f);
                }
                else
                {
                    // Flat: one frontal fill from the camera direction + high ambient → minimal form shading.
                    RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.62f);
                    keyGo = MakeDirLight("IconFill", new Vector3(v.Pitch + 10f, v.Yaw, 0f), Color.white, 0.95f);
                }
                DynamicGI.UpdateEnvironment();

                // ── Camera ──────────────────────────────────────────────────────────────────────────────────
                camGo = new GameObject("IconCam");
                var cam = camGo.AddComponent<Camera>();
                var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
                camData.renderPostProcessing = false; // see the caution ledger in the class doc
                camData.renderShadows = false;        // a 52px icon gains nothing and shadow acne costs pixels
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = v.Bg;
                cam.fieldOfView = 28f;
                cam.allowMSAA = true;
                FrameSubject(cam, subject, v.Yaw, v.Pitch);

                foreach (int size in new[] { IconSize, RefSize })
                {
                    string file = subjectId + "__" + v.Id + "__" + size.ToString(CultureInfo.InvariantCulture) + ".png";
                    string path = Path.Combine(outDir, file);
                    if (RenderToPng(cam, size, size, path, out string renderPath))
                    {
                        written++;
                        manifest.AppendLine(string.Join("|", subjectId, v.Id,
                                                        size.ToString(CultureInfo.InvariantCulture), file,
                                                        verts.ToString(CultureInfo.InvariantCulture),
                                                        tris.ToString(CultureInfo.InvariantCulture),
                                                        renderPath, SystemInfo.graphicsDeviceType.ToString()));
                        Debug.Log("[IconBaker] baked subject=" + subjectId + " variant=" + v.Id +
                                  " size=" + size + " renderPath=" + renderPath + " -> " + path);
                    }
                    else
                    {
                        Debug.LogError("[IconBaker] render FAILED subject=" + subjectId + " variant=" + v.Id +
                                       " size=" + size);
                    }
                }
            }
            finally
            {
                DestroyNow(camGo); DestroyNow(keyGo); DestroyNow(rimGo); DestroyNow(fillGo); DestroyNow(subject);
                foreach (var o in temps) DestroyNow(o);
            }
            return written;
        }

        // ── Subjects — the ACTUAL in-world props (plus one flagged prototype stand-in) ──────────────────────

        private static GameObject BuildSubject(string subjectId, List<UnityEngine.Object> temps,
                                               out int verts, out int tris)
        {
            var root = new GameObject("IconSubject_" + subjectId);
            Shader vc = Shader.Find("FarHorizon/LowPolyVertexColor");
            if (vc == null)
                throw new Exception("[IconBaker] shader FarHorizon/LowPolyVertexColor not found — the icon " +
                                    "would bake magenta. Is the project's shader set imported?");

            verts = 0; tris = 0;
            switch (subjectId)
            {
                case "iron_ore_pile":
                case "iron_ore_pile_rust":
                {
                    // THE ACTUAL LOOTABLE PROP the player picks up: OrePile's 3-lump faceted cluster.
                    // Reached by reflection because OrePile.BuildOreClusterMesh is `private static` in the
                    // Runtime asmdef. PRODUCTIONIZATION NOTE: a real IconBaker needs an explicit
                    // icon-subject seam (e.g. a static mesh-factory accessor or a subject registry) — a
                    // prototype may reflect, production must not.
                    Mesh mesh = InvokePrivateStaticMesh(typeof(FarHorizon.OrePile), "BuildOreClusterMesh");
                    temps.Add(mesh);
                    // The `_rust` sibling pairs the SAME shipped pile mesh with the SAME shipped ore-VEIN
                    // material instead of the rock material. Rationale (round-1 self-review): the shipped pile
                    // is wired to `rockMat` (MovementCameraScene.cs:2953), so a faithful icon of the pickup
                    // reads as generic GREY stone — it cannot say "IRON ore", which is precisely the ticket's
                    // complaint. Both materials already exist in the shipped scene; only the pairing differs.
                    // This variant asks the Sponsor the real question, and flags a possible world-side
                    // follow-up (should the looted pile itself carry rust?).
                    bool rust = subjectId == "iron_ore_pile_rust";
                    var mat = MakeTinted(vc, rust ? "IconOreVeinMat" : "IconOreRockMat",
                                         rust ? OreVeinRust : OreRockGrey);
                    temps.Add(mat);
                    AddRenderer(root, "OrePileVisual", mesh, mat, Vector3.zero);
                    verts = mesh.vertexCount; tris = mesh.triangles.Length / 3;
                    break;
                }

                case "iron_ore_veined":
                {
                    // THE ACTUAL IN-WORLD ORE NODE recipe (MovementCameraScene.BuildOreNodeVisual:2997-3037):
                    // a grey FacetedRock body + 3 rusty FacetedRock vein lumps on the upper surface. Included
                    // as a variant because the LOOTED pile above carries the GREY rock material only — see the
                    // report: the pile alone reads as generic stone, the veins are what say "IRON ore".
                    const float rockRadius = 0.58f;
                    const int seed = 86300;
                    var rng = new System.Random(seed);
                    // BuildOreNodeVisual draws one yaw from the same rng before the veins — replay it so the
                    // vein placement matches the shipped node exactly.
                    rng.NextDouble();

                    Mesh body = LowPolyMeshes.FacetedRock(rockRadius, 0.42f, seed); temps.Add(body);
                    var rockMat = MakeTinted(vc, "IconOreRockMat", OreRockGrey); temps.Add(rockMat);
                    AddRenderer(root, "OreRock", body, rockMat, Vector3.zero);
                    verts += body.vertexCount; tris += body.triangles.Length / 3;

                    var veinMat = MakeTinted(vc, "IconOreVeinMat", OreVeinRust); temps.Add(veinMat);
                    for (int i = 0; i < 3; i++)
                    {
                        float va = (float)(rng.NextDouble() * Math.PI * 2.0);
                        float vr = rockRadius * 0.55f;
                        var vpos = new Vector3(Mathf.Cos(va) * vr,
                                               rockRadius * (0.35f + 0.35f * (float)rng.NextDouble()),
                                               Mathf.Sin(va) * vr);
                        Mesh vm = LowPolyMeshes.FacetedRock(0.15f + 0.05f * (float)rng.NextDouble(), 0.5f,
                                                            seed + 991 + i);
                        temps.Add(vm);
                        AddRenderer(root, "Vein" + i, vm, veinMat, vpos);
                        verts += vm.vertexCount; tris += vm.triangles.Length / 3;
                    }
                    break;
                }

                case "iron_ingot_proto":
                {
                    // ⚠ PROTOTYPE STAND-IN — there is NO iron-ingot mesh anywhere in the project (verified: the
                    // forge adds the ingot straight to the pack, `Forge.cs:327-330`; no `*ingot*` mesh/FBX/blend
                    // exists). This is a minimal in-code faceted bar so the Sponsor has something to judge; a
                    // shipped ingot prop belongs on the Blender route (asset-routing.md: hero/held props →
                    // blender-asset-pipeline.md, shared palette material, faceted-chunky).
                    Mesh mesh = IngotBarMesh(); temps.Add(mesh);
                    var mat = MakeTinted(vc, "IconIronIngotMat", IronIngotGrey); temps.Add(mat);
                    AddRenderer(root, "IngotVisual", mesh, mat, Vector3.zero);
                    verts = mesh.vertexCount; tris = mesh.triangles.Length / 3;
                    break;
                }

                default:
                    throw new Exception("[IconBaker] unknown subject id '" + subjectId + "'");
            }
            return root;
        }

        private static Mesh InvokePrivateStaticMesh(Type t, string method)
        {
            var mi = t.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic);
            if (mi == null)
                throw new Exception("[IconBaker] " + t.Name + "." + method + " not found — the prototype's " +
                                    "reflection hook is stale. FIX THE SEAM, do not guess a substitute mesh " +
                                    "(a substitute would make the sheet a render of something the player " +
                                    "never sees).");
            var mesh = mi.Invoke(null, null) as Mesh;
            if (mesh == null)
                throw new Exception("[IconBaker] " + t.Name + "." + method + " returned null.");
            return mesh;
        }

        private static void AddRenderer(GameObject root, string name, Mesh mesh, Material mat, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Material MakeTinted(Shader vc, string name, Color tint)
        {
            var m = new Material(vc) { name = name };
            if (m.HasProperty("_Tint")) m.SetColor("_Tint", tint);
            return m;
        }

        private static GameObject MakeDirLight(string name, Vector3 euler, Color c, float intensity)
        {
            var go = new GameObject(name);
            go.transform.rotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = c;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return go;
        }

        // ── The prototype ingot mesh ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// REAL-WORLD ANCHOR (lowpoly-quality.md §0): an iron ingot is a small CAST BAR of metal — a solid
        /// trapezoidal block that rests FLAT on its WIDE base, with a smaller flat top face and four gently
        /// sloping sides. It is low and long (a bar, not a cube or a spike), blunt-edged, and one-hand
        /// liftable. The SIDE PROFILE must therefore read as a trapezoid that is WIDER AT THE BOTTOM.
        ///
        /// Three rings (base → shoulder → top plateau) give: 1 bottom cap, 4 sloping sides, 4 chamfer faces,
        /// 1 top cap = 10 quads = 20 tris. Distinct verts per face carry explicit flat per-face normals (the
        /// house faceted pattern — lowpoly-quality.md §1: never RecalculateNormals a flat-shaded mesh), and
        /// per-face vertex-colour VALUE steps act as the light proxy (the shader does
        /// <c>albedo = IN.color.rgb * _Tint.rgb</c>). The chamfer band is the BRIGHTEST value — the discrete
        /// "caught-sun" edge polygon of lowpoly-quality.md Rec 5, done in code for the prototype.
        ///
        /// Outward winding is ENFORCED (not assumed) — an inward-wound flat-shaded face is silently culled by
        /// URP `Cull Back` (unity-conventions.md §Low-poly mesh patterns, the −Z grid + FacetedRock bugs).
        /// </summary>
        public static Mesh IngotBarMesh()
        {
            // Half-extents per ring. Base 0.300 x 0.170, top plateau 0.208 x 0.092, height 0.100 → a low,
            // long bar with an unmistakable wide-bottom trapezoid silhouette.
            Vector3[] r0 = Ring(0.150f, 0.085f, 0.000f); // base (widest) — sits ON the ground plane, y=0
            Vector3[] r1 = Ring(0.118f, 0.058f, 0.086f); // shoulder
            Vector3[] r2 = Ring(0.104f, 0.046f, 0.100f); // top plateau

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();

            // Bottom cap — reverse of the top-cap order so its normal points −Y.
            AddQuad(verts, norms, cols, tris, r0[0], r0[3], r0[2], r0[1], 0.58f);

            // Side band r0 → r1. ±X faces slightly brighter than ±Z for facet-to-facet variation.
            for (int i = 0; i < 4; i++)
            {
                float value = (i % 2 == 0) ? 0.76f : 0.86f;
                AddQuad(verts, norms, cols, tris, r0[i], r0[(i + 1) % 4], r1[(i + 1) % 4], r1[i], value);
            }

            // Chamfer band r1 → r2 — the brightest "caught-sun" edge polygons.
            for (int i = 0; i < 4; i++)
                AddQuad(verts, norms, cols, tris, r1[i], r1[(i + 1) % 4], r2[(i + 1) % 4], r2[i], 1.00f);

            // Top cap.
            AddQuad(verts, norms, cols, tris, r2[0], r2[1], r2[2], r2[3], 0.94f);

            // ── Outward-winding enforcement (house pattern, lowpoly-quality.md §1) ──────────────────────────
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < verts.Count; i++) centre += verts[i];
            centre /= Mathf.Max(1, verts.Count);
            int flips = 0;
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                Vector3 fn = Vector3.Cross(b - a, c - a).normalized;
                Vector3 fc = (a + b + c) / 3f;
                if (Vector3.Dot(fn, fc - centre) < 0f)
                {
                    int tmp = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = tmp;
                    // Re-bake the three verts' normals to the corrected face normal.
                    Vector3 corrected = -fn;
                    norms[tris[t]] = corrected; norms[tris[t + 1]] = corrected; norms[tris[t + 2]] = corrected;
                    flips++;
                }
            }
            if (flips > 0)
                Debug.LogWarning("[IconBaker] IngotBarMesh flipped " + flips + " inward-wound faces — the " +
                                 "hand-authored quad order regressed; fix the order, don't rely on the pass.");

            var mesh = new Mesh { name = "IronIngotBar_proto" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3[] Ring(float hx, float hz, float y)
        {
            // The corner order that yields a +Y normal for a cap quad emitted as (p0,p1,p2,p3).
            return new[]
            {
                new Vector3(-hx, y, -hz),
                new Vector3(-hx, y,  hz),
                new Vector3( hx, y,  hz),
                new Vector3( hx, y, -hz),
            };
        }

        private static void AddQuad(List<Vector3> verts, List<Vector3> norms, List<Color> cols, List<int> tris,
                                    Vector3 a, Vector3 b, Vector3 c, Vector3 d, float value)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            var col = new Color(value, value, value, 1f); // alpha 1 — _AOStrength defaults to 0, so unused
            int bi = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            for (int i = 0; i < 4; i++) { norms.Add(n); cols.Add(col); }
            tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);
            tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 3);
        }

        // ── Framing + readback ──────────────────────────────────────────────────────────────────────────────

        // Fit the camera to the subject's PROJECTED SILHOUETTE, not its bounding sphere. Fit-to-slot IS the
        // icon requirement (see the class doc on why PR #39's zoom-to-fit caution does not apply to icon
        // bakes) — but round 1 fitted the bounding SPHERE, which over-pads badly for a flat/wide subject: the
        // ore pile came back filling only ~12% of the frame (measured coverage), i.e. a tiny cluster of bits
        // inside a mostly-empty 52px slot. Fitting the projected silhouette makes every subject fill the same
        // fraction of the frame regardless of its aspect, which is what a slot sprite needs.
        //
        // Iterative because the perspective projection of an off-centre silhouette has no closed form: place
        // the camera, measure the worst-case normalised screen offset, scale the distance by the miss, repeat.
        // Converges in 2-3 passes; 6 is belt-and-suspenders.
        private static void FrameSubject(Camera cam, GameObject subject, float yaw, float pitch)
        {
            Bounds b = CombinedBounds(subject);
            float radius = Mathf.Max(1e-3f, b.extents.magnitude);
            Vector3 dir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
            Vector3[] pts = WorldVertices(subject);

            cam.aspect = 1f; // pin it — a batchmode camera with no target has no reliable game-view aspect
            const float target = 0.88f; // fill 88% of the half-extent → a small, even margin on all sides
            float dist = radius * 3f;

            for (int iter = 0; iter < 6; iter++)
            {
                cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                cam.transform.position = b.center - dir * dist;
                cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 4f);
                cam.farClipPlane = dist + radius * 8f;

                float worst = 0f;
                bool anyBehind = false;
                for (int i = 0; i < pts.Length; i++)
                {
                    Vector3 vp = cam.WorldToViewportPoint(pts[i]);
                    if (vp.z <= 0.001f) { anyBehind = true; break; }
                    worst = Mathf.Max(worst, Mathf.Max(Mathf.Abs(vp.x * 2f - 1f), Mathf.Abs(vp.y * 2f - 1f)));
                }
                if (anyBehind) { dist *= 1.6f; continue; }
                if (worst <= 1e-4f) break;
                float ratio = worst / target;
                if (Mathf.Abs(ratio - 1f) < 0.01f) break;
                dist *= ratio;
            }

            // Final placement at the converged distance.
            cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            cam.transform.position = b.center - dir * dist;
            cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 4f);
            cam.farClipPlane = dist + radius * 8f;
        }

        private static Vector3[] WorldVertices(GameObject root)
        {
            var all = new List<Vector3>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) all.Add(m.MultiplyPoint3x4(v));
            }
            return all.ToArray();
        }

        private static Bounds CombinedBounds(GameObject root)
        {
            var rends = root.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return new Bounds(root.transform.position, Vector3.one * 0.1f);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        /// <summary>
        /// Offscreen RT readback WITH ALPHA. Deliberately NOT
        /// <see cref="FarHorizon.RenderTextureCapture.CaptureCameraToTexture"/>: that helper reads back into an
        /// <c>RGB24</c> texture, which DISCARDS the alpha channel — and a transparent-background icon is
        /// exactly an alpha deliverable. Same SubmitRenderRequest-preferred structure, ARGB32 readback.
        /// PRODUCTIONIZATION NOTE: production should add an alpha-capable overload to the shared helper rather
        /// than keeping this fork.
        /// </summary>
        private static bool RenderToPng(Camera cam, int width, int height, string path, out string renderPath)
        {
            renderPath = "none";
            // SUPERSAMPLE instead of MSAA: render at SS× then alpha-weighted box-downsample on the CPU. MSAA on
            // an RT destination handed to SubmitRenderRequest is not something this project has proven, and an
            // MSAA resolve during ReadPixels is an extra unproven step; a 4× box downsample is deterministic,
            // needs no engine support, and gives cleaner edges on a 64px faceted silhouette. The alpha-weighted
            // (premultiplied) resolve is what keeps a transparent-background icon free of a dark halo.
            const int SS = 4;
            int rw = width * SS, rh = height * SS;
            var rt = new RenderTexture(rw, rh, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            RenderTexture prevActive = RenderTexture.active;
            var tex = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request))
                {
                    RenderPipeline.SubmitRenderRequest(cam, request);
                    renderPath = "SubmitRenderRequest";
                }
                else
                {
                    // Visible downgrade, never silent — and note the PR #21 multi-submesh caution in the class
                    // doc applies to THIS path specifically.
                    Debug.LogWarning("[IconBaker] SubmitRenderRequest unsupported — falling back to " +
                                     "cam.Render() (see the PR #21 multi-submesh caution). device=" +
                                     SystemInfo.graphicsDeviceType);
                    RenderTexture prevTarget = cam.targetTexture;
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = prevTarget;
                    renderPath = "cam.Render";
                }

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rw, rh), 0, 0);
                tex.Apply();

                Color32[] src = tex.GetPixels32();
                var dst = new Color32[width * height];
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float aSum = 0f, rSum = 0f, gSum = 0f, bSum = 0f;
                    for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        Color32 c = src[(y * SS + sy) * rw + (x * SS + sx)];
                        float a = c.a / 255f;
                        aSum += a; rSum += c.r * a; gSum += c.g * a; bSum += c.b * a;
                    }
                    int n = SS * SS;
                    float inv = aSum > 1e-4f ? 1f / aSum : 0f;
                    dst[y * width + x] = new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(rSum * inv), 0, 255),
                                                     (byte)Mathf.Clamp(Mathf.RoundToInt(gSum * inv), 0, 255),
                                                     (byte)Mathf.Clamp(Mathf.RoundToInt(bSum * inv), 0, 255),
                                                     (byte)Mathf.Clamp(Mathf.RoundToInt(aSum / n * 255f), 0, 255));
                }

                var outTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    outTex.SetPixels32(dst);
                    outTex.Apply(false, false);
                    byte[] png = outTex.EncodeToPNG();
                    string full = Path.GetFullPath(path);
                    Directory.CreateDirectory(Path.GetDirectoryName(full));
                    File.WriteAllBytes(full, png);
                }
                finally { DestroyNow(outTex); }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[IconBaker] readback failed: " + e);
                return false;
            }
            finally
            {
                RenderTexture.active = prevActive;
                DestroyNow(tex);
                rt.Release();
                DestroyNow(rt);
            }
        }

        // ── CLI + misc ──────────────────────────────────────────────────────────────────────────────────────

        // Normalize with Path.GetFullPath at PARSE time — a relative -captureDir/-iconOutDir silently writes
        // PNGs against the Unity process CWD (ticket 86caa9zpp / PR #226).
        private static string ResolveOutDir()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-iconOutDir") return Path.GetFullPath(args[i + 1]);
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "art-src", "iconbaker-proto"));
        }

        private static void DestroyNow(UnityEngine.Object o)
        {
            if (o == null) return;
            UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
