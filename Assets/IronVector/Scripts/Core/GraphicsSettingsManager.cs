using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IronVector
{
    /// <summary>Runtime URP graphics quality, FPS cap and VSync manager driven by LocalProfile.</summary>
    public static class GraphicsSettingsManager
    {
        public static readonly string[] PresetNames = { "Low (Max FPS)", "Medium", "High", "Ultra" };

        public static void Apply()
        {
            if (Application.isBatchMode) return;

            int q = Mathf.Clamp(LocalProfile.GraphicsQuality, 0, 3);
            QualitySettings.vSyncCount = LocalProfile.VSync ? 1 : 0;
            Application.targetFrameRate = LocalProfile.TargetFps <= 0 ? -1 : LocalProfile.TargetFps;

            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return;

            switch (q)
            {
                case 0: // Low
                    urp.renderScale = 0.78f;
                    urp.msaaSampleCount = 1;
                    urp.shadowDistance = 55f;
                    urp.shadowCascadeCount = 1;
                    urp.mainLightShadowmapResolution = 1024;
                    break;
                case 1: // Medium
                    urp.renderScale = 0.90f;
                    urp.msaaSampleCount = 2;
                    urp.shadowDistance = 90f;
                    urp.shadowCascadeCount = 2;
                    urp.mainLightShadowmapResolution = 2048;
                    break;
                case 2: // High
                    urp.renderScale = 1.00f;
                    urp.msaaSampleCount = 4;
                    urp.shadowDistance = 125f;
                    urp.shadowCascadeCount = 2;
                    urp.mainLightShadowmapResolution = 2048;
                    break;
                case 3: // Ultra
                    urp.renderScale = 1.15f;
                    urp.msaaSampleCount = 4;
                    urp.shadowDistance = 175f;
                    urp.shadowCascadeCount = 4;
                    urp.mainLightShadowmapResolution = 4096;
                    break;
            }
        }
    }
}
