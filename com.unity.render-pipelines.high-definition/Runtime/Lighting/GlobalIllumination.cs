using System;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.HighDefinition
{
    /// <summary>
    /// A volume component that holds settings for the global illumination (screen space and ray traced).
    /// </summary>
    [Serializable, VolumeComponentMenu("Lighting/Screen Space Global Illumination")]
    [HDRPHelpURLAttribute("Ray-Traced-Global-Illumination")]
    public sealed class GlobalIllumination : VolumeComponentWithQuality
    {
        bool UsesQualityMode()
        {
            // The default value is set to quality. So we should be in quality if not overriden or we have an override set to quality
            return !mode.overrideState || mode == RayTracingMode.Quality;
        }

        /// <summary>
        /// Enable screen space global illumination.
        /// </summary>
        [Tooltip("Enable screen space global illumination.")]
        public BoolParameter enable = new BoolParameter(false);

        /// <summary>
        /// The thickness of the depth buffer value used for the ray marching step
        /// </summary>
        [Tooltip("Controls the thickness of the depth buffer used for ray marching.")]
        public ClampedFloatParameter depthBufferThickness = new ClampedFloatParameter(0.1f, 0.0f, 0.5f);

        GlobalIllumination()
        {
            displayName = "Screen Space Global Illumination";
        }

        /// <summary>
        /// The number of steps that should be used during the ray marching pass.
        /// </summary>
        public int raySteps
        {
            get
            {
                if (!UsesQualitySettings())
                    return m_RaySteps.value;
                else
                    return GetLightingQualitySettings().SSGIRaySteps[(int)quality.value];
            }
            set { m_RaySteps.value = value; }
        }
        [SerializeField]
        [Tooltip("Controls the number of steps used for ray marching.")]
        private ClampedIntParameter m_RaySteps = new ClampedIntParameter(48, 32, 256);

        /// <summary>
        /// Defines the radius for the spatial filter
        /// </summary>
        public int filterRadius
        {
            get
            {
                if (!UsesQualitySettings())
                    return m_FilterRadius.value;
                else
                    return GetLightingQualitySettings().SSGIFilterRadius[(int)quality.value];
            }
            set { m_FilterRadius.value = value; }
        }
        [Tooltip("Filter Radius")]
        [SerializeField]
        private ClampedIntParameter m_FilterRadius = new ClampedIntParameter(2, 2, 16);

        /// <summary>
        /// Toggles ray traced global illumination.
        /// </summary>
        [Tooltip("Toggles ray traced global illumination.")]
        public BoolParameter rayTracing = new BoolParameter(false);

        /// <summary>
        /// Defines the layers that GI should include.
        /// </summary>
        [Tooltip("Defines the layers that GI should include.")]
        public LayerMaskParameter layerMask = new LayerMaskParameter(-1);

        /// <summary>
        /// The LOD Bias HDRP applies to textures in the global illumination.
        /// </summary>
        [Tooltip("The LOD Bias HDRP applies to textures in the global illumination. A higher value increases performance and makes denoising easier, but it might reduce visual fidelity.")]
        public ClampedIntParameter textureLodBias = new ClampedIntParameter(7, 0, 7);

        /// <summary>
        /// Controls the length of GI rays.
        /// </summary>
        public float rayLength
        {
            get
            {
                if (!UsesQualitySettings() || UsesQualityMode())
                    return m_RayLength.value;
                else
                    return GetLightingQualitySettings().RTGIRayLength[(int)quality.value];
            }
            set { m_RayLength.value = value; }
        }

        /// <summary>
        /// Controls the clamp of intensity.
        /// </summary>
        public float clampValue
        {
            get
            {
                if (!UsesQualitySettings() || UsesQualityMode())
                    return m_ClampValue.value;
                else
                    return GetLightingQualitySettings().RTGIClampValue[(int)quality.value];
            }
            set { m_ClampValue.value = value; }
        }

        /// <summary>
        /// Controls which version of the effect should be used.
        /// </summary>
        [Tooltip("Controls which version of the effect should be used.")]
        public RayTracingModeParameter mode = new RayTracingModeParameter(RayTracingMode.Quality);

        // Performance
        /// <summary>
        /// Defines if the effect should be evaluated at full resolution.
        /// </summary>
        public bool fullResolution
        {
            get
            {
                if (!UsesQualitySettings())
                    return m_FullResolution.value;
                else
                    return GetLightingQualitySettings().RTGIFullResolution[(int)quality.value];
            }
            set { m_FullResolution.value = value; }
        }

        /// <summary>
        /// Defines what radius value should be used to pre-filter the signal.
        /// </summary>
        public int upscaleRadius
        {
            get
            {
                if (!UsesQualitySettings())
                    return m_UpscaleRadius.value;
                else
                    return GetLightingQualitySettings().RTGIUpScaleRadius[(int)quality.value];
            }
            set { m_UpscaleRadius.value = value; }
        }

        // Quality
        /// <summary>
        /// Number of samples for evaluating the effect.
        /// </summary>
        [Tooltip("Number of samples for GI.")]
        public ClampedIntParameter sampleCount = new ClampedIntParameter(2, 1, 32);

        /// <summary>
        /// Number of bounces for evaluating the effect.
        /// </summary>
        [Tooltip("Number of bounces for GI.")]
        public ClampedIntParameter bounceCount = new ClampedIntParameter(1, 1, 8);

        // Filtering
        /// <summary>
        /// Defines if the ray traced global illumination should be denoised.
        /// </summary>
        public bool denoise
        {
            get
            {
                if (!UsesQualitySettings() || UsesQualityMode())
                    return m_Denoise.value;
                else
                    return GetLightingQualitySettings().RTGIDenoise[(int)quality.value];
            }
            set { m_Denoise.value = value; }
        }

        /// <summary>
        /// Defines if the denoiser should be evaluated at half resolution.
        /// </summary>
        public bool halfResolutionDenoiser
        {
            get
            {
                if (!UsesQualitySettings() || UsesQualityMode())
                    return m_HalfResolutionDenoiser.value;
                else
                    return GetLightingQualitySettings().RTGIHalfResDenoise[(int)quality.value];
            }
            set { m_HalfResolutionDenoiser.value = value; }
        }

        /// <summary>
        /// Controls the radius of the global illumination denoiser (First Pass).
        /// </summary>
        public float denoiserRadius
        {
            get
            {
                if (!UsesQualitySettings() || UsesQualityMode())
                    return m_DenoiserRadius.value;
                else
                    return GetLightingQualitySettings().RTGIDenoiserRadius[(int)quality.value];
            }
            set { m_DenoiserRadius.value = value; }
        }

        /// <summary>
        /// Defines if the second denoising pass should be enabled.
        /// </summary>
        public bool secondDenoiserPass
        {
            get
            {
                if (!UsesQualitySettings() || UsesQualityMode())
                    return m_SecondDenoiserPass.value;
                else
                    return GetLightingQualitySettings().RTGISecondDenoise[(int)quality.value];
            }
            set { m_SecondDenoiserPass.value = value; }
        }


        // RTGI
        [SerializeField, FormerlySerializedAs("rayLength")]
        private MinFloatParameter m_RayLength = new MinFloatParameter(50.0f, 0.01f);

        [SerializeField, FormerlySerializedAs("clampValue")]
        [Tooltip("Controls the clamp of intensity.")]
        private ClampedFloatParameter m_ClampValue = new ClampedFloatParameter(1.0f, 0.001f, 10.0f);

        [SerializeField, FormerlySerializedAs("fullResolution")]
        [Tooltip("Full Resolution")]
        private BoolParameter m_FullResolution = new BoolParameter(false);

        [SerializeField, FormerlySerializedAs("upscaleRadius")]
        [Tooltip("Upscale Radius")]
        private ClampedIntParameter m_UpscaleRadius = new ClampedIntParameter(2, 2, 4);

        [SerializeField, FormerlySerializedAs("denoise")]
        [Tooltip("Denoise the ray-traced GI.")]
        private BoolParameter m_Denoise = new BoolParameter(true);

        [SerializeField, FormerlySerializedAs("halfResolutionDenoiser")]
        [Tooltip("Use a half resolution denoiser.")]
        private BoolParameter m_HalfResolutionDenoiser = new BoolParameter(false);

        [SerializeField, FormerlySerializedAs("denoiserRadius")]
        [Tooltip("Controls the radius of the GI denoiser (First Pass).")]
        private ClampedFloatParameter m_DenoiserRadius = new ClampedFloatParameter(0.6f, 0.001f, 1.0f);

        [SerializeField, FormerlySerializedAs("secondDenoiserPass")]
        [Tooltip("Enable second denoising pass.")]
        private BoolParameter m_SecondDenoiserPass = new BoolParameter(true);
    }
}
