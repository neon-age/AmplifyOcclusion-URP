using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AmplifyOcclusion
{
    [VolumeComponentMenu("Amplify Creations/Amplify Occlusion V2")]
    public class AmplifyOcclusionVolume : VolumeComponent, IPostProcessComponent
    {
        public enum ApplicationMethod
        {
            PostEffect = 0,
            Debug
        }
        public enum PerPixelNormalSource
        {
            None = 0,
            GBuffer
        }

        [Serializable] public class ApplicationMethodParameter : VolumeParameter<ApplicationMethod> { }
        [Serializable] public class SampleCountLevelVolumeParameter : VolumeParameter<SampleCountLevel> { }
        //[Serializable] public class PerPixelNormalSourceVolumeParameter : VolumeParameter<PerPixelNormalSource> {}


        //[Tooltip("Enable Amplify Occlusion.")]
        //public BoolParameter enabled = new BoolParameter(true, true);

        [Header("Ambient Occlusion")]
        [Tooltip("How to inject the occlusion: Post Effect = Overlay, Debug - Vizualize.")]
        public ApplicationMethodParameter ApplyMethod = new ApplicationMethodParameter { value = ApplicationMethod.PostEffect, overrideState = true };

        [Tooltip("Number of samples per pass.")]
        public SampleCountLevelVolumeParameter SampleCount = new SampleCountLevelVolumeParameter { value = SampleCountLevel.Medium, overrideState = true };
        public NoInterpTextureParameter RampTexture = new NoInterpTextureParameter(null);
        //[Tooltip("Source of per-pixel normals: None = All, GBuffer = Deferred.")]
        //public PerPixelNormalSourceVolumeParameter PerPixelNormals = new PerPixelNormalSourceVolumeParameter { value = PerPixelNormalSource.None };

        [Tooltip("Final applied intensity of the occlusion effect.")]
        public ClampedFloatParameter Intensity = new ClampedFloatParameter(1.0f, 0, 1, true);

        [Tooltip("Color tint for occlusion.")]
        public ColorParameter Tint = new ColorParameter(Color.black);

        [Tooltip("Radius spread of the occlusion.")]
        public ClampedFloatParameter Radius = new ClampedFloatParameter(2.0f, 0, 32, true);

        [Tooltip("Power exponent attenuation of the occlusion.")]
        public ClampedFloatParameter PowerExponent = new ClampedFloatParameter(1.8f, 0, 16, true);

        [Tooltip("Controls the initial occlusion contribution offset.")]
        public ClampedFloatParameter Bias = new ClampedFloatParameter(0.05f, 0, 0.99f);

        [Tooltip("Controls the thickness occlusion contribution.")]
        public ClampedFloatParameter Thickness = new ClampedFloatParameter(1.0f, 0, 1.0f, true);

        [Tooltip("Compute the Occlusion and Blur at half of the resolution.")]
        public BoolParameter Downsample = new BoolParameter(true, true);

        [Tooltip("Cache optimization for best performance / quality tradeoff.")]
        public BoolParameter CacheAware = new BoolParameter(false);

        [Header("Distance Fade")]
        [Tooltip("Control parameters at faraway.")]
        public BoolParameter FadeEnabled = new BoolParameter(false);

        [Tooltip("Distance in Unity unities that start to fade.")]
        public FloatParameter FadeStart = new FloatParameter(100.0f);

        [Tooltip("Length distance to performe the transition.")]
        public FloatParameter FadeLength = new FloatParameter(50.0f);

        [Tooltip("Final Intensity parameter.")]
        public ClampedFloatParameter FadeToIntensity = new ClampedFloatParameter(0.0f, 0, 1);
        public ColorParameter FadeToTint = new ColorParameter(Color.black);

        [Tooltip("Final Radius parameter.")]
        public ClampedFloatParameter FadeToRadius = new ClampedFloatParameter(2.0f, 0, 32);

        [Tooltip("Final PowerExponent parameter.")]
        public ClampedFloatParameter FadeToPowerExponent = new ClampedFloatParameter(1.0f, 0, 16);

        [Tooltip("Final Thickness parameter.")]
        public ClampedFloatParameter FadeToThickness = new ClampedFloatParameter(1.0f, 0, 1.0f);

        [Header("Bilateral Blur")]
        public BoolParameter BlurEnabled = new BoolParameter(true, true);

        [Tooltip("Radius in screen pixels.")]
        public ClampedIntParameter BlurRadius = new ClampedIntParameter(3, 1, 4, true);

        [Tooltip("Number of times that the Blur will repeat.")]
        public ClampedIntParameter BlurPasses = new ClampedIntParameter(1, 1, 4, true);

        [Tooltip("Sharpness of blur edge-detection: 0 = Softer Edges, 20 = Sharper Edges.")]
        public ClampedFloatParameter BlurSharpness = new ClampedFloatParameter(15.0f, 0, 20, true);
/*
        [Header("Temporal Filter")]
        [Tooltip("Accumulates the effect over the time.")]
        public BoolParameter FilterEnabled = new BoolParameter(true, true);
        public BoolParameter FilterDownsample = new BoolParameter(true, true);

        [Tooltip("Controls the accumulation decayment: 0 = More flicker with less ghosting, 1 = Less flicker with more ghosting.")]
        public ClampedFloatParameter FilterBlending = new ClampedFloatParameter(0.80f, 0, 1, true);

        [Tooltip("Controls the discard sensitivity based on the motion of the scene and objects.")]
        public ClampedFloatParameter FilterResponse = new ClampedFloatParameter(0.5f, 0, 1, true);
*/
        public bool IsActive() => true;
        public bool IsTileCompatible() => true;

        protected override void OnEnable()
        {
            base.OnEnable();
            displayName = "Amplify Occlusion";
        }
    }
}