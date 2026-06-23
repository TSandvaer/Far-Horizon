using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// PlayMode coverage for the 86cabh907 SHAFT-LENGTH PICKER (HeldAxeLengthPicker) — the unstick instrument
    /// that lets the Sponsor PICK the haft length in-hand instead of us guessing.
    ///
    /// CATCHES THE BUG CLASS, not the instance. The #100 head-resize DIAL passed unit tests but NO-OPPED at
    /// runtime (it deformed mesh verts and the rig stomped it / it only touched the data layer). So the
    /// load-bearing assertion here is NOT "the index advanced" — it is that the VISIBLE holder MeshFilter's
    /// sharedMesh REFERENCE actually changed to the variant mesh AND that change SURVIVES a HeldToolRig.LateUpdate
    /// frame (the same false-green family — a data-layer assert greens while the rig silently restores the mesh).
    /// The picker reuses the PROVEN mesh-REFERENCE swap (the weapon cycle), never a vert deform, so it must pass
    /// this where the dial failed.
    ///
    /// The harness has no Resources/AxeLengthVariants.prefab, so the four variant meshes are injected via
    /// reflection into the picker's resolved-variants array (the same pattern the dial tests use for the lineup).
    /// </summary>
    public class HeldAxeLengthPickerPlayModeTests
    {
        private GameObject _go;
        private HeldWeaponCycleDebug _cycle;
        private HeldAxeLengthPicker _picker;
        private Mesh[] _variantMeshes;

        [TearDown]
        public void TearDown()
        {
            if (_variantMeshes != null)
                foreach (var m in _variantMeshes) if (m != null) Object.Destroy(m);
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name.Contains("synthetic"))
                    Object.Destroy(mf.sharedMesh);
            if (_go != null) Object.Destroy(_go);
        }

        private static Mesh MakeAxeMesh(string name, float haftBottomY)
        {
            // A minimal axe-like mesh: a head box up top (y 1.4..2.0) + a haft column whose BOTTOM is
            // haftBottomY (longer haft = lower bottom). The four variants differ ONLY in haftBottomY, exactly
            // like the real length variants differ only in haft length.
            var m = new Mesh { name = name };
            m.vertices = new[]
            {
                new Vector3(0f, haftBottomY, 0f), new Vector3(0f, 1.1f, 0f),
                new Vector3(-0.3f, 1.4f, -0.06f), new Vector3(0.3f, 1.4f, -0.06f),
                new Vector3(0.3f, 2.0f, 0.06f), new Vector3(-0.3f, 2.0f, 0.06f),
            };
            m.triangles = new[] { 0, 1, 2, 2, 3, 4, 4, 5, 2 };
            m.RecalculateBounds();
            return m;
        }

        // Build the real runtime topology: MeshFilter on the rig-driven ROOT (single-node FBX collapse); the
        // cycle re-homes the mesh onto a child holder in Awake (#100), the picker shares that holder.
        private void BuildHeroAxeWithPicker()
        {
            _go = new GameObject("HeroAxe");
            var mf = _go.AddComponent<MeshFilter>();
            mf.sharedMesh = MakeAxeMesh("synthetic_axe_shipped", 0.0f); // shipped length (longest bottom-most)
            _go.AddComponent<MeshRenderer>();
            var hand = new GameObject("RightHand").transform;
            hand.position = new Vector3(2f, 1f, 0f);
            var rig = _go.AddComponent<HeldAxeRig>(); // drives the ROOT transform every LateUpdate
            rig.hand = hand;
            _cycle = _go.AddComponent<HeldWeaponCycleDebug>();
            _picker = _go.AddComponent<HeldAxeLengthPicker>();
        }

        // Inject the four variant meshes into the picker's resolved array + mark it resolved (no Resources prefab
        // in the harness). The four differ by haft length (distinct meshes, distinct references).
        private void InjectVariants()
        {
            _variantMeshes = new[]
            {
                MakeAxeMesh("synthetic_axe_len11", -0.10f),
                MakeAxeMesh("synthetic_axe_len12", -0.25f),
                MakeAxeMesh("synthetic_axe_len13", -0.40f),
                MakeAxeMesh("synthetic_axe_len14", -0.55f),
            };
            typeof(HeldAxeLengthPicker).GetField("_variantMeshes", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_picker, _variantMeshes);
            typeof(HeldAxeLengthPicker).GetField("_resolved", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_picker, true);
        }

        private MeshFilter Holder()
        {
            var f = typeof(HeldWeaponCycleDebug).GetField("_meshHolder", BindingFlags.NonPublic | BindingFlags.Instance);
            return (MeshFilter)f.GetValue(_cycle);
        }

        private void SetPickerIndex(int idx)
        {
            typeof(HeldAxeLengthPicker).GetField("_index", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_picker, idx);
        }

        private object Invoke(string method)
        {
            return typeof(HeldAxeLengthPicker).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_picker, null);
        }

        // (1) THE LOAD-BEARING TEST: applying a length variant actually SWAPS the visible holder mesh REFERENCE,
        // and that swap SURVIVES a HeldToolRig.LateUpdate frame (the #100 dial-no-op bug class).
        [UnityTest]
        public IEnumerator ApplyVariant_SwapsVisibleHolderMesh_AndSurvivesRigFrame()
        {
            BuildHeroAxeWithPicker();
            yield return null; // Awake re-homes the mesh onto a child holder; rig LateUpdate runs
            InjectVariants();

            var holder = Holder();
            Assert.IsNotNull(holder, "the cycle must own a mesh holder the picker shares");
            Assert.AreNotEqual(_go.transform, holder.transform,
                "#100: the displayed mesh must be on a CHILD holder the rig never touches");

            Mesh shippedMesh = holder.sharedMesh; // the shipped (pre-pick) mesh

            // Pick variant index 2 (1.3x) and apply.
            SetPickerIndex(2);
            Invoke("ApplyVariant");

            // The VISIBLE holder mesh reference must now be the variant mesh — not the shipped one, not merely a
            // data-layer change (the dial's false-green failure mode).
            Assert.AreSame(_variantMeshes[2], holder.sharedMesh,
                "the picked length variant mesh must be the one DISPLAYED on the holder (a real reference swap)");
            Assert.AreNotSame(shippedMesh, holder.sharedMesh,
                "after picking a variant the shipped axe mesh must no longer be displayed");

            // CRITICAL (#100 false-green family): let rig LateUpdate frames run — the swap must SURVIVE. The rig
            // drives the ROOT transform, not the holder mesh, so the variant mesh must persist; AND the picker's
            // Update re-asserts it if anything restored the shipped mesh.
            yield return null;
            yield return null;
            Assert.AreSame(_variantMeshes[2], holder.sharedMesh,
                "#100: the picked variant mesh must SURVIVE HeldToolRig.LateUpdate frames (not be stomped/restored)");
        }

        // (2) The picker re-asserts the chosen variant after the weapon-cycle restores the shipped axe mesh
        // (cycling axe->knife->...->axe re-applies the captured original). The picked length must survive that
        // round-trip — Update re-applies it the next frame.
        [UnityTest]
        public IEnumerator PickedVariant_SurvivesWeaponCycleRoundTrip()
        {
            BuildHeroAxeWithPicker();
            yield return null;
            InjectVariants();
            var holder = Holder();

            SetPickerIndex(1); // 1.2x
            Invoke("ApplyVariant");
            Assert.AreSame(_variantMeshes[1], holder.sharedMesh);

            // Simulate the weapon cycle restoring the shipped axe mesh on the holder (what ApplyCurrent does on
            // index 0): stomp the holder back to a different mesh.
            var shippedAgain = MakeAxeMesh("synthetic_axe_restored", 0.0f);
            holder.sharedMesh = shippedAgain;
            Assert.AreNotSame(_variantMeshes[1], holder.sharedMesh, "precondition: the holder was stomped off the variant");

            // The picker's Update must re-assert the picked variant within a frame (IsAxeHeld is true: index 0).
            yield return null;
            Assert.AreSame(_variantMeshes[1], holder.sharedMesh,
                "the picker must RE-ASSERT the chosen length after the weapon-cycle restored the shipped mesh");
            Object.Destroy(shippedAgain);
        }

        // (3) The picker is INERT when a NON-axe weapon is held (knife/sword/spear have no shaft variants).
        [UnityTest]
        public IEnumerator Picker_IsInert_WhenNonAxeHeld()
        {
            BuildHeroAxeWithPicker();
            yield return null;
            InjectVariants();
            var holder = Holder();

            // Select a non-axe weapon on the cycle (knife = index 1).
            typeof(HeldWeaponCycleDebug).GetField("_index", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_cycle, 1);
            Assert.IsFalse(_cycle.IsAxeHeld, "precondition: a non-axe weapon is held");

            Mesh knifeMesh = holder.sharedMesh;
            SetPickerIndex(2);
            // The Update re-assert is gated on IsAxeHeld — run a frame; the holder must NOT be forced to a variant.
            yield return null;
            Assert.AreSame(knifeMesh, holder.sharedMesh,
                "the length picker must be INERT while a non-axe weapon is held (no shaft variants for knife/sword/spear)");
        }

        // (4) The contract arrays line up: one node name, one length factor, one ratio per variant; the cycle key
        // is a layout-safe LETTER (Danish-keyboard-safe — never US punctuation). Catches a drift between the
        // baked variants and the picker's surfaced numbers, and a rebind onto a punctuation key.
        [Test]
        public void Contract_ArraysAligned_AndKeyIsLayoutSafe()
        {
            Assert.AreEqual(4, HeldAxeLengthPicker.VariantNodeNames.Length, "expect 4 length variants");
            Assert.AreEqual(HeldAxeLengthPicker.VariantNodeNames.Length, HeldAxeLengthPicker.VariantLengthFactor.Length,
                "one length factor per variant node");
            Assert.AreEqual(HeldAxeLengthPicker.VariantNodeNames.Length, HeldAxeLengthPicker.VariantHaftHeadRatio.Length,
                "one haft:head ratio per variant node");
            // factors are the brief's 1.1/1.2/1.3/1.4, strictly increasing.
            Assert.AreEqual(new[] { 1.1f, 1.2f, 1.3f, 1.4f }, HeldAxeLengthPicker.VariantLengthFactor);
            for (int i = 1; i < HeldAxeLengthPicker.VariantHaftHeadRatio.Length; i++)
                Assert.Greater(HeldAxeLengthPicker.VariantHaftHeadRatio[i], HeldAxeLengthPicker.VariantHaftHeadRatio[i - 1],
                    "haft:head ratio must increase with length");

            var go = new GameObject("HeroAxe");
            go.AddComponent<MeshFilter>();
            var picker = go.AddComponent<HeldAxeLengthPicker>();
            // The cycle key must be a LETTER (A..Z) — layout-safe on the Sponsor's Danish keyboard; never the
            // US-position punctuation keys that shift on Danish.
            Assert.IsTrue(picker.lengthCycleKey >= KeyCode.A && picker.lengthCycleKey <= KeyCode.Z,
                "the length-cycle key must be a layout-safe LETTER (Danish-keyboard-safe), got " + picker.lengthCycleKey);
            foreach (var punct in new[] { KeyCode.Semicolon, KeyCode.Quote, KeyCode.LeftBracket, KeyCode.RightBracket,
                KeyCode.Equals, KeyCode.Minus, KeyCode.Comma, KeyCode.Period, KeyCode.Slash })
                Assert.AreNotEqual(punct, picker.lengthCycleKey,
                    "the length-cycle key must NOT be US-position punctuation (shifts on Danish layout)");
            // Must not collide with the always-on weapon-cycle [B] or arm-switch [N] or head dials [O]/[I].
            foreach (var reserved in new[] { KeyCode.B, KeyCode.N, KeyCode.O, KeyCode.I, KeyCode.K,
                KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D })
                Assert.AreNotEqual(reserved, picker.lengthCycleKey,
                    "the length-cycle key must not collide with a reserved key (got " + picker.lengthCycleKey + ")");
            Object.Destroy(go);
        }
    }
}
