using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FarHorizon;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// Regression guard for soak-fix #1 — "when he walks he is walking in the air" (ticket 86ca8rdkp).
    ///
    /// DIAGNOSE-VIA-TRACE (diagnostic-traces-before-hypothesized-fixes): the ground-trace (-groundTrace,
    /// 2026-06-15) OVERTURNED the ticket's hypothesis (a Walk-vs-Idle clip root-Y mismatch). Measured data:
    /// the WALK feet ride only +0.0005u above the IDLE feet (no float difference), and the sole sits at world
    /// Y 0.081 in BOTH states — but the VISIBLE Zone-D terrain under the player is at Y 0.020, so the feet
    /// float ~0.06u ABOVE the sand the player SEES. ROOT CAUSE: the NavMeshAgent grounds the player ROOT on
    /// the flat NavMesh collider (which rides above the dipping visual terrain); the FBX-origin feet hang
    /// above the visible surface. (Same family as "the asset is fine, the VIEW is the problem" — here it is
    /// the GROUND the agent stands on that diverges from the visible terrain.)
    ///
    /// FIX: CastawayCharacter.ApplyGroundSnap raycasts the Ground layer each frame and drives the avatar
    /// root's local Y so the feet plant on the VISIBLE terrain.
    ///
    /// THE BUG CLASS: the avatar feet must sit ON the visible terrain, regardless of where the agent's NavMesh
    /// ground point is. We reproduce the divergence directly: a player root parked ABOVE a visible terrain
    /// collider, with a real CastawayCharacter child. The guard asserts the feet end up AT the terrain (planted),
    /// and the deliberate-break half (snap off) leaves them floating — proving the snap is load-bearing.
    /// </summary>
    public class CastawayGroundSnapPlayModeTests
    {
        private GameObject _terrain;
        private GameObject _playerGo;
        private GameObject _avatarGo;
        private CastawayCharacter _castaway;

        // The agent grounds the player root HERE (above the visible terrain) — the float source.
        private const float RootY = 0.60f;
        // The visible terrain top sits HERE (below the root) — where the feet must end up.
        private const float TerrainY = 0.10f;

        private void BuildRig(bool snap)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            // A flat MeshCollider plane on the Ground layer whose surface is at TerrainY. A 10u quad collider.
            _terrain = new GameObject("VisibleTerrain");
            if (groundLayer >= 0) _terrain.layer = groundLayer;
            _terrain.transform.position = new Vector3(0f, TerrainY, 0f);
            var mf = _terrain.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-5f, 0f, -5f), new Vector3(5f, 0f, -5f),
                new Vector3(-5f, 0f, 5f), new Vector3(5f, 0f, 5f)
            };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;
            var col = _terrain.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;

            // The player root parked at the agent's (higher) ground Y. The avatar child hangs off it; without
            // the snap, the FBX-origin feet sit at RootY — floating RootY-TerrainY above the visible terrain.
            _playerGo = new GameObject("Player");
            _playerGo.transform.position = new Vector3(0f, RootY, 0f);

            _avatarGo = new GameObject("CastawayAvatar");
            _avatarGo.transform.SetParent(_playerGo.transform, false);
            _avatarGo.transform.localPosition = Vector3.zero; // FBX origin == feet, at the root Y (the float)
            _castaway = _avatarGo.AddComponent<CastawayCharacter>();
            _castaway.groundSnap = snap;
            _castaway.groundMask = groundLayer >= 0 ? (LayerMask)(1 << groundLayer) : (LayerMask)~0;

            // No modelPrefab: the ground-snap logic is independent of the instantiated mesh (it raycasts +
            // sets the avatar-root local Y). CastawayCharacter LogErrors on a null modelPrefab (a real error
            // in the game), so expect that one log rather than suppress the production guard.
            LogAssert.Expect(LogType.Error,
                "[CastawayCharacter] modelPrefab not wired — cannot build avatar");
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrain != null) Object.Destroy(_terrain);
            if (_playerGo != null) Object.Destroy(_playerGo);
        }

        // The CORE grounding guard: the avatar feet snap DOWN onto the visible terrain (Y≈TerrainY), not left
        // floating at the agent's higher root Y.
        [UnityTest]
        public IEnumerator AvatarFeet_SnapToVisibleTerrain_NotLeftFloatingAtAgentRoot()
        {
            BuildRig(snap: true);
            for (int i = 0; i < 40; i++) yield return null; // the snap lerps toward the target — let it settle

            float feetY = _avatarGo.transform.position.y;
            Assert.That(feetY, Is.EqualTo(TerrainY).Within(0.03f),
                $"the avatar feet must snap onto the VISIBLE terrain (Y≈{TerrainY}); got {feetY:F3}. " +
                $"Leaving them at the agent root Y ({RootY}) is the 'walking in the air' float.");

            // And the snap must have moved them DOWN by ~the float gap (not a no-op).
            Assert.That(_castaway.GroundSnapLocalY, Is.LessThan(-0.1f),
                $"the ground-snap must drive the avatar-root local Y DOWN onto the terrain " +
                $"(got {_castaway.GroundSnapLocalY:F3}); a near-zero snap means the fix regressed.");
        }

        // The deliberate-break half (success-test discipline): with the snap OFF the feet stay at the agent
        // root Y (the float). Proves the snap — not some other effect — is what grounds the feet.
        [UnityTest]
        public IEnumerator GroundSnapOff_FeetFloatAtAgentRoot_ProvingTheSnapIsLoadBearing()
        {
            BuildRig(snap: false);
            for (int i = 0; i < 40; i++) yield return null;

            float feetY = _avatarGo.transform.position.y;
            Assert.That(feetY, Is.EqualTo(RootY).Within(0.01f),
                $"with the snap OFF the feet stay at the agent root Y ({RootY}) — the float. Got {feetY:F3}.");
        }
    }
}
