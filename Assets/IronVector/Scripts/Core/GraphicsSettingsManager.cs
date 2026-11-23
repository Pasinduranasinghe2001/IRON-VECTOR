using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IronVector
{
    /// <summary>Runtime URP graphics quality, brightness exposure, FPS cap and VSync manager driven by LocalProfile.</summary>
    public static class GraphicsSettingsManager
    {
        public static readonly string[] PresetNames = { "Low (Max FPS)", "Medium", "High", "Ultra" };

        public static void Apply()
        {
            if (Application.isBatchMode) return;

            int q = Mathf.Clamp(LocalProfile.GraphicsQuality, 0, 3);
            QualitySettings.vSyncCount = LocalProfile.VSync ? 1 : 0;
            Application.targetFrameRate = LocalProfile.TargetFps <= 0 ? -1 : LocalProfile.TargetFps;

            Volume vol = Object.FindAnyObjectByType<Volume>();
            if (vol != null && vol.sharedProfile != null && vol.sharedProfile.TryGet<ColorAdjustments>(out ColorAdjustments adj))
            {
                adj.postExposure.Override(LocalProfile.Brightness);
            }

            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return;

            switch (q)
            {
                case 0: // Low
                    urp.renderScale = 0.80f;
                    urp.msaaSampleCount = 1;
                    urp.shadowDistance = 55f;
                    urp.shadowCascadeCount = 1;
                    urp.mainLightShadowmapResolution = 1024;
                    break;
                case 1: // Medium
                    urp.renderScale = 0.92f;
                    urp.msaaSampleCount = 2;
                    urp.shadowDistance = 95f;
                    urp.shadowCascadeCount = 2;
                    urp.mainLightShadowmapResolution = 2048;
                    break;
                case 2: // High
                    urp.renderScale = 1.00f;
                    urp.msaaSampleCount = 4;
                    urp.shadowDistance = 135f;
                    urp.shadowCascadeCount = 2;
                    urp.mainLightShadowmapResolution = 2048;
                    break;
                case 3: // Ultra
                    urp.renderScale = 1.15f;
                    urp.msaaSampleCount = 4;
                    urp.shadowDistance = 185f;
                    urp.shadowCascadeCount = 4;
                    urp.mainLightShadowmapResolution = 4096;
                    break;
            }
        }
    }
}
