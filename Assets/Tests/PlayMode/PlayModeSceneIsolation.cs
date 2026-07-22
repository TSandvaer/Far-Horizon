using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FarHorizon.PlayTests
{
    /// <summary>
    /// Shared PlayMode scene-isolation helper — the ROOT-CAUSE FIX for the long-standing advisory
    /// PlayMode-job hang (ticket 86cabfa21).
    ///
    /// Many locomotion / scene fixtures need a FRESH empty scene as the ONLY loaded scene, so a
    /// leaked "Boot" scene's Ground colliders can't foul a snap-raycast (cross-fixture leak fix).
    /// The old INLINE idiom every such fixture copied —
    ///     var empty = SceneManager.CreateScene(...); SceneManager.SetActiveScene(empty);
    ///     for each other loaded scene: op = SceneManager.UnloadSceneAsync(s);
    ///                                 while (!op.isDone) yield return null;   // UNBOUNDED
    /// HANGS THE WHOLE PLAYMODE RUN headlessly. Under `-batchmode -runTests -testPlatform PlayMode`
    /// the loaded scenes at [UnitySetUp] time include the test framework's OWN "InitTestScene…", and
    /// `SceneManager.UnloadSceneAsync` on THAT scene NEVER completes (it hosts the running test
    /// controller — Unity cannot unload it mid-run), so the unbounded `while (!op.isDone)` spins
    /// forever. Because the first ALPHABETICAL fixture with this idiom (AirborneAirControl) runs
    /// immediately after play-mode-enter, the hang LOOKED like a play-mode-enter deadlock — the exact
    /// symptom that got the whole PlayMode suite quarantined onto the cancellable advisory job.
    /// (Devon bisected it to this line on 2026-07-03; it is NOT EnterPlayModeOptions / UUM-142421 /
    /// an Android module — all three prior guesses were refuted against ground truth.)
    ///
    /// This helper does the isolation SAFELY and is the single place future fixtures should call:
    ///   * SKIP any "InitTestScene*" scene — never unload the test framework's own scene.
    ///   * BOUND every unload wait (5s wall-clock) — a never-completing unload can never hang again.
    /// Use `yield return PlayModeSceneIsolation.IsolateInFreshScene("MyTag");` in a [UnitySetUp].
    /// </summary>
    public static class PlayModeSceneIsolation
    {
        /// <summary>
        /// Create a fresh, uniquely-named empty scene, make it the active scene, and unload every
        /// OTHER real loaded scene — but NEVER the test framework's InitTestScene, and always with a
        /// bounded wait so a stuck unload can't hang the run.
        /// </summary>
        public static IEnumerator IsolateInFreshScene(string tag)
        {
            var empty = SceneManager.CreateScene(tag + "_" + System.Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                // NEVER unload the test framework's own InitTestScene: its async unload never
                // completes headless (it hosts the running test controller), so a wait on it hangs
                // the whole PlayMode run forever — the 86cabfa21 root cause.
                if (s == empty || !s.isLoaded || s.name.StartsWith("InitTestScene")) continue;
                var op = SceneManager.UnloadSceneAsync(s);
                if (op != null)
                {
                    float t0 = Time.realtimeSinceStartup;
                    while (!op.isDone && Time.realtimeSinceStartup - t0 < 5f) yield return null;
                    // 86cajk7vb (Drew's #249 NIT): the 5s cap used to continue SILENTLY on a stuck unload, so a
                    // never-completing UnloadSceneAsync degraded to "isolation didn't fully happen" with NO signal
                    // — the next fixture could see a leaked scene's colliders. Surface the timeout so a future
                    // regression of the 86cabfa21 hang class is VISIBLE in the log instead of silent.
                    if (!op.isDone)
                        Debug.LogWarning($"[PlayModeSceneIsolation] '{tag}': UnloadSceneAsync of scene '{s.name}' " +
                            "did not complete within the 5s bound — the scene stays loaded (isolation incomplete). " +
                            "This is the bounded-wait backstop for the 86cabfa21 unbounded-unload hang.");
                }
            }
        }
    }
}
