using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AmplifyOcclusion
{
    public class AmplifyOcclusionRenderer : ScriptableRendererFeature
    {
        AmplifyOcclusionRenderPass renderPass;

        public override void Create()
        {
            if (renderPass == null)
                renderPass = new AmplifyOcclusionRenderPass();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                renderPass?.Dispose();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            //if (renderingData.cameraData.postProcessEnabled)
            renderer.EnqueuePass(renderPass);
        }

        class AmplifyOcclusionRenderPass : ScriptableRenderPass
        {
            public AmplifyOcclusionVolume settings;

            // Current state variables
            private bool m_HDR = true;
            private bool m_MSAA = true;

            // Previous state variables
            private SampleCountLevel m_prevSampleCount = SampleCountLevel.Low;
            private bool m_prevDownsample = false;
            private bool m_prevCacheAware = false;
            private bool m_prevBlurEnabled = false;
            private int m_prevBlurRadius = 0;
            private int m_prevBlurPasses = 0;
            private bool m_prevFilterEnabled = true;
            private bool m_prevFilterDownsample = true;
            private bool m_prevHDR = true;
            private bool m_prevMSAA = true;

            private RenderTargetIdentifier[] applyDebugTargetsTemporal = new RenderTargetIdentifier[2];
            private RenderTargetIdentifier[] applyPostEffectTargetsTemporal = new RenderTargetIdentifier[2];

            private bool m_isSceneView = false;
            //private bool UsingTemporalFilter { get { return ((settings.FilterEnabled == true) && (m_isSceneView == false)); } }
            //private bool UsingFilterDownsample { get { return (settings.Downsample == true) && (settings.FilterDownsample == true) && (UsingTemporalFilter == true); } }
            private bool UsingMotionVectors = false;
            private bool m_paramsChanged = true;
            private bool m_clearHistory = true;

            private float m_oneOverDepthScale = (1.0f / 65504.0f); // 65504.0f max half float
            private Camera camera;
            private CameraData cameraData;
            private RenderTargetIdentifier source;

            static private Mesh m_quadMesh = null;
            static Texture2D defaultRampTex;

            private void createDefaultRamp()
            {
                if (defaultRampTex != null)
                    return;
                const int width = 128;
                defaultRampTex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
                var gradient = new Gradient() { colorKeys = new GradientColorKey[] { 
                    new GradientColorKey(new Color(0, 0, 0, 1), 0), 
                    new GradientColorKey(new Color(1, 1, 1, 1), 1), 
                    }};
                for (int x = 0; x < width; x++)
                    defaultRampTex.SetPixel(x, 0, gradient.Evaluate((float)x / width));
                defaultRampTex.Apply(false, true);
            }
            private void createQuadMesh()
            {
                if (m_quadMesh != null)
                    return;
                m_quadMesh = new Mesh();
                m_quadMesh.vertices = new Vector3[] {
                    new Vector3(-1,-1,0.5f),
                    new Vector3(-1,1,0.5f),
                    new Vector3(1,1,0.5f),
                    new Vector3(1,-1,0.5f)
                };
                m_quadMesh.uv = new Vector2[] {
                    new Vector2(0,1),
                    new Vector2(0,0),
                    new Vector2(1,0),
                    new Vector2(1,1)
                };
                m_quadMesh.SetIndices(new int[] { 0, 1, 2, 3 }, MeshTopology.Quads, 0);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                cameraData = renderingData.cameraData;
                camera = cameraData.camera;
                source = cameraData.renderer.cameraColorTarget;
                if (camera.cameraType != CameraType.Game)
                    return;
                //var renderer = renderingData.cameraData.renderer;
                //ConfigureTarget(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);

                settings = VolumeManager.instance.stack.GetComponent<AmplifyOcclusionVolume>();

                createQuadMesh();
                createDefaultRamp();
                checkMaterials(true);
                UpdateGlobalShaderConstants(cmd, camera);
                checkParamsChanged(camera);
                UpdateGlobalShaderConstants_AmbientOcclusion(cmd, camera);
                updateParams();

                Shader.SetGlobalFloat("_RenderViewportScaleFactor", 1.0f);

                var rampTex = settings.RampTexture.value;
                m_applyOcclusionMat.SetTexture("_OcclusionRamp", rampTex == null ? defaultRampTex : rampTex );

                //renderPassEvent = AfterOpaque ? RenderPassEvent.AfterRenderingOpaques : RenderPassEvent.AfterRenderingPrePasses + 1;

                //ConfigureInput(ScriptableRenderPassInput.Color);
                ConfigureInput(ScriptableRenderPassInput.Depth);
                //ConfigureInput(ScriptableRenderPassInput.Normal);

            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (camera.cameraType != CameraType.Game)
                    return;
                m_isSceneView = camera.cameraType == CameraType.SceneView;

                if (settings == null || !settings.IsActive())
                    return;

                var cmd = CommandBufferPool.Get("AmplifyOcclusion");
                cmd.Clear();
                cmd.BeginSample("AO - Render");

                m_curStepIdx = m_sampleStep & 1;

                UpdateGlobalShaderConstants_Matrices(cmd, camera);

                commandBuffer_FillComputeOcclusion(cmd);

                if (settings.ApplyMethod == AmplifyOcclusionVolume.ApplicationMethod.Debug)
                {
                    commandBuffer_FillApplyDebug(cmd, source, source);
                }
                else
                {
                    //commandBuffer_FillApplyDebug(cmd, source, source);
                    commandBuffer_FillApplyPostEffect(cmd, source, source, ref renderingData);
                }

                m_sampleStep++; // No clamp, free running counter

                cmd.EndSample("AO - Render");

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                AmplifyOcclusionCommon.SafeReleaseRT(ref m_occlusionDepthRT);
                AmplifyOcclusionCommon.SafeReleaseRT(ref m_depthMipmap);
                releaseTemporalRT();
            }

            private TargetDesc m_target = new TargetDesc();

            private static readonly int id_ScreenToTargetScale = Shader.PropertyToID("_ScreenToTargetScale");

            void UpdateGlobalShaderConstants(CommandBuffer cb, Camera aCamera)
            {
                AmplifyOcclusionCommon.UpdateGlobalShaderConstants(cb, ref m_target, aCamera, settings.Downsample.value, false); // UsingTemporalFilter

                //if( m_hdRender == null )
                //{
                // HDSRP _ScreenToTargetScale = { w / RTHandle.maxWidth, h / RTHandle.maxHeight } : xy = currFrame, zw = prevFram
                // Fill _ScreenToTargetScale for LWSRP
                cb.SetGlobalVector(id_ScreenToTargetScale, new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
                //}
            }

            void UpdateGlobalShaderConstants_AmbientOcclusion(CommandBuffer cb, Camera aCamera)
            {
                // Ambient Occlusion
                cb.SetGlobalFloat(PropertyID._AO_Radius, settings.Radius.value);
                cb.SetGlobalFloat(PropertyID._AO_PowExponent, settings.PowerExponent.value);
                cb.SetGlobalFloat(PropertyID._AO_Bias, settings.Bias.value * settings.Bias.value);
                cb.SetGlobalColor(PropertyID._AO_Levels, new Color(settings.Tint.value.r, settings.Tint.value.g, settings.Tint.value.b, settings.Intensity.value));

                float invThickness = (1.0f - settings.Thickness.value);
                cb.SetGlobalFloat(PropertyID._AO_ThicknessDecay, (1.0f - invThickness * invThickness) * 0.98f);

                float AO_BufDepthToLinearEye = aCamera.farClipPlane * m_oneOverDepthScale;
                cb.SetGlobalFloat(PropertyID._AO_BufDepthToLinearEye, AO_BufDepthToLinearEye);

                if (settings.BlurEnabled == true)
                {
                    float AO_BlurSharpness = settings.BlurSharpness.value * 100.0f * AO_BufDepthToLinearEye;

                    cb.SetGlobalFloat(PropertyID._AO_BlurSharpness, AO_BlurSharpness);
                }

                // Distance Fade
                if (settings.FadeEnabled == true)
                {
                    settings.FadeStart.value = Mathf.Max(0.0f, settings.FadeStart.value);
                    settings.FadeLength.value = Mathf.Max(0.01f, settings.FadeLength.value);

                    float rcpFadeLength = 1.0f / settings.FadeLength.value;

                    cb.SetGlobalVector(PropertyID._AO_FadeParams, new Vector2(settings.FadeStart.value, rcpFadeLength));
                    float invFadeThickness = (1.0f - settings.FadeToThickness.value);
                    cb.SetGlobalVector(PropertyID._AO_FadeValues, new Vector4(settings.FadeToIntensity.value, settings.FadeToRadius.value, settings.FadeToPowerExponent.value, (1.0f - invFadeThickness * invFadeThickness) * 0.98f));
                    cb.SetGlobalColor(PropertyID._AO_FadeToTint, new Color(settings.FadeToTint.value.r, settings.FadeToTint.value.g, settings.FadeToTint.value.b, 0.0f));
                }
                else
                {
                    cb.SetGlobalVector(PropertyID._AO_FadeParams, new Vector2(0.0f, 0.0f));
                }
                /*
                                if (UsingTemporalFilter == true)
                                {
                                    AmplifyOcclusionCommon.CommandBuffer_TemporalFilterDirectionsOffsets(cb, m_sampleStep);
                                }
                                else*/
                {
                    cb.SetGlobalFloat(PropertyID._AO_TemporalDirections, 0);
                    cb.SetGlobalFloat(PropertyID._AO_TemporalOffsets, 0);
                }
            }


            private AmplifyOcclusionViewProjMatrix m_viewProjMatrix = new AmplifyOcclusionViewProjMatrix();

            void UpdateGlobalShaderConstants_Matrices(CommandBuffer cb, Camera aCamera)
            {
                m_viewProjMatrix.UpdateGlobalShaderConstants_Matrices(cb, aCamera, false); // UsingTemporalFilter
            }


            void PerformBlit(CommandBuffer cb, RenderTargetIdentifier destination, Material mat, int pass)
            {
                //Blit(cb, source, destination, mat, pass);
                //CoreUtils.SetRenderTarget(cb, destination);
                cb.SetRenderTarget(destination);
                //cb.SetRenderTargetWithLoadStoreAction(destination, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                cb.DrawMesh(m_quadMesh, Matrix4x4.identity, mat, 0, pass);
                //CoreUtils.DrawFullScreen(cb, mat, pass);
            }
            void PerformBlit(CommandBuffer cb, Material mat, int pass)
            {
                cb.DrawMesh(m_quadMesh, Matrix4x4.identity, mat, 0, pass);
            }

            // Render Materials
            static private Material m_occlusionMat = null;
            static private Material m_blurMat = null;
            static private Material m_applyOcclusionMat = null;

            private void checkMaterials(bool aThroughErrorMsg)
            {
                if (m_occlusionMat == null)
                {
                    m_occlusionMat = AmplifyOcclusionCommon.CreateMaterialWithShaderName("Hidden/Amplify Occlusion/OcclusionPostProcessing", aThroughErrorMsg);
                }

                if (m_blurMat == null)
                {
                    m_blurMat = AmplifyOcclusionCommon.CreateMaterialWithShaderName("Hidden/Amplify Occlusion/BlurPostProcessing", aThroughErrorMsg);
                }

                if (m_applyOcclusionMat == null)
                {
                    m_applyOcclusionMat = AmplifyOcclusionCommon.CreateMaterialWithShaderName("Hidden/Amplify Occlusion/ApplyPostProcessing", aThroughErrorMsg);
                }
            }


            private RenderTextureFormat m_occlusionRTFormat = RenderTextureFormat.RGHalf;
            //private RenderTextureFormat m_occlusionRTFormat = RenderTextureFormat.RGHalf;
            private RenderTextureFormat m_accumTemporalRTFormat = RenderTextureFormat.ARGB32;
            private RenderTextureFormat m_motionIntensityRTFormat = RenderTextureFormat.R8;

            private bool checkRenderTextureFormats()
            {
                // test the two fallback formats first
                if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32) && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                {
                    m_occlusionRTFormat = RenderTextureFormat.RGHalf;
                    if (!SystemInfo.SupportsRenderTextureFormat(m_occlusionRTFormat))
                    {
                        m_occlusionRTFormat = RenderTextureFormat.RGFloat;
                        if (!SystemInfo.SupportsRenderTextureFormat(m_occlusionRTFormat))
                        {
                            // already tested above
                            m_occlusionRTFormat = RenderTextureFormat.ARGBHalf;
                        }
                    }

                    return true;
                }
                return false;
            }

            private RenderTexture m_occlusionDepthRT = null;
            private RenderTexture[] m_temporalAccumRT = null;
            private RenderTexture m_depthMipmap = null;

            private uint m_sampleStep = 0;
            private uint m_curStepIdx = 0;
            private string[] m_tmpMipString = null;
            private int m_numberMips = 0;

            private void commandBuffer_FillComputeOcclusion(CommandBuffer cb)
            {
                cb.BeginSample("AO 1 - ComputeOcclusion");

                Vector4 oneOverFullSize_Size = new Vector4(1.0f / (float)m_target.fullWidth,
                                                            1.0f / (float)m_target.fullHeight,
                                                            m_target.fullWidth,
                                                            m_target.fullHeight);

                int sampleCountPass = ((int)settings.SampleCount.value) * AmplifyOcclusionCommon.PerPixelNormalSourceCount;

                int occlusionPass = (ShaderPass.OcclusionLow_None +
                                      sampleCountPass +
                                      ShaderPass.OcclusionLow_None);
                //((settings.PerPixelNormals == PerPixelNormalSource.None) ? ShaderPass.OcclusionLow_None : ShaderPass.OcclusionLow_GBufferOctaEncoded));


                if (settings.CacheAware == true)
                {
                    occlusionPass += ShaderPass.OcclusionLow_None_UseDynamicDepthMips;

                    // Construct Depth mipmaps
                    int previouslyTmpMipRT = 0;

                    for (int i = 0; i < m_numberMips; i++)
                    {
                        int tmpMipRT;

                        int width = m_target.fullWidth >> (i + 1);
                        int height = m_target.fullHeight >> (i + 1);

                        tmpMipRT = AmplifyOcclusionCommon.SafeAllocateTemporaryRT(cb, m_tmpMipString[i],
                                                                                    width, height,
                                                                                    RenderTextureFormat.RFloat,
                                                                                    RenderTextureReadWrite.Linear,
                                                                                    FilterMode.Bilinear);

                        // _AO_CurrDepthSource was previously set
                        cb.SetRenderTarget(tmpMipRT);

                        PerformBlit(cb, m_occlusionMat, ((i == 0) ? ShaderPass.ScaleDownCloserDepthEven_CameraDepthTexture : ShaderPass.ScaleDownCloserDepthEven));

                        cb.CopyTexture(tmpMipRT, 0, 0, m_depthMipmap, 0, i);

                        if (previouslyTmpMipRT != 0)
                        {
                            AmplifyOcclusionCommon.SafeReleaseTemporaryRT(cb, previouslyTmpMipRT);
                        }

                        previouslyTmpMipRT = tmpMipRT;

                        cb.SetGlobalTexture(PropertyID._AO_CurrDepthSource, tmpMipRT); // Set next MipRT ID
                    }

                    AmplifyOcclusionCommon.SafeReleaseTemporaryRT(cb, previouslyTmpMipRT);

                    cb.SetGlobalTexture(PropertyID._AO_SourceDepthMipmap, m_depthMipmap);
                }


                //if ((settings.Downsample == true) && (UsingFilterDownsample == false))
                if (settings.Downsample == true)
                {
                    int halfWidth = m_target.fullWidth / 2;
                    int halfHeight = m_target.fullHeight / 2;

                    int tmpSmallOcclusionRT = AmplifyOcclusionCommon.SafeAllocateTemporaryRT(cb, "_AO_SmallOcclusionTexture",
                                                                                                halfWidth, halfHeight,
                                                                                                m_occlusionRTFormat,
                                                                                                RenderTextureReadWrite.Linear,
                                                                                                FilterMode.Bilinear);


                    cb.SetGlobalVector(PropertyID._AO_Target_TexelSize, new Vector4(1.0f / (m_target.fullWidth / 2.0f),
                                                                                    1.0f / (m_target.fullHeight / 2.0f),
                                                                                    m_target.fullWidth / 2.0f,
                                                                                    m_target.fullHeight / 2.0f));

                    PerformBlit(cb, tmpSmallOcclusionRT, m_occlusionMat, occlusionPass);

                    cb.SetRenderTarget(default(RenderTexture));
                    cb.EndSample("AO 1 - ComputeOcclusion");

                    if (settings.BlurEnabled == true)
                    {
                        commandBuffer_Blur(cb, tmpSmallOcclusionRT, halfWidth, halfHeight);
                    }

                    // Combine
                    cb.BeginSample("AO 2b - Combine");

                    cb.SetGlobalTexture(PropertyID._AO_CurrOcclusionDepth, tmpSmallOcclusionRT);

                    cb.SetGlobalVector(PropertyID._AO_Target_TexelSize, oneOverFullSize_Size);

                    PerformBlit(cb, m_occlusionDepthRT, m_occlusionMat, ShaderPass.CombineDownsampledOcclusionDepth);

                    AmplifyOcclusionCommon.SafeReleaseTemporaryRT(cb, tmpSmallOcclusionRT);

                    cb.SetRenderTarget(default(RenderTexture));
                    cb.EndSample("AO 2b - Combine");
                }
                else
                {/*
                    if (UsingFilterDownsample == true)
                    {
                        // Must use proper float precision 2.0 division to avoid artefacts
                        cb.SetGlobalVector(PropertyID._AO_Target_TexelSize, new Vector4(1.0f / (m_target.fullWidth / 2.0f),
                                                                                          1.0f / (m_target.fullHeight / 2.0f),
                                                                                          m_target.fullWidth / 2.0f,
                                                                                          m_target.fullHeight / 2.0f));
                    }
                    else*/
                    {
                        cb.SetGlobalVector(PropertyID._AO_Target_TexelSize, new Vector4(1.0f / (float)m_target.width,
                                                                                          1.0f / (float)m_target.height,
                                                                                          m_target.width,
                                                                                          m_target.height));
                    }

                    //PerformBlit(cb, m_occlusionDepthRT, m_occlusionMat, occlusionPass);
                    //Blit(cb, source, m_occlusionDepthRT, m_occlusionMat, occlusionPass);

                    //cb.SetGlobalTexture(PropertyID._MainTex, source);
                    cb.SetRenderTarget(m_occlusionDepthRT);
                    cb.DrawMesh(m_quadMesh, Matrix4x4.identity, m_occlusionMat, 0, occlusionPass);

                    //cb.SetRenderTarget(default(RenderTexture));
                    cb.EndSample("AO 1 - ComputeOcclusion");

                    if (settings.BlurEnabled == true)
                    {
                        commandBuffer_Blur(cb, m_occlusionDepthRT, m_target.width, m_target.height);
                    }
                }
            }


            int commandBuffer_NeighborMotionIntensity(CommandBuffer cb, int aSourceWidth, int aSourceHeight)
            {
                int tmpRT = AmplifyOcclusionCommon.SafeAllocateTemporaryRT(cb, "_AO_IntensityTmp_" + aSourceWidth.ToString(),
                                                                            aSourceWidth / 2, aSourceHeight / 2,
                                                                            m_motionIntensityRTFormat,
                                                                            RenderTextureReadWrite.Linear,
                                                                            FilterMode.Bilinear);


                cb.SetGlobalVector("_AO_Target_TexelSize", new Vector4(1.0f / (aSourceWidth / 2.0f),
                                                                         1.0f / (aSourceHeight / 2.0f),
                                                                         aSourceWidth / 2.0f,
                                                                         aSourceHeight / 2.0f));
                PerformBlit(cb, tmpRT, m_occlusionMat, ShaderPass.NeighborMotionIntensity);

                int tmpBlurRT = AmplifyOcclusionCommon.SafeAllocateTemporaryRT(cb, "_AO_BlurIntensityTmp",
                                                                                aSourceWidth / 2, aSourceHeight / 2,
                                                                                m_motionIntensityRTFormat,
                                                                                RenderTextureReadWrite.Linear,
                                                                                FilterMode.Bilinear);

                // Horizontal
                cb.SetGlobalTexture(PropertyID._AO_CurrMotionIntensity, tmpRT);
                PerformBlit(cb, tmpBlurRT, m_blurMat, ShaderPass.BlurHorizontalIntensity);

                // Vertical
                cb.SetGlobalTexture(PropertyID._AO_CurrMotionIntensity, tmpBlurRT);
                PerformBlit(cb, tmpRT, m_blurMat, ShaderPass.BlurVerticalIntensity);

                AmplifyOcclusionCommon.SafeReleaseTemporaryRT(cb, tmpBlurRT);

                cb.SetGlobalTexture(PropertyID._AO_CurrMotionIntensity, tmpRT);

                return tmpRT;
            }


            void commandBuffer_Blur(CommandBuffer cb, RenderTargetIdentifier aSourceRT, int aSourceWidth, int aSourceHeight)
            {
                cb.BeginSample("AO 2 - Blur");

                int tmpBlurRT = AmplifyOcclusionCommon.SafeAllocateTemporaryRT(cb, "_AO_BlurTmp",
                                                                                aSourceWidth, aSourceHeight,
                                                                                m_occlusionRTFormat,
                                                                                RenderTextureReadWrite.Linear,
                                                                                FilterMode.Point);

                // Apply Cross Bilateral Blur
                for (int i = 0; i < settings.BlurPasses.value; i++)
                {
                    // Horizontal
                    cb.SetGlobalTexture(PropertyID._AO_CurrOcclusionDepth, aSourceRT);

                    int blurHorizontalPass = ShaderPass.BlurHorizontal1 + (settings.BlurRadius.value - 1) * 2;

                    PerformBlit(cb, tmpBlurRT, m_blurMat, blurHorizontalPass);


                    // Vertical
                    cb.SetGlobalTexture(PropertyID._AO_CurrOcclusionDepth, tmpBlurRT);

                    int blurVerticalPass = ShaderPass.BlurVertical1 + (settings.BlurRadius.value - 1) * 2;

                    PerformBlit(cb, aSourceRT, m_blurMat, blurVerticalPass);
                }

                AmplifyOcclusionCommon.SafeReleaseTemporaryRT(cb, tmpBlurRT);

                cb.SetRenderTarget(default(RenderTexture));
                cb.EndSample("AO 2 - Blur");
            }


            int getTemporalPass()
            {
                return ((UsingMotionVectors == true) ? (1 << 0) : 0);
            }

            /*
                        void commandBuffer_TemporalFilter(CommandBuffer cb)
                        {
                            if ((m_clearHistory == true) || (m_paramsChanged == true))
                            {
                                clearHistory(cb);
                            }

                            // Temporal Filter
                            float temporalAdj = Mathf.Lerp(0.01f, 0.99f, settings.FilterBlending.value);

                            cb.SetGlobalFloat(PropertyID._AO_TemporalCurveAdj, temporalAdj);
                            cb.SetGlobalFloat(PropertyID._AO_TemporalMotionSensibility, settings.FilterResponse.value * settings.FilterResponse.value + 0.01f);

                            cb.SetGlobalTexture(PropertyID._AO_CurrOcclusionDepth, m_occlusionDepthRT);
                            cb.SetGlobalTexture(PropertyID._AO_TemporalAccumm, m_temporalAccumRT[1 - m_curStepIdx]);
                        }*/

            private RenderTargetIdentifier curTemporalRT;

            void commandBuffer_FillApplyPostEffect(CommandBuffer cb, RenderTargetIdentifier aSourceRT, RenderTargetIdentifier aDestinyRT, ref RenderingData renderData)
            {
                cb.BeginSample("AO 3 - ApplyPostEffect");


                /*
                                if (UsingTemporalFilter)
                                {
                                    commandBuffer_TemporalFilter(cb);

                                    int tmpMotionIntensityRT = 0;

                                    if (UsingMotionVectors == true)
                                    {
                                        tmpMotionIntensityRT = commandBuffer_NeighborMotionIntensity(cb, m_target.fullWidth, m_target.fullHeight);
                                    }

                                    if (UsingFilterDownsample == false)
                                    {
                                        applyPostEffectTargetsTemporal[0] = aDestinyRT;
                                        applyPostEffectTargetsTemporal[1] = new RenderTargetIdentifier(m_temporalAccumRT[m_curStepIdx]);

                                        cb.SetRenderTarget(applyPostEffectTargetsTemporal, applyPostEffectTargetsTemporal[0]  );// Not used, just to make Unity happy
                                        PerformBlit(cb, m_applyOcclusionMat, ShaderPass.ApplyPostEffectTemporal + getTemporalPass());
                                    }
                                    else
                                    {
                                        // UsingFilterDownsample == true

                                        RenderTargetIdentifier temporalRTid = new RenderTargetIdentifier(m_temporalAccumRT[m_curStepIdx]);

                                        cb.SetRenderTarget(temporalRTid);
                                        PerformBlit(cb, m_occlusionMat, ShaderPass.Temporal + getTemporalPass());

                                        cb.SetGlobalTexture(PropertyID._AO_TemporalAccumm, temporalRTid);
                                        cb.SetRenderTarget(aDestinyRT);
                                        PerformBlit(cb, m_applyOcclusionMat, ShaderPass.ApplyCombineFromTemporal);
                                    }

                                    if (UsingMotionVectors == true)
                                    {
                                        AmplifyOcclusionCommon.SafeReleaseTemporaryRT(cb, tmpMotionIntensityRT);
                                    }
                                }
                                else */
                {
                    cb.SetGlobalTexture(PropertyID._AO_CurrOcclusionDepth, m_occlusionDepthRT);

                    cb.SetGlobalTexture(PropertyID._MainTex, aSourceRT);
                    //m_applyOcclusionMat.SetTexture("_MainTex", aSourceRT);

                    cb.SetRenderTarget(aDestinyRT);
                    cb.DrawMesh(m_quadMesh, Matrix4x4.identity, m_applyOcclusionMat, 0, ShaderPass.ApplyPostEffect);
                    //Blit(cb, ref renderData, m_applyOcclusionMat, ShaderPass.ApplyPostEffect);
                    //PerformBlit(cb, aDestinyRT, m_applyOcclusionMat, ShaderPass.ApplyPostEffect);
                }

                cb.SetRenderTarget(default(RenderTexture));
                cb.EndSample("AO 3 - ApplyPostEffect");
            }


            void commandBuffer_FillApplyDebug(CommandBuffer cb, RenderTargetIdentifier aSourceRT, RenderTargetIdentifier aDestinyRT)
            {
                cb.BeginSample("AO 3 - ApplyDebug");

                cb.SetGlobalTexture(PropertyID._MainTex, aSourceRT);
                /*
                if (UsingTemporalFilter)
                {
                    commandBuffer_TemporalFilter(cb);

                    int tmpMotionIntensityRT = 0;

                    if (UsingMotionVectors == true)
                    {
                        tmpMotionIntensityRT = commandBuffer_NeighborMotionIntensity(cb, m_target.fullWidth, m_target.fullHeight);
                    }

                    if (UsingFilterDownsample == false)
                    {
                        applyDebugTargetsTemporal[0] = aDestinyRT;
                        applyDebugTargetsTemporal[1] = new RenderTargetIdentifier(m_temporalAccumRT[m_curStepIdx]);

                        cb.SetRenderTarget(applyDebugTargetsTemporal, applyDebugTargetsTemporal[0]  );// Not used, just to make Unity happy
                        PerformBlit(cb, m_applyOcclusionMat, ShaderPass.ApplyDebugTemporal + getTemporalPass());
                    }
                    else
                    {
                        // UsingFilterDownsample == true

                        RenderTargetIdentifier temporalRTid = new RenderTargetIdentifier(m_temporalAccumRT[m_curStepIdx]);

                        cb.SetRenderTarget(temporalRTid);
                        PerformBlit(cb, m_occlusionMat, ShaderPass.Temporal + getTemporalPass());

                        cb.SetGlobalTexture(PropertyID._AO_TemporalAccumm, temporalRTid);
                        cb.SetRenderTarget(aDestinyRT);
                        PerformBlit(cb, m_applyOcclusionMat, ShaderPass.ApplyDebugCombineFromTemporal);
                    }

                    if (UsingMotionVectors == true)
                    {
                        AmplifyOcclusionCommon.SafeReleaseTemporaryRT(cb, tmpMotionIntensityRT);
                    }
                }
                else*/
                {
                    cb.SetGlobalTexture(PropertyID._AO_CurrOcclusionDepth, m_occlusionDepthRT);
                    PerformBlit(cb, aDestinyRT, m_applyOcclusionMat, ShaderPass.ApplyDebug);
                }

                cb.SetRenderTarget(default(RenderTexture));
                cb.EndSample("AO 3 - ApplyDebug");
            }

            private void clearHistory(CommandBuffer cb)
            {
                m_clearHistory = false;

                if ((m_temporalAccumRT != null) && (m_occlusionDepthRT != null))
                {
                    cb.SetGlobalTexture(PropertyID._AO_CurrOcclusionDepth, m_occlusionDepthRT);
                    cb.SetRenderTarget(m_temporalAccumRT[0]);
                    PerformBlit(cb, m_occlusionMat, ShaderPass.ClearTemporal);

                    cb.SetGlobalTexture(PropertyID._AO_CurrOcclusionDepth, m_occlusionDepthRT);
                    cb.SetRenderTarget(m_temporalAccumRT[1]);
                    PerformBlit(cb, m_occlusionMat, ShaderPass.ClearTemporal);
                }
            }

            private void checkParamsChanged(Camera aCamera)
            {
                bool HDR = aCamera.allowHDR; // && tier?
                bool MSAA = aCamera.allowMSAA &&
                            QualitySettings.antiAliasing >= 1;

                int antiAliasing = MSAA ? QualitySettings.antiAliasing : 1;
                //int antiAliasing = 1;

                if (m_occlusionDepthRT != null)
                {
                    if ((m_occlusionDepthRT.width != m_target.width) ||
                        (m_occlusionDepthRT.height != m_target.height) ||
                        (m_prevMSAA != MSAA) ||
                        //(m_prevFilterEnabled != UsingTemporalFilter) ||
                        //(m_prevFilterDownsample != UsingFilterDownsample) ||
                        (!m_occlusionDepthRT.IsCreated()) ||
                        (m_temporalAccumRT != null && (!m_temporalAccumRT[0].IsCreated() || !m_temporalAccumRT[1].IsCreated()))
                        )
                    {
                        AmplifyOcclusionCommon.SafeReleaseRT(ref m_occlusionDepthRT);
                        AmplifyOcclusionCommon.SafeReleaseRT(ref m_depthMipmap);
                        releaseTemporalRT();

                        m_paramsChanged = true;
                    }
                }

                if (m_temporalAccumRT != null)
                {
                    if (m_temporalAccumRT.Length != 2)
                    {
                        m_temporalAccumRT = null;
                    }
                }

                if (m_occlusionDepthRT == null)
                {
                    m_occlusionDepthRT = AmplifyOcclusionCommon.SafeAllocateRT("_AO_OcclusionDepthTexture",
                                                                                m_target.width,
                                                                                m_target.height,
                                                                                m_occlusionRTFormat,
                                                                                RenderTextureReadWrite.Linear,
                                                                                FilterMode.Bilinear);
                }


                if (m_temporalAccumRT == null) // UsingTemporalFilter
                {
                    m_temporalAccumRT = new RenderTexture[2];

                    m_temporalAccumRT[0] = AmplifyOcclusionCommon.SafeAllocateRT("_AO_TemporalAccum_0",
                                                                                    m_target.width,
                                                                                    m_target.height,
                                                                                    m_accumTemporalRTFormat,
                                                                                    RenderTextureReadWrite.Linear,
                                                                                    FilterMode.Bilinear,
                                                                                    antiAliasing);

                    m_temporalAccumRT[1] = AmplifyOcclusionCommon.SafeAllocateRT("_AO_TemporalAccum_1",
                                                                                    m_target.width,
                                                                                    m_target.height,
                                                                                    m_accumTemporalRTFormat,
                                                                                    RenderTextureReadWrite.Linear,
                                                                                    FilterMode.Bilinear,
                                                                                    antiAliasing);

                    m_clearHistory = true;
                }

                if ((settings.CacheAware == true) && (m_depthMipmap == null))
                {
                    m_depthMipmap = AmplifyOcclusionCommon.SafeAllocateRT("_AO_DepthMipmap",
                                                                            m_target.fullWidth >> 1,
                                                                            m_target.fullHeight >> 1,
                                                                            RenderTextureFormat.RFloat,
                                                                            RenderTextureReadWrite.Linear,
                                                                            FilterMode.Point,
                                                                            1,
                                                                            true);

                    int minSize = (int)Mathf.Min(m_target.fullWidth, m_target.fullHeight);
                    m_numberMips = (int)(Mathf.Log((float)minSize, 2.0f) + 1.0f) - 1;

                    m_tmpMipString = null;
                    m_tmpMipString = new string[m_numberMips];

                    for (int i = 0; i < m_numberMips; i++)
                    {
                        m_tmpMipString[i] = "_AO_TmpMip_" + i.ToString();
                    }
                }
                else
                {
                    if ((settings.CacheAware == false) && (m_depthMipmap != null))
                    {
                        AmplifyOcclusionCommon.SafeReleaseRT(ref m_depthMipmap);
                        m_tmpMipString = null;
                    }
                }

                if ((m_prevSampleCount != settings.SampleCount.value) ||
                    (m_prevDownsample != settings.Downsample.value) ||
                    (m_prevCacheAware != settings.CacheAware.value) ||
                    (m_prevBlurEnabled != settings.BlurEnabled.value) ||
                    (m_prevBlurPasses != settings.BlurPasses.value) ||
                    (m_prevBlurRadius != settings.BlurRadius.value) ||
                    //(m_prevFilterEnabled != UsingTemporalFilter) ||
                    //(m_prevFilterDownsample != UsingFilterDownsample) ||
                    (m_prevHDR != HDR) ||
                    (m_prevMSAA != MSAA))
                {
                    m_clearHistory |= (m_prevHDR != HDR);
                    m_clearHistory |= (m_prevMSAA != MSAA);

                    m_HDR = HDR;
                    m_MSAA = MSAA;

                    m_paramsChanged = true;
                }
            }


            private void updateParams()
            {
                m_prevSampleCount = settings.SampleCount.value;
                m_prevDownsample = settings.Downsample.value;
                m_prevCacheAware = settings.CacheAware.value;
                m_prevBlurEnabled = settings.BlurEnabled.value;
                m_prevBlurPasses = settings.BlurPasses.value;
                m_prevBlurRadius = settings.BlurRadius.value;
                //m_prevFilterEnabled = UsingTemporalFilter;
                //m_prevFilterDownsample = UsingFilterDownsample;
                m_prevHDR = m_HDR;
                m_prevMSAA = m_MSAA;

                m_paramsChanged = false;
            }


            public DepthTextureMode GetCameraFlags()
            {
                return DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
            }

            private void releaseTemporalRT()
            {
                if (m_temporalAccumRT != null)
                {
                    if (m_temporalAccumRT.Length != 0)
                    {
                        AmplifyOcclusionCommon.SafeReleaseRT(ref m_temporalAccumRT[0]);
                        AmplifyOcclusionCommon.SafeReleaseRT(ref m_temporalAccumRT[1]);
                    }
                }

                m_temporalAccumRT = null;
            }
        }
    }
}