using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarHorizon.EditorTools
{
    /// <summary>
    /// DIAGNOSTIC (ticket 86cajt6jb — WorldLook Sky fog seam). Dumps the ground-truth fog colour the
    /// runtime WorldLook Sky PlayMode seam-kill test reads: (1) the COMMITTED Boot.unity RenderSettings.fogColor
    /// as it sits on disk (what a bare LoadScene sees), and (2) the value a FRESH BootstrapProject.Run bakes
    /// (what the CI playmode job — which re-bootstraps first — sees). Answers AC1: is the committed value stale
    /// vs the palette (0.80 R) or does a writer set 0.42?
    ///
    /// Run: Unity -batchmode -quit -projectPath . -executeMethod FarHorizon.EditorTools.FogSeamDump.DumpCommitted
    ///  and Unity -batchmode -quit -projectPath . -executeMethod FarHorizon.EditorTools.FogSeamDump.DumpAfterBootstrap
    /// </summary>
    public static class FogSeamDump
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        public static void DumpCommitted()
        {
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            LogFog("COMMITTED-ON-DISK");
        }

        public static void DumpAfterBootstrap()
        {
            BootstrapProject.Run();
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            LogFog("AFTER-BOOTSTRAP-RESAVE");
        }

        private static void LogFog(string tag)
        {
            Color fog = RenderSettings.fogColor;
            var sky = RenderSettings.skybox;
            string horiz = (sky != null && sky.HasProperty("_HorizonColor"))
                ? sky.GetColor("_HorizonColor").ToString("F4") : "<no _HorizonColor>";
            Color pal = FarHorizon.WorldLookPalette.SkyHorizon;
            Debug.Log($"[fog-seam-dump] {tag} fog={fog.ToString("F4")} fogOn={RenderSettings.fog} " +
                      $"skybox._HorizonColor={horiz} palette.SkyHorizon={pal.ToString("F4")} " +
                      $"seamKillR_ok={(Mathf.Abs(fog.r - pal.r) <= 0.02f)}");
        }
    }
}
